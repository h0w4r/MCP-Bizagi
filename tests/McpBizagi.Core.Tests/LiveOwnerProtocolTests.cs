using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Evidence admission policy only; these tests do not accredit native activation.</summary>
public sealed class LiveOwnerProtocolTests : IDisposable
{
    private readonly string root;
    private readonly LiveOwnerRequest request;
    private readonly LiveOwnerStarted started;
    private readonly LiveOwnerExit exit;

    public LiveOwnerProtocolTests()
    {
        string id = Guid.NewGuid().ToString("D");
        root = Path.Combine(Path.GetTempPath(), "mcp-owner-policy", id);
        Directory.CreateDirectory(root);
        request = new(1, id, Guid.NewGuid().ToString("N"), root, Path.Combine(root, "owner.exe"),
            Path.Combine(root, "editor.exe"), root, Path.Combine(root, "input.bpm"), new string('a', 64),
            Path.Combine(root, "working.bpm"), "S-1-5-21-1001", 1, 30, 120, 15);
        var at = DateTimeOffset.UtcNow.AddMinutes(-1);
        started = new(1, id, new(101, at, request.OwnerExecutable), new(102, at.AddSeconds(1), request.LiveExecutable));
        exit = new(1, id, started.Owner, started.Editor, 0, true, 3, at.AddSeconds(20));
    }

    [Fact] public void MatchedEvidenceIsAdmittedWithoutClaimingAnActualProcessWasRun()
    { LiveOwnerProtocol.Validate(request, root); LiveOwnerProtocol.ValidateExit(request, started, exit); }

    [Theory][InlineData("version")][InlineData("session")][InlineData("working")][InlineData("identity")][InlineData("desktop")]
    public void InvalidLaunchIsRejected(string field)
    {
        var invalid = field switch
        {
            "version" => request with { Version = 2 }, "session" => request with { SessionId = Guid.NewGuid().ToString("D") },
            "working" => request with { WorkingCopy = request.OriginalPath }, "identity" => request with { UserSid = "" },
            _ => request with { DesktopSessionId = 0 }
        };
        Assert.Throws<InvalidDataException>(() => LiveOwnerProtocol.Validate(invalid, root));
    }

    [Theory][InlineData("pid")][InlineData("start")][InlineData("executable")][InlineData("session")][InlineData("time")][InlineData("count")]
    public void SubstitutedExitEvidenceIsRejected(string field)
    {
        var invalid = field switch
        {
            "pid" => exit with { Editor = exit.Editor with { ProcessId = 999 } },
            "start" => exit with { Editor = exit.Editor with { StartedAt = exit.Editor.StartedAt.AddSeconds(1) } },
            "executable" => exit with { Owner = exit.Owner with { Executable = request.LiveExecutable } },
            "session" => exit with { SessionId = Guid.NewGuid().ToString("D") },
            "time" => exit with { ObservedAt = started.Owner.StartedAt.AddSeconds(-1) },
            _ => exit with { ObservedProcesses = 0 }
        };
        Assert.Throws<InvalidDataException>(() => LiveOwnerProtocol.ValidateExit(request, started, invalid));
    }

    [Fact] public void AbnormalExitIsRetainedAsEvidenceRatherThanRelabelledSuccess()
    { LiveOwnerProtocol.ValidateExit(request, started, exit with { ExitCode = -1, AllOwnedExited = false }); }

    [Fact] public void JournalCannotOverwriteAnEarlierObservation()
    {
        LiveOwnerProtocol.WriteNew(root, "test.json", started);
        Assert.Throws<IOException>(() => LiveOwnerProtocol.WriteNew(root, "test.json", exit));
        Assert.Contains("101", File.ReadAllText(Path.Combine(root, "test.json")));
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
