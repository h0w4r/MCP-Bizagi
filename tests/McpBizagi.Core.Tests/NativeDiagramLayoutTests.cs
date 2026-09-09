using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Pure planning invariants using the actual solver, not native accreditation.</summary>
public sealed class NativeDiagramLayoutTests
{
    private static string Id(int i) => $"00000000-0000-4000-8000-{i:000000000000}";
    private static NativeElement[] Graph() =>
    [
        new() { Id = Id(1), Kind = "Collaboration", DiagramId = Id(1) },
        new() { Id = Id(2), Kind = "Participant", ParentId = Id(1), DiagramId = Id(1), IsMainParticipant = false, Geometry = new() { X = 30, Y = 30, Width = 1000, Height = 800 } },
        new() { Id = Id(3), Kind = "Process", ParentId = Id(2), DiagramId = Id(1) },
        new() { Id = Id(4), Kind = "UserTask", ParentId = Id(3), DiagramId = Id(1), Geometry = new() { X = 200, Y = 110, Width = 120, Height = 80 } },
        new() { Id = Id(5), Kind = "ManualTask", ParentId = Id(3), DiagramId = Id(1), Geometry = new() { X = 500, Y = 110, Width = 120, Height = 80 } },
        new() { Id = Id(6), Kind = "SequenceFlow", ParentId = Id(3), DiagramId = Id(1), SourceId = Id(4), TargetId = Id(5), SourcePort = "4", TargetPort = "3", Points = [new() { X = 320, Y = 150 }, new() { X = 500, Y = 150 }] }
    ];
    private static NativeDiagramLayoutPlanner.Plan Plan(NativeElement[] graph, string direction = "Right", Action<string>? progress = null, CancellationToken token = default)
        => NativeDiagramLayoutPlanner.Calculate(graph, new() { DiagramId = Id(1), Direction = direction }, progress ?? (_ => { }), token);

    [Theory] [InlineData("Right")] [InlineData("Down")]
    public void ActualSolverPlansTopologyWithoutMutatingOriginal(string direction)
    {
        var graph = Graph(); string before = JsonSerializer.Serialize(graph); var phases = new List<string>();
        var plan = Plan(graph, direction, phases.Add);
        Assert.Equal(before, JsonSerializer.Serialize(graph)); Assert.Equal(64, plan.AssemblySha256.Length);
        Assert.Equal(JsonSerializer.Serialize(plan), JsonSerializer.Serialize(Plan(graph, direction)));
        var a = plan.Changes.Single(c => c.ElementId == Id(4)).Geometry!; var b = plan.Changes.Single(c => c.ElementId == Id(5)).Geometry!;
        Assert.True(direction == "Right" ? a.X < b.X : a.Y < b.Y);
        Assert.Contains(phases, p => p.StartsWith("native_diagram_layout_placement:"));
        Assert.Contains(phases, p => p.StartsWith("native_diagram_layout_routing:"));
        Assert.Equal("native_diagram_layout_plan_verified", phases[^1]);
    }

    [Theory] [InlineData("placement")] [InlineData("routing")]
    public void OperatorCancellationReachesEachRealAlgorithm(string phase)
    {
        using var cancellation = new CancellationTokenSource(); int observed = 0;
        Assert.Throws<OperationCanceledException>(() => Plan(Graph(), progress: p =>
        { if (p.StartsWith("native_diagram_layout_" + phase + ":")) { observed++; cancellation.Cancel(); } }, token: cancellation.Token));
        Assert.True(observed > 0);
    }

    [Theory] [InlineData(0)] [InlineData(1)]
    public void EmptyAndSingleNodeSurfacesAreNotDropped(int count)
    {
        var graph = Graph().Take(3 + count).ToArray(); var plan = Plan(graph);
        Assert.Contains(plan.Changes, c => c.ElementId == Id(2));
        Assert.Equal(1 + count, plan.Changes.Length);
    }

    [Theory] [InlineData("unknown-port")] [InlineData("drift")] [InlineData("cross-diagram")] [InlineData("uncovered")]
    public void UnrepresentedOrInconsistentInputCannotBecomePartialSuccess(string defect)
    {
        var graph = Graph();
        if (defect == "unknown-port") graph[^1].SourcePort = "74";
        if (defect == "drift") graph[^1].Points[0].Y += 3;
        if (defect == "cross-diagram") graph[^1].DiagramId = Id(99);
        if (defect == "uncovered") graph = graph.Append(new NativeElement { Id = Id(8), Kind = "TextAnnotation", ParentId = Id(1), DiagramId = Id(1), Geometry = new() { Width = 100, Height = 80 } }).ToArray();
        Assert.ThrowsAny<Exception>(() => Plan(graph));
    }

    [Fact]
    public void CoverageBoundPrecedesAlgorithms()
    {
        var graph = Graph().Concat(Enumerable.Range(10, 1001).Select(i => new NativeElement { Id = Id(i), DiagramId = Id(1) })).ToArray();
        var phases = new List<string>(); Assert.Throws<NotSupportedException>(() => Plan(graph, progress: phases.Add)); Assert.Empty(phases);
    }

    [Fact]
    public void IndependentGeometryGateDetectsPortDrift()
    {
        var graph = Graph(); graph[^1].Points[0].Y += 3;
        Assert.Throws<InvalidDataException>(() => NativeDiagramLayoutGeometry.Verify(graph, Id(1)));
    }
    [Fact]
    public void NativeConnectorPlaceholderIsNotANodeRectangle()
    {
        var graph = Graph(); graph[^1].Geometry = new();
        Assert.Contains(Plan(graph).Changes, c => c.ElementId == Id(6) && c.Operation == "reconnect");
    }
    [Fact]
    public void NonfiniteConnectorCoordinatesFailBeforeAlgorithms()
    {
        var graph = Graph(); graph[^1].Points[0].X = float.NaN;
        var phases = new List<string>(); Assert.Throws<InvalidDataException>(() => Plan(graph, progress: phases.Add)); Assert.Empty(phases);
    }
}
