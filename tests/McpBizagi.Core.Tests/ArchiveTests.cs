using System.IO.Compression;
using System.Text;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class ArchiveTests
{
    private static byte[] Archive(params (string Name, string Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
            foreach (var entry in entries)
                using (var writer = new StreamWriter(archive.CreateEntry(entry.Name).Open(), Encoding.UTF8)) writer.Write(entry.Content);
        return buffer.ToArray();
    }
    [Fact] public void InvalidContainerIsRejected() => Assert.Throws<InvalidDataException>(() => NativeArchive.Validate([1, 2, 3]));
    [Fact] public void RequiredMetadataIsChecked() => Assert.Throws<InvalidDataException>(() => NativeArchive.Validate(Archive(("data.xml", "<data/>"))));
    [Fact] public void ParentTraversalIsRejected() => Assert.Throws<InvalidDataException>(() => NativeArchive.Validate(Archive(("ModelInfo.xml", "<model/>"), ("../escape.xml", "<data/>"))));
    [Fact] public void DuplicateEntriesAreRejected() => Assert.Throws<InvalidDataException>(() => NativeArchive.Validate(Archive(("ModelInfo.xml", "<model/>"), ("modelinfo.xml", "<model/>"))));
    [Fact] public void ValidMetadataCanBeInspectedWithoutExtraction() => NativeArchive.Validate(Archive(("ModelInfo.xml", "<model/>")));
}
