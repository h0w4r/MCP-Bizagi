using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Planner and independent intent tests; these do not accredit the installed editor.</summary>
public sealed class NativeSurfaceLayoutTests
{
    private static string Id(int n) => $"00000000-0000-4000-8000-{n:000000000000}";
    private static NativeElement[] Graph() =>
    [
        new() { Id = Id(1), Kind = "Process", DiagramId = Id(9) },
        new() { Id = Id(2), Kind = "UserTask", ParentId = Id(1), DiagramId = Id(9), Geometry = new() { X = 700, Y = 120, Width = 100, Height = 80 } },
        new() { Id = Id(3), Kind = "UserTask", ParentId = Id(1), DiagramId = Id(9), Geometry = new() { X = 160, Y = 120, Width = 120, Height = 60 } },
        new() { Id = Id(4), Kind = "SequenceFlow", ParentId = Id(1), DiagramId = Id(9), SourceId = Id(2), TargetId = Id(3) }
    ];
    private static NativeSurfaceLayoutRequest Request(string direction = "Right") => new() { DiagramId = Id(9), OwnerId = Id(1), Direction = direction };

    [Theory] [InlineData("Right")] [InlineData("Down")]
    public void ActualSolverUsesTopologyPreservesDimensionsAndDoesNotMutateItsInput(string direction)
    {
        var graph = Graph(); string original = JsonSerializer.Serialize(graph); var phases = new List<string>();
        var plan = NativeSurfaceLayoutPlanner.Calculate(graph, Request(direction), phases.Add, default);
        Assert.Equal(original, JsonSerializer.Serialize(graph));
        var a = plan.Alignment.Placements[0]; var b = plan.Alignment.Placements[1];
        Assert.True(direction == "Right" ? a.X < b.X : a.Y < b.Y);
        Assert.Equal(2, plan.NodeCount); Assert.Equal(1, plan.ConnectionCount);
        Assert.Equal(64, plan.AssemblySha256.Length); Assert.Equal(64, plan.SourceGraphSha256.Length);
        Assert.Equal("native_layout_plan_verified", phases[^1]);
        Assert.Contains(phases, p => p.StartsWith("native_layout_planning:"));
        foreach (var change in NativeAlignmentPolicy.Expected(graph, plan.Alignment))
        {
            var old = graph.Single(e => e.Id == change.ElementId);
            Assert.Equal(old.Geometry!.Width, change.Geometry!.Width); Assert.Equal(old.Geometry.Height, change.Geometry.Height);
        }
        Assert.Equal(JsonSerializer.Serialize(plan), JsonSerializer.Serialize(NativeSurfaceLayoutPlanner.Calculate(graph, Request(direction), _ => { }, default)));
    }

    [Theory] [InlineData("cycle")] [InlineData("self")] [InlineData("parallel")]
    public void TopologyDoesNotDropNonTreeEdges(string topology)
    {
        var graph = Graph().Append(new NativeElement { Id = Id(5), Kind = "SequenceFlow", ParentId = Id(1), DiagramId = Id(9),
            SourceId = topology == "cycle" ? Id(3) : Id(2), TargetId = topology == "parallel" ? Id(3) : Id(2) }).ToArray();
        var plan = NativeSurfaceLayoutPlanner.Calculate(graph, Request(), _ => { }, default);
        Assert.Equal(2, plan.ConnectionCount); Assert.Equal(2, plan.Envelopes.Length);
    }

    [Fact]
    public void AttachedEventsAndManualLabelsAreIncludedInPackingAndExpectedDeltas()
    {
        var graph = Graph().Append(new NativeElement { Id = Id(6), Kind = "BoundaryEvent", ParentId = Id(1), DiagramId = Id(9),
            Geometry = new() { X = 739, Y = 189, Width = 22, Height = 22 }, Event = new() { AttachedToActivityId = Id(2), Mode = "Boundary" },
            Style = new() { LabelBounds = new() { X = 640, Y = 220, Width = 210, Height = 40 } } }).ToArray();
        graph[3].SourceId = Id(6);
        graph[3].Points = [new() { X = 750, Y = 211 }, new() { X = 160, Y = 150 }];
        var plan = NativeSurfaceLayoutPlanner.Calculate(graph, Request(), _ => { }, default);
        Assert.DoesNotContain(plan.Alignment.ElementIds, id => id == Id(6));
        var envelope = plan.Envelopes.Single(e => e.ElementId == Id(2));
        Assert.Equal(210, envelope.Width); Assert.Equal(140, envelope.Height);
        var changes = NativeAlignmentPolicy.Expected(graph, plan.Alignment);
        var host = changes.Single(c => c.ElementId == Id(2)); var boundary = changes.Single(c => c.ElementId == Id(6));
        Assert.Equal(39, boundary.Geometry!.X - host.Geometry!.X); Assert.Equal(69, boundary.Geometry.Y - host.Geometry.Y);
        Assert.Equal(boundary.Geometry.X - 99, boundary.Style!.LabelBounds!.X);
    }

    [Theory] [InlineData(49, 80)] [InlineData(50, 91)] [InlineData(61, 91)]
    [InlineData(double.NaN, 91)]
    public void BoundaryDockingDoesNotSilentlyAcceptTangentialOrCornerOrigins(double dx, double dy)
    {
        var graph = Graph().Concat(new NativeElement[]
        {
            new() { Id = Id(6), Kind = "BoundaryEvent", ParentId = Id(1), Geometry = new() { X = 739, Y = 189, Width = 22, Height = 22 }, Event = new() { AttachedToActivityId = Id(2) } },
            new() { Id = Id(7), Kind = "SequenceFlow", SourceId = Id(6), Points = [new() { X = 700 + dx, Y = 120 + dy }, new() { X = 900, Y = 211 }] }
        }).ToArray();
        if (dx == 50 && dy == 91) NativeSurfaceLayoutPlanner.VerifyBoundaryRoutes(graph, Id(1));
        else Assert.Throws<NotSupportedException>(() => NativeSurfaceLayoutPlanner.VerifyBoundaryRoutes(graph, Id(1)));
    }

    [Theory]
    [InlineData("Lane")] [InlineData("Milestone")] [InlineData("TextAnnotation")]
    [InlineData("expanded")] [InlineData("cross-route")] [InlineData("orphan-boundary")]
    public void UnsupportedContentFailsBeforeNativeWrites(string kind)
    {
        var graph = Graph();
        if (kind == "expanded") graph[1].Geometry!.Expanded = true;
        else if (kind == "cross-route") graph[3].ParentId = Id(99);
        else if (kind == "orphan-boundary") graph = graph.Append(new NativeElement { Id = Id(6), Kind = "BoundaryEvent", ParentId = Id(1), DiagramId = Id(9) }).ToArray();
        else graph[1].Kind = kind;
        Assert.ThrowsAny<Exception>(() => NativeSurfaceLayoutPlanner.Calculate(graph, Request(), _ => { }, default));
    }

    [Fact]
    public void OperatorCancellationReachesTheActualAlgorithm()
    {
        using var cancel = new CancellationTokenSource(); int events = 0;
        Assert.Throws<OperationCanceledException>(() => NativeSurfaceLayoutPlanner.Calculate(Graph(), Request(), phase =>
        {
            if (phase.StartsWith("native_layout_planning:")) { events++; cancel.Cancel(); }
        }, cancel.Token));
        Assert.True(events > 0);
    }

    [Fact]
    public void OversizedTransactionIsRejectedBeforeStartingTheSolver()
    {
        var graph = Graph().Concat(Enumerable.Range(100, 1001).Select(n => new NativeElement { Id = Id(n), ParentId = Id(1), Kind = "UserTask" })).ToArray();
        var progress = new List<string>();
        Assert.Throws<NotSupportedException>(() => NativeSurfaceLayoutPlanner.Calculate(graph, Request(), progress.Add, default));
        Assert.Empty(progress);
    }

    [Theory] [InlineData("missing")] [InlineData("duplicate")] [InlineData("fractional")] [InlineData("nan")] [InlineData("mixed")]
    public void CalculatedIntentCannotBeAmbiguous(string defect)
    {
        var plan = NativeSurfaceLayoutPlanner.Calculate(Graph(), Request(), _ => { }, default).Alignment;
        if (defect == "missing") plan.Placements = [];
        if (defect == "duplicate") plan.Placements[1].ElementId = plan.Placements[0].ElementId;
        if (defect == "fractional") plan.Placements[0].X = 1.2;
        if (defect == "nan") plan.Placements[0].Y = double.NaN;
        if (defect == "mixed") plan.Mode = "Left";
        Assert.Throws<InvalidDataException>(() => NativeAlignmentPolicy.Validate(plan));
    }
}
