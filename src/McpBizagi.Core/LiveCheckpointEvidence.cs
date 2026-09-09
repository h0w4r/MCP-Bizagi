using McpBizagi.Contracts;

namespace McpBizagi.Core;

public sealed record CapturedLiveCheckpoint(string OperationId, string SessionId, string DocumentRevision,
    string Revision, byte[] Bytes, byte[] PreviousBytes);

/// <summary>Adopts only hash-bound native checkpoint artifacts, never caller-selected private files.</summary>
public static class LiveCheckpointEvidence
{
    public static CapturedLiveCheckpoint Capture(string directory, LiveSessionRequest request, LiveSessionReply reply)
    {
        LiveSessionProtocol.Validate(request);
        var checkpoint = reply.Checkpoint;
        var snapshot = reply.Snapshot;
        if (request.Action != "checkpoint" || reply.State != "completed" || reply.OperationId != request.OperationId ||
            checkpoint == null || snapshot == null || snapshot.SessionId != request.SessionId || snapshot.Dirty ||
            checkpoint.DestinationPublished || snapshot.Revision != checkpoint.DocumentRevision ||
            string.IsNullOrWhiteSpace(checkpoint.DocumentRevision) || snapshot.DiskRevision != checkpoint.Revision ||
            checkpoint.PreviousRevision != request.ExpectedDiskRevision)
            throw new InvalidDataException("Checkpoint receipt does not describe a completed, clean, matching working-copy save.");
        WorkspaceFiles.RequireRevision(checkpoint.Revision);
        WorkspaceFiles.RequireRevision(checkpoint.PreviousRevision);
        var files = new WorkspaceFiles(directory);
        string prefix = Guid.Parse(request.OperationId).ToString("D");
        // Exact native naming binds both artifacts to this request, not merely to the session directory.
        RequirePath(files, checkpoint.ArtifactPath, prefix + ".checkpoint.bpm");
        RequirePath(files, checkpoint.PreviousArtifactPath, prefix + ".before.bpm");
        files.Resolve(snapshot.Path);
        byte[] bytes = files.Read(checkpoint.ArtifactPath), previous = files.Read(checkpoint.PreviousArtifactPath);
        if (BpmnDocument.Revision(bytes) != checkpoint.Revision || BpmnDocument.Revision(previous) != checkpoint.PreviousRevision)
            throw new IOException("Checkpoint evidence changed since its native receipt; no publication was attempted.");
        return new(request.OperationId, request.SessionId, checkpoint.DocumentRevision, checkpoint.Revision, bytes, previous);
    }

    public static void RequireExternalDestination(string destination, params string[] privateRoots)
    {
        string full = Path.GetFullPath(destination);
        foreach (string root in privateRoots)
        {
            string relative = Path.GetRelativePath(Path.GetFullPath(root), full);
            if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Live publication cannot overwrite the session registry or private operation state.");
        }
    }

    private static void RequirePath(WorkspaceFiles files, string actual, string expected)
    {
        if (!Path.IsPathFullyQualified(actual) || !files.Resolve(actual).Equals(files.Resolve(expected),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Checkpoint artifact path does not match the native operation identity.");
    }
}
