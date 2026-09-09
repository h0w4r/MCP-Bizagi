using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class LiveWorkflows
{
    public OperationView Open(string path, string expectedRevision)
    {
        if (!options.ExperimentalNative) throw new InvalidOperationException("Experimental native operations are disabled.");
        WorkspaceFiles.RequireRevision(expectedRevision);
        var workspace = new WorkspaceFiles(options.Workspace);
        string source = workspace.Resolve(path);
        if (!Path.GetExtension(source).Equals(".bpm", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Live open requires an existing native .bpm.");
        byte[] bytes = workspace.Read(source);
        if (BpmnDocument.Revision(bytes) != expectedRevision) throw new IOException("Live open source revision conflict.");
        NativeCustomArtifactPolicy.ValidateArchiveReferences(NativeArchive.ReadEntries(bytes));
        string ownerExecutable = Path.GetFullPath(liveOptions.OwnerExecutable), editorExecutable = Path.GetFullPath(liveOptions.Executable);
        foreach (string executable in new[] { ownerExecutable, editorExecutable })
        {
            new WorkspaceFiles(Path.GetDirectoryName(executable)!).Resolve(executable);
            if (!File.Exists(executable)) throw new FileNotFoundException("The configured live executable is missing.", executable);
        }
        string installation = options.Installation ?? throw new InvalidOperationException("No installed native engine was found.");
        return operations.Start("live_open", async (id, progress, token) =>
        {
            string sessionId = Guid.NewGuid().ToString("D"), directory = client.SessionDirectory(sessionId);
            Directory.CreateDirectory(directory);
            string working = Path.Combine(directory, "working.bpm");
            using (var stream = new FileStream(working, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            using var identity = WindowsIdentity.GetCurrent(); using var self = Process.GetCurrentProcess();
            var request = new LiveOwnerRequest(1, sessionId, id, directory, ownerExecutable, editorExecutable, installation, source,
                expectedRevision, working, identity.User!.Value, self.SessionId, options.ConnectionSeconds, options.InactivitySeconds, Math.Max(15, options.CleanupSeconds));
            LiveOwnerProtocol.Validate(request, directory);
            LiveOwnerProtocol.WriteNew(directory, "owner-request.json", request);
            WriteEvidence(DirectoryFor(id), "open.json", request);
            token.ThrowIfCancellationRequested();
            progress("live_owner_registering");
            // Windows activates the owner outside this process tree. Cancellation never stops that task.
            LiveOwnerTask.Launch(request);
            progress("live_owner_started_waiting_native_readiness");
            var snapshot = await WaitReady(request, progress, token);
            WriteEvidence(DirectoryFor(id), "opened.json", new { request.SessionId, snapshot });
            return new { sessionId, originalPath = source, originalRevision = expectedRevision, workingCopy = working, snapshot,
                warning = "The dedicated visible editor owns a staged working copy. MCP termination does not close it. Use live_checkpoint/live_publish/live_close explicitly." };
        });
    }

    private async Task<LiveSessionSnapshot> WaitReady(LiveOwnerRequest request, Action<string> progress, CancellationToken token)
    {
        var files = new WorkspaceFiles(request.Directory);
        var inactivity = Stopwatch.StartNew(); string lastActivity = "";
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (File.Exists(files.Resolve("native-page-load-error.txt")))
                throw new IOException("Native page-load handshake failed; the dedicated editor may show an error dialog. Inspect native-page-load-error.txt; no live mutation was dispatched.");
            if (File.Exists(files.Resolve("owner-error.json"))) throw new IOException("Live owner failed: " + System.Text.Encoding.UTF8.GetString(files.Read("owner-error.json")));
            if (File.Exists(files.Resolve("owner-exit.json"))) throw new IOException("Native editor exited before readiness; inspect retained owner evidence.");
            string activity = lastActivity;
            if (File.Exists(files.Resolve("owner-activity.json")))
            {
                // Atomic telemetry replacement is allowed while this read holds an old complete file handle.
                try
                {
                    using var stream = new FileStream(files.Resolve("owner-activity.json"), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                    using var json = JsonDocument.Parse(stream);
                    activity = json.RootElement.GetProperty("cpu") + ":" + json.RootElement.GetProperty("io");
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { progress("live_startup_telemetry_temporarily_unavailable"); } // Failure does not count as activity.
            }
            if (activity != lastActivity) { lastActivity = activity; inactivity.Restart(); progress("live_native_startup_activity"); }
            if (File.Exists(files.Resolve("connection.json")))
            {
                var probe = new LiveSessionRequest { SessionId = request.SessionId, OperationId = Guid.NewGuid().ToString("D"), Action = "read" };
                try
                {
                    var reply = await client.Execute(probe, progress, token);
                    if (reply.State == "completed" && reply.Snapshot != null) return reply.Snapshot;
                    if (reply.Code != "live_editor_not_ready") throw new InvalidOperationException(reply.Code + ": " + reply.Message);
                    progress("live_native_editor_initializing");
                }
                catch (TimeoutException) when (!token.IsCancellationRequested)
                {
                    // Only this startup read probe is retried, never a mutation or
                    // a launch. Actual owner CPU/I/O still governs the inactivity window.
                    progress("live_startup_read_probe_waiting");
                }
            }
            if (inactivity.Elapsed.TotalSeconds > options.InactivitySeconds)
                throw new TimeoutException("Native startup made no observable progress. The independent owner was not stopped; inspect live_sessions_list before any new launch.");
            await Task.Delay(500, token);
        }
    }

    public object ListSessions()
    {
        var registry = new WorkspaceFiles(liveOptions.Root);
        var result = new List<object>();
        foreach (string directory in Directory.EnumerateDirectories(registry.Root).Where(d => Guid.TryParseExact(Path.GetFileName(d), "D", out _)).Take(1000))
        {
            var files = new WorkspaceFiles(registry.Resolve(directory));
            if (!File.Exists(files.Resolve("owner-request.json"))) continue;
            try
            {
                var request = JsonSerializer.Deserialize<LiveOwnerRequest>(files.Read("owner-request.json"))!;
                LiveOwnerProtocol.Validate(request, directory);
                string state = "starting";
                if (File.Exists(files.Resolve("owner-exit.json")))
                {
                    var started = JsonSerializer.Deserialize<LiveOwnerStarted>(files.Read("owner-started.json"))!;
                    var exit = JsonSerializer.Deserialize<LiveOwnerExit>(files.Read("owner-exit.json"))!;
                    LiveOwnerProtocol.ValidateExit(request, started, exit);
                    state = exit.AllOwnedExited && exit.ExitCode == 0 ? "closed" : "exited_requires_review";
                    if (!File.Exists(files.Resolve("task-removed.json"))) state += "_task_cleanup_unverified";
                }
                else if (File.Exists(files.Resolve("owner-error.json"))) state = "owner_failed";
                else if (File.Exists(files.Resolve("owner-started.json")))
                {
                    var started = JsonSerializer.Deserialize<LiveOwnerStarted>(files.Read("owner-started.json"))!;
                    LiveOwnerProtocol.ValidateStarted(request, started);
                    state = IsOriginalProcessAlive(started.Editor) ? "running_readiness_not_queried" : "editor_exited_cleanup_unverified";
                }
                result.Add(new { request.SessionId, request.OperationId, request.OriginalPath, request.OriginalRevision, request.WorkingCopy, state });
            }
            catch (Exception error) { result.Add(new { sessionId = Path.GetFileName(directory), state = "unreadable", error = error.Message }); }
        }
        return new { sessions = result, limit = 1000, warning = "Process presence is not native document readiness. Readiness requires live_read; sessions are never relaunched by this query." };
    }

    private static bool IsOriginalProcessAlive(LiveProcessIdentity identity)
    {
        try
        {
            using var process = Process.GetProcessById(identity.ProcessId); _ = process.SafeHandle;
            return !process.HasExited && process.StartTime.ToUniversalTime() == identity.StartedAt.UtcDateTime &&
                string.Equals(process.MainModule?.FileName, identity.Executable, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }
    }
}
