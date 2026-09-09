using System.Text.Json;

namespace McpBizagi.Core;

/// <summary>Private operator-configured launch contract; MCP callers never choose executables or principals.</summary>
public sealed record LiveOwnerRequest(int Version, string SessionId, string OperationId, string Directory,
    string OwnerExecutable, string LiveExecutable, string Installation, string OriginalPath, string OriginalRevision,
    string WorkingCopy, string UserSid, int DesktopSessionId, int ConnectionSeconds, int InactivitySeconds, int CleanupSeconds)
{
    public string TaskName => "MCP-Bizagi-Live-" + Guid.Parse(SessionId).ToString("N");
}

public sealed record LiveProcessIdentity(int ProcessId, DateTimeOffset StartedAt, string Executable);
public sealed record LiveOwnerStarted(int Version, string SessionId, LiveProcessIdentity Owner, LiveProcessIdentity Editor);
public sealed record LiveOwnerExit(int Version, string SessionId, LiveProcessIdentity Owner, LiveProcessIdentity Editor,
    int ExitCode, bool AllOwnedExited, int ObservedProcesses, DateTimeOffset ObservedAt);

public static class LiveOwnerProtocol
{
    public static void Validate(LiveOwnerRequest request, string directory)
    {
        if (request.Version != 1 || !Guid.TryParseExact(request.SessionId, "D", out var session) || session == Guid.Empty ||
            !Guid.TryParseExact(request.OperationId, "N", out _) || request.ConnectionSeconds < 1 || request.InactivitySeconds < 10 || request.CleanupSeconds < 1)
            throw new InvalidDataException("Invalid live owner launch contract.");
        var files = new WorkspaceFiles(directory);
        if (!files.Root.Equals(Path.GetFullPath(request.Directory), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(files.Root).Equals(session.ToString("D"), StringComparison.OrdinalIgnoreCase) ||
            !files.Resolve("working.bpm").Equals(request.WorkingCopy, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Live owner staging identity mismatch.");
        WorkspaceFiles.RequireRevision(request.OriginalRevision);
        if (string.IsNullOrWhiteSpace(request.UserSid) || !request.UserSid.StartsWith("S-1-", StringComparison.Ordinal) || request.DesktopSessionId < 1)
            throw new InvalidDataException("Live activation requires an interactive user identity.");
        foreach (string path in new[] { request.OwnerExecutable, request.LiveExecutable, request.Installation, request.OriginalPath })
            if (!Path.IsPathFullyQualified(path)) throw new InvalidDataException("Live owner paths must be absolute.");
    }

    public static void ValidateStarted(LiveOwnerRequest request, LiveOwnerStarted started)
    {
        // Binding every identity prevents unrelated or stale process journals from
        // being interpreted as this launch, even after a process ID is recycled.
        if (started.Version != 1 || started.SessionId != request.SessionId ||
            started.Owner == null || started.Editor == null || started.Owner.ProcessId <= 0 || started.Editor.ProcessId <= 0 ||
            started.Owner.ProcessId == started.Editor.ProcessId || started.Owner.StartedAt == default ||
            started.Editor.StartedAt < started.Owner.StartedAt ||
            !string.Equals(started.Owner.Executable, request.OwnerExecutable, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(started.Editor.Executable, request.LiveExecutable, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Live owner start evidence identity mismatch.");
    }

    public static void ValidateExit(LiveOwnerRequest request, LiveOwnerStarted started, LiveOwnerExit exit)
    {
        ValidateStarted(request, started);
        if (exit.Version != 1 || exit.SessionId != request.SessionId || exit.Owner != started.Owner || exit.Editor != started.Editor ||
            exit.ObservedProcesses < 1 || exit.ObservedAt < started.Editor.StartedAt)
            throw new InvalidDataException("Live owner exit evidence identity mismatch.");
    }

    public static void WriteNew(string directory, string name, object value)
    {
        // An immutable fully flushed journal is never replaced or treated as a queue to replay.
        var files = new WorkspaceFiles(directory);
        string stage = files.Resolve(name + ".tmp"), final = files.Resolve(name);
        using (var output = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(output, value); output.Flush(true); }
        File.Move(stage, final);
    }
}
