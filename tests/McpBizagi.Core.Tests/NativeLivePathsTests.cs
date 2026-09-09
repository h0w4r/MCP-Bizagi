using McpBizagi.BizagiAdapter;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class NativeLivePathsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mcp-live-path-tests-" + Guid.NewGuid().ToString("N"));
    public NativeLivePathsTests() => Directory.CreateDirectory(root);

    [Fact]
    public void ExistingOwnedNativeWorkingCopyIsAccepted()
    {
        string file = Path.Combine(root, "Document Ω.BPM"); File.WriteAllText(file, "path policy only");
        Assert.Equal(file, NativeLivePaths.RequireWorkingCopy(root, file));
    }

    [Fact]
    public void ParentTraversalAndSiblingPrefixAreRejected()
    {
        string sibling = root + "-sibling";
        Directory.CreateDirectory(sibling);
        try
        {
            string file = Path.Combine(sibling, "other.bpm"); File.WriteAllText(file, "path policy only");
            Assert.Throws<InvalidOperationException>(() => NativeLivePaths.RequireWorkingCopy(root, file));
            string escaped = Path.Combine(root, "..", Path.GetFileName(sibling), "other.bpm");
            Assert.Throws<InvalidOperationException>(() => NativeLivePaths.RequireWorkingCopy(root, escaped));
        }
        finally { Directory.Delete(sibling, true); }
    }

    [Theory]
    [InlineData("missing.bpm", false)][InlineData("document.bpmn", true)]
    public void MissingAndForeignFilesAreRejected(string name, bool exists)
    {
        string file = Path.Combine(root, name); if (exists) File.WriteAllText(file, "path policy only");
        Assert.Throws<InvalidOperationException>(() => NativeLivePaths.RequireWorkingCopy(root, file));
    }

    [Fact]
    public void RelativeWorkingCopyPathsAreRejected()
        => Assert.Throws<InvalidOperationException>(() => NativeLivePaths.RequireWorkingCopy(root, "document.bpm"));

    // Only this uniquely created test directory is owned by the fixture.
    public void Dispose() => Directory.Delete(root, true);
}
