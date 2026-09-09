using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Preserves graphical enclosure membership without turning BPMN groups into process owners.</summary>
public static class NativeDiagramGroupLayout
{
    public sealed record Receipt(string GroupId, string[] EnclosedIds, NativeGeometry Before, NativeGeometry After);
    private const double Tolerance = 0.01;
    private static bool Contains(NativeGeometry a, NativeGeometry b) => b.X >= a.X - Tolerance && b.Y >= a.Y - Tolerance && b.X + b.Width <= a.X + a.Width + Tolerance && b.Y + b.Height <= a.Y + a.Height + Tolerance;
    private static bool Intersects(NativeGeometry a, NativeGeometry b) => a.X < b.X + b.Width - Tolerance && b.X < a.X + a.Width - Tolerance && a.Y < b.Y + b.Height - Tolerance && b.Y < a.Y + a.Height - Tolerance;
    private static NativeElement[] Groups(NativeElement[] graph, string diagram) => graph.Where(e => e.DiagramId == diagram && e.Kind == "Group").ToArray();

    private static NativeElement[] Anchors(NativeElement[] graph, string diagram)
    {
        // Root shapes use global diagram coordinates. Embedded children use their
        // own coordinate space and are represented here by the visible subprocess.
        var processes = graph.Where(e => e.DiagramId == diagram && e.Kind == "Process").Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        return graph.Where(e => e.DiagramId == diagram &&
            (e.Kind == "Participant" && e.IsMainParticipant == false || processes.Contains(e.ParentId) &&
             e.Kind is not "Lane" and not "Milestone" and not "SequenceFlow" and not "MessageFlow" and not "Association")).ToArray();
    }

    private static string[] Members(NativeElement group, NativeElement[] anchors)
    {
        if (group.ParentId != group.DiagramId || group.Geometry is not { Expanded: true })
            throw new InvalidDataException("Group layout requires a diagram-owned intrinsic expanded group.");
        var bounds = group.Geometry;
        // Crossing a pool band is normal for a graphical group. Cutting through
        // an activity is ambiguous and must not be silently treated as exclusion.
        foreach (var anchor in anchors.Where(a => a.Kind != "Participant"))
        {
            var box = NativeDiagramSurfacePlanner.Visual(anchor);
            if (Intersects(bounds, box) && !Contains(bounds, box))
                throw new NotSupportedException("Group boundary cuts a visible root element: " + group.Id + "/" + anchor.Id);
        }
        return anchors.Where(a => Contains(bounds, NativeDiagramSurfacePlanner.Visual(a))).Select(a => a.Id).Order(StringComparer.Ordinal).ToArray();
    }

    private static NativeGeometry Union(IEnumerable<NativeGeometry> boxes)
    {
        var values = boxes.ToArray(); double x = values.Min(b => b.X), y = values.Min(b => b.Y);
        return new() { X = x, Y = y, Width = values.Max(b => b.X + b.Width) - x, Height = values.Max(b => b.Y + b.Height) - y };
    }

    public static (NativeMutation[] Changes, Receipt[] Receipts) Calculate(NativeElement[] before, NativeElement[] positioned, string diagram, CancellationToken token)
    {
        var oldAnchors = Anchors(before, diagram).ToDictionary(e => e.Id, StringComparer.Ordinal);
        var newAnchors = Anchors(positioned, diagram).ToDictionary(e => e.Id, StringComparer.Ordinal);
        var changes = new List<NativeMutation>(); var receipts = new List<Receipt>();
        foreach (var group in Groups(before, diagram))
        {
            token.ThrowIfCancellationRequested();
            var members = Members(group, oldAnchors.Values.ToArray()); var old = group.Geometry!;
            var next = new NativeGeometry { X = old.X, Y = old.Y, Width = old.Width, Height = old.Height, Expanded = true };
            if (members.Length != 0)
            {
                var source = Union(members.Select(id => NativeDiagramSurfacePlanner.Visual(oldAnchors[id])));
                var target = Union(members.Select(id => NativeDiagramSurfacePlanner.Visual(newAnchors[id])));
                // Keep all four original margins, not an invented uniform padding.
                next.X = target.X - (source.X - old.X); next.Y = target.Y - (source.Y - old.Y);
                next.Width = target.Width + old.Width - source.Width; next.Height = target.Height + old.Height - source.Height;
            }
            changes.Add(new() { Operation = "update", ElementId = group.Id, Geometry = next });
            receipts.Add(new(group.Id, members, old, next));
        }
        Verify(before, NativeDiagramLayoutPlanner.Predict(positioned, changes.ToArray()), diagram);
        return (changes.ToArray(), receipts.ToArray());
    }

    public static void Verify(NativeElement[] before, NativeElement[] after, string diagram)
    {
        var oldGroups = Groups(before, diagram); var newGroups = Groups(after, diagram).ToDictionary(e => e.Id, StringComparer.Ordinal);
        if (oldGroups.Length != newGroups.Count) throw new InvalidDataException("Graphical group identity count changed.");
        var oldAnchors = Anchors(before, diagram); var newAnchors = Anchors(after, diagram);
        foreach (var group in oldGroups)
        {
            if (!newGroups.TryGetValue(group.Id, out var next) || !Members(group, oldAnchors).SequenceEqual(Members(next, newAnchors)))
                throw new InvalidDataException("Diagram layout changes graphical group membership: " + group.Id);
            foreach (var other in oldGroups.Where(e => e.Id != group.Id))
            {
                var a = group.Geometry!; var b = other.Geometry!; var c = next.Geometry!; var d = newGroups[other.Id].Geometry!;
                if (Contains(a, b) != Contains(c, d) || Intersects(a, b) != Intersects(c, d))
                    throw new InvalidDataException("Diagram layout changes group nesting or overlap relationships.");
            }
        }
    }
}
