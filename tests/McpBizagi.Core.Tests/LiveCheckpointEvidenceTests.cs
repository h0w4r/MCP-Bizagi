using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class LiveCheckpointEvidenceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mcp-checkpoint-evidence-" + Guid.NewGuid().ToString("N"));
    private readonly LiveSessionRequest request;
    private readonly LiveSessionReply reply;

    public LiveCheckpointEvidenceTests()
    {
        Directory.CreateDirectory(root);
        request = new() { SessionId = Guid.NewGuid().ToString("D"), OperationId = Guid.NewGuid().ToString("D"),
            Action = "checkpoint", ExpectedRevision = "live-before", ExpectedDiskRevision = BpmnDocument.Revision([1, 2]) };
        // These are deliberately not native models: this suite tests evidence policy, never engine accreditation.
        string artifact = Path.Combine(root, request.OperationId + ".checkpoint.bpm"), previous = Path.Combine(root, request.OperationId + ".before.bpm");
        File.WriteAllBytes(artifact, [3, 4]); File.WriteAllBytes(previous, [1, 2]);
        reply = new() { OperationId = request.OperationId, State = "completed", Snapshot = new() { SessionId = request.SessionId,
                Revision = "live-after", DiskRevision = BpmnDocument.Revision([3, 4]), Path = Path.Combine(root, "working.bpm") },
            Checkpoint = new() { ArtifactPath = artifact, PreviousArtifactPath = previous, DocumentRevision = "live-after",
                Revision = BpmnDocument.Revision([3, 4]), PreviousRevision = request.ExpectedDiskRevision } };
    }

    [Fact] public void HashBoundCompletedReceiptCanBeCapturedAfterEditorExit()
    {
        var result = LiveCheckpointEvidence.Capture(root, request, reply);
        Assert.Equal(new byte[] { 3, 4 }, result.Bytes); Assert.Equal(new byte[] { 1, 2 }, result.PreviousBytes);
        Assert.Equal(request.SessionId, result.SessionId);
    }

    [Theory]
    [InlineData("identity")][InlineData("dirty")][InlineData("revision")][InlineData("disk")]
    [InlineData("published")][InlineData("state")][InlineData("action")][InlineData("session")]
    public void MismatchedReceiptCannotAuthorizePublication(string field)
    {
        switch (field)
        {
            case "identity": reply.OperationId = Guid.NewGuid().ToString("D"); break;
            case "dirty": reply.Snapshot!.Dirty = true; break;
            case "revision": reply.Snapshot!.Revision = "later"; break;
            case "disk": reply.Snapshot!.DiskRevision = new string('a', 64); break;
            case "published": reply.Checkpoint!.DestinationPublished = true; break;
            case "state": reply.State = "uncertain"; break;
            case "action": request.Action = "read"; request.ExpectedDiskRevision = ""; break;
            case "session": reply.Snapshot!.SessionId = Guid.NewGuid().ToString("D"); break;
        }
        Assert.Throws<InvalidDataException>(() => LiveCheckpointEvidence.Capture(root, request, reply));
    }

    [Theory][InlineData(true)][InlineData(false)]
    public void TamperingEitherArchiveIsRejected(bool checkpoint)
    {
        File.WriteAllBytes(checkpoint ? reply.Checkpoint!.ArtifactPath : reply.Checkpoint!.PreviousArtifactPath, [9]);
        Assert.Throws<IOException>(() => LiveCheckpointEvidence.Capture(root, request, reply));
    }

    [Fact] public void OtherOperationArtifactCannotBeSubstitutedEvenWithIdenticalBytes()
    {
        string other = Path.Combine(root, Guid.NewGuid().ToString("D") + ".checkpoint.bpm");
        File.Copy(reply.Checkpoint!.ArtifactPath, other); reply.Checkpoint.ArtifactPath = other;
        Assert.Throws<InvalidDataException>(() => LiveCheckpointEvidence.Capture(root, request, reply));
    }

    [Fact] public void RegistryAndStateAreNeverPublicationDestinations()
    {
        Assert.Throws<UnauthorizedAccessException>(() => LiveCheckpointEvidence.RequireExternalDestination(Path.Combine(root, "state", "model.bpm"), Path.Combine(root, "state")));
        Assert.Throws<UnauthorizedAccessException>(() => LiveCheckpointEvidence.RequireExternalDestination(root, root));
        LiveCheckpointEvidence.RequireExternalDestination(root + "-sibling/model.bpm", root);
    }

    // Remove only the unique fixture directory created by this test instance.
    public void Dispose() => Directory.Delete(root, true);
}
