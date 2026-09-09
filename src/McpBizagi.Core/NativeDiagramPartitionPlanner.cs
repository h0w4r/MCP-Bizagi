using System.Text.Json;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Bottom-up container planning and complete diagram-owned route coverage.</summary>
internal static class NativeDiagramPartitionPlanner
{
    public static async Task<NativeMutation[]> PlanAsync(NativeElement[] source, string diagram, NativeDiagramLayoutContext context)
    {
        context.Token.ThrowIfCancellationRequested();
        var graph = JsonSerializer.Deserialize<NativeElement[]>(JsonSerializer.Serialize(source))!;
        var original = source.ToDictionary(e => e.Id); var working = graph.ToDictionary(e => e.Id);
        var updates = new Dictionary<string, NativeMutation>(); var sizes = new Dictionary<string, NativeSize>();
        var evidence = new List<object>(); var stack = new HashSet<string>();
        void Accept(IEnumerable<NativeMutation> changes)
        {
            foreach (var change in changes)
            {
                if (sizes.TryGetValue(change.ElementId, out var size)) change.ExpandedSize = size;
                if (!updates.TryAdd(change.ElementId, change)) throw new InvalidDataException("Duplicate candidate geometry ownership.");
            }
        }
        async Task Nested(string owner)
        {
            context.Token.ThrowIfCancellationRequested();
            if (!stack.Add(owner) || stack.Count > 100) throw new InvalidDataException("Cyclic or excessively deep subprocess layout.");
            foreach (var sub in graph.Where(e => e.ParentId == owner && e.SubProcess != null))
            {
                await Nested(sub.Id).ConfigureAwait(false);
                var children = NativeDiagramSurfacePlanner.Surface(graph, sub.Id, 60, 60, context, original: source);
                Accept(children);
                if (children.Length == 0) continue;
                double right = 0, bottom = 0;
                foreach (var change in children)
                {
                    if (change.Geometry is { } g)
                    {
                        var visual = NativeDiagramSurfacePlanner.Visual(working[change.ElementId]);
                        right = Math.Max(right, g.X + visual.Width); bottom = Math.Max(bottom, g.Y + visual.Height);
                    }
                    foreach (var p in change.Points) { right = Math.Max(right, p.X); bottom = Math.Max(bottom, p.Y); }
                    if (change.Style?.LabelBounds is { } label) { right = Math.Max(right, (label.X ?? throw new InvalidDataException("Partial label X")) + (label.Width ?? throw new InvalidDataException("Partial label width"))); bottom = Math.Max(bottom, (label.Y ?? throw new InvalidDataException("Partial label Y")) + (label.Height ?? throw new InvalidDataException("Partial label height"))); }
                }
                var size = new NativeSize { Width = Math.Ceiling(Math.Max(right + 60, sub.Geometry!.Width + 60)), Height = Math.Ceiling(Math.Max(bottom + 60, sub.Geometry.Height + 60)) };
                sizes.Add(sub.Id, size);
                if (sub.ExpandedGeometry == null) throw new InvalidDataException("Native expanded-size evidence missing.");
                var attached = graph.Where(e => e.Kind == "BoundaryEvent" && e.Event?.AttachedToActivityId == sub.Id).ToArray();
                if (sub.Geometry.Expanded && attached.Length != 0 &&
                    (sub.ExpandedGeometry.Width != size.Width || sub.ExpandedGeometry.Height != size.Height))
                {
                    var resolve = context.ResolveAnchors ?? throw new NotSupportedException("Resized expanded anchors require an actual native resolver.");
                    var resolved = await resolve(new() { DiagramId = diagram, HostId = sub.Id, Size = size }).ConfigureAwait(false);
                    context.Token.ThrowIfCancellationRequested();
                    if (!resolved.Select(c => c.ElementId).Order().SequenceEqual(attached.Select(e => e.Id).Order()))
                        throw new InvalidDataException("Native anchor resolution must cover the exact source attachment set.");
                    // Only anchor geometry enters packing. Native preview pool reflow,
                    // neighbor movement, port changes and label damage are discarded.
                    foreach (var change in resolved)
                    {
                        var anchor = working[change.ElementId];
                        anchor.Geometry = change.Geometry ?? throw new InvalidDataException("Missing resolved anchor rectangle.");
                        if (change.Style?.LabelBounds is { } label)
                            anchor.Style!.LabelBounds = new() { X = label.X!.Value, Y = label.Y!.Value, Width = label.Width!.Value, Height = label.Height!.Value };
                    }
                    context.ResolvedHosts.Add(sub.Id, size);
                    context.Surfaces.Add(new { nativeAnchorHost = sub.Id, requestedSize = size, resolvedAnchors = resolved });
                }
                sub.ExpandedGeometry.Width = size.Width; sub.ExpandedGeometry.Height = size.Height;
            }
            stack.Remove(owner);
        }
        var pools = graph.Where(e => e.DiagramId == diagram && e.Kind == "Participant" && e.IsMainParticipant == false).OrderBy(e => e.Geometry!.Y).ThenBy(e => e.Geometry!.X).ToArray();
        if (pools.Length == 0) throw new InvalidDataException("Diagram layout requires an actual visible native pool.");
        double cursorY = pools.Min(p => p.Geometry!.Y);
        foreach (var pool in pools)
        {
            context.Token.ThrowIfCancellationRequested();
            var process = graph.Single(e => e.ParentId == pool.Id && e.Kind == "Process");
            var oldPool = original[pool.Id].Geometry!;
            var lanes = graph.Where(e => e.ParentId == process.Id && e.Kind == "Lane").OrderBy(e => e.Geometry!.Y).ToArray();
            var stages = graph.Where(e => e.ParentId == process.Id && e.Kind == "Milestone").OrderBy(e => e.Geometry!.X).ToArray();
            var partitionIds = lanes.Concat(stages).Select(e => e.Id).ToHashSet();
            var shapes = graph.Where(e => e.ParentId == process.Id && e.Kind is not "SequenceFlow" and not "MessageFlow" and not "Association" and not "BoundaryEvent" && !partitionIds.Contains(e.Id)).ToArray();
            int rows = Math.Max(1, lanes.Length), columns = Math.Max(1, stages.Length);
            var membership = new Dictionary<string, (int Row, int Column)>();
            foreach (var shape in shapes)
            {
                // Source membership uses the original visible rectangle, before any expanded child resize.
                var old = NativeDiagramSurfacePlanner.Visual(original[shape.Id]); double x = old.X - oldPool.X, y = old.Y - oldPool.Y;
                int[] laneMatches = lanes.Length == 0 ? [0] : lanes.Select((l, i) => (l, i)).Where(p => y >= p.l.Geometry!.Y && y + old.Height <= p.l.Geometry.Y + p.l.Geometry.Height).Select(p => p.i).ToArray();
                int[] stageMatches = stages.Length == 0 ? [0] : stages.Select((s, i) => (s, i)).Where(p => x >= p.s.Geometry!.X && x + old.Width <= p.s.Geometry.X + p.s.Geometry.Width).Select(p => p.i).ToArray();
                if (laneMatches.Length != 1 || stageMatches.Length != 1 || x < 50 || y < 0 || x + old.Width > oldPool.Width || y + old.Height > oldPool.Height)
                    throw new InvalidDataException("Ambiguous or out-of-pool source partition membership; no implicit reassignment: " + shape.Id);
                membership.Add(shape.Id, (laneMatches[0], stageMatches[0]));
            }
            await Nested(process.Id).ConfigureAwait(false);
            var rowHeights = Enumerable.Repeat(180d, rows).ToArray(); var columnWidths = Enumerable.Repeat(240d, columns).ToArray();
            var cellBounds = new Dictionary<(int Row, int Column), (double X, double Y)>();
            var finalPool = new NativeGeometry { X = oldPool.X, Y = cursorY, Width = oldPool.Width, Height = oldPool.Height };
            var changes = NativeDiagramSurfacePlanner.Surface(graph.Where(e => !partitionIds.Contains(e.Id)).ToArray(), process.Id, 0, 0, context, positions =>
            {
                foreach (var cell in membership.GroupBy(p => p.Value))
                {
                    var envelopes = cell.Select(p => NativeDiagramSurfacePlanner.Envelope(graph, working[p.Key], positions[p.Key])).ToArray();
                    double x = envelopes.Min(g => g.X), y = envelopes.Min(g => g.Y);
                    double right = envelopes.Max(g => g.X + g.Width);
                    double bottom = envelopes.Max(g => g.Y + g.Height);
                    cellBounds.Add(cell.Key, (x, y));
                    columnWidths[cell.Key.Column] = Math.Max(columnWidths[cell.Key.Column], Math.Ceiling(right - x + 120));
                    rowHeights[cell.Key.Row] = Math.Max(rowHeights[cell.Key.Row], Math.Ceiling(bottom - y + 120));
                }
                finalPool.Width = 50 + columnWidths.Sum(); finalPool.Height = rowHeights.Sum();
                foreach (var member in membership)
                {
                    var cell = member.Value; var origin = cellBounds[cell]; var p = positions[member.Key];
                    p.X = finalPool.X + 50 + columnWidths.Take(cell.Column).Sum() + 60 + p.X - origin.X;
                    p.Y = finalPool.Y + rowHeights.Take(cell.Row).Sum() + 60 + p.Y - origin.Y;
                }
            }, original: source);
            Accept(changes);
            if (shapes.Length == 0) { finalPool.Width = 50 + columnWidths.Sum(); finalPool.Height = rowHeights.Sum(); }
            Accept([new NativeMutation { Operation = "update", ElementId = pool.Id, Geometry = finalPool }]);
            double laneY = 0;
            for (int r = 0; r < lanes.Length; r++)
            {
                Accept([new NativeMutation { Operation = "update", ElementId = lanes[r].Id, Geometry = new() { X = 50, Y = laneY, Width = finalPool.Width - 50, Height = rowHeights[r] } }]);
                laneY += rowHeights[r];
            }
            double stageX = 50;
            for (int c = 0; c < stages.Length; c++)
            {
                Accept([new NativeMutation { Operation = "update", ElementId = stages[c].Id, Geometry = new() { X = stageX, Y = 0, Width = columnWidths[c], Height = finalPool.Height } }]);
                stageX += columnWidths[c];
            }
            evidence.Add(new { poolId = pool.Id, processId = process.Id, finalPool, rowHeights, columnWidths,
                membership = membership.Select(p => new { elementId = p.Key, row = p.Value.Row, column = p.Value.Column, laneId = lanes.Length == 0 ? null : lanes[p.Value.Row].Id, milestoneId = stages.Length == 0 ? null : stages[p.Value.Column].Id }) });
            cursorY = finalPool.Y + finalPool.Height + 120;
        }
        var messages = graph.Where(e => e.DiagramId == diagram && e.ParentId == diagram && e.Kind == "MessageFlow").ToArray();
        if (messages.Length != 0)
        {
            var processes = graph.Where(e => pools.Any(p => p.Id == e.ParentId) && e.Kind == "Process").Select(e => e.Id).ToHashSet();
            var roots = graph.Where(e => processes.Contains(e.ParentId) && e.SourceId == "" && e.Kind is not "Lane" and not "Milestone").ToArray();
            var projected = JsonSerializer.Deserialize<NativeElement[]>(JsonSerializer.Serialize(roots.Concat(messages)))!;
            foreach (var item in projected)
            {
                // A routing-only projection puts visible root obstacles into diagram coordinates.
                // This temporary ParentId is never sent as a native ownership mutation.
                item.ParentId = diagram;
                if (!updates.TryGetValue(item.Id, out var move)) continue;
                item.Geometry = move.Geometry;
                if (item.ExpandedGeometry != null) { item.ExpandedGeometry.X = move.Geometry!.X; item.ExpandedGeometry.Y = move.Geometry.Y; }
                if (move.Style?.LabelBounds is { } label)
                    item.Style!.LabelBounds = new() { X = label.X!.Value, Y = label.Y!.Value, Width = label.Width!.Value, Height = label.Height!.Value };
            }
            var headers = pools.Select(p => updates[p.Id].Geometry!).Select(g => new NativeGeometry { X = g.X, Y = g.Y, Width = 50, Height = g.Height }).ToArray();
            var routed = NativeDiagramSurfacePlanner.Surface(projected, diagram, 0, 0, context,
                original: source, routeOnly: true, barriers: headers);
            Accept(routed.Where(c => c.Operation == "reconnect"));
        }
        // Groups are graphical enclosures, not filled route obstacles or native
        // process owners. Recalculate their bounds from the original membership.
        var grouped = NativeDiagramGroupLayout.Calculate(source, NativeDiagramLayoutPlanner.Predict(source, updates.Values.ToArray()), diagram, context.Token);
        Accept(grouped.Changes);
        context.Surfaces.AddRange(grouped.Receipts);
        var uncovered = graph.Where(e => e.DiagramId == diagram && (e.Geometry != null || e.SourceId != "") && e.Kind is not "Collaboration" and not "Process" and not "Participant" && !updates.ContainsKey(e.Id)).ToArray();
        if (uncovered.Length != 0) throw new NotSupportedException("Diagram layout has no complete constraint for: " + string.Join(",", uncovered.Select(e => e.Kind + ":" + e.Id)));
        context.Pools.AddRange(evidence);
        return updates.Values.ToArray();
    }
}
