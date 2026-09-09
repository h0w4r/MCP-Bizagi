using McpBizagi.Contracts;
using Newtonsoft.Json;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    public sealed partial class LiveSession
    {
        private Task<LiveSessionReply> CloseAsync(LiveSessionRequest request, string fingerprint, LiveSessionSnapshot before,
            Action markDispatched, CancellationToken cancellation) => OnUi(() =>
        {
            // Recheck on the message-loop thread immediately before native Close.
            var current = Snapshot();
            if (current.Dirty || current.Revision != before.Revision || current.DiskRevision != request.ExpectedDiskRevision)
                throw new InvalidOperationException("Close requires the current clean native document and unchanged working-copy disk revision.");
            string path = NativeLivePaths.RequireWorkingCopy(engine.workRoot, current.Path);
            string checkpointId = Guid.Parse(request.CheckpointOperationId).ToString("D");
            if (!receipts.ContainsKey(checkpointId)) throw new InvalidOperationException("Close requires a checkpoint retained by this native session.");
            var saved = JsonConvert.DeserializeObject<LiveSessionReply>(File.ReadAllText(ReceiptPath(checkpointId)));
            var checkpoint = saved?.Checkpoint;
            string artifact = Path.Combine(engine.workRoot, checkpointId + ".checkpoint.bpm");
            if (saved?.State != "completed" || saved.OperationId != checkpointId || saved.Snapshot?.SessionId != sessionId ||
                saved.Snapshot.Dirty || checkpoint == null || checkpoint.DocumentRevision != current.Revision ||
                checkpoint.Revision != request.ExpectedDiskRevision || checkpoint.ArtifactPath != artifact ||
                Hash(ReadStable(artifact)) != checkpoint.Revision || Hash(ReadStable(path)) != checkpoint.Revision)
                throw new InvalidOperationException("Close checkpoint does not prove the current document is durably retained.");
            // Persist admission BEFORE invoking the vendor lifecycle. The process
            // may end before JSON-RPC transmits a reply; admission is not exit proof.
            var reply = Retain(request, fingerprint, current, "closing", "live_close_admitted", checkpoint);
            reply.Warnings = new[] { "Only the managed working copy is retained. Original-file publication is a separate operation. Close admission alone does not prove process exit." };
            WriteReceipt(request.OperationId, reply);
            markDispatched();
            form.Close();
            if (!form.IsDisposed)
            {
                reply.State = "rejected"; reply.Code = "live_close_cancelled";
                reply.Message = "The native application cancelled closing; its editor remains open.";
                WriteReceipt(request.OperationId, reply);
            }
            return reply;
        }, cancellation);
    }
}
