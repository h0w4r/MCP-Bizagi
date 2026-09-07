namespace McpBizagi.Core;

/// <summary>Versioned prepublication facts. Stage and backup names are bound to one transaction.</summary>
public sealed record FileCommitIntent(int Version, string TransactionId, string Path, string StagePath,
    string? BackupPath, string? ExpectedRevision, string OutputRevision, long Bytes);
public sealed record CommitFileObservation(string Path, string State, string? Revision, long? Bytes, string? Error);
public sealed record FileCommitObservation(string State, CommitFileObservation Destination,
    CommitFileObservation Stage, CommitFileObservation? Backup, string Explanation);

public sealed partial class WorkspaceFiles
{
    public static void RequireRevision(string revision)
    {
        if (revision == null || revision.Length != 64 || revision.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvalidDataException("Expected a lowercase SHA-256 revision.");
    }

    /// <summary>Read-only reconciliation. A matching destination alone never proves a replacement.</summary>
    public FileCommitObservation ObserveCommit(FileCommitIntent intent)
    {
        if (intent.Version != 1 || !Guid.TryParseExact(intent.TransactionId, "N", out _) || intent.Bytes < 0 || intent.Bytes > BpmnDocument.MaxXmlCharacters * 4)
            throw new InvalidDataException("Unsupported or invalid commit intent.");
        RequireRevision(intent.OutputRevision);
        if (intent.ExpectedRevision != null) RequireRevision(intent.ExpectedRevision);
        string path = Resolve(intent.Path);
        // Do not let edited private receipts turn reconciliation into an arbitrary state-file reader.
        string stage = Path.Combine(Path.GetDirectoryName(path)!, ".mcp-bizagi-" + intent.TransactionId + ".tmp");
        string? backup = intent.ExpectedRevision == null ? null : path + "." + intent.TransactionId + ".bak";
        if (intent.Path != path || intent.StagePath != stage || intent.BackupPath != backup)
            throw new InvalidDataException("Commit intent paths do not match their confined transaction identity.");
        var destination = ObserveFile(path); var staged = ObserveFile(stage); var previous = backup == null ? null : ObserveFile(backup);
        FileCommitObservation Result(string state, string explanation) => new(state, destination, staged, previous, explanation);
        if (new[] { destination, staged, previous }.Any(f => f?.State == "unreadable"))
            return Result("unreadable", "At least one receipt path could not be read. No write, rollback or replay was attempted.");
        bool desired = destination.Revision == intent.OutputRevision && destination.Bytes == intent.Bytes;
        bool initial = intent.ExpectedRevision == null ? destination.State == "absent" : destination.Revision == intent.ExpectedRevision;
        bool stageMatches = staged.Revision == intent.OutputRevision && staged.Bytes == intent.Bytes;
        bool priorMatches = intent.ExpectedRevision == null || previous?.Revision == intent.ExpectedRevision;
        if (desired && staged.State == "absent" && priorMatches)
            return Result("applied", "Current destination and required backup match the intent; the stage was consumed. This is an observation, not proof against later identical external writes.");
        if (initial && stageMatches && (previous == null || previous.State == "absent"))
            return Result("not_applied", "Original destination state and intact staged bytes are present; no publication is observed. A new explicit request is required to write.");
        if ((destination.State == "present" && !initial && !desired) || (staged.State == "present" && !stageMatches) ||
            (previous?.State == "present" && !priorMatches))
            return Result("conflict", "Content differs from the recorded revisions. Retain every version and resolve explicitly; never automatically restore over newer data.");
        return Result("ambiguous", "Available destination, stage and backup facts do not establish a unique outcome. Nothing was replayed or deleted.");
    }

    private CommitFileObservation ObserveFile(string path)
    {
        try { byte[] bytes = Read(path); return new(path, "present", BpmnDocument.Revision(bytes), bytes.LongLength, null); }
        catch (FileNotFoundException) { return new(path, "absent", null, null, null); }
        catch (DirectoryNotFoundException) { return new(path, "absent", null, null, null); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { return new(path, "unreadable", null, null, error.Message); }
    }
}
