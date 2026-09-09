using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class LiveWorkflows
{
    public OperationView Publish(string checkpointOperationId, string destinationPath, string expectedDestinationRevision, LiveElementPatch[] expectedChanges)
    {
        var operation = operations.Get(checkpointOperationId);
        if (operation.Kind != "live_checkpoint" || operation.State is "running" or "cancelling")
            throw new InvalidOperationException("Publication requires a terminal live_checkpoint and its completed durable native receipt.");
        var journal = new WorkspaceFiles(DirectoryFor(checkpointOperationId));
        var request = JsonSerializer.Deserialize<LiveSessionRequest>(journal.Read("request.json")) ?? throw new InvalidDataException("Missing checkpoint request.");
        if (request.OperationId != Guid.ParseExact(checkpointOperationId, "N").ToString("D")) throw new InvalidDataException("Checkpoint journal identity mismatch.");
        LiveSessionReply reply;
        try { reply = JsonSerializer.Deserialize<LiveSessionReply>(journal.Read("reply.json")) ?? throw new InvalidDataException("Empty checkpoint reply."); }
        catch (FileNotFoundException)
        {
            // The native flushed receipt can outlive a lost MCP reply. Read it without replaying Save.
            reply = JsonSerializer.Deserialize<LiveSessionReply>(new WorkspaceFiles(client.SessionDirectory(request.SessionId)).Read(request.OperationId + ".receipt.json"))
                ?? throw new InvalidDataException("No completed durable native checkpoint receipt; reconcile before publication.");
        }
        var checkpoint = LiveCheckpointEvidence.Capture(client.SessionDirectory(request.SessionId), request, reply);
        string destination = new WorkspaceFiles(options.Workspace).Resolve(destinationPath);
        LiveCheckpointEvidence.RequireExternalDestination(destination, liveOptions.Root, options.State);
        return native.PublishLiveCheckpoint(checkpointOperationId, checkpoint, destination, expectedDestinationRevision, expectedChanges);
    }
}

public sealed partial class NativeWorkflows
{
    public OperationView PublishLiveCheckpoint(string checkpointOperationId, CapturedLiveCheckpoint checkpoint,
        string destinationPath, string expectedDestinationRevision, LiveElementPatch[] expectedChanges)
    {
        WorkspaceFiles.RequireRevision(expectedDestinationRevision);
        if (expectedChanges == null) throw new ArgumentException("Explicit expected changes are required; use an empty array for a no-change save.");
        // Freeze caller-owned data before queuing; later .NET mutations cannot alter journaled intent or output bytes.
        expectedChanges = JsonSerializer.Deserialize<LiveElementPatch[]>(JsonSerializer.Serialize(expectedChanges))!;
        checkpoint = checkpoint with { Bytes = checkpoint.Bytes.ToArray(), PreviousBytes = checkpoint.PreviousBytes.ToArray() };
        // Reuse the current native mutation fidelity policy instead of inventing a weaker live-save comparer.
        if (expectedChanges.Length > 0) LiveSessionProtocol.Validate(new LiveSessionRequest { SessionId = checkpoint.SessionId,
            OperationId = checkpoint.OperationId, Action = "update", ExpectedRevision = checkpoint.DocumentRevision, Changes = expectedChanges });
        var mutations = expectedChanges.Select(p => new NativeMutation { Operation = "update", ElementId = p.ElementId, Name = p.Name, Documentation = p.Documentation }).ToArray();
        if (mutations.Length > 0) NativeEditPlan.Validate(mutations);
        var original = ReadNative(destinationPath);
        if (original.Revision != expectedDestinationRevision) throw new IOException("Revision conflict: publication destination changed since inspection.");
        return operations.Start("live_publish", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "edited.bpm");
            CommitReceipt(directory, "live-publication.json", new { checkpointOperationId, checkpoint.SessionId, checkpoint.DocumentRevision,
                checkpoint.Revision, destinationPath, expectedDestinationRevision, expectedChanges });
            await File.WriteAllBytesAsync(Path.Combine(directory, "original.bpm"), original.Bytes, token);
            await File.WriteAllBytesAsync(source, checkpoint.Bytes, token);
            var opened = await Execute(new EngineRequest { OperationId = id, Action = "inspect", InputPath = source }, RunDirectory(id, "source-reader"), progress, token);
            if (!checkpoint.Bytes.AsSpan().SequenceEqual(File.ReadAllBytes(source))) throw new IOException("Checkpoint validation snapshot changed.");
            var fidelity = mutations.Length == 0 ? NativeFidelity.Compare(original.Bytes, checkpoint.Bytes)
                : NativeMutationFidelity.Compare(original.Bytes, checkpoint.Bytes, mutations, opened.Elements);
            CommitReceipt(directory, "live-publication-fidelity.json", fidelity);
            if (!fidelity.Preserved) throw new InvalidDataException("Live publication rejected unexplained archive changes; destination and checkpoint remain untouched.");
            progress("live_publication_fidelity_verified");
            NativeCommitIntent? intent = null;
            var commit = files.Commit(destinationPath, checkpoint.Bytes, expectedDestinationRevision, file =>
            {
                intent = new(1, id, files.Root, "live-checkpoint:" + checkpointOperationId, checkpoint.Revision, DateTimeOffset.UtcNow, file);
                CommitReceipt(directory, "commit-intent.json", intent);
                progress("native_commit_prepared");
            }, token);
            CommitReceipt(directory, "commit-published.json", new { commit, publishedAt = DateTimeOffset.UtcNow });
            progress("native_commit_published");
            var readback = await VerifyCommittedNative(id, directory, intent!, progress, token);
            return new { checkpointOperationId, checkpoint.SessionId, checkpoint.DocumentRevision, commit, fidelity, readback,
                destinationPublished = true, byteIdentityPreserved = true, liveSessionModified = false,
                warning = "Published this exact retained checkpoint, not subsequent live edits. The editor keeps its working-copy identity. Reconcile interruption with native_commit_reconcile; never repeat the write blindly." };
        });
    }
}
