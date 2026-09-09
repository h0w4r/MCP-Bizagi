using McpBizagi.Contracts;
using McpBizagi.Core;
using System.Text.Json;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Independent policy tests only; native editor operation requires separate MCP acceptance.</summary>
public sealed class NativeAlignmentTests
{
    private static string Id(int n) => $"00000000-0000-4000-8000-{n:000000000000}";
    private static NativeElement[] Graph() =>
    [
        new() { Id = Id(1), Kind = "Collaboration", DiagramId = Id(1) },
        new() { Id = Id(2), Kind = "Participant", ParentId = Id(1), DiagramId = Id(1) },
        new() { Id = Id(3), Kind = "Process", ParentId = Id(2), DiagramId = Id(1) },
        new() { Id = Id(4), Kind = "UserTask", ParentId = Id(3), DiagramId = Id(1), Geometry = new() { X = 20, Y = 30, Width = 100, Height = 60 } },
        new() { Id = Id(5), Kind = "ManualTask", ParentId = Id(3), DiagramId = Id(1), Geometry = new() { X = 220, Y = 100, Width = 80, Height = 40 } },
        new() { Id = Id(6), Kind = "Task", ParentId = Id(3), DiagramId = Id(1), Geometry = new() { X = 170, Y = 70, Width = 30, Height = 30 } }
    ];
    private static NativeAlignmentRequest Request(string mode = "Bottom") => new() { DiagramId = Id(1), Mode = mode, ElementIds = [Id(4), Id(5)] };

    private static NativeElement[] WithAnchors() => Graph().Concat(new NativeElement[]
    {
        new() { Id = Id(9), Kind = "BoundaryEvent", ParentId = Id(3), DiagramId = Id(1),
            Geometry = new() { X = 59, Y = 79, Width = 22, Height = 22 }, Event = new() { Mode = "Boundary", AttachedToActivityId = Id(4), IsInterrupting = false } },
        new() { Id = Id(10), Kind = "BoundaryEvent", ParentId = Id(3), DiagramId = Id(1),
            Geometry = new() { X = 249, Y = 129, Width = 22, Height = 22 }, Event = new() { Mode = "Boundary", AttachedToActivityId = Id(5) } },
        new() { Id = Id(11), Kind = "SequenceFlow", ParentId = Id(3), DiagramId = Id(1), SourceId = Id(9), TargetId = Id(6) }
    }).ToArray();

    [Fact]
    public void HostTranslationIncludesOnlyItsMovedAttachedEvents()
    {
        var graph = WithAnchors(); var changes = NativeAlignmentPolicy.Expected(graph, Request());
        Assert.Equal(2, changes.Length);
        var boundary = changes.Single(c => c.ElementId == Id(9));
        Assert.Equal((59d, 129d, 22d, 22d), (boundary.Geometry!.X, boundary.Geometry.Y, boundary.Geometry.Width, boundary.Geometry.Height));
        Assert.DoesNotContain(changes, c => c.ElementId == Id(10));
        Assert.Null(boundary.EventProperties); Assert.Null(boundary.EventPayloads);
        Assert.False(graph.Single(e => e.Id == Id(9)).Event!.IsInterrupting);
    }

    [Fact]
    public void BoundaryManualLabelFollowsTheSameHostDelta()
    {
        var graph = WithAnchors(); graph.Single(e => e.Id == Id(9)).Style = new() { LabelBounds = new() { X = 40, Y = 100, Width = 80, Height = 30 } };
        var boundary = NativeAlignmentPolicy.Expected(graph, Request()).Single(c => c.ElementId == Id(9));
        Assert.Equal(40, boundary.Style!.LabelBounds!.X); Assert.Equal(150, boundary.Style.LabelBounds.Y);
        Assert.Equal(80, boundary.Style.LabelBounds.Width); Assert.Equal(30, boundary.Style.LabelBounds.Height);
    }

    [Theory]
    [InlineData("owner")] [InlineData("diagram")] [InlineData("bounds")]
    public void MalformedAttachedContextCannotBecomeAnImplicitMove(string defect)
    {
        var graph = WithAnchors(); var boundary = graph.Single(e => e.Id == Id(9));
        if (defect == "owner") boundary.ParentId = Id(90);
        if (defect == "diagram") boundary.DiagramId = Id(90);
        if (defect == "bounds") boundary.Geometry!.X = double.NaN;
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.Expected(graph, Request()));
    }

    [Fact]
    public void CallbackMayRouteAnAttachedEventButNotMoveAnotherHostsEvent()
    {
        var callback = JsonSerializer.Serialize(new[] { Update(Id(9), attached: Id(4)), Update(Id(11), Id(9), Id(6)) });
        var changes = NativeAlignmentPolicy.CallbackIntent(WithAnchors(), Request(), callback);
        Assert.Contains(changes, c => c.ElementId == Id(9) && c.Operation == "update");
        Assert.Contains(changes, c => c.ElementId == Id(11) && c.Operation == "reconnect");
        var unrelated = JsonSerializer.Serialize(new[] { Update(Id(10), attached: Id(5)) });
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.CallbackIntent(WithAnchors(), Request(), unrelated));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("00000000-0000-4000-8000-000000000005")]
    public void CallbackCannotDetachOrReassignAnImplicitBoundary(string? attachment)
    {
        var callback = JsonSerializer.Serialize(new[] { Update(Id(9), attached: attachment) });
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.CallbackIntent(WithAnchors(), Request(), callback));
    }

    [Theory]
    [InlineData("{\"sourcePort\":4,\"targetPort\":\"3\"}", "4", "3")]
    [InlineData("{}", "", "")]
    [InlineData("null", "", "")]
    [InlineData("{\"sourcePort\":null,\"targetPort\":0}", "", "0")]
    public void CallbackPortIntentIsCapturedIndependently(string graphics, string source, string target)
    {
        string element = JsonSerializer.Serialize(new { id = Id(8), sourceRef = Id(4), targetRef = Id(5),
            waypoints = new[] { new { x = 120, y = 80 }, new { x = 220, y = 120 } }, graphicalElementProperties = JsonSerializer.Deserialize<JsonElement>(graphics) });
        string callback = JsonSerializer.Serialize(new[] { new { element } });
        var route = NativeAlignmentPolicy.CallbackIntent(WithFlow(), Request(), callback).Single(c => c.Operation == "reconnect");
        Assert.Equal(source, route.SourcePort); Assert.Equal(target, route.TargetPort);
    }

    [Theory]
    [InlineData("{\"sourcePort\":true}")] [InlineData("{\"sourcePort\":75}")]
    [InlineData("{\"sourcePort\":\"04\"}")] [InlineData("{\"sourcePort\":4.0}")] [InlineData("[]")]
    public void MalformedCallbackPortsAreNotSilentlyAccepted(string graphics)
    {
        string element = JsonSerializer.Serialize(new { id = Id(8), sourceRef = Id(4), targetRef = Id(5),
            waypoints = new[] { new { x = 120, y = 80 }, new { x = 220, y = 120 } }, graphicalElementProperties = JsonSerializer.Deserialize<JsonElement>(graphics) });
        string callback = JsonSerializer.Serialize(new[] { new { element } });
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.CallbackIntent(WithFlow(), Request(), callback));
    }

    [Theory]
    [InlineData("Top", 20, 30, 220, 30)]
    [InlineData("Bottom", 20, 80, 220, 100)]
    [InlineData("Left", 20, 30, 20, 100)]
    [InlineData("Right", 200, 30, 220, 100)]
    [InlineData("Horizontal", 20, 55, 220, 65)]
    [InlineData("Vertical", 110, 30, 120, 100)]
    public void Expected_UsesWholeSelectionBoundsAndPreservesSizes(string mode, double ax, double ay, double bx, double by)
    {
        var graph = Graph(); var changes = NativeAlignmentPolicy.Expected(graph, Request(mode));
        var first = changes.SingleOrDefault(c => c.ElementId == Id(4))?.Geometry ?? graph[3].Geometry!;
        var second = changes.SingleOrDefault(c => c.ElementId == Id(5))?.Geometry ?? graph[4].Geometry!;
        Assert.Equal((ax, ay, 100d, 60d), (first.X, first.Y, first.Width, first.Height));
        Assert.Equal((bx, by, 80d, 40d), (second.X, second.Y, second.Width, second.Height));
        Assert.All(changes, change => { Assert.Equal("update", change.Operation); Assert.Null(change.Style); Assert.Null(change.Name); });
    }

    [Theory]
    [InlineData("HorizontalEvenly", 155, 70)]
    [InlineData("VerticalEvenly", 170, 80)]
    public void Distribution_UsesEqualGapsIncludingShapeSizes(string mode, double x, double y)
    {
        var request = Request(mode); request.ElementIds = [Id(4), Id(5), Id(6)];
        var changes = NativeAlignmentPolicy.Expected(Graph(), request);
        var middle = Assert.Single(changes);
        Assert.Equal(Id(6), middle.ElementId); Assert.Equal(x, middle.Geometry!.X); Assert.Equal(y, middle.Geometry.Y);
    }

    [Fact]
    public void AlreadyAligned_IsAnExplicitEmptyDeltaNotAFabricatedCallback()
    {
        var graph = Graph(); graph[4].Geometry!.Y = 50;
        Assert.Empty(NativeAlignmentPolicy.Expected(graph, Request()));
    }

    [Theory]
    [InlineData("None")]
    [InlineData("bottom")]
    [InlineData("Bottom');eval('x")]
    public void UnknownMode_IsRejected(string mode) => Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.Validate(Request(mode)));

    [Fact]
    public void DuplicateSelection_IsRejected()
    {
        var request = Request(); request.ElementIds = [Id(4), Id(4)];
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.Validate(request));
    }

    [Fact]
    public void Distribution_NeedsThreeActualNodes() => Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.Validate(Request("HorizontalEvenly")));

    [Theory]
    [InlineData("Participant")]
    [InlineData("Lane")]
    [InlineData("BoundaryEvent")]
    [InlineData("SequenceFlow")]
    [InlineData("TextAnnotation")]
    public void NativeFrontendFilteredShapes_AreRejectedNotSilentlySkipped(string kind)
    {
        var graph = Graph(); graph[3].Kind = kind;
        Assert.Throws<NotSupportedException>(() => NativeAlignmentPolicy.Expected(graph, Request()));
    }

    [Fact]
    public void DifferentDiagram_IsRejected()
    {
        var graph = Graph(); graph[4].DiagramId = Id(90);
        Assert.Throws<NotSupportedException>(() => NativeAlignmentPolicy.Expected(graph, Request()));
    }

    [Fact]
    public void DifferentOwner_IsRejected()
    {
        var graph = Graph().Append(new NativeElement { Id = Id(7), Kind = "Process", ParentId = Id(2), DiagramId = Id(1) }).ToArray();
        graph[4].ParentId = Id(7);
        Assert.Throws<NotSupportedException>(() => NativeAlignmentPolicy.Expected(graph, Request()));
    }

    [Fact]
    public void EmbeddedSurface_RequiresTheExplicitNativeParent()
    {
        var graph = Graph(); graph[2].Kind = "SubProcess"; graph[2].SubProcess = new();
        var request = Request(); request.SubProcessId = Id(3);
        Assert.Single(NativeAlignmentPolicy.Expected(graph, request));
        request.SubProcessId = Id(90);
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.Expected(graph, request));
    }

    [Fact]
    public void ManualLabel_PreservesNodeRelativeOffsetAndSize()
    {
        var graph = Graph(); graph[3].Style = new() { LabelBounds = new() { X = 25, Y = 40, Width = 80, Height = 40 } };
        var label = Assert.Single(NativeAlignmentPolicy.Expected(graph, Request())).Style!.LabelBounds!;
        Assert.Equal(25, label.X); Assert.Equal(90, label.Y); Assert.Equal(80, label.Width); Assert.Equal(40, label.Height);
    }

    [Fact]
    public void FractionalManualLabelTranslation_IsRejectedNotRounded()
    {
        var graph = Graph(); graph[3].Style = new() { LabelBounds = new() { X = 25, Y = 40, Width = 80, Height = 40 } };
        graph[4].Geometry!.Y = 101;
        Assert.Throws<NotSupportedException>(() => NativeAlignmentPolicy.Expected(graph, Request("Horizontal")));
    }

    [Theory]
    [InlineData("{\"id\":\"a\",\"id\":\"b\"}")]
    [InlineData("{\"id\":\"a\",\"location\":{\"x\":0,\"x\":1}}")]
    public void CallbackCannotUseDuplicateNestedProperties(string element)
    {
        string payload = JsonSerializer.Serialize(new[] { new { element, changed = (string?)null, @params = new { } } });
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.CallbackIntent(WithFlow(), Request(), payload));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1000001)]
    public void InvalidCoordinates_AreRejected(double value)
    {
        var graph = Graph(); graph[3].Geometry!.X = value;
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.Expected(graph, Request()));
    }

    [Fact]
    public void ForgedNoOpReceipt_IsRejectedBeforeArchiveParsing()
    {
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.Compare([], [], Graph(), Graph(), Request(),
            new() { Mode = "Bottom", SelectedElementIds = [Id(4), Id(5)], NoOp = true }));
    }

    private static NativeElement[] WithFlow() => Graph().Append(new NativeElement
        { Id = Id(8), Kind = "SequenceFlow", ParentId = Id(3), DiagramId = Id(1), SourceId = Id(4), TargetId = Id(5) }).ToArray();
    private static object Update(string id, string? source = null, string? target = null, string? action = null, string? attached = null) => new
    {
        element = JsonSerializer.Serialize(new { id, sourceRef = source, targetRef = target, attachedToRefId = attached, waypoints = new[] { new { x = 120, y = 80 }, new { x = 220, y = 120 } } }),
        changed = action, @params = new { }
    };

    [Fact]
    public void ActualCallbackIntent_RetainsRoutesWithoutTrustingReopenedCoordinates()
    {
        string payload = JsonSerializer.Serialize(new[] { Update(Id(8), Id(4), Id(5)) });
        var changes = NativeAlignmentPolicy.CallbackIntent(WithFlow(), Request(), payload);
        Assert.Equal(2, changes.Length); Assert.Equal("update", changes[0].Operation);
        Assert.Equal("reconnect", changes[1].Operation); Assert.Equal(120, changes[1].Points[0].X);
        Assert.Equal(Id(4), changes[1].SourceId); Assert.Equal(Id(5), changes[1].TargetId);
    }

    [Fact]
    public void CallbackCannotOmitAnExistingEndpoint()
    {
        string payload = JsonSerializer.Serialize(new[] { Update(Id(8), Id(4), null) });
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.CallbackIntent(WithFlow(), Request(), payload));
    }

    [Fact]
    public void CallbackCannotChangeAnUnselectedNode()
    {
        string payload = JsonSerializer.Serialize(new[] { Update(Id(6)) });
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.CallbackIntent(WithFlow(), Request(), payload));
    }

    [Fact]
    public void CallbackCannotSmuggleSemanticActions()
    {
        string payload = JsonSerializer.Serialize(new[] { Update(Id(4), action: "ChangeElementType") });
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.CallbackIntent(WithFlow(), Request(), payload));
    }

    [Fact]
    public void CallbackCannotRepeatAnIdentity()
    {
        var route = Update(Id(8), Id(4), Id(5)); string payload = JsonSerializer.Serialize(new[] { route, route });
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.CallbackIntent(WithFlow(), Request(), payload));
    }

    [Fact]
    public void CallbackCannotRerouteAnUnrelatedConnection()
    {
        var graph = WithFlow(); graph[^1].SourceId = Id(6); graph[^1].TargetId = Id(7);
        string payload = JsonSerializer.Serialize(new[] { Update(Id(8), Id(6), Id(7)) });
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.CallbackIntent(graph, Request(), payload));
    }
}
