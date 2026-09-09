using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Group geometry contracts only; these do not accredit native persistence.</summary>
public sealed class NativeDiagramGroupLayoutTests
{
    private static string Id(int id) => $"00000000-0000-4000-8000-{id:000000000000}";
    private static NativeElement Item(int id, string kind, int parent, double x, double y, double w, double h) => new()
    { Id = Id(id), Kind = kind, DiagramId = Id(1), ParentId = Id(parent), IsMainParticipant = kind == "Participant" ? false : null,
        Geometry = new() { X = x, Y = y, Width = w, Height = h, Expanded = kind == "Group" } };
    private static NativeElement[] Graph() =>
    [
        Item(1, "Collaboration", 0, 0, 0, 90, 60), Item(2, "Participant", 1, 30, 30, 1000, 800),
        Item(3, "Process", 2, 0, 0, 90, 60), Item(4, "UserTask", 3, 200, 110, 120, 80),
        Item(5, "UserTask", 3, 500, 110, 120, 80), Item(6, "Group", 1, 10, 15, 1050, 855),
        Item(7, "Group", 1, 190, 100, 440, 100), Item(8, "Group", 1, 5000, 5000, 100, 100)
    ];
    private static NativeElement[] Copy(NativeElement[] graph) => JsonSerializer.Deserialize<NativeElement[]>(JsonSerializer.Serialize(graph))!;

    [Theory] [InlineData("Right")] [InlineData("Down")]
    public void CompletePlannerPreservesPoolAndNestedGraphicalEnclosures(string direction)
    {
        var graph = Graph(); string input = JsonSerializer.Serialize(graph);
        var plan = NativeDiagramLayoutPlanner.Calculate(graph, new() { DiagramId = Id(1), Direction = direction }, _ => { }, default);
        Assert.Equal(input, JsonSerializer.Serialize(graph));
        Assert.Equal(3, plan.Changes.Count(c => c.ElementId == Id(6) || c.ElementId == Id(7) || c.ElementId == Id(8)));
        var empty = plan.Changes.Single(c => c.ElementId == Id(8)).Geometry!;
        Assert.Equal(5000, empty.X); Assert.Equal(100, empty.Width); Assert.True(empty.Expanded);
    }

    [Fact]
    public void PoolMarginsRemainAsymmetric()
    {
        var graph = Graph(); var moved = Copy(graph);
        moved.Single(e => e.Id == Id(2)).Geometry = new() { X = 100, Y = 150, Width = 1100, Height = 900 };
        foreach (var node in moved.Where(e => e.ParentId == Id(3))) { node.Geometry!.X += 70; node.Geometry.Y += 120; }
        var plan = NativeDiagramGroupLayout.Calculate(graph, moved, Id(1), default);
        var box = plan.Changes.Single(c => c.ElementId == Id(6)).Geometry!;
        Assert.Equal(80, box.X); Assert.Equal(135, box.Y);
        Assert.Equal(1150, box.Width); Assert.Equal(955, box.Height);
    }

    [Fact]
    public void ClippedActivityCannotBeSilentlyExcluded()
    {
        var graph = Graph(); graph.Single(e => e.Id == Id(7)).Geometry!.X = 210;
        Assert.Throws<NotSupportedException>(() => NativeDiagramGroupLayout.Calculate(graph, Copy(graph), Id(1), default));
    }

    [Fact]
    public void NewAccidentalMemberFailsIndependentReadback()
    {
        var graph = Graph(); var moved = Copy(graph);
        moved.Single(e => e.Id == Id(8)).Geometry = new() { X = 195, Y = 105, Width = 130, Height = 90, Expanded = true };
        Assert.Throws<InvalidDataException>(() => NativeDiagramGroupLayout.Verify(graph, moved, Id(1)));
    }

    [Fact]
    public void GroupOverlapDriftFailsEvenWithoutNodeMembershipChange()
    {
        var graph = Graph().Append(Item(9, "Group", 1, 5200, 5000, 100, 100)).ToArray(); var moved = Copy(graph);
        moved.Single(e => e.Id == Id(9)).Geometry!.X = 5050;
        Assert.Throws<InvalidDataException>(() => NativeDiagramGroupLayout.Verify(graph, moved, Id(1)));
    }

    [Fact]
    public void OperatorCancellationStopsGroupPlanning()
    {
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Throws<OperationCanceledException>(() => NativeDiagramGroupLayout.Calculate(Graph(), Graph(), Id(1), token.Token));
    }
}
