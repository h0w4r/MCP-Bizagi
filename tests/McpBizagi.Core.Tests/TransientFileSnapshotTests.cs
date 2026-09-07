using McpBizagi.BizagiAdapter;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Real local I/O contracts, separate from installed-engine rendering accreditation.</summary>
public sealed class TransientFileSnapshotTests
{
    [Fact] public void ObserverDoesNotDenyConcurrentWriterOrReplacement()
    {
        string path = Path.GetTempFileName(), replacement = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "original");
            using (var observation = TransientFileSnapshot.Open(path))
            {
                // Match a native XML writer's sharing while the observer still holds its read handle.
                using (var writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))) writer.Write("updated");
                File.WriteAllText(replacement, "replaced Ω");
                File.Replace(replacement, path, null);
                Assert.Equal("replaced Ω", TransientFileSnapshot.ReadText(path));
                Assert.False(observation.CanWrite);
            }
        }
        finally { File.Delete(path); File.Delete(replacement); }
    }
    [Fact] public void SnapshotReadsUnicodeWithoutChangingTheFile()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "<value>日本語 Ω</value>"); var before = File.ReadAllBytes(path);
            Assert.Equal("<value>日本語 Ω</value>", TransientFileSnapshot.ReadText(path));
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(10)]
    public void SnapshotIsBoundedBeforeItConsumesUntrustedSize(int limit)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, new string('x', 20));
            if (limit < 1) Assert.Throws<ArgumentOutOfRangeException>(() => TransientFileSnapshot.ReadText(path, limit));
            else Assert.Throws<InvalidDataException>(() => TransientFileSnapshot.ReadText(path, limit));
        }
        finally { File.Delete(path); }
    }
}
