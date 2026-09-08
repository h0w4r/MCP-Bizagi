using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using McpBizagi.Contracts;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Pure relocation-policy adversarial tests; never used as native operational evidence.</summary>
public sealed class NativeReparentingTests
{
    private static string Id(int n) => $"00000000-0000-4000-8000-{n:000000000000}";
    private static readonly string D = Id(1), Pool = Id(2), P = Id(3), A = Id(4), Task = Id(5), B = Id(6);
    private const string Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static NativeReparenting[] Moves => [new() { ElementId = Task, ExpectedParentId = A, TargetParentId = B }];
    private static NativeElement[] Graph()
    {
        NativeElement E(string id, string kind, string parent) => new() { Id = id, Kind = kind, ElementType = kind, ParentId = parent, DiagramId = D };
        var graph = new[] { E(D, "Collaboration", ""), E(Pool, "Participant", D), E(P, "Process", Pool), E(A, "SubProcess", P), E(B, "SubProcess", P), E(Task, "UserTask", A) };
        foreach (var node in graph.Where(e => e.Kind == "SubProcess")) node.SubProcess = new() { Kind = "SubProcess" };
        graph[^1].Geometry = new() { X = 20, Y = 30, Width = 100, Height = 60 };
        return graph;
    }
    private static string Xml(bool moved, int x = 20)
    {
        string task = $"<Activity Id='{Task}' Name='Task Ω'><Documentation>日本語</Documentation><NodeGraphicsInfos><NodeGraphicsInfo Width='100' Height='60'><Coordinates XCoordinate='{x}' YCoordinate='30'/></NodeGraphicsInfo></NodeGraphicsInfos></Activity>";
        string Set(string id, string content) => $"<ActivitySet Id='{id}'><Activities>{content}</Activities></ActivitySet>";
        return $"<Package xmlns='{Ns}' Id='{D}'><Pools><Pool Id='{Pool}' Process='{P}'/></Pools><WorkflowProcesses><WorkflowProcess Id='{P}'><ActivitySets>{Set(A, moved ? "" : task)}{Set(B, moved ? task : "")}</ActivitySets><Activities><Activity Id='{A}'><BlockActivity ActivitySetId='{A}'/></Activity><Activity Id='{B}'><BlockActivity ActivitySetId='{B}'/></Activity></Activities></WorkflowProcess></WorkflowProcesses></Package>";
    }
    private static byte[] Archive(string xml, string payload = "unchanged")
    {
        // A small synthetic archive isolates comparison logic from the proprietary engine.
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("ModelInfo.xml").Open())) writer.Write("<ModelInfo/>");
            using (var writer = new StreamWriter(zip.CreateEntry("unknown.bin").Open())) writer.Write(payload);
            using var nestedBytes = new MemoryStream();
            using (var nested = new ZipArchive(nestedBytes, ZipArchiveMode.Create, true))
            { using var writer = new StreamWriter(nested.CreateEntry("Diagram.xml").Open()); writer.Write(xml); }
            using var stream = zip.CreateEntry(D + ".diag").Open(); stream.Write(nestedBytes.ToArray());
        }
        return bytes.ToArray();
    }
    private static NativeFidelityReport Compare(string oldXml, string newXml, string payload = "unchanged", NativeReparenting[]? moves = null)
    {
        moves ??= Moves; var graph = Graph();
        return NativeReparentingPolicy.Compare(Archive(oldXml), Archive(newXml, payload), graph, NativeReparentingPolicy.Expected(graph, moves), moves);
    }
    private static void Reject(Action action)
    {
        var error = Record.Exception(action);
        Assert.True(error is InvalidDataException or InvalidOperationException or NotSupportedException, "Expected an explicit rejected contract, not an unrelated runtime fault: " + error);
    }
    [Fact] public void ExactContainedMovePreservesNativePayload() => Assert.True(Compare(Xml(false), Xml(true)).Preserved);
    [Fact] public void ExplicitPositionUsesExistingGeometryProjection()
    {
        var moves = Moves; moves[0].Position = new() { X = 70, Y = 30 };
        Assert.True(Compare(Xml(false), Xml(true, 70), moves: moves).Preserved);
    }
    [Fact] public void OriginalBinaryChangesAreNotHidden() => Assert.False(Compare(Xml(false), Xml(true), "changed").Preserved);
    [Fact] public void MovedUnknownXmlChangesAreNotHidden() => Assert.False(Compare(Xml(false), Xml(true).Replace("日本語", "lost")).Preserved);
    [Fact] public void WrongDurableOwnerRejects() => Reject(() => Compare(Xml(false), Xml(false)));
    [Fact] public void UnknownCollectionAttributesReject() => Reject(() => Compare(Xml(false).Replace("<Activities>", "<Activities unknown='preserve'>"), Xml(true)));
    [Fact] public void AddedCollectionCommentsReject() => Reject(() => Compare(Xml(false), Xml(true).Replace("<Activities>", "<Activities><!--unexpected-->")));
    [Fact] public void PreservedWhitespaceRejects() => Reject(() => Compare(Xml(false).Replace("<ActivitySet ", "<ActivitySet xml:space='preserve' "), Xml(true)));
    [Fact] public void ChangedUnrelatedShapeRejects() => Assert.False(Compare(Xml(false), Xml(true).Replace($"<Activity Id='{A}'>", $"<Activity Id='{A}' Unknown='changed'>")).Preserved);
    [Fact] public void AddedUnrequestedCollectionMemberRejects() => Reject(() => Compare(Xml(false), Xml(true).Replace("<Activities></Activities>", $"<Activities><Activity Id='{Id(99)}'/></Activities>")));
    [Fact] public void ExpectedOwnerIsAnActualPrecondition()
    {
        var moves = Moves; moves[0].ExpectedParentId = P; Reject(() => NativeReparentingPolicy.Expected(Graph(), moves));
    }
    [Fact] public void CyclicTargetRejects()
    {
        var graph = Graph(); graph.Single(e => e.Id == B).ParentId = A;
        Reject(() => NativeReparentingPolicy.Expected(graph, [new() { ElementId = A, ExpectedParentId = P, TargetParentId = B }]));
    }
    [Fact] public void OverlappingSelectionRejects() => Reject(() => NativeReparentingPolicy.Expected(Graph(),
        [new() { ElementId = A, ExpectedParentId = P, TargetParentId = B }, Moves[0]]));
    [Fact] public void InconsistentSourceDiagramCannotBeSilentlyRepaired()
    {
        var graph = Graph(); graph.Single(e => e.Id == B).DiagramId = Id(99); Reject(() => NativeReparentingPolicy.Expected(graph, Moves));
    }
    private static NativeElement[] CrossGraph()
    {
        // Distinct real containment chains, rather than changing only a label.
        return Graph().Concat(new[] {
            new NativeElement { Id = Id(90), Kind = "Collaboration", DiagramId = Id(90) },
            new NativeElement { Id = Id(91), Kind = "Participant", ParentId = Id(90), DiagramId = Id(90) },
            new NativeElement { Id = Id(92), Kind = "Process", ParentId = Id(91), DiagramId = Id(90) }
        }).ToArray();
    }
    private static NativeReparenting CrossMove() => new() { ElementId = A, ExpectedParentId = P,
        TargetParentId = Id(92), ExpectedDiagramId = D, TargetDiagramId = Id(90) };
    [Fact] public void CrossDiagramRequiresExplicitDiagramIdentities()
    {
        var move = CrossMove(); move.ExpectedDiagramId = null; move.TargetDiagramId = null;
        Reject(() => NativeReparentingPolicy.Expected(CrossGraph(), [move]));
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void StaleCrossDiagramIdentityRejects(bool source)
    {
        var move = CrossMove(); if (source) move.ExpectedDiagramId = Id(99); else move.TargetDiagramId = Id(99);
        Reject(() => NativeReparentingPolicy.Expected(CrossGraph(), [move]));
    }
    [Fact] public void CrossDiagramSubtreeAndNestedIoFollowFinalOwner()
    {
        var graph = CrossGraph(); graph.Single(e => e.Id == Task).DataFlow = new() {
            Inputs = [new() { Id = Id(80), ParentId = Task, DiagramId = D, Kind = "DataInput" }],
            InputAssociations = [new() { Id = Id(81), ParentId = Task, DiagramId = D, Kind = "DataInputAssociation" }] };
        var after = NativeReparentingPolicy.Expected(graph, [CrossMove()]).ToDictionary(e => e.Id);
        Assert.Equal(Id(92), after[A].ParentId); Assert.Equal(A, after[Task].ParentId);
        Assert.Equal(Id(90), after[A].DiagramId); Assert.Equal(Id(90), after[Task].DiagramId);
        Assert.Equal(Id(90), after[Task].DataFlow!.Inputs[0].DiagramId);
        Assert.Equal(Id(90), after[Task].DataFlow!.InputAssociations[0].DiagramId);
        Assert.Equal(D, after[B].DiagramId); Assert.Equal(D, graph.Single(e => e.Id == Task).DiagramId);
    }
    [Fact] public void IncidentMessageFlowCannotBeAbandonedInSourceDiagram()
    {
        var graph = CrossGraph().Append(new NativeElement { Id = Id(70), Kind = "MessageFlow", ParentId = D,
            DiagramId = D, SourceId = Task, TargetId = B }).ToArray();
        Reject(() => NativeReparentingPolicy.Expected(graph, [CrossMove()]));
    }
    [Fact] public void IncompleteSequenceClosureRejects()
    {
        var graph = Graph().Concat(new[] { new NativeElement { Id = Id(9), Kind = "SequenceFlow", ParentId = A, DiagramId = D, SourceId = Task, TargetId = B } }).ToArray();
        Reject(() => NativeReparentingPolicy.Expected(graph, Moves));
    }
    [Fact] public void BoundaryCannotBeLeftBehind()
    {
        var graph = Graph().Concat(new[] { new NativeElement { Id = Id(9), Kind = "BoundaryEvent", ParentId = A, DiagramId = D, Event = new() { AttachedToActivityId = Task } } }).ToArray();
        Reject(() => NativeReparentingPolicy.Expected(graph, Moves));
    }
    [Fact] public void ReadbackMustPreserveEveryOtherObservedField()
    {
        var graph = Graph(); var after = NativeReparentingPolicy.Expected(graph, Moves); after.Single(e => e.Id == Task).Documentation = "lost";
        Reject(() => NativeReparentingPolicy.Verify(graph, after, Moves));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeIoCompanionMovesWithItsOwnerAndRetainsUnknownContent(bool alter)
    {
        var graph = Graph(); string binding = Id(20); graph[^1].DataFlow = new() { InputAssociations = [new() { Id = binding, Kind = "DataInputAssociation", ParentId = Task }] };
        string WithBinding(bool moved)
        {
            var doc = XDocument.Parse(Xml(moved)); XNamespace ns = Ns;
            doc.Descendants(ns + "ActivitySet").Single(e => (string?)e.Attribute("Id") == (moved ? B : A)).Add(
                new XElement(ns + "DataAssociations", new XElement(ns + "DataAssociation", new XAttribute("Id", binding), new XAttribute("Unknown", moved && alter ? "changed" : "preserve"))));
            return doc.ToString();
        }
        var report = NativeReparentingPolicy.Compare(Archive(WithBinding(false)), Archive(WithBinding(true)), graph, NativeReparentingPolicy.Expected(graph, Moves), Moves);
        Assert.Equal(!alter, report.Preserved);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("attribute")]
    [InlineData("comment")]
    [InlineData("text")]
    [InlineData("port-payload")]
    [InlineData("port-owner")]
    public void WholeSubprocessRelocatesItsFlattenedActivitySetAcrossProcesses(string alteration)
    {
        string pool2 = Id(10), process2 = Id(11); var graph = Graph().Concat(new[] {
            new NativeElement { Id = pool2, Kind = "Participant", ParentId = D, DiagramId = D },
            new NativeElement { Id = process2, Kind = "Process", ParentId = pool2, DiagramId = D } }).ToArray();
        NativeReparenting[] moves = [new() { ElementId = A, ExpectedParentId = P, TargetParentId = process2 }];
        string portId = Id(21);
        graph.Single(e => e.Id == Task).DataFlow = new() { Inputs = [new() { Id = portId, Kind = "DataInput", ParentId = Task }] };
        XNamespace ns = Ns; var original = XDocument.Parse(Xml(false));
        original.Descendants(ns + "WorkflowProcess").Single().Add(new XElement(ns + "DataInputOutputs",
            new XElement(ns + "DataInput", new XAttribute("Id", portId), new XAttribute("Unknown", "retained"))));
        original.Root!.Element(ns + "Pools")!.Add(new XElement(ns + "Pool", new XAttribute("Id", pool2), new XAttribute("Process", process2)));
        original.Root.Element(ns + "WorkflowProcesses")!.Add(new XElement(ns + "WorkflowProcess", new XAttribute("Id", process2)));
        var moved = new XDocument(original);
        var target = moved.Descendants(ns + "WorkflowProcess").Single(e => (string?)e.Attribute("Id") == process2);
        var activity = moved.Descendants(ns + "Activity").Single(e => (string?)e.Attribute("Id") == A); activity.Remove();
        var set = moved.Descendants(ns + "ActivitySet").Single(e => (string?)e.Attribute("Id") == A); set.Remove();
        target.Add(new XElement(ns + "ActivitySets", set), new XElement(ns + "Activities", activity));
        var port = moved.Descendants(ns + "DataInput").Single();
        if (alteration != "port-owner")
        {
            var container = port.Parent!; port.Remove(); container.Remove();
            target.Add(new XElement(ns + "DataInputOutputs", port));
        }
        if (alteration == "port-payload") port.SetAttributeValue("Unknown", "changed");
        // Comparison-only empty-wrapper cleanup must not hide arbitrary owner payload.
        if (alteration == "attribute") target.SetAttributeValue("Unrequested", "changed");
        if (alteration == "comment") target.Add(new XComment("Retain this unexpected comment"));
        if (alteration == "text") target.Add(new XText("Retain this unexpected text"));
        NativeFidelityReport CompareMoved() => NativeReparentingPolicy.Compare(Archive(original.ToString()), Archive(moved.ToString()), graph, NativeReparentingPolicy.Expected(graph, moves), moves);
        if (alteration == "port-owner") { Reject(() => CompareMoved()); return; }
        var report = CompareMoved();
        Assert.True(report.Preserved == (alteration == "none"), JsonSerializer.Serialize(report.Differences));
    }
}
