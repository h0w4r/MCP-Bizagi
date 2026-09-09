using System.Text.Json;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Independent selection math and archive acceptance; never executes the editor's layout algorithm.</summary>
public static class NativeAlignmentPolicy
{
    public static readonly string[] Modes = ["Top", "Bottom", "Left", "Right", "Horizontal", "Vertical", "HorizontalEvenly", "VerticalEvenly"];
    public static void Validate(NativeAlignmentRequest request)
    {
        if (request == null || !Modes.Contains(request.Mode) || !Guid.TryParseExact(request.DiagramId, "D", out _))
            throw new InvalidDataException("Alignment requires a native diagram identity and an explicit supported mode.");
        if (request.SubProcessId != "" && !Guid.TryParseExact(request.SubProcessId, "D", out _)) throw new InvalidDataException("Invalid subprocess identity.");
        if (request.ElementIds == null || request.ElementIds.Length is < 2 or > 1000 || request.ElementIds.Any(id => !Guid.TryParseExact(id, "D", out _)) ||
            request.ElementIds.Distinct(StringComparer.Ordinal).Count() != request.ElementIds.Length)
            throw new InvalidDataException("Supply 2-1000 distinct native selection identities.");
        if (request.Mode.EndsWith("Evenly", StringComparison.Ordinal) && request.ElementIds.Length < 3)
            throw new InvalidDataException("Distribution requires at least three selected nodes.");
    }

    public static NativeMutation[] Expected(NativeElement[] before, NativeAlignmentRequest request)
    {
        Validate(request);
        var graph = before.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var selected = request.ElementIds.Select(id => graph.TryGetValue(id, out var node) ? node : throw new InvalidDataException("Unknown alignment selection identity.")).ToArray();
        foreach (var node in selected)
        {
            // Native editor ignores pools, lanes, connections, boundary-hosted shapes and
            // text annotations. Reject them rather than report a partially applied selection.
            if (node.DiagramId != request.DiagramId || node.Geometry == null || node.Geometry.Expanded ||
                node.Kind is "Participant" or "Process" or "Collaboration" or "Lane" or "Milestone" or "BoundaryEvent" or "SequenceFlow" or "MessageFlow" or "Association" or "TextAnnotation" or "FormattedTextArtifact" or "HeaderArtifact" or "Resource" or "DataStore")
                throw new NotSupportedException("Selection contains an unsupported native layout shape or surface.");
            if (!graph.TryGetValue(node.ParentId, out var parent) ||
                (request.SubProcessId == "" ? parent.Kind != "Process" : node.ParentId != request.SubProcessId || parent.SubProcess == null))
                throw new InvalidDataException("Selected nodes must belong directly to the requested diagram/subprocess surface.");
            var g = node.Geometry;
            if (new[] { g.X, g.Y, g.Width, g.Height }.Any(n => !double.IsFinite(n) || Math.Abs(n) > 1000000) || g.Width <= 0 || g.Height <= 0)
                throw new InvalidDataException("Native selection has invalid or unbounded shape geometry.");
        }
        if (selected.Select(e => e.ParentId).Distinct().Count() != 1) throw new NotSupportedException("Cross-owner alignment needs its own containment contract; no partial selection is applied.");
        double left = selected.Min(e => e.Geometry!.X), right = selected.Max(e => e.Geometry!.X + e.Geometry.Width);
        double top = selected.Min(e => e.Geometry!.Y), bottom = selected.Max(e => e.Geometry!.Y + e.Geometry.Height);
        var targets = selected.ToDictionary(e => e.Id, e => (X: e.Geometry!.X, Y: e.Geometry.Y));
        if (request.Mode.EndsWith("Evenly", StringComparison.Ordinal))
        {
            bool horizontal = request.Mode == "HorizontalEvenly";
            var ordered = selected.OrderBy(e => horizontal ? e.Geometry!.X : e.Geometry!.Y).ToArray();
            double gap = ((horizontal ? right - left : bottom - top) - selected.Sum(e => horizontal ? e.Geometry!.Width : e.Geometry!.Height)) / (selected.Length - 1);
            double cursor = horizontal ? left : top;
            foreach (var node in ordered)
            {
                targets[node.Id] = horizontal ? (cursor, node.Geometry!.Y) : (node.Geometry!.X, cursor);
                cursor += (horizontal ? node.Geometry!.Width : node.Geometry!.Height) + gap;
            }
        }
        else foreach (var node in selected)
        {
            var g = node.Geometry!;
            targets[node.Id] = request.Mode switch
            {
                "Top" => (g.X, top), "Bottom" => (g.X, bottom - g.Height),
                "Left" => (left, g.Y), "Right" => (right - g.Width, g.Y),
                "Horizontal" => (g.X, (top + bottom - g.Height) / 2),
                "Vertical" => ((left + right - g.Width) / 2, g.Y),
                _ => throw new InvalidDataException("Unknown alignment mode.")
            };
        }
        var moving = selected.Where(e => Math.Abs(e.Geometry!.X - targets[e.Id].X) > 0.000001 || Math.Abs(e.Geometry.Y - targets[e.Id].Y) > 0.000001).ToList();
        var movingHosts = moving.ToDictionary(e => e.Id, StringComparer.Ordinal);
        foreach (var boundary in before.Where(e => e.Kind == "BoundaryEvent" && e.Event != null && movingHosts.ContainsKey(e.Event.AttachedToActivityId)))
        {
            var host = movingHosts[boundary.Event!.AttachedToActivityId];
            var bounds = boundary.Geometry ?? throw new InvalidDataException("Attached boundary event has no native geometry.");
            if (boundary.ParentId != host.ParentId || boundary.DiagramId != host.DiagramId || bounds.Expanded ||
                new[] { bounds.X, bounds.Y, bounds.Width, bounds.Height }.Any(n => !double.IsFinite(n) || Math.Abs(n) > 1000000) || bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidDataException("Attached boundary event has invalid geometry or ownership.");
            // Host sizes are unchanged by alignment. Preserve the exact stored attachment
            // offset by translation; do not guess a new side, resize, or detach the event.
            targets.Add(boundary.Id, (bounds.X + targets[host.Id].X - host.Geometry!.X, bounds.Y + targets[host.Id].Y - host.Geometry.Y));
            moving.Add(boundary);
        }
        var changes = moving.Select(e =>
            {
                var target = targets[e.Id]; var old = e.Geometry!;
                var change = new NativeMutation { Operation = "update", ElementId = e.Id, Geometry = new NativeGeometry
                    { X = target.X, Y = target.Y, Width = old.Width, Height = old.Height, Expanded = old.Expanded } };
                // Preserve a manual label's size and offset relative to its selected node.
                // The editor may substitute fallback text bounds; that is not permission
                // to erase the user's stored rectangle. Zero bounds stay automatic.
                if (e.Style?.LabelBounds is { } label && (label.X != 0 || label.Y != 0 || label.Width != 0 || label.Height != 0))
                {
                    var moved = new NativeLabelBoundsPatch { X = label.X + target.X - old.X, Y = label.Y + target.Y - old.Y, Width = label.Width, Height = label.Height };
                    if (new[] { moved.X!.Value, moved.Y!.Value, label.Width, label.Height }.Any(n => !double.IsFinite(n) || n != Math.Truncate(n)))
                        throw new NotSupportedException("Alignment would create nonrepresentable fractional manual label bounds; no rounding is permitted.");
                    change.Style = new NativeStylePatch { LabelBounds = moved };
                }
                return change;
            }).ToArray();
        if (changes.Length != 0) NativeEditPlan.Validate(changes);
        return changes;
    }

    public static NativeFidelityReport Compare(byte[] original, byte[] result, NativeElement[] before, NativeElement[] reopened, NativeAlignmentRequest request, NativeAlignmentReceipt receipt)
    {
        var expected = Expected(before, request);
        var affected = request.ElementIds.Concat(expected.Select(c => c.ElementId)).ToHashSet(StringComparer.Ordinal);
        if (receipt.Mode != request.Mode || !receipt.SelectedElementIds.SequenceEqual(request.ElementIds) || receipt.NoOp != (expected.Length == 0))
            throw new InvalidDataException("Native alignment receipt does not describe the requested selection or no-op state.");
        if (receipt.Changes == null || receipt.Changes.Select(c => c.ElementId).Distinct().Count() != receipt.Changes.Length)
            throw new InvalidDataException("Duplicate or absent native alignment change receipt.");
        if (receipt.NoOp)
        {
            if (receipt.Changes.Length != 0 || receipt.CallbackSha256 != "") throw new InvalidDataException("No-op layout must not invent callbacks or mutations.");
            return NativeFidelity.Compare(original, result);
        }
        if (receipt.CallbackSha256.Length != 64 || receipt.EditorAssetSha256.Length != 64) throw new InvalidDataException("Missing native layout callback/asset fingerprint.");
        var nodes = receipt.Changes.Where(c => c.Operation == "update").ToArray();
        if (nodes.Length != expected.Length || expected.Any(e => !nodes.Any(n => JsonSerializer.Serialize(n) == JsonSerializer.Serialize(e))))
            throw new InvalidDataException("Native layout node intent differs from independent selection math.");
        foreach (var route in receipt.Changes.Where(c => c.Operation != "update"))
        {
            var old = before.SingleOrDefault(e => e.Id == route.ElementId) ?? throw new InvalidDataException("Unknown native layout connection.");
            if (route.Operation != "reconnect" || old.DiagramId != request.DiagramId ||
                !affected.Contains(old.SourceId) && !affected.Contains(old.TargetId) ||
                route.SourceId != old.SourceId || route.TargetId != old.TargetId)
                throw new InvalidDataException("Alignment cannot change semantic endpoints or unrelated connectors.");
        }
        return NativeMutationFidelity.Compare(original, result, receipt.Changes, reopened);
    }

    public static NativeMutation[] CallbackIntent(NativeElement[] before, NativeAlignmentRequest request, string payload)
    {
        var changes = new List<NativeMutation>(Expected(before, request));
        var affected = request.ElementIds.Concat(changes.Select(c => c.ElementId)).ToHashSet(StringComparer.Ordinal);
        if (payload.Length > 16 * 1024 * 1024) throw new InvalidDataException("Native callback exceeds the transaction size bound.");
        using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 64 });
        RejectDuplicateProperties(document.RootElement);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() is < 1 or > 1000)
            throw new InvalidDataException("Native callback must contain 1-1000 element records.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var update in document.RootElement.EnumerateArray())
        {
            if (update.TryGetProperty("changed", out var action) && action.ValueKind != JsonValueKind.Null)
                throw new InvalidDataException("Alignment callback must not contain a semantic action.");
            if (update.TryGetProperty("params", out var hints) && (hints.ValueKind != JsonValueKind.Object || hints.EnumerateObject().Any()))
                throw new InvalidDataException("Unknown native alignment callback hints cannot be ignored.");
            using var element = JsonDocument.Parse(update.GetProperty("element").GetString()!, new JsonDocumentOptions { MaxDepth = 64 });
            var dto = element.RootElement;
            RejectDuplicateProperties(dto);
            string id = dto.GetProperty("id").GetString()!;
            if (!seen.Add(id)) throw new InvalidDataException("Duplicate identity in native layout callback.");
            var old = before.SingleOrDefault(e => e.Id == id) ?? throw new InvalidDataException("Unknown native callback element.");
            if (old.DiagramId != request.DiagramId) throw new InvalidDataException("Callback crossed diagram ownership.");
            if (old.Kind == "BoundaryEvent" && (!dto.TryGetProperty("attachedToRefId", out var attachment) || attachment.ValueKind != JsonValueKind.String ||
                attachment.GetString() != old.Event?.AttachedToActivityId)) throw new InvalidDataException("Native callback changed a boundary attachment.");
            if (old.Kind is "SequenceFlow" or "MessageFlow" or "Association")
            {
                string Ref(string field) => dto.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
                if (!affected.Contains(old.SourceId) && !affected.Contains(old.TargetId) || Ref("sourceRef") != old.SourceId || Ref("targetRef") != old.TargetId)
                    throw new InvalidDataException("Native callback changed semantic endpoints or an unrelated connector.");
                var points = dto.GetProperty("waypoints");
                if (points.GetArrayLength() is < 2 or > 10000) throw new InvalidDataException("Unsupported native route point count.");
                changes.Add(new NativeMutation { Operation = "reconnect", ElementId = id, SourceId = old.SourceId, TargetId = old.TargetId,
                    SourcePort = CallbackPort(dto, "sourcePort"), TargetPort = CallbackPort(dto, "targetPort"),
                    Points = points.EnumerateArray().Select(p => new NativePoint { X = p.GetProperty("x").GetDouble(), Y = p.GetProperty("y").GetDouble() }).ToArray() });
            }
            else if (!affected.Contains(id)) throw new InvalidDataException("Native callback changed an unrelated node.");
        }
        NativeEditPlan.Validate(changes.ToArray());
        return changes.ToArray();
    }

    private static string CallbackPort(JsonElement element, string name)
    {
        // The installed command DTO maps a missing/null property to an absent native
        // port. Independently capture that clear rather than silently retaining old data.
        if (!element.TryGetProperty("graphicalElementProperties", out var graphics) || graphics.ValueKind == JsonValueKind.Null) return "";
        if (graphics.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Native routing callback graphics must be an object.");
        if (!graphics.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return "";
        string port = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!, JsonValueKind.Number => value.GetRawText(),
            _ => throw new InvalidDataException("Native routing callback has an invalid port value.")
        };
        NativeConnectorPortPolicy.Validate(port); return port;
    }

    private static void RejectDuplicateProperties(JsonElement value)
    {
        // Keep host and worker interpretation consistent instead of accepting last-key wins.
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate property in native layout callback.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicateProperties(item);
    }
}
