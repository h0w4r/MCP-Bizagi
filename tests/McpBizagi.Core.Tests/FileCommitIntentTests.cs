using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Filesystem transaction policy tests, not substitutes for installed-engine MCP acceptance.</summary>
public sealed class FileCommitIntentTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mcp-commit-tests-" + Guid.NewGuid().ToString("N"));
    private WorkspaceFiles Files => new(root);
    private static readonly byte[] Original = [1, 2, 3], Updated = [4, 5, 6, 7];

    [Theory] [InlineData(false)] [InlineData(true)]
    public void PublishedBytesAndRequiredBackupMatchDurableIntent(bool replace)
    {
        string? previous = replace ? Files.Commit("nested Ω/file.bpm", Original, null).Revision : null;
        FileCommitIntent? intent = null;
        var result = Files.Commit("nested Ω/file.bpm", Updated, previous, value =>
        {
            intent = value;
            Assert.Equal(Updated, File.ReadAllBytes(value.StagePath));
            Assert.Equal("not_applied", Files.ObserveCommit(value).State);
        });
        Assert.NotNull(intent);
        Assert.Equal("applied", Files.ObserveCommit(intent!).State);
        Assert.Equal(Updated, File.ReadAllBytes(result.Path));
        if (replace) Assert.Equal(Original, File.ReadAllBytes(result.BackupPath!));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void PreparationCancellationRetainsStageWithoutChangingDestination(bool replace)
    {
        string? previous = replace ? Files.Commit("model.bpm", Original, null).Revision : null;
        FileCommitIntent? intent = null;
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => Files.Commit("model.bpm", Updated, previous,
            value => { intent = value; cancellation.Cancel(); }, cancellation.Token));
        Assert.NotNull(intent);
        Assert.Equal("not_applied", Files.ObserveCommit(intent!).State);
        if (replace) Assert.Equal(Original, Files.Read("model.bpm")); else Assert.False(File.Exists(Files.Resolve("model.bpm")));
    }

    [Fact] public void JournalFailureNeverPublishesAndRetainsPotentiallyReferencedStage()
    {
        FileCommitIntent? intent = null;
        Assert.Throws<IOException>(() => Files.Commit("model.bpm", Updated, null, value => { intent = value; throw new IOException("Journal unavailable"); }));
        Assert.Equal("not_applied", Files.ObserveCommit(intent!).State);
    }

    [Fact] public void StaleDestinationNeverReachesPreparedCallback()
    {
        Files.Commit("model.bpm", Original, null);
        bool called = false;
        Assert.Throws<IOException>(() => Files.Commit("model.bpm", Updated, new string('0', 64), _ => called = true));
        Assert.False(called); Assert.Equal(Original, Files.Read("model.bpm")); Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }

    [Fact] public void MissingBackupIsAmbiguousEvenWhenDestinationMatches()
    {
        string prior = Files.Commit("model.bpm", Original, null).Revision;
        FileCommitIntent? intent = null;
        var saved = Files.Commit("model.bpm", Updated, prior, value => intent = value);
        File.Delete(saved.BackupPath!);
        Assert.Equal("ambiguous", Files.ObserveCommit(intent!).State);
        Assert.Equal(Updated, Files.Read("model.bpm"));
    }

    [Fact] public void ChangedDestinationIsConflictAndIsNeverRolledBack()
    {
        FileCommitIntent? intent = null;
        var saved = Files.Commit("model.bpm", Updated, null, value => intent = value);
        File.WriteAllBytes(saved.Path, Original);
        Assert.Equal("conflict", Files.ObserveCommit(intent!).State);
        Assert.Equal(Original, Files.Read("model.bpm"));
    }

    [Fact] public void IdenticalReplacementNeedsConsumedStageAndMatchingBackup()
    {
        string prior = Files.Commit("model.bpm", Original, null).Revision;
        FileCommitIntent? intent = null;
        Files.Commit("model.bpm", Original, prior, value => { intent = value; Assert.Equal("not_applied", Files.ObserveCommit(value).State); });
        Assert.Equal("applied", Files.ObserveCommit(intent!).State);
    }

    [Fact] public void LockedDestinationIsUnreadableNotAbsent()
    {
        FileCommitIntent? intent = null;
        var saved = Files.Commit("model.bpm", Updated, null, value => intent = value);
        using var handle = new FileStream(saved.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal("unreadable", Files.ObserveCommit(intent!).State);
    }

    [Fact] public void CorruptedStageCannotBePublished()
    {
        FileCommitIntent? intent = null;
        Assert.Throws<IOException>(() => Files.Commit("model.bpm", Updated, null, value => { intent = value; File.WriteAllBytes(value.StagePath, Original); }));
        Assert.Equal("conflict", Files.ObserveCommit(intent!).State);
        Assert.False(File.Exists(Files.Resolve("model.bpm")));
    }

    [Fact] public void TamperedReceiptCannotSelectAnArbitraryFileOrUnknownSchema()
    {
        FileCommitIntent? intent = null;
        Files.Commit("model.bpm", Updated, null, value => intent = value);
        Assert.Throws<InvalidDataException>(() => Files.ObserveCommit(intent! with { StagePath = Files.Resolve("private.txt") }));
        Assert.Throws<InvalidDataException>(() => Files.ObserveCommit(intent! with { Version = 2 }));
        Assert.Throws<InvalidDataException>(() => Files.ObserveCommit(intent! with { OutputRevision = "not-sha256" }));
        Assert.Throws<UnauthorizedAccessException>(() => Files.ObserveCommit(intent! with { Path = Path.Combine(root, "..", "outside.bpm") }));
    }

    public void Dispose()
    {
        // Only remove the unique test-owned directory after checking its resolved parent and prefix.
        if (Directory.Exists(root) && Path.GetDirectoryName(root) == Path.TrimEndingDirectorySeparator(Path.GetTempPath()) &&
            Path.GetFileName(root).StartsWith("mcp-commit-tests-", StringComparison.Ordinal)) Directory.Delete(root, true);
    }
}
