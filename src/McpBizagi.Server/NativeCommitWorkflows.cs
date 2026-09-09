using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed record NativeCommitIntent(int Version, string OperationId, string Workspace, string Source,
    string SourceRevision, DateTimeOffset PreparedAt, FileCommitIntent File);

public sealed partial class NativeWorkflows
{
    public OperationView CommitNative(string path, string expectedRevision, string destinationPath, string? expectedDestinationRevision)
    {
        WorkspaceFiles.RequireRevision(expectedRevision);
        if (expectedDestinationRevision != null) WorkspaceFiles.RequireRevision(expectedDestinationRevision);
        if (!Path.GetExtension(destinationPath).Equals(".bpm", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Native commit requires a .bpm destination.");
        string destination = files.Resolve(destinationPath);
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Revision conflict: native source changed since inspection.");
        return operations.Start("native_commit", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            // Load real native bytes before any destination write. Archive parsing alone is not engine acceptance.
            var opened = await Execute(new EngineRequest { OperationId = id, Action = "inspect", InputPath = source }, RunDirectory(id, "source-reader"), progress, token);
            if (!input.Bytes.AsSpan().SequenceEqual(File.ReadAllBytes(source))) throw new IOException("Source snapshot changed during native validation.");
            NativeCommitIntent? intent = null;
            var commit = files.Commit(destination, input.Bytes, expectedDestinationRevision, file =>
            {
                intent = new(1, id, files.Root, path, input.Revision, DateTimeOffset.UtcNow, file);
                CommitReceipt(directory, "commit-intent.json", intent);
                progress("native_commit_prepared");
                if (ReadNative(path).Revision != expectedRevision) throw new IOException("Revision conflict: native source changed before publication.");
            }, token);
            // This independent receipt survives cancellation or host death even when OperationView.Result is null.
            CommitReceipt(directory, "commit-published.json", new { commit, publishedAt = DateTimeOffset.UtcNow });
            progress("native_commit_published");
            var readback = await VerifyCommittedNative(id, directory, intent!, progress, token);
            return new { commit, opened, readback, byteIdentityPreserved = true,
                receipt = Path.Combine(directory, "commit-intent.json"),
                warning = "Byte-exact file adoption with native readback; it does not synchronize an open or unsaved Modeler document. Reconcile this operation after interruption instead of repeating a write." };
        });
    }

    public OperationView ReconcileNativeCommit(string operationId)
    {
        var original = operations.Get(operationId);
        if (original.Kind is not ("native_commit" or "live_publish") || original.State is "running" or "cancelling")
            throw new InvalidOperationException("Reconciliation requires a terminal native_commit or live_publish operation from this state directory.");
        return operations.Start("native_commit_reconcile", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id);
            string receipt = Path.Combine(RunDirectory(operationId, "artifacts"), "commit-intent.json");
            byte[] receiptBytes;
            try { receiptBytes = new WorkspaceFiles(Path.GetDirectoryName(receipt)!).Read(Path.GetFileName(receipt)); }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                // Absence can mean early failure OR subsequently deleted evidence. Never infer no side effect.
                return new { originalOperationId = operationId, original.State, observedState = "missing_intent", nativeReadbackVerified = false,
                    explanation = "No durable intent was found. Missing evidence cannot establish publication outcome; inspect the original operation and retained files. No write was replayed." };
            }
            var intent = JsonSerializer.Deserialize<NativeCommitIntent>(receiptBytes)
                ?? throw new InvalidDataException("Empty native commit intent.");
            if (intent.Version != 1 || intent.OperationId != operationId || intent.Workspace != files.Root || intent.SourceRevision != intent.File.OutputRevision)
                throw new InvalidDataException("Native commit intent does not match this operation and workspace.");
            CommitReceipt(directory, "reconciled-intent.json", intent);
            var observation = files.ObserveCommit(intent.File);
            CommitReceipt(directory, "reconciliation-observation.json", observation);
            token.ThrowIfCancellationRequested();
            if (observation.State != "applied")
                return new { originalOperationId = operationId, original.State, observedState = observation.State, nativeReadbackVerified = false, observation, writeReplayed = false };
            var readback = await VerifyCommittedNative(id, directory, intent, progress, token);
            return new { originalOperationId = operationId, original.State, observedState = "applied", nativeReadbackVerified = true, readback, writeReplayed = false };
        });
    }

    private async Task<object> VerifyCommittedNative(string id, string directory, NativeCommitIntent intent, Action<string> progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        progress("native_commit_readback_snapshot");
        byte[] bytes = files.Read(intent.File.Path);
        if (BpmnDocument.Revision(bytes) != intent.File.OutputRevision || bytes.LongLength != intent.File.Bytes)
            throw new IOException("Published destination changed before native readback. Inspect the commit receipts; no rollback was attempted.");
        string snapshot = Path.Combine(directory, "committed.bpm");
        await File.WriteAllBytesAsync(snapshot, bytes, token);
        var reopened = await Execute(new EngineRequest { OperationId = id, Action = "inspect", InputPath = snapshot }, RunDirectory(id, "destination-reader"), progress, token);
        var observation = files.ObserveCommit(intent.File);
        if (observation.State != "applied" || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(snapshot)))
            throw new IOException("Destination or recovery evidence changed during native readback. Reconcile the intent; no write was replayed.");
        var result = new { observation, reopened, nativeReadbackVerified = true, revision = intent.File.OutputRevision, verifiedAt = DateTimeOffset.UtcNow };
        CommitReceipt(directory, "commit-native-readback.json", result);
        return result;
    }

    private static void CommitReceipt(string directory, string name, object receipt)
    {
        // Independent append-only phase files. A partial temporary receipt is never treated as committed.
        string file = Path.Combine(directory, name), temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, receipt, new JsonSerializerOptions { WriteIndented = true }); stream.Flush(true); }
        File.Move(temporary, file, false);
    }
}
