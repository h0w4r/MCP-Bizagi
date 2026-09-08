using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using McpBizagi.Contracts;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Synthetic adversarial archive tests, not installed-engine accreditation.</summary>
public sealed class NativeCrossDiagramReparentingTests
{
    private static string Id(int n) => $"00000000-0000-4000-8000-{n:000000000000}";
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static NativeElement[] Graph() => [
        new() { Id = Id(1), Kind = "Collaboration", DiagramId = Id(1) },
        new() { Id = Id(2), Kind = "Participant", ParentId = Id(1), DiagramId = Id(1) },
        new() { Id = Id(3), Kind = "Process", ParentId = Id(2), DiagramId = Id(1) },
        new() { Id = Id(4), Kind = "Collaboration", DiagramId = Id(4) },
        new() { Id = Id(5), Kind = "Participant", ParentId = Id(4), DiagramId = Id(4) },
        new() { Id = Id(6), Kind = "Process", ParentId = Id(5), DiagramId = Id(4) },
        new() { Id = Id(7), Kind = "UserTask", ElementType = "UserTask", ParentId = Id(3), DiagramId = Id(1) }
    ];
    private static NativeReparenting[] Moves => [new() { ElementId = Id(7), ExpectedParentId = Id(3), TargetParentId = Id(6),
        ExpectedDiagramId = Id(1), TargetDiagramId = Id(4) }];
    private static byte[] Archive(bool moved, string alteration = "none")
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Entry(ZipArchive z, string name, string value)
            { using var stream = z.CreateEntry(name).Open(); stream.Write(Encoding.UTF8.GetBytes(value)); }
            Entry(zip, "ModelInfo.xml", "<ModelInfo/>");
            Entry(zip, "unknown.bin", moved && alteration == "unrelated-bytes" ? "altered" : "retained");
            foreach (int d in new[] { 1, 4 })
            {
                bool hasTask = d == (moved ? 4 : 1);
                var process = new XElement(Ns + "WorkflowProcess", new XAttribute("Id", Id(d + 2)));
                if (hasTask) process.Add(new XElement(Ns + "Activities", new XElement(Ns + "Activity", new XAttribute("Id", Id(7)),
                    new XAttribute("Name", "Preserved Ω"), new XElement(Ns + "Documentation", moved && alteration == "task-payload" ? "changed" : "日本語"))));
                var doc = new XDocument(new XElement(Ns + "Package", new XAttribute("Id", Id(d)),
                    new XElement(Ns + "Pools", new XElement(Ns + "Pool", new XAttribute("Id", Id(d + 1)), new XAttribute("Process", Id(d + 2)))),
                    new XElement(Ns + "WorkflowProcesses", process)));
                if (moved && hasTask && alteration == "collection-comment") process.Element(Ns + "Activities")!.Add(new XComment("unknown"));
                if (moved && alteration == "diagram-payload") doc.Root!.SetAttributeValue("Unknown", "changed");
                var values = new XElement("DiagramAttributeValues");
                bool hasOwner = alteration == "wrong-value-owner" && moved ? d == 1 : hasTask;
                if (hasOwner) values.Add(new XElement("ElementAttributeValues", new XAttribute("ElementId", Id(7)), new XAttribute("Unknown", "keep"),
                    new XElement("Values", new XElement("ExtendedAttributeValue", new XAttribute("Id", Id(8)), new XAttribute("Type", "FileEmbedded"),
                        new XElement("Content", "attachment:Evidence Ω.bin"), new XElement("Unknown", moved && alteration == "attribute-payload" ? "changed" : "retain")))));
                if (moved && alteration == "attribute-comment") values.Add(new XComment("must not disappear"));
                using var nestedBytes = new MemoryStream();
                using (var nested = new ZipArchive(nestedBytes, ZipArchiveMode.Create, true))
                {
                    Entry(nested, "Diagram.xml", doc.ToString());
                    Entry(nested, "ExtendedAttributeValues.xml", values.ToString());
                    if (d == 1 && alteration == "presentation-actions") Entry(nested, "Actions.xml", "<Actions><Action id='preserve'/></Actions>");
                    if (d == 1 && alteration == "simulation-results") Entry(nested, "BPSimDataResult.xml", "<Results><Result id='preserve'/></Results>");
                    if (hasTask && !(moved && alteration == "missing-binary") || moved && d == 1 && alteration == "duplicate-binary")
                        Entry(nested, "Files/" + Id(7) + "/Evidence Ω.bin", moved && alteration == "binary-payload" ? "changed" : "retained binary");
                }
                using var stream = zip.CreateEntry(Id(d) + ".diag").Open(); stream.Write(nestedBytes.ToArray());
            }
        }
        return output.ToArray();
    }
    [Theory]
    [InlineData("none")]
    [InlineData("unrelated-bytes")]
    [InlineData("task-payload")]
    [InlineData("diagram-payload")]
    [InlineData("attribute-payload")]
    [InlineData("binary-payload")]
    [InlineData("wrong-value-owner")]
    [InlineData("missing-binary")]
    [InlineData("duplicate-binary")]
    [InlineData("collection-comment")]
    [InlineData("attribute-comment")]
    public void OnlyDeclaredRelocationCanPassArchiveFidelity(string alteration)
    {
        var graph = Graph(); NativeFidelityReport? report = null;
        var failure = Record.Exception(() => report = NativeReparentingPolicy.Compare(Archive(false), Archive(true, alteration), graph,
            NativeReparentingPolicy.Expected(graph, Moves), Moves, new EngineReply { DiagramState = new() }));
        if (alteration == "none") { Assert.Null(failure); Assert.True(report!.Preserved); }
        else if (failure != null) Assert.IsType<InvalidDataException>(failure);
        else Assert.False(report!.Preserved);
    }
    [Fact]
    public void CrossDiagramPreflightRequiresNativeEvidence()
    {
        Assert.Throws<InvalidDataException>(() => NativeReparentingPolicy.Preflight(Archive(false), new EngineReply { Elements = Graph() }, Moves));
    }
    [Theory] [InlineData("presentation-actions")] [InlineData("simulation-results")]
    public void DiagramScopedContentRequiresAnExplicitMigrationContract(string content)
    {
        var reply = new EngineReply { Elements = Graph(), Metadata = new(), Documentation = new(), DiagramState = new() };
        Assert.Throws<NotSupportedException>(() => NativeReparentingPolicy.Preflight(Archive(false, content), reply, Moves));
    }
    [Fact]
    public void MovedConfiguredSimulationReferencesCannotBeSilentlyAbandoned()
    {
        var reply = new EngineReply { Elements = Graph(), Documentation = new(), DiagramState = new(), Metadata = new() {
            Simulations = [new() { DiagramId = Id(1), Xml = $"<BPSimData xmlns='http://www.bpsim.org/schemas/1.0'><Scenario><ElementParameters elementRef='{Id(7)}'/></Scenario></BPSimData>" }] } };
        Assert.Throws<NotSupportedException>(() => NativeReparentingPolicy.Preflight(Archive(false), reply, Moves));
    }
}
