using System.IO.Compression;
using System.Text.Json;
using McpBizagi.Contracts;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Creation policy tests are not a substitute for installed-engine MCP acceptance.</summary>
public sealed class NativeModelCreationTests
{
    [Theory]
    [InlineData("")] [InlineData(" ")] [InlineData(".")] [InlineData("..")] [InlineData("../outside")]
    [InlineData("a\\b")] [InlineData("x:y")] [InlineData("x?")] [InlineData("a\nb")]
    [InlineData("CON")] [InlineData("lpt1.xml")] [InlineData("PRN")] [InlineData("trailing.")] [InlineData("trailing ")]
    public void InvalidInitialLabelIsRejected(string name) => Assert.Throws<InvalidDataException>(() => NativeModelCreation.Prepare([name]));

    [Fact] public void CountAndNamesAreBoundedAndUnique()
    {
        Assert.Throws<InvalidDataException>(() => NativeModelCreation.Prepare(null!));
        Assert.Throws<InvalidDataException>(() => NativeModelCreation.Prepare([]));
        Assert.Throws<InvalidDataException>(() => NativeModelCreation.Prepare(Enumerable.Range(0, 101).Select(n => "Diagram " + n).ToArray()));
        Assert.Throws<InvalidDataException>(() => NativeModelCreation.Prepare(["one", "ONE"]));
        Assert.Throws<InvalidDataException>(() => NativeModelCreation.Prepare([new string('a', 121)]));
    }
    [Fact] public void PreparationRetainsOrderAndIndependentNativeIdentities()
    {
        var patch = NativeModelCreation.Prepare(["日本語 Ω", "Second diagram"]);
        NativeDiagramPolicy.Validate(patch);
        Assert.Equal(2, patch.Changes.Select(c => c.DiagramId).Distinct().Count());
        Assert.Equal(new[] { "日本語 Ω", "Second diagram" }, patch.Changes.Select(c => c.Name));
        Assert.Equal(patch.Changes.Select(c => c.DiagramId), patch.OpenedItems!.Select(i => i.DiagramId));
        Assert.Equal(new[] { true, false }, patch.OpenedItems!.Select(i => i.IsSelected));
    }
    private static byte[] Archive(NativeDiagramPatch patch)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            using (var info = new StreamWriter(zip.CreateEntry("ModelInfo.xml").Open())) info.Write("<BizAgiModelInfo/>");
            foreach (var c in patch.Changes)
            {
                using var inner = new MemoryStream();
                using (var diagram = new ZipArchive(inner, ZipArchiveMode.Create, true))
                {
                    using var writer = new StreamWriter(diagram.CreateEntry("Diagram.xml").Open());
                    writer.Write(new System.Xml.Linq.XElement(System.Xml.Linq.XName.Get("Package", "http://www.wfmc.org/2009/XPDL2.2"),
                        new System.Xml.Linq.XAttribute("Id", c.DiagramId), new System.Xml.Linq.XAttribute("Name", c.Name!)).ToString());
                }
                using var entry = zip.CreateEntry(c.DiagramId + ".diag").Open(); entry.Write(inner.ToArray());
            }
        }
        return memory.ToArray();
    }
    private static EngineReply Snapshot(NativeDiagramPatch patch) => new()
    {
        Success = true,
        DiagramState = new() { Diagrams = patch.Changes.Select(c => new NativeDiagramInfo { Id = c.DiagramId, Name = c.Name! }).ToArray(), OpenedItems = patch.OpenedItems! },
        Elements = patch.Changes.SelectMany(c => new[]
        {
            new NativeElement { Id = c.DiagramId, DiagramId = c.DiagramId, Kind = "Collaboration", Name = c.Name! },
            new NativeElement { Id = Guid.NewGuid().ToString(), DiagramId = c.DiagramId, Kind = "Participant", IsMainParticipant = true },
            new NativeElement { Id = Guid.NewGuid().ToString(), DiagramId = c.DiagramId, Kind = "Participant", IsMainParticipant = false },
            new NativeElement { Id = Guid.NewGuid().ToString(), DiagramId = c.DiagramId, Kind = "Process" },
            new NativeElement { Id = Guid.NewGuid().ToString(), DiagramId = c.DiagramId, Kind = "Process" }
        }).ToArray(),
        Scenarios = patch.Changes.Select(c => new NativeScenario { Id = "scenario", DiagramId = c.DiagramId }).ToArray()
    };
    [Theory]
    [InlineData("none")] [InlineData("names")] [InlineData("duplicate-diagram")] [InlineData("missing-node")]
    [InlineData("duplicate-node")] [InlineData("type")] [InlineData("geometry")]
    [InlineData("scenario")] [InlineData("tabs")] [InlineData("failure")] [InlineData("archive")] [InlineData("runtime-parent")] [InlineData("main-participant")]
    public void ReadbackMustMatchAllExposedNativeSemantics(string fault)
    {
        var patch = NativeModelCreation.Prepare(["First Ω", "Second"]); var created = Snapshot(patch);
        var reopened = JsonSerializer.Deserialize<EngineReply>(JsonSerializer.Serialize(created))!;
        byte[] archive = Archive(patch);
        switch (fault)
        {
            case "names": reopened.DiagramState!.Diagrams[0].Name = "Changed"; break;
            case "runtime-parent": created.Elements[0].ParentId = reopened.Elements[0].ParentId = Guid.NewGuid().ToString(); break;
            case "main-participant": reopened.Elements[1].IsMainParticipant = false; break;
            case "duplicate-diagram": reopened.DiagramState!.Diagrams[1].Id = reopened.DiagramState.Diagrams[0].Id; break;
            case "missing-node": reopened.Elements = reopened.Elements.Skip(1).ToArray(); break;
            case "duplicate-node": reopened.Elements[1].Id = reopened.Elements[0].Id; break;
            case "type": reopened.Elements[1].Kind = "Task"; break;
            case "geometry": reopened.Elements[1].Geometry = new() { X = 500, Y = 20, Width = 700, Height = 350 }; break;
            case "scenario": reopened.Scenarios[0].Name = "Changed"; break;
            case "tabs": Array.Reverse(reopened.DiagramState!.OpenedItems); break;
            case "failure": reopened.Success = false; break;
            case "archive": archive = Archive(NativeModelCreation.Prepare(["Unrelated"])); break;
        }
        if (fault == "none") NativeModelCreation.Verify(archive, patch, created, reopened);
        else Assert.Throws<InvalidDataException>(() => NativeModelCreation.Verify(archive, patch, created, reopened));
    }
}
