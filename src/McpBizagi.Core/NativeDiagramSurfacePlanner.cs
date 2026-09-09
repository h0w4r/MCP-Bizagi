using System.Text.Json;
using McpBizagi.Contracts;
using Microsoft.Msagl.Core;
using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Routing;
using Microsoft.Msagl.Routing.Rectilinear;

namespace McpBizagi.Core;

/// <summary>Native geometry constraints around the pinned placement and rectilinear routing algorithms.</summary>
internal static class NativeDiagramSurfacePlanner
{
    public static NativeGeometry Visual(NativeElement e) => e.Geometry!.Expanded && e.ExpandedGeometry != null ? e.ExpandedGeometry : e.Geometry!;
    public static NativeGeometry Envelope(NativeElement[] all, NativeElement host, NativeGeometry? position = null)
    {
        var visible = Visual(host); double dx = (position?.X ?? visible.X) - visible.X, dy = (position?.Y ?? visible.Y) - visible.Y;
        var boxes = new List<NativeGeometry> { visible };
        foreach (var item in new[] { host }.Concat(all.Where(e => e.Kind == "BoundaryEvent" && e.Event?.AttachedToActivityId == host.Id)))
        {
            if (item.Id != host.Id) boxes.Add(Visual(item));
            if (item.Style?.LabelBounds is { } label && (label.X != 0 || label.Y != 0 || label.Width != 0 || label.Height != 0))
                boxes.Add(new() { X = label.X, Y = label.Y, Width = label.Width, Height = label.Height });
        }
        double x = boxes.Min(g => g.X), y = boxes.Min(g => g.Y);
        return new() { X = x + dx, Y = y + dy, Width = boxes.Max(g => g.X + g.Width) - x, Height = boxes.Max(g => g.Y + g.Height) - y };
    }

    public static NativeMutation[] Surface(NativeElement[] all, string owner, double x, double y, NativeDiagramLayoutContext context,
        Action<Dictionary<string, NativeGeometry>>? constrain = null, NativeElement[]? original = null,
        bool routeOnly = false, NativeGeometry[]? barriers = null)
    {
        context.Token.ThrowIfCancellationRequested();
        original ??= all;
        var originalById = original.ToDictionary(e => e.Id);
        var children = all.Where(e => e.ParentId == owner).ToArray();
        var connections = children.Where(e => e.Kind is "SequenceFlow" or "Association" or "MessageFlow").ToArray();
        var boundaries = children.Where(e => e.Kind == "BoundaryEvent").ToArray();
        var shapes = children.Except(connections).Except(boundaries).ToArray();
        if (shapes.Any(e => e.Geometry == null || e.Kind is "Lane" or "Milestone")) throw new NotSupportedException("Unrepresented surface record.");
        if (shapes.Length == 0 && connections.Length == 0 && boundaries.Length == 0) return [];
        var byId = shapes.ToDictionary(e => e.Id);
        foreach (var boundary in boundaries)
        {
            if (!byId.TryGetValue(boundary.Event?.AttachedToActivityId ?? "", out var host)) throw new InvalidDataException("Unresolved source boundary host.");
            // Resized-host attachment positioning needs an explicit side/offset contract; do not move it accidentally.
            var old = Visual(originalById[host.Id]); var now = Visual(host);
            if (old.Width != now.Width || old.Height != now.Height) throw new NotSupportedException("Boundary attached to resized expanded host needs a separate anchor-resize contract.");
        }
        string Host(string id) => byId.ContainsKey(id) ? id : boundaries.SingleOrDefault(b => b.Id == id)?.Event?.AttachedToActivityId
            ?? throw new NotSupportedException("Cross-surface connector cannot be silently omitted.");
        var envelopes = shapes.ToDictionary(e => e.Id, e => Envelope(all, e));
        var graph = new GeometryGraph();
        var nodes = shapes.ToDictionary(e => e.Id, e => { var b = envelopes[e.Id]; var n = new Node(CurveFactory.CreateRectangle(b.Width, b.Height, new Point()), e.Id); graph.Nodes.Add(n); return n; });
        var edges = connections.ToDictionary(e => e.Id, e => { var edge = new Edge(nodes[Host(e.SourceId)], nodes[Host(e.TargetId)]) { UserData = e.Id }; graph.Edges.Add(edge); return edge; });
        var settings = new SugiyamaLayoutSettings { LayerSeparation = 100, NodeSeparation = 70, Transformation = PlaneTransformation.Rotation(context.Direction == "Right" ? Math.PI / 2 : 0), RandomSeedForOrdering = 17 };
        settings.EdgeRoutingSettings.EdgeRoutingMode = EdgeRoutingMode.None;
        var cancel = new CancelToken(); using var registration = context.Token.Register(() => cancel.Canceled = true);
        var algorithm = new LayeredLayout(graph, settings);
        algorithm.ProgressChanged += (_, e) => context.Report("placement", owner, e.RatioComplete);
        if (!routeOnly) algorithm.Run(cancel);
        double minX = nodes.Values.Min(n => n.BoundingBox.Left), maxY = nodes.Values.Max(n => n.BoundingBox.Top);
        var positions = new Dictionary<string, NativeGeometry>();
        foreach (var old in shapes)
        {
            var b = nodes[old.Id].BoundingBox; var envelope = envelopes[old.Id]; var g = old.Geometry!;
            positions.Add(old.Id, new() { X = routeOnly ? g.X : Math.Round(b.Left - minX + x + g.X - envelope.X), Y = routeOnly ? g.Y : Math.Round(maxY - b.Top + y + g.Y - envelope.Y), Width = g.Width, Height = g.Height, Expanded = g.Expanded });
        }
        constrain?.Invoke(positions);
        foreach (var boundary in boundaries)
        {
            var host = byId[boundary.Event!.AttachedToActivityId]; var p = positions[host.Id]; var g = boundary.Geometry!;
            positions.Add(boundary.Id, new() { X = g.X + p.X - host.Geometry!.X, Y = g.Y + p.Y - host.Geometry.Y, Width = g.Width, Height = g.Height });
        }
        var changes = new List<NativeMutation>();
        var actual = shapes.Concat(boundaries).ToDictionary(e => e.Id);
        var visiblePositions = actual.ToDictionary(p => p.Key, p => { var g = Visual(p.Value); var q = positions[p.Key]; return new NativeGeometry { X = q.X, Y = q.Y, Width = g.Width, Height = g.Height }; });
        foreach (var old in actual.Values)
        {
            var geometry = positions[old.Id]; var g = old.Geometry!;
            var change = new NativeMutation { Operation = "update", ElementId = old.Id, Geometry = geometry };
            if (old.Style?.LabelBounds is { } label && (label.X != 0 || label.Y != 0 || label.Width != 0 || label.Height != 0))
                change.Style = new NativeStylePatch { LabelBounds = new NativeLabelBoundsPatch { X = label.X + geometry.X - g.X, Y = label.Y + geometry.Y - g.Y, Width = label.Width, Height = label.Height } };
            changes.Add(change);
        }
        // Layout envelopes prevent node/label collisions. Routing uses real visible rectangles,
        // not their envelopes; an anchored event may overlap its host by definition.
        var routeNodes = visiblePositions.ToDictionary(p => p.Key, p => new Node(CurveFactory.CreateRectangle(p.Value.Width, p.Value.Height, new Point(p.Value.X + p.Value.Width / 2, -p.Value.Y - p.Value.Height / 2))));
        var obstacles = routeNodes.ToDictionary(p => p.Key, p => new RelativeShape(() => p.Value.BoundaryCurve));
        var extraObstacles = new List<RelativeShape>();
        foreach (var barrier in barriers ?? [])
        {
            var curve = CurveFactory.CreateRectangle(barrier.Width, barrier.Height, new Point(barrier.X + barrier.Width / 2, -barrier.Y - barrier.Height / 2));
            extraObstacles.Add(new RelativeShape(() => curve));
        }
        foreach (var change in changes.Where(c => c.Style?.LabelBounds != null))
        {
            var label = change.Style!.LabelBounds!; var g = visiblePositions[change.ElementId];
            double lx = label.X ?? throw new InvalidDataException("Partial label X"), ly = label.Y ?? throw new InvalidDataException("Partial label Y");
            double lw = label.Width ?? throw new InvalidDataException("Partial label width"), lh = label.Height ?? throw new InvalidDataException("Partial label height");
            // Fully internal text belongs to its shape. External manual labels are real obstacles.
            if (label.X >= g.X && label.Y >= g.Y && label.X + label.Width <= g.X + g.Width && label.Y + label.Height <= g.Y + g.Height) continue;
            var curve = CurveFactory.CreateRectangle(lw, lh, new Point(lx + lw / 2, -ly - lh / 2));
            extraObstacles.Add(new RelativeShape(() => curve));
        }
        NativePoint Point(NativeGeometry box, string port) => port switch
        {
            "1" => new() { X = (float)(box.X + box.Width / 2), Y = (float)box.Y },
            "2" => new() { X = (float)(box.X + box.Width / 2), Y = (float)(box.Y + box.Height) },
            "3" => new() { X = (float)box.X, Y = (float)(box.Y + box.Height / 2) },
            "4" => new() { X = (float)(box.X + box.Width), Y = (float)(box.Y + box.Height / 2) },
            _ => throw new NotSupportedException("Offset/unknown native port requires its actual shape-specific mapping; no invented remapping.")
        };
        var portEvidence = new List<object>();
        Port MakePort(NativeElement connection, bool source)
        {
            string id = source ? connection.SourceId : connection.TargetId;
            var oldConnection = originalById[connection.Id]; var oldBox = Visual(originalById[id]);
            var oldPoint = source ? oldConnection.Points.First() : oldConnection.Points.Last();
            string? stored = source ? oldConnection.SourcePort : oldConnection.TargetPort;
            string port = stored ?? "";
            if (port is "" or "0")
            {
                var matches = new[] { "1", "2", "3", "4" }.Where(p => { var point = Point(oldBox, p); return Math.Abs(point.X - oldPoint.X) < 0.01 && Math.Abs(point.Y - oldPoint.Y) < 0.01; }).ToArray();
                if (matches.Length != 1) throw new InvalidDataException("Unmapped source port is not a unique observed midpoint.");
                port = matches[0];
            }
            var expected = Point(oldBox, port);
            if (Math.Abs(expected.X - oldPoint.X) > 0.01 || Math.Abs(expected.Y - oldPoint.Y) > 0.01) throw new InvalidDataException("Original persisted route disagrees with its explicit port.");
            var final = Point(visiblePositions[id], port);
            // MSAGL's nudger treats a non-null curve as an interval in which a port
            // may slide. A null-curve floating port is the library's fixed-point
            // constraint; register it with the real obstacle to retain membership.
            var result = new FloatingPort(null, new Point(final.X, -final.Y));
            obstacles[id].Ports.Insert(result);
            portEvidence.Add(new { connectionId = connection.Id, endpointId = id, source, stored, geometryPort = port, before = oldPoint, after = final });
            return result;
        }
        foreach (var connection in connections)
        {
            var edge = edges[connection.Id]; edge.SourcePort = MakePort(connection, true); edge.TargetPort = MakePort(connection, false);
            edge.EdgeGeometry.SourceArrowhead = null; edge.EdgeGeometry.TargetArrowhead = null;
        }
        var router = new RectilinearEdgeRouter(obstacles.Values.Concat(extraObstacles)) { Padding = 15, CornerFitRadius = 0 };
        router.ProgressChanged += (_, e) => context.Report("routing", owner, e.RatioComplete);
        foreach (var edge in graph.Edges) router.AddEdgeGeometryToRoute(edge.EdgeGeometry);
        router.Run(cancel);
        foreach (var connection in connections)
        {
            var points = new List<NativePoint>();
            void Read(ICurve curve)
            {
                if (curve is Curve composite) { foreach (var piece in composite.Segments) Read(piece); return; }
                if (curve is not LineSegment) throw new InvalidDataException("Non-line route is not approximated into success.");
                foreach (var p in new[] { curve.Start, curve.End })
                { var q = new NativePoint { X = (float)p.X, Y = (float)-p.Y }; if (points.Count == 0 || points[^1].X != q.X || points[^1].Y != q.Y) points.Add(q); }
            }
            Read(edges[connection.Id].Curve);
            var edge = edges[connection.Id];
            if (points.Count < 2 || Math.Abs(points[0].X - edge.SourcePort.Location.X) > 0.01 || Math.Abs(points[0].Y + edge.SourcePort.Location.Y) > 0.01 ||
                Math.Abs(points[^1].X - edge.TargetPort.Location.X) > 0.01 || Math.Abs(points[^1].Y + edge.TargetPort.Location.Y) > 0.01)
                throw new InvalidDataException("Router moved a declared native port; no native write permitted.");
            changes.Add(new() { Operation = "reconnect", ElementId = connection.Id, SourceId = connection.SourceId, TargetId = connection.TargetId, SourcePort = connection.SourcePort, TargetPort = connection.TargetPort, Points = points.ToArray() });
        }
        context.Surfaces.Add(new { owner, ports = portEvidence, changes, routeOnly });
        return changes.ToArray();
    }
}
