using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using McpBizagi.Core;
using McpBizagi.Server;

// The Windows scheduler, not a stdio MCP process, owns this process's ancestry.
if (args.Length != 1) return 1;
string root = Path.GetFullPath(args[0]);
LiveOwnerRequest? request = null;
try
{
    var files = new WorkspaceFiles(root);
    request = JsonSerializer.Deserialize<LiveOwnerRequest>(files.Read("owner-request.json")) ?? throw new InvalidDataException("Missing owner request.");
    LiveOwnerProtocol.Validate(request, root);
    using var self = Process.GetCurrentProcess();
    using var identity = WindowsIdentity.GetCurrent();
    if (identity.User?.Value != request.UserSid || self.SessionId != request.DesktopSessionId ||
        !string.Equals(self.MainModule?.FileName, request.OwnerExecutable, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Owner activation must use the configured executable and original interactive user/session.");
    string accountState = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MCP-Bizagi");
    var accountFiles = new WorkspaceFiles(accountState);
    // File-share exclusion survives async continuations and admits one owner per account, across registries.
    using var accountLease = new FileStream(accountFiles.Resolve("live-owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    if (BpmnDocument.Revision(files.Read(request.WorkingCopy)) != request.OriginalRevision)
        throw new IOException("Working copy changed before native launch.");
    LiveProcessIdentity Identify(Process p) => new(p.Id, p.StartTime.ToUniversalTime(), p.MainModule!.FileName!);
    using var job = new WorkerJob();
    using var editor = new Process { StartInfo = new(request.LiveExecutable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
    editor.StartInfo.ArgumentList.Add(request.WorkingCopy);
    editor.StartInfo.Environment["MCP_BIZAGI_LIVE_DIRECTORY"] = root;
    editor.StartInfo.Environment["MCP_BIZAGI_LIVE_SESSION"] = request.SessionId;
    editor.StartInfo.Environment["BIZAGI_MODELER_PATH"] = request.Installation;
    editor.StartInfo.Environment["MCP_BIZAGI_CONNECTION_SECONDS"] = request.ConnectionSeconds.ToString();
    editor.StartInfo.Environment["MCP_BIZAGI_INACTIVITY_SECONDS"] = request.InactivitySeconds.ToString();
    editor.Start();
    try { job.Assign(editor); }
    catch { if (!editor.HasExited) editor.Kill(); throw; } // Native loading cannot start before our marker.
    var started = new LiveOwnerStarted(1, request.SessionId, Identify(self), Identify(editor));
    LiveOwnerProtocol.WriteNew(root, "owner-started.json", started);
    File.WriteAllText(files.Resolve("job-attached"), editor.Id.ToString());
    async Task Copy(StreamReader input, string name)
    {
        await using var output = new StreamWriter(files.Resolve(name));
        while (await input.ReadLineAsync() is { } line) { await output.WriteLineAsync(line); await output.FlushAsync(); }
    }
    var stdout = Copy(editor.StandardOutput, "owner-child-stdout.log");
    var stderr = Copy(editor.StandardError, "owner-child-stderr.log");
    var owned = new Dictionary<int, Process>();
    try
    {
        while (!editor.HasExited)
        {
            foreach (int pid in job.ProcessIds()) if (!owned.ContainsKey(pid))
            {
                try { var child = Process.GetProcessById(pid); _ = child.SafeHandle; owned.Add(pid, child); }
                catch (ArgumentException) { /* A short-lived owned child already exited. */ }
                catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode is 5 or 87)
                {
                    // A child can disappear between job enumeration and OpenProcess.
                    // The authoritative job-empty query still gates cleanup; a failed
                    // optional sample must not terminate an otherwise healthy editor.
                }
            }
            var activity = job.Activity();
            try
            {
                string stage = files.Resolve("owner-activity.json.tmp");
                await File.WriteAllTextAsync(stage, JsonSerializer.Serialize(new { cpu = activity.CpuTicks, io = activity.IoBytes, at = DateTimeOffset.UtcNow }));
                File.Move(stage, files.Resolve("owner-activity.json"), true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Windows sharing/rename failures may map to either exception.
                // Telemetry is non-authoritative: never end the owner because a sampler cannot write.
                try { File.AppendAllText(files.Resolve("owner-telemetry-errors.log"), DateTimeOffset.UtcNow + " " + error.Message + Environment.NewLine); }
                catch (Exception diagnostic) when (diagnostic is IOException or UnauthorizedAccessException) { /* Preserve the live editor even when diagnostic storage fails. */ }
            }
            await Task.Delay(500);
        }
        int exitCode = editor.ExitCode;
        // Native editor ended: terminate only remaining descendants in its owned job, then drain inherited pipes.
        job.TerminateRemaining();
        var cleanup = Stopwatch.StartNew();
        while ((job.ProcessIds().Length != 0 || owned.Values.Any(p => !p.HasExited)) && cleanup.Elapsed.TotalSeconds < request.CleanupSeconds) await Task.Delay(50);
        bool allExited = job.ProcessIds().Length == 0 && owned.Values.All(p => p.HasExited);
        LiveOwnerProtocol.WriteNew(root, "owner-exit.json", new LiveOwnerExit(1, request.SessionId, started.Owner, started.Editor,
            exitCode, allExited, owned.Count, DateTimeOffset.UtcNow));
        if (!allExited) throw new IOException("Owned descendant cleanup was not verified; inspect retained owner evidence.");
        await Task.WhenAll(stdout, stderr);
        return exitCode;
    }
    finally { foreach (var child in owned.Values) child.Dispose(); }
}
catch (Exception error)
{
    LiveOwnerProtocol.WriteNew(root, "owner-error.json", new { error = error.ToString(), at = DateTimeOffset.UtcNow });
    return 1;
}
finally
{
    if (request != null)
    {
        try { LiveOwnerTask.RemoveOwnDefinition(request); LiveOwnerProtocol.WriteNew(root, "task-removed.json", new { request.TaskName, at = DateTimeOffset.UtcNow }); }
        catch (Exception error) { LiveOwnerProtocol.WriteNew(root, "task-cleanup-error.json", new { error = error.Message }); }
    }
}
