using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Source-derived selection closure and placement. No worker-provided success substitutes for these checks.</summary>
public static partial class NativeSelectionCopyPolicy
{
    public static void Validate(NativeSelectionCopyRequest request)
    {
        if (request == null) throw new InvalidDataException("Missing native selection copy request.");
        NativeMetadataPolicy.RequireId(request.SourceDiagramId); NativeMetadataPolicy.RequireId(request.TargetParentId);
        if (request.ElementIds == null || request.ElementIds.Length is < 1 or > 1000 || request.ElementIds.Distinct(StringComparer.Ordinal).Count() != request.ElementIds.Length)
            throw new InvalidDataException("Copy requires 1-1000 distinct explicit native roots.");
        foreach (string id in request.ElementIds) NativeMetadataPolicy.RequireId(id);
        if (request.Position == null || new[] { request.Position.X, request.Position.Y }.Any(n => !double.IsFinite(n) || n < 0 || n > 1000000))
            throw new InvalidDataException("Copy requires bounded nonnegative destination coordinates.");
    }

    public static NativeElement[] Closure(NativeElement[] source, NativeSelectionCopyRequest request)
    {
        Validate(request);
        var graph = source.ToDictionary(e => e.Id, StringComparer.Ordinal);
        if (!graph.TryGetValue(request.SourceDiagramId, out var diagram) || diagram.Kind != "Collaboration" ||
            !graph.TryGetValue(request.TargetParentId, out var target) || target.Kind != "Process" && target.SubProcess == null)
            throw new InvalidDataException("Copy requires an existing source diagram and destination process or embedded subprocess.");
        var roots = request.ElementIds.Select(id => graph.TryGetValue(id, out var item) ? item : throw new InvalidDataException("Copy selection is absent.")).ToArray();
        foreach (var item in roots)
        {
            if (item.DiagramId != diagram.Id || item.Geometry == null || !graph.TryGetValue(item.ParentId, out var parent) || parent.Kind != "Process" && parent.SubProcess == null || item.Kind == "DataStore")
                throw new NotSupportedException("Select contained native elements/subtrees; diagram catalogs and participant partitions have separate copy contracts.");
            string parentId = item.ParentId; var seen = new HashSet<string>();
            while (parentId != "")
            {
                if (!seen.Add(parentId) || !graph.TryGetValue(parentId, out var owner)) throw new InvalidDataException("Cyclic or orphaned source containment.");
                if (request.ElementIds.Contains(parentId)) throw new InvalidDataException("Select a subtree root without selecting its descendants again.");
                parentId = owner.ParentId;
            }
        }
        if (roots.Where(e => graph[e.ParentId].SubProcess != null).Select(e => e.ParentId).Distinct().Count() > 1 ||
            roots.Any(e => graph[e.ParentId].SubProcess != null) && roots.Select(e => e.ParentId).Distinct().Count() != 1)
            throw new InvalidDataException("Selection roots must share one coordinate canvas, not unrelated embedded canvases.");
        var all = source.Concat(source.SelectMany(NativeDataFlowPolicy.OwnedNodes)).ToDictionary(e => e.Id);
        var ids = request.ElementIds.ToHashSet(StringComparer.Ordinal);
        bool added;
        do { added = false; foreach (var item in all.Values) if (ids.Contains(item.ParentId)) added |= ids.Add(item.Id); } while (added);
        foreach (var item in all.Values.Where(e => ids.Contains(e.Id)))
        {
            var references = new[] { item.SourceId, item.TargetId }.Concat(item.DefaultSequenceFlowIds)
                .Concat(item.Event == null ? [] : new[] { item.Event.AttachedToActivityId }.Concat(item.Event.Definitions.Select(d => d.Compensation?.ActivityId ?? "")));
            if (references.Where(id => id != "").Any(id => !ids.Contains(id)))
                throw new InvalidDataException("Copy the complete connector, boundary, default-flow and compensation reference closure.");
        }
        Offset(source, request);
        return all.Values.Where(e => ids.Contains(e.Id)).ToArray();
    }

    public static (float X, float Y) Offset(NativeElement[] source, NativeSelectionCopyRequest request)
    {
        var roots = request.ElementIds.Select(id => source.Single(e => e.Id == id)).ToArray();
        // Native CommandHelper uses connector vertices, not their zero-sized graphical box.
        var anchors = roots.SelectMany(e => e.SourceId != "" || e.TargetId != "" ? e.Points :
            [new NativePoint { X = e.Geometry!.X, Y = e.Geometry.Y }]).ToArray();
        if (anchors.Length == 0 || anchors.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || Math.Abs(p.X) > 1000000 || Math.Abs(p.Y) > 1000000))
            throw new InvalidDataException("Source selection has no bounded native placement anchors.");
        float dx = (float)request.Position!.X - (float)anchors.Min(p => p.X), dy = (float)request.Position.Y - (float)anchors.Min(p => p.Y);
        if (roots.Any(e => e.Points.Length != 0 || e.Style is { LabelBounds: var b } && (b.X != 0 || b.Y != 0 || b.Width != 0 || b.Height != 0)) &&
            (dx != MathF.Truncate(dx) || dy != MathF.Truncate(dy)))
            throw new InvalidDataException("Native connector/manual-label translation would round the requested offset.");
        return (dx, dy);
    }
}
