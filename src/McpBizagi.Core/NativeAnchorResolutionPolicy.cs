using System.Text;
using System.Text.Json;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Accepts native anchor coordinates, not the preview's incidental labels, routes or pool reflow.</summary>
public static class NativeAnchorResolutionPolicy
{
    public const string EditorSha256 = "eec7db9e1474632e0e712c5df29ddc5b93aecb765cd8bc93422a1007ad8de1a9";

    private static int Side(NativeGeometry anchor, NativeGeometry host)
    {
        double x = anchor.X + anchor.Width / 2, y = anchor.Y + anchor.Height / 2;
        var distances = new[] { Math.Abs(y - host.Y), Math.Abs(y - host.Y - host.Height), Math.Abs(x - host.X), Math.Abs(x - host.X - host.Width) };
        var sides = Enumerable.Range(0, 4).Where(i => distances[i] < 0.01).ToArray();
        if (sides.Length != 1 || x < host.X || x > host.X + host.Width || y < host.Y || y > host.Y + host.Height)
            throw new InvalidDataException("Boundary anchor does not have an unambiguous bounded host side.");
        return sides[0];
    }

    public static NativeMutation[] Resolve(NativeElement[] source, NativeAnchorResizeRequest request, EngineReply preview, string callback)
    {
        if (callback.Length > 16 * 1024 * 1024) throw new InvalidDataException("Oversized native callback.");
        var host = source.Single(e => e.Id == request.HostId && e.DiagramId == request.DiagramId);
        var oldBounds = host.ExpandedGeometry ?? throw new InvalidDataException("Missing original expanded bounds.");
        var anchors = source.Where(e => e.Kind == "BoundaryEvent" && e.Event?.AttachedToActivityId == host.Id).ToArray();
        var receipt = preview.Alignment ?? throw new InvalidDataException("Missing actual native preview receipt.");
        if (!preview.Success || preview.Artifacts.Length != 0 || receipt.NoOp || receipt.Mode != "AnchorPreview" ||
            !receipt.SelectedElementIds.SequenceEqual([host.Id]) || receipt.EditorAssetSha256 != EditorSha256 ||
            receipt.CallbackSha256 != BpmnDocument.Revision(Encoding.UTF8.GetBytes(callback)) || anchors.Length == 0)
            throw new InvalidDataException("Native anchor preview provenance is incomplete.");
        var actual = preview.Elements.ToDictionary(e => e.Id, StringComparer.Ordinal);
        if (!actual.Keys.Order().SequenceEqual(source.Select(e => e.Id).Order())) throw new InvalidDataException("Preview changed native identities.");
        var resized = actual[host.Id]; var newBounds = resized.ExpandedGeometry;
        if (resized.Geometry?.Expanded != true || newBounds == null || newBounds.X != oldBounds.X || newBounds.Y != oldBounds.Y ||
            newBounds.Width != request.Size.Width || newBounds.Height != request.Size.Height || resized.ParentId != host.ParentId)
            throw new InvalidDataException("Native editor did not produce the requested host size and origin.");
        // Parse the retained callback independently from the worker's structured geometry.
        using var document = JsonDocument.Parse(callback);
        CheckKeys(document.RootElement);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() is < 1 or > 1000)
            throw new InvalidDataException("Invalid native callback record count.");
        var dtos = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var update in document.RootElement.EnumerateArray())
        {
            if (update.TryGetProperty("changed", out var changed) && changed.ValueKind != JsonValueKind.Null)
                throw new InvalidDataException("Preview emitted a semantic command.");
            if (update.TryGetProperty("params", out var hints) && (hints.ValueKind != JsonValueKind.Object || hints.EnumerateObject().Any()))
                throw new InvalidDataException("Preview emitted unknown command parameters.");
            using var element = JsonDocument.Parse(update.GetProperty("element").GetString()!);
            CheckKeys(element.RootElement);
            if (!actual.ContainsKey(element.RootElement.GetProperty("id").GetString()!))
                throw new InvalidDataException("Preview callback contains an unknown identity.");
            dtos.Add(element.RootElement.GetProperty("id").GetString()!, element.RootElement.Clone());
        }
        void Match(string id, NativeGeometry bounds)
        {
            var dto = dtos[id];
            foreach (var pair in new[] { ("x", bounds.X), ("y", bounds.Y), ("width", bounds.Width), ("height", bounds.Height) })
                if (dto.GetProperty(pair.Item1).GetDouble() != pair.Item2)
                    throw new InvalidDataException("Native callback and actual command geometry disagree.");
        }
        Match(host.Id, newBounds);
        var changes = new List<NativeMutation>();
        foreach (var anchor in anchors)
        {
            var after = actual[anchor.Id]; var old = anchor.Geometry!; var geometry = after.Geometry!;
            if (after.Kind != anchor.Kind || after.ParentId != anchor.ParentId || after.DiagramId != anchor.DiagramId ||
                JsonSerializer.Serialize(after.Event) != JsonSerializer.Serialize(anchor.Event) ||
                geometry.Width != old.Width || geometry.Height != old.Height ||
                new[] { geometry.X, geometry.Y, geometry.Width, geometry.Height }.Any(n => !double.IsFinite(n) || Math.Abs(n) > 1000000) ||
                Side(old, oldBounds) != Side(geometry, newBounds))
                throw new InvalidDataException("Native anchor preview changed attachment identity, dimensions or side.");
            Match(anchor.Id, geometry);
            if (dtos[anchor.Id].GetProperty("attachedToRefId").GetString() != host.Id)
                throw new InvalidDataException("Native anchor callback changed its host.");
            // Clone only source metadata and apply the observed position. The raw native
            // label rectangle is intentionally discarded in favor of the source contract.
            var projected = JsonSerializer.Deserialize<NativeGeometry>(JsonSerializer.Serialize(old))!;
            projected.X = geometry.X; projected.Y = geometry.Y;
            var change = new NativeMutation { Operation = "update", ElementId = anchor.Id, Geometry = projected };
            if (anchor.Style?.LabelBounds is { } label && (label.X != 0 || label.Y != 0 || label.Width != 0 || label.Height != 0))
                change.Style = new() { LabelBounds = new() { X = label.X + geometry.X - old.X, Y = label.Y + geometry.Y - old.Y,
                    Width = label.Width, Height = label.Height } };
            changes.Add(change);
        }
        return changes.ToArray();
    }

    private static void CheckKeys(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in node.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate native callback property.");
                CheckKeys(property.Value);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array) foreach (var item in node.EnumerateArray()) CheckKeys(item);
    }
}
