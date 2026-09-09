using System.Diagnostics;
using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;
using StreamJsonRpc;

namespace McpBizagi.Server;

public sealed partial class LiveSessionClient
{
    private async Task<LiveSessionReply> ObserveClose(LiveSessionRequest request, LiveConnection descriptor, Process process, Process? owner,
        Task<LiveSessionReply> invocation, Action<string> progress, CancellationToken token)
    {
        var files = new WorkspaceFiles(SessionDirectory(request.SessionId));
        var inactivity = Stopwatch.StartNew();
        TimeSpan previousCpu = process.TotalProcessorTime;
        string previousEvidence = "";
        bool observedRpc = false;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (invocation.IsCompleted && !observedRpc)
            {
                observedRpc = true;
                try
                {
                    var reply = await invocation;
                    ValidateReplyIdentity(reply, request.SessionId, request.OperationId);
                    if (reply.State != "closing") return reply;
                }
                catch (ConnectionLostException) { progress("live_close_transport_ended_not_exit_proof"); }
            }
            LiveSessionReply? retained = null;
            string receiptPath = request.OperationId + ".receipt.json";
            if (File.Exists(files.Resolve(receiptPath)))
            {
                retained = JsonSerializer.Deserialize<LiveSessionReply>(files.Read(receiptPath));
                if (retained == null) throw new InvalidDataException("Empty close receipt.");
                ValidateReplyIdentity(retained, request.SessionId, request.OperationId);
                if (retained.State is "rejected" or "uncertain") return retained;
            }
            if (process.HasExited)
            {
                if (retained?.State != "closing" || retained.Code != "live_close_admitted" || retained.Checkpoint == null || retained.Snapshot == null)
                    throw new IOException("Editor exited without verified close admission; preserve its working files and investigate.");
                ValidateCloseAdmission(files, request, retained);
                if (process.ExitCode != 0) throw new IOException("Managed editor exited abnormally: " + process.ExitCode);
                // Native Close must not rewrite or lose the checkpointed model.
                if (BpmnDocument.Revision(files.Read(retained.Snapshot.Path)) != request.ExpectedDiskRevision ||
                    BpmnDocument.Revision(files.Read(retained.Checkpoint.ArtifactPath)) != request.ExpectedDiskRevision)
                    throw new IOException("Native files changed during closing; no rollback was attempted.");
                retained.State = "completed"; retained.Code = "live_editor_exit_verified";
                bool ownedTreeVerified = owner != null && await ObserveOwnerCleanup(files, descriptor, owner, progress, token);
                retained.EditorExit = new() { ProcessId = descriptor.ProcessId, StartedAt = descriptor.StartedAt,
                    ObservedAt = DateTimeOffset.UtcNow, ExitCode = process.ExitCode, OwnedTreeVerified = ownedTreeVerified };
                string stage = files.Resolve(request.OperationId + ".close-exit.json.tmp"), final = files.Resolve(request.OperationId + ".close-exit.json");
                using (var output = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { JsonSerializer.Serialize(output, retained); output.Flush(true); }
                File.Move(stage, final);
                progress("live_editor_exit_verified");
                return retained;
            }
            // Liveness alone never advances this inactivity window. Observe actual
            // native CPU and journal/log writes; do not kill the editor on expiry.
            process.Refresh();
            string evidence = string.Join(";", Directory.EnumerateFiles(files.Root, "*.log").Concat(Directory.EnumerateFiles(files.Root, request.OperationId + ".*"))
                .Order(StringComparer.Ordinal).Select(p => { var f = new FileInfo(p); return f.Name + ":" + f.Length + ":" + f.LastWriteTimeUtc.Ticks; }));
            TimeSpan cpu = process.TotalProcessorTime;
            if (cpu != previousCpu || evidence != previousEvidence)
            { previousCpu = cpu; previousEvidence = evidence; inactivity.Restart(); progress("live_close_native_activity"); }
            if (inactivity.Elapsed.TotalSeconds > options.InactivitySeconds)
                throw new TimeoutException("Native closing made no observable progress within the inactivity window. The editor was not killed; retain its files and reconcile, never repeat the close blindly.");
            await Task.Delay(250, token);
        }
    }

    private Process? PinCloseOwner(WorkspaceFiles files, LiveConnection connection)
    {
        // Keep legacy explicitly managed companion sessions usable without claiming
        // that their external supervisor was this production owner.
        if (!File.Exists(files.Resolve("owner-request.json"))) return null;
        var (_, started) = ReadOwnerIdentity(files, connection);
        var owner = Process.GetProcessById(started.Owner.ProcessId);
        try
        {
            _ = owner.SafeHandle;
            if (owner.HasExited || owner.StartTime.ToUniversalTime() != started.Owner.StartedAt.UtcDateTime ||
                !string.Equals(owner.MainModule?.FileName, started.Owner.Executable, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The managed live owner identity is no longer running; no Close was dispatched.");
            return owner;
        }
        catch { owner.Dispose(); throw; }
    }

    private (LiveOwnerRequest Request, LiveOwnerStarted Started) ReadOwnerIdentity(WorkspaceFiles files, LiveConnection connection)
    {
        var request = JsonSerializer.Deserialize<LiveOwnerRequest>(files.Read("owner-request.json")) ?? throw new InvalidDataException("Missing live owner request.");
        var started = JsonSerializer.Deserialize<LiveOwnerStarted>(files.Read("owner-started.json")) ?? throw new InvalidDataException("Missing live owner identity.");
        LiveOwnerProtocol.Validate(request, files.Root); LiveOwnerProtocol.ValidateStarted(request, started);
        if (!string.Equals(request.OwnerExecutable, Path.GetFullPath(live.OwnerExecutable), StringComparison.OrdinalIgnoreCase) ||
            started.Editor.ProcessId != connection.ProcessId || started.Editor.StartedAt != connection.StartedAt ||
            !string.Equals(started.Editor.Executable, connection.Executable, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Owner and native connection identities disagree.");
        return (request, started);
    }

    private async Task<bool> ObserveOwnerCleanup(WorkspaceFiles files, LiveConnection connection, Process owner, Action<string> progress, CancellationToken token)
    {
        var (request, started) = ReadOwnerIdentity(files, connection);
        // This bound applies only after the native root ended, never to live work.
        var cleanup = Stopwatch.StartNew();
        while (!owner.HasExited)
        {
            token.ThrowIfCancellationRequested();
            if (cleanup.Elapsed.TotalSeconds > request.CleanupSeconds + options.ConnectionSeconds)
                throw new TimeoutException("Native editor ended but independent-owner cleanup is not yet verified. Reconcile retained evidence; do not repeat Close.");
            await Task.Delay(100, token);
        }
        if (owner.ExitCode != 0) throw new IOException("Live owner exited abnormally: " + owner.ExitCode);
        RequireOwnerCleanupEvidence(files, request, started);
        progress("live_owned_process_tree_exit_verified");
        return true;
    }

    private static LiveOwnerExit RequireOwnerCleanupEvidence(WorkspaceFiles files, LiveOwnerRequest request, LiveOwnerStarted started)
    {
        var exit = JsonSerializer.Deserialize<LiveOwnerExit>(files.Read("owner-exit.json")) ?? throw new InvalidDataException("Missing owner exit evidence.");
        LiveOwnerProtocol.ValidateExit(request, started, exit);
        using var removal = JsonDocument.Parse(files.Read("task-removed.json"));
        if (exit.ExitCode != 0 || !exit.AllOwnedExited || removal.RootElement.GetProperty("TaskName").GetString() != request.TaskName)
            throw new IOException("Owned native descendants or launch-task cleanup were not verified.");
        return exit;
    }

    public LiveSessionReply RetainedCloseObservation(LiveSessionRequest request)
    {
        // Reconciliation after editor exit needs no pipe and never redispatches Close.
        var files = new WorkspaceFiles(SessionDirectory(request.SessionId));
        string name = request.OperationId + ".close-exit.json";
        if (!File.Exists(files.Resolve(name)))
        {
            if (File.Exists(files.Resolve("owner-exit.json")) && File.Exists(files.Resolve("task-removed.json")))
            {
                // If MCP died after admission, recover from the independent owner's
                // actual native/job exit evidence; never replay the Close request.
                var connection = JsonSerializer.Deserialize<LiveConnection>(files.Read("connection.json"), DescriptorJson)!;
                var (launch, started) = ReadOwnerIdentity(files, connection);
                var ownerExit = RequireOwnerCleanupEvidence(files, launch, started);
                var admitted = JsonSerializer.Deserialize<LiveSessionReply>(files.Read(request.OperationId + ".receipt.json"))!;
                ValidateCloseAdmission(files, request, admitted);
                admitted.State = "completed"; admitted.Code = "live_editor_exit_verified";
                admitted.EditorExit = new() { ProcessId = started.Editor.ProcessId, StartedAt = started.Editor.StartedAt,
                    ExitCode = ownerExit.ExitCode, ObservedAt = ownerExit.ObservedAt, OwnedTreeVerified = true };
                return admitted;
            }
            return new() { OperationId = request.OperationId, State = "unknown", Code = "close_exit_not_observed",
                Message = "No retained process-exit observation. This is not proof that the editor remains open or that closing failed; inspect native and owner evidence. No close was replayed." };
        }
        var reply = JsonSerializer.Deserialize<LiveSessionReply>(files.Read(name));
        if (reply?.OperationId != request.OperationId || reply.Snapshot?.SessionId != request.SessionId || reply.EditorExit == null ||
            reply.State != "completed" || reply.Code != "live_editor_exit_verified" || reply.EditorExit.ExitCode != 0)
            throw new InvalidDataException("Retained close observation identity or outcome mismatch.");
        return reply;
    }

    private static void ValidateCloseAdmission(WorkspaceFiles files, LiveSessionRequest request, LiveSessionReply admitted)
    {
        if (admitted.OperationId != request.OperationId || admitted.State != "closing" || admitted.Code != "live_close_admitted" ||
            admitted.Snapshot?.SessionId != request.SessionId || admitted.Snapshot.Dirty || admitted.Snapshot.Revision != request.ExpectedRevision ||
            admitted.Snapshot.DiskRevision != request.ExpectedDiskRevision || admitted.Checkpoint?.Revision != request.ExpectedDiskRevision ||
            admitted.Checkpoint.DocumentRevision != request.ExpectedRevision ||
            admitted.Checkpoint.ArtifactPath != files.Resolve(Guid.Parse(request.CheckpointOperationId).ToString("D") + ".checkpoint.bpm"))
            throw new InvalidDataException("Retained native close admission does not match the guarded request.");
        if (BpmnDocument.Revision(files.Read(admitted.Snapshot.Path)) != request.ExpectedDiskRevision ||
            BpmnDocument.Revision(files.Read(admitted.Checkpoint.ArtifactPath)) != request.ExpectedDiskRevision)
            throw new IOException("Native close archives no longer match their retained revisions.");
    }
}
