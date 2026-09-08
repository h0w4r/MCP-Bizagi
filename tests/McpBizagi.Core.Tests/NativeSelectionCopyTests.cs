using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Independent policy adversarial tests, not installed-engine accreditation.</summary>
public sealed class NativeSelectionCopyTests
{
    private static string Id(int n) => $"00000000-0000-4000-8000-{n:000000000000}";
    private static NativeElement[] Graph() =>
    [
        new() { Id = Id(1), DiagramId = Id(1), Kind = "Collaboration" },
        new() { Id = Id(2), DiagramId = Id(1), ParentId = Id(1), Kind = "Participant" },
        new() { Id = Id(3), DiagramId = Id(1), ParentId = Id(2), Kind = "Process" },
        new() { Id = Id(4), DiagramId = Id(1), ParentId = Id(3), Kind = "Task", Name = "Review Ω", Geometry = new() { X = 20, Y = 30, Width = 100, Height = 60 } }
    ];
    private static NativeSelectionCopyRequest Request() => new() { SourceDiagramId = Id(1), TargetParentId = Id(3), ElementIds = [Id(4)], Position = new() { X = 40, Y = 50 } };
    private static NativeSelectionCopyReceipt Receipt() => new() { SourceDiagramId = Id(1), TargetDiagramId = Id(1), TargetParentId = Id(3), Identities = [new() { SourceId = Id(4), TargetId = Id(5) }] };
    private static NativeElement[] Reopened()
    {
        var source = Graph(); var copy = JsonSerializer.Deserialize<NativeElement>(JsonSerializer.Serialize(source[3]))!;
        copy.Id = Id(5); copy.Geometry!.X = 40; copy.Geometry.Y = 50;
        return [.. source, copy];
    }
    private static byte[] Archive(bool copied, string change = "")
    {
        const string ns = "http://www.wfmc.org/2009/XPDL2.2";
        string Node(int id, int x, int y, string name, string unknown = "keep") => $"<Activity Id='{Id(id)}' Name='{name}'><NodeGraphicsInfos><NodeGraphicsInfo><Coordinates XCoordinate='{x}' YCoordinate='{y}'/></NodeGraphicsInfo></NodeGraphicsInfos><Unknown Id='{Id(4)}' value='{unknown}'/></Activity>";
        string original = Node(4, 20, 30, change == "original" ? "changed" : "Review Ω");
        string extra = !copied || change == "missing-record" ? "" : Node(5, change == "position" ? 41 : 40, 50, change == "name" ? "changed" : "Review Ω", change == "unknown" ? "changed" : "keep");
        string xml = $"<Package xmlns='{ns}' Id='{Id(1)}'><PackageHeader><XPDLVersion>2.2</XPDLVersion></PackageHeader><WorkflowProcesses><WorkflowProcess Id='{Id(3)}'><Activities>{original}{extra}</Activities></WorkflowProcess></WorkflowProcesses></Package>";
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("ModelInfo.xml").Open())) writer.Write("<ModelInfo/>");
            using var inner = new MemoryStream();
            using (var diagram = new ZipArchive(inner, ZipArchiveMode.Create, true))
            {
                void Text(string name, string text) { using var writer = new StreamWriter(diagram.CreateEntry(name).Open()); writer.Write(text); }
                Text("Diagram.xml", xml);
                string values = change == "empty-whitespace" ? "<DiagramAttributeValues>\n  \n</DiagramAttributeValues>" :
                    change == "unknown-values" ? "<DiagramAttributeValues><!--retain unknown content--></DiagramAttributeValues>" : "<DiagramAttributeValues/>";
                Text("ExtendedAttributeValues.xml", values.Replace("<DiagramAttributeValues", "<DiagramAttributeValues xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'"));
                Text("ImageArtifactImages/" + Id(4) + ".png", "original encoded bytes");
                if (copied && change != "missing-image") Text("ImageArtifactImages/" + Id(5) + ".png", change == "image-bytes" ? "changed" : "original encoded bytes");
            }
            using var output = zip.CreateEntry(Id(1) + ".diag").Open(); output.Write(inner.ToArray());
        }
        return stream.ToArray();
    }

    [Fact] public void CompleteCopyRetainsUnknownGuidTextAndSourceBytes()
    {
        var result = NativeSelectionCopyPolicy.Compare(Archive(false), Archive(true), Graph(), Reopened(), Request(), Receipt());
        Assert.True(result.Preserved, JsonSerializer.Serialize(result));
    }
    [Theory]
    [InlineData("name")][InlineData("position")][InlineData("unknown")][InlineData("original")]
    [InlineData("missing-record")][InlineData("missing-image")][InlineData("image-bytes")]
    [InlineData("unknown-values")]
    public void CorruptCopiedOrOriginalArchiveIsRejected(string change)
    {
        Assert.False(NativeSelectionCopyPolicy.Compare(Archive(false), Archive(true, change), Graph(), Reopened(), Request(), Receipt()).Preserved);
    }
    [Fact] public void EmptyKnownValuesContainerMayRetainSerializerWhitespaceOnly()
    {
        Assert.True(NativeSelectionCopyPolicy.Compare(Archive(false), Archive(true, "empty-whitespace"), Graph(), Reopened(), Request(), Receipt()).Preserved);
    }
    [Theory]
    [InlineData("destination")][InlineData("reuse")][InlineData("duplicate")][InlineData("missing")][InlineData("bpmn")][InlineData("parent")]
    public void UntrustedReceiptOrReaderCannotRedefineIntent(string change)
    {
        var receipt = Receipt(); var after = Reopened();
        switch (change)
        {
            case "destination": receipt.TargetDiagramId = Id(9); break;
            case "reuse": receipt.Identities[0].TargetId = Id(4); break;
            case "duplicate": receipt.Identities = [receipt.Identities[0], receipt.Identities[0]]; break;
            case "missing": receipt.Identities = []; break;
            case "bpmn": receipt.Identities[0].TargetBpmnId = "invented"; break;
            case "parent": after[^1].ParentId = Id(2); break;
        }
        Assert.Throws<InvalidDataException>(() => NativeSelectionCopyPolicy.Compare(Archive(false), Archive(true), Graph(), after, Request(), receipt));
    }
    [Theory]
    [InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(-1)][InlineData(1000001)]
    public void InvalidDestinationCoordinateIsRejected(double value)
    {
        var request = Request(); request.Position!.X = value;
        Assert.Throws<InvalidDataException>(() => NativeSelectionCopyPolicy.Validate(request));
    }
    [Fact] public void DuplicateSelectionIsRejected()
    {
        var request = Request(); request.ElementIds = [Id(4), Id(4)];
        Assert.Throws<InvalidDataException>(() => NativeSelectionCopyPolicy.Validate(request));
    }
    [Fact] public void AbsentAndExternalReferencesAreRejectedBeforeEditor()
    {
        var request = Request(); request.ElementIds = [Id(9)];
        Assert.Throws<InvalidDataException>(() => NativeSelectionCopyPolicy.Closure(Graph(), request));
        var graph = Graph(); graph[3].DefaultSequenceFlowIds = [Id(9)];
        Assert.Throws<InvalidDataException>(() => NativeSelectionCopyPolicy.Closure(graph, Request()));
    }
    [Fact] public void ConnectorPlacementUsesVerticesRatherThanZeroSizedBox()
    {
        var graph = Graph(); graph[3].SourceId = Id(4); graph[3].TargetId = Id(4);
        graph[3].Points = [new() { X = 10, Y = 15 }, new() { X = 50, Y = 60 }];
        Assert.Equal((30f, 35f), NativeSelectionCopyPolicy.Offset(graph, Request()));
        var request = Request(); request.Position!.X = 40.5;
        Assert.Throws<InvalidDataException>(() => NativeSelectionCopyPolicy.Closure(graph, request));
    }
    [Fact] public void CompleteSubtreeIncludesOwnedDataNodesAndRejectsDoubleSelection()
    {
        var graph = Graph(); graph[3].Kind = "SubProcess"; graph[3].SubProcess = new();
        var task = new NativeElement { Id = Id(6), Kind = "Task", ParentId = Id(4), DiagramId = Id(1), Geometry = new() { X = 5, Y = 10 },
            DataFlow = new() { Inputs = [new() { Id = Id(7), Kind = "DataInput", ParentId = Id(6), DiagramId = Id(1) }] } };
        graph = [.. graph, task];
        Assert.Equal(new[] { Id(4), Id(6), Id(7) }, NativeSelectionCopyPolicy.Closure(graph, Request()).Select(e => e.Id).Order().ToArray());
        var request = Request(); request.ElementIds = [Id(4), Id(6)];
        Assert.Throws<InvalidDataException>(() => NativeSelectionCopyPolicy.Closure(graph, request));
    }
    [Fact] public void OwnedDataAssociationCannotEscapeSelectedClosure()
    {
        var graph = Graph(); graph[3].DataFlow = new()
        {
            Inputs = [new() { Id = Id(6), ParentId = Id(4), DiagramId = Id(1), Kind = "DataInput" }],
            InputAssociations = [new() { Id = Id(7), ParentId = Id(4), DiagramId = Id(1), Kind = "DataAssociation", SourceId = Id(9), TargetId = Id(6) }]
        };
        Assert.Throws<InvalidDataException>(() => NativeSelectionCopyPolicy.Closure(graph, Request()));
    }
    [Fact] public void SeparateEmbeddedCanvasesCannotBeCombinedAsOneSelection()
    {
        var graph = Graph(); graph[3].Kind = "SubProcess"; graph[3].SubProcess = new();
        graph = [.. graph,
            new() { Id = Id(6), ParentId = Id(4), DiagramId = Id(1), Kind = "Task", Geometry = new() { X = 5, Y = 10 } },
            new() { Id = Id(7), ParentId = Id(3), DiagramId = Id(1), Kind = "Task", Geometry = new() { X = 15, Y = 20 } }];
        var request = Request(); request.ElementIds = [Id(6), Id(7)];
        Assert.Throws<InvalidDataException>(() => NativeSelectionCopyPolicy.Closure(graph, request));
    }
    [Fact] public void ManualLabelAtOriginStillRequiresAnIntegralOffset()
    {
        var graph = Graph(); graph[3].Style = new() { LabelBounds = new() { Width = 80, Height = 40 } };
        var request = Request(); request.Position!.X = 40.5;
        Assert.Throws<InvalidDataException>(() => NativeSelectionCopyPolicy.Offset(graph, request));
    }
    [Theory]
    [InlineData("urn:source", true)]
    [InlineData("urn:destination", false)]
    public void CrossDiagramCopyPreservesInheritedQNameMeaning(string targetNamespace, bool sameMeaning)
    {
        var ns = XNamespace.Get("http://www.wfmc.org/2009/XPDL2.2");
        byte[] CrossArchive(bool copied)
        {
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("ModelInfo.xml").Open())) writer.Write("<ModelInfo/>");
                foreach (bool target in new[] { false, true })
                {
                    var sourceEntries = NativeArchive.ReadEntries(Archive(target && copied));
                    var xml = XDocument.Parse(System.Text.Encoding.UTF8.GetString(sourceEntries[Id(1) + ".diag!/Diagram.xml"]));
                    xml.Root!.SetAttributeValue(XNamespace.Xmlns + "q", target ? targetNamespace : "urn:source");
                    foreach (var node in xml.Descendants(ns + "Unknown")) node.SetAttributeValue("Reference", "q:Payload");
                    if (target)
                    {
                        xml.Root.SetAttributeValue("Id", Id(8)); xml.Descendants(ns + "WorkflowProcess").Single().SetAttributeValue("Id", Id(10));
                        xml.Descendants(ns + "Activity").Where(e => (string?)e.Attribute("Id") == Id(4)).Remove();
                    }
                    using var nested = new MemoryStream();
                    using (var diagram = new ZipArchive(nested, ZipArchiveMode.Create, true))
                    {
                        foreach (var pair in sourceEntries.Where(e => e.Key.Contains("!/")))
                        {
                            string leaf = pair.Key.Split("!/")[1];
                            using var output = diagram.CreateEntry(leaf).Open();
                            if (leaf == "Diagram.xml") { using var writer = new StreamWriter(output); writer.Write(xml); }
                            else output.Write(pair.Value);
                        }
                    }
                    using var destination = zip.CreateEntry(Id(target ? 8 : 1) + ".diag").Open(); destination.Write(nested.ToArray());
                }
            }
            return stream.ToArray();
        }
        NativeElement[] destination = [new() { Id = Id(8), DiagramId = Id(8), Kind = "Collaboration" },
            new() { Id = Id(9), DiagramId = Id(8), ParentId = Id(8), Kind = "Participant" },
            new() { Id = Id(10), DiagramId = Id(8), ParentId = Id(9), Kind = "Process" }];
        var request = Request(); request.TargetParentId = Id(10);
        var receipt = Receipt(); receipt.TargetDiagramId = Id(8); receipt.TargetParentId = Id(10);
        var after = Reopened(); after[^1].ParentId = Id(10); after[^1].DiagramId = Id(8);
        NativeFidelityReport Check() => NativeSelectionCopyPolicy.Compare(CrossArchive(false), CrossArchive(true), [.. Graph(), .. destination], [.. after, .. destination], request, receipt);
        if (sameMeaning) Assert.True(Check().Preserved);
        else Assert.Throws<InvalidDataException>(() => Check());
    }
}
