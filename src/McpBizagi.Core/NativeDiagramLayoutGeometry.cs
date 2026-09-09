using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Checks route/port and obstacle invariants independently from MSAGL's return value.</summary>
public static class NativeDiagramLayoutGeometry
{
    private sealed record Box(string Id, NativeGeometry Geometry, string? Host = null);
    private static bool Flow(NativeElement e) => e.Kind is "SequenceFlow" or "MessageFlow" or "Association";
    private static bool Overlap(NativeGeometry a, NativeGeometry b) => a.X < b.X + b.Width - 0.01 && b.X < a.X + a.Width - 0.01 && a.Y < b.Y + b.Height - 0.01 && b.Y < a.Y + a.Height - 0.01;
    private static bool Contains(NativeGeometry outer, NativeGeometry inner) => inner.X >= outer.X - 0.01 && inner.Y >= outer.Y - 0.01 && inner.X + inner.Width <= outer.X + outer.Width + 0.01 && inner.Y + inner.Height <= outer.Y + outer.Height + 0.01;

    public static void Verify(NativeElement[] graph, string diagram)
    {
        var selected = graph.Where(e => e.DiagramId == diagram).ToArray();
        var byId = selected.ToDictionary(e => e.Id, StringComparer.Ordinal);
        foreach (var owner in selected.Where(e => e.Kind is "Process" or "Collaboration" || e.SubProcess != null))
        {
            var parents = owner.Kind == "Collaboration" ? selected.Where(e => e.Kind == "Process").Select(e => e.Id).ToHashSet() : new HashSet<string> { owner.Id };
            var nodes = selected.Where(e => parents.Contains(e.ParentId) && !Flow(e) && e.Kind is not "Lane" and not "Milestone").ToArray();
            var boxes = nodes.Select(e => new Box(e.Id, NativeDiagramSurfacePlanner.Visual(e), e.Event?.AttachedToActivityId)).ToArray();
            for (int i = 0; i < boxes.Length; i++) for (int j = 0; j < i; j++)
                if (boxes[i].Host != boxes[j].Id && boxes[j].Host != boxes[i].Id && Overlap(boxes[i].Geometry, boxes[j].Geometry))
                    throw new InvalidDataException("Diagram layout node overlap: " + boxes[i].Id + "/" + boxes[j].Id);
            var obstacles = boxes.ToList();
            foreach (var node in nodes)
            {
                if (node.Style?.LabelBounds is not { } label || label.Width == 0 && label.Height == 0) continue;
                var box = new NativeGeometry { X = label.X, Y = label.Y, Width = label.Width, Height = label.Height };
                if (label.Width <= 0 || label.Height <= 0) throw new InvalidDataException("Unrepresentable manual label.");
                if (!Contains(NativeDiagramSurfacePlanner.Visual(node), box)) obstacles.Add(new(node.Id + ":label", box));
            }
            if (owner.Kind == "Collaboration")
                obstacles.AddRange(selected.Where(e => e.Kind == "Participant" && e.IsMainParticipant == false).Select(e =>
                    new Box(e.Id + ":header", new() { X = e.Geometry!.X, Y = e.Geometry.Y, Width = 50, Height = e.Geometry.Height })));
            foreach (var flow in selected.Where(e => e.ParentId == owner.Id && Flow(e)))
            {
                if (!byId.TryGetValue(flow.SourceId, out var source) || !byId.TryGetValue(flow.TargetId, out var target) || flow.Points.Length < 2)
                    throw new InvalidDataException("Unresolved diagram route.");
                VerifyPort(flow.Points[0], NativeDiagramSurfacePlanner.Visual(source), flow.SourcePort);
                VerifyPort(flow.Points[^1], NativeDiagramSurfacePlanner.Visual(target), flow.TargetPort);
                for (int i = 1; i < flow.Points.Length; i++)
                {
                    var a = flow.Points[i - 1]; var b = flow.Points[i];
                    bool horizontal = Math.Abs(a.Y - b.Y) < 0.01, vertical = Math.Abs(a.X - b.X) < 0.01;
                    if (!horizontal && !vertical) throw new InvalidDataException("Nonorthogonal layout route.");
                    foreach (var obstacle in obstacles)
                    {
                        var g = obstacle.Geometry;
                        if (horizontal && a.Y > g.Y + 0.01 && a.Y < g.Y + g.Height - 0.01 && Math.Min(a.X, b.X) < g.X + g.Width - 0.01 && Math.Max(a.X, b.X) > g.X + 0.01 ||
                            vertical && a.X > g.X + 0.01 && a.X < g.X + g.Width - 0.01 && Math.Min(a.Y, b.Y) < g.Y + g.Height - 0.01 && Math.Max(a.Y, b.Y) > g.Y + 0.01)
                            throw new InvalidDataException("Layout route intersects a node, external label or header: " + flow.Id + "/" + obstacle.Id);
                    }
                }
            }
            if (owner.SubProcess != null && owner.ExpandedGeometry != null)
            {
                var container = new NativeGeometry { Width = owner.ExpandedGeometry.Width, Height = owner.ExpandedGeometry.Height };
                if (boxes.Any(b => !Contains(container, b.Geometry))) throw new InvalidDataException("Expanded container does not contain its children.");
            }
        }
    }

    private static void VerifyPort(NativePoint point, NativeGeometry box, string? port)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new InvalidDataException("Invalid route endpoint.");
        var midpoints = new[] { (X: box.X + box.Width / 2, Y: box.Y), (X: box.X + box.Width / 2, Y: box.Y + box.Height),
            (X: box.X, Y: box.Y + box.Height / 2), (X: box.X + box.Width, Y: box.Y + box.Height / 2) };
        bool At((double X, double Y) p) => Math.Abs(point.X - p.X) < 0.01 && Math.Abs(point.Y - p.Y) < 0.01;
        if (string.IsNullOrEmpty(port) || port == "0")
        { if (midpoints.Count(At) == 1) return; }
        else if (port is "1" or "2" or "3" or "4" && At(midpoints[int.Parse(port, System.Globalization.CultureInfo.InvariantCulture) - 1])) return;
        throw new InvalidDataException("Layout route does not match its preserved native port.");
    }
}
