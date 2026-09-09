using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using McpBizagi.Contracts;
using Microsoft.Msagl.Core;
using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Layout.Layered;

namespace McpBizagi.Core;

/// <summary>Server-side topology placement, followed by installed-editor routing and archive verification.</summary>
public static class NativeSurfaceLayoutPlanner
{
    public const string Dependency = "Msagl 1.2.1";
    public static readonly string[] Directions = ["Right", "Down"];
    public sealed record Envelope(string ElementId, double X, double Y, double Width, double Height);
    public sealed record Plan(NativeAlignmentRequest Alignment, string Dependency, string AssemblySha256,
        string SourceGraphSha256, string Direction, int NodeCount, int ConnectionCount, Envelope[] Envelopes);

    public static void Validate(NativeSurfaceLayoutRequest request)
    {
        if (request == null || !Guid.TryParseExact(request.DiagramId, "D", out _) ||
            !Guid.TryParseExact(request.OwnerId, "D", out _) || !Directions.Contains(request.Direction))
            throw new InvalidDataException("Surface layout requires diagram/owner UUIDs and direction Right or Down.");
    }

    public static Plan Calculate(NativeElement[] source, NativeSurfaceLayoutRequest request, Action<string> progress, CancellationToken cancellation)
    {
        Validate(request);
        cancellation.ThrowIfCancellationRequested();
        var all = source.ToDictionary(e => e.Id, StringComparer.Ordinal);
        if (!all.TryGetValue(request.OwnerId, out var owner) || owner.DiagramId != request.DiagramId ||
            owner.Kind != "Process" && owner.SubProcess == null)
            throw new InvalidDataException("Surface owner must be an actual process or embedded subprocess in the requested diagram.");
        var direct = source.Where(e => e.ParentId == owner.Id).ToArray();
        if (direct.Length > 1000)
            throw new NotSupportedException("Surface exceeds the 1000-record native transaction bound, including nodes, anchors and routes.");
        var boundaries = direct.Where(e => e.Kind == "BoundaryEvent").ToArray();
        var connections = direct.Where(e => e.Kind is "SequenceFlow" or "MessageFlow" or "Association").OrderBy(e => e.Id, StringComparer.Ordinal).ToArray();
        var nodes = direct.Except(boundaries).Except(connections).OrderBy(e => e.Id, StringComparer.Ordinal).ToArray();
        var alignment = new NativeAlignmentRequest { DiagramId = request.DiagramId, SubProcessId = owner.Kind == "Process" ? "" : owner.Id,
            Mode = "Calculated", ElementIds = nodes.Select(e => e.Id).ToArray(),
            Placements = nodes.Select(e => new NativeLayoutPlacement { ElementId = e.Id, X = e.Geometry?.X ?? 0, Y = e.Geometry?.Y ?? 0 }).ToArray() };
        // Reuse the independently checked node/surface contract, including unknown
        // shapes, expanded nodes, partitions and native manual-label constraints.
        NativeAlignmentPolicy.Expected(source, alignment);
        var byId = nodes.ToDictionary(e => e.Id, StringComparer.Ordinal);
        foreach (var boundary in boundaries)
            if (boundary.Event == null || !byId.ContainsKey(boundary.Event.AttachedToActivityId) || boundary.Geometry == null)
                throw new NotSupportedException("Surface contains an unresolved boundary attachment; no partial layout is permitted.");
        VerifyBoundaryRoutes(source, owner.Id);
        var closure = nodes.Concat(boundaries).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var connection in source.Where(e => e.Kind is "SequenceFlow" or "MessageFlow" or "Association"))
            if ((closure.Contains(connection.SourceId) || closure.Contains(connection.TargetId)) &&
                (connection.ParentId != owner.Id || connection.DiagramId != owner.DiagramId ||
                 !closure.Contains(connection.SourceId) || !closure.Contains(connection.TargetId)))
                throw new NotSupportedException("Cross-surface routes require a multi-owner containment contract; the source is retained.");
        // Pack a host together with its existing anchored events and manual labels.
        // Native commands move those dependents; the solver does not detach or resize them.
        var originalEnvelopes = nodes.ToDictionary(e => e.Id, e => Bounds(e, boundaries.Where(b => b.Event!.AttachedToActivityId == e.Id)), StringComparer.Ordinal);
        var graph = new GeometryGraph();
        var geometryNodes = nodes.ToDictionary(e => e.Id, e =>
        {
            var envelope = originalEnvelopes[e.Id];
            var node = new Node(CurveFactory.CreateRectangle(envelope.Width, envelope.Height, new Point()), e.Id);
            graph.Nodes.Add(node); return node;
        }, StringComparer.Ordinal);
        string Host(string id) => byId.ContainsKey(id) ? id : boundaries.SingleOrDefault(b => b.Id == id)?.Event?.AttachedToActivityId
            ?? throw new NotSupportedException("Surface connector has an unresolved endpoint.");
        foreach (var connection in connections)
            graph.Edges.Add(new Edge(geometryNodes[Host(connection.SourceId)], geometryNodes[Host(connection.TargetId)]) { UserData = connection.Id });
        var settings = new SugiyamaLayoutSettings { LayerSeparation = 100, NodeSeparation = 70, RandomSeedForOrdering = 17,
            Transformation = PlaneTransformation.Rotation(request.Direction == "Right" ? Math.PI / 2 : 0) };
        settings.EdgeRoutingSettings.EdgeRoutingMode = EdgeRoutingMode.None;
        var algorithm = new LayeredLayout(graph, settings);
        var cancel = new CancelToken();
        using var registration = cancellation.Register(() => cancel.Canceled = true);
        algorithm.ProgressChanged += (_, e) =>
        {
            cancellation.ThrowIfCancellationRequested();
            progress("native_layout_planning:" + e.RatioComplete.ToString("0.000000", CultureInfo.InvariantCulture));
        };
        progress($"native_layout_planning_start:nodes={nodes.Length};connections={connections.Length}");
        try { algorithm.Run(cancel); }
        catch (Exception) when (cancellation.IsCancellationRequested) { throw new OperationCanceledException(cancellation); }
        cancellation.ThrowIfCancellationRequested();
        double minX = geometryNodes.Values.Min(n => n.BoundingBox.Left), maxY = geometryNodes.Values.Max(n => n.BoundingBox.Top);
        double originX = Math.Max(60, originalEnvelopes.Values.Min(e => e.X)), originY = Math.Max(60, originalEnvelopes.Values.Min(e => e.Y));
        var envelopes = new List<Envelope>();
        foreach (var node in nodes)
        {
            var box = geometryNodes[node.Id].BoundingBox; var old = originalEnvelopes[node.Id]; var shape = node.Geometry!;
            if (Math.Abs(box.Width - old.Width) > 0.001 || Math.Abs(box.Height - old.Height) > 0.001)
                throw new InvalidDataException("Layout engine unexpectedly resized a native envelope.");
            // Quantize host positions once, before native commands and independent gates.
            var target = alignment.Placements.Single(p => p.ElementId == node.Id);
            target.X = Math.Round(box.Left - minX + originX + shape.X - old.X);
            target.Y = Math.Round(maxY - box.Top + originY + shape.Y - old.Y);
            envelopes.Add(old with { X = old.X + target.X - shape.X, Y = old.Y + target.Y - shape.Y });
        }
        VerifyEnvelopes(envelopes.ToArray());
        // Revalidate all actual deltas, attached events and translated manual labels
        // before invoking a writer. Whole-archive verification remains a separate gate.
        NativeAlignmentPolicy.Expected(source, alignment);
        progress("native_layout_plan_verified");
        return new(alignment, Dependency, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(LayeredLayout).Assembly.Location))),
            BpmnDocument.Revision(JsonSerializer.SerializeToUtf8Bytes(source)), request.Direction, nodes.Length, connections.Length, envelopes.ToArray());
    }

    private static Envelope Bounds(NativeElement host, IEnumerable<NativeElement> attached)
    {
        var rectangles = new List<NativeGeometry>();
        foreach (var item in new[] { host }.Concat(attached))
        {
            var g = item.Geometry ?? throw new InvalidDataException("Layout envelope has no geometry.");
            rectangles.Add(g);
            if (item.Style?.LabelBounds is { } label && (label.X != 0 || label.Y != 0 || label.Width != 0 || label.Height != 0))
                rectangles.Add(new NativeGeometry { X = label.X, Y = label.Y, Width = label.Width, Height = label.Height });
        }
        if (rectangles.Any(g => new[] { g.X, g.Y, g.Width, g.Height }.Any(n => !double.IsFinite(n) || Math.Abs(n) > 1000000) || g.Width <= 0 || g.Height <= 0))
            throw new InvalidDataException("Invalid node, attachment or manual-label envelope.");
        double x = rectangles.Min(g => g.X), y = rectangles.Min(g => g.Y);
        return new(host.Id, x, y, rectangles.Max(g => g.X + g.Width) - x, rectangles.Max(g => g.Y + g.Height) - y);
    }

    public static void VerifyEnvelopes(Envelope[] envelopes)
    {
        for (int i = 0; i < envelopes.Length; i++) for (int j = 0; j < i; j++)
        {
            var a = envelopes[i]; var b = envelopes[j];
            if (a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height)
                throw new InvalidDataException("Calculated native node/attachment/label envelopes overlap.");
        }
    }

    /// <summary>Fail closed on boundary routes whose stored origin is not the unambiguous outward anchor.</summary>
    public static void VerifyBoundaryRoutes(NativeElement[] graph, string ownerId)
    {
        foreach (var boundary in graph.Where(e => e.Kind == "BoundaryEvent" && e.ParentId == ownerId))
        {
            var routes = graph.Where(e => e.SourceId == boundary.Id).ToArray();
            if (routes.Length == 0) continue;
            var b = boundary.Geometry ?? throw new InvalidDataException("Boundary route has no source geometry.");
            var h = graph.SingleOrDefault(e => e.Id == boundary.Event?.AttachedToActivityId)?.Geometry
                ?? throw new InvalidDataException("Boundary route has no host geometry.");
            double x = b.X + b.Width / 2, y = b.Y + b.Height / 2;
            var sides = new[] { (Distance: Math.Abs(y - h.Y), X: x, Y: b.Y),
                (Distance: Math.Abs(y - h.Y - h.Height), X: x, Y: b.Y + b.Height),
                (Distance: Math.Abs(x - h.X), X: b.X, Y: y),
                (Distance: Math.Abs(x - h.X - h.Width), X: b.X + b.Width, Y: y) };
            var selected = sides.Where(s => s.Distance < 0.001).ToArray();
            if (selected.Length != 1 || x < h.X - 0.001 || x > h.X + h.Width + 0.001 || y < h.Y - 0.001 || y > h.Y + h.Height + 0.001 ||
                routes.Any(r => r.Points == null || r.Points.Length < 2 || !double.IsFinite(r.Points[0].X) || !double.IsFinite(r.Points[0].Y) ||
                Math.Abs(r.Points[0].X - selected[0].X) > 0.001 || Math.Abs(r.Points[0].Y - selected[0].Y) > 0.001))
                throw new NotSupportedException("Automatic placement requires unambiguous outward boundary-route origins. Ambiguous/corner/offset anchors or native docking drift need a separate route contract; original retained.");
        }
    }
}
