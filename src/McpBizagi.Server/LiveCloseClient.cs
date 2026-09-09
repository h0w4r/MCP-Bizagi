using System.Diagnostics;
using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;
using StreamJsonRpc;

namespace McpBizagi.Server;

public sealed partial class LiveSessionClient
{
    private async Task<LiveSessionReply> ObserveClose(LiveSessionRequest request, LiveConnection descriptor, Process process,
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
                if (process.ExitCode != 0) throw new IOException("Managed editor exited abnormally: " + process.ExitCode);
                // Native Close must not rewrite or lose the checkpointed model.
                if (BpmnDocument.Revision(files.Read(retained.Snapshot.Path)) != request.ExpectedDiskRevision ||
                    BpmnDocument.Revision(files.Read(retained.Checkpoint.ArtifactPath)) != request.ExpectedDiskRevision)
                    throw new IOException("Native files changed during closing; no rollback was attempted.");
                retained.State = "completed"; retained.Code = "live_editor_exit_verified";
                retained.EditorExit = new() { ProcessId = descriptor.ProcessId, StartedAt = descriptor.StartedAt,
                    ObservedAt = DateTimeOffset.UtcNow, ExitCode = process.ExitCode, OwnedTreeVerified = false };
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

    public LiveSessionReply RetainedCloseObservation(LiveSessionRequest request)
    {
        // Reconciliation after editor exit needs no pipe and never redispatches Close.
        var files = new WorkspaceFiles(SessionDirectory(request.SessionId));
        string name = request.OperationId + ".close-exit.json";
        if (!File.Exists(files.Resolve(name))) return new() { OperationId = request.OperationId, State = "unknown", Code = "close_exit_not_observed",
            Message = "No retained process-exit observation. This is not proof that the editor remains open or that closing failed; inspect native and owner evidence. No close was replayed." };
        var reply = JsonSerializer.Deserialize<LiveSessionReply>(files.Read(name));
        if (reply?.OperationId != request.OperationId || reply.Snapshot?.SessionId != request.SessionId || reply.EditorExit == null ||
            reply.State != "completed" || reply.Code != "live_editor_exit_verified" || reply.EditorExit.ExitCode != 0)
            throw new InvalidDataException("Retained close observation identity or outcome mismatch.");
        return reply;
    }
}
