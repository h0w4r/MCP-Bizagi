using System.Collections;
using McpBizagi.Contracts;
using Newtonsoft.Json;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private void MigratePresentation(object model, NativePresentationTransfer[] transfers, Action<string> progress)
    {
        if (transfers.Length == 0) return;
        var snapshot = Presentation(model); var graph = Graph(model).ToDictionary(g => Text(g.Value, "Id"));
        var selected = new List<(NativePresentationTransfer Transfer, object Value)>();
        var payloads = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var transfer in transfers)
        {
            var a = transfer.SourceAction;
            var observed = snapshot.Actions.Single(x => x.DiagramId == a.DiagramId && x.ElementId == a.ElementId);
            if (JsonConvert.SerializeObject(observed) != JsonConvert.SerializeObject(a) || !graph.TryGetValue(a.ElementId, out var owner) || owner.DiagramId != transfer.TargetDiagramId)
                throw new InvalidDataException("Native action source or final owner differs from migration intent.");
            var collection = (IList)DiagramActions(model, a.DiagramId);
            object value = Items(collection).Single(x => Text(x, "ElementId") == a.ElementId);
            selected.Add((transfer, value));
            if (a.TypeValue == "Normal" && a.Type is "File" or "Image")
            {
                string original = Path.Combine(ActionFolder(model, a.DiagramId), a.Content.Substring(12));
                payloads[original] = File.ReadAllBytes(original);
            }
        }
        // Remove all selected objects first; no batch step can become a different step's source.
        foreach (var item in selected) ((IList)DiagramActions(model, item.Transfer.SourceAction.DiagramId)).Remove(item.Value);
        foreach (var item in selected)
        {
            var a = item.Transfer.SourceAction; string target = item.Transfer.TargetDiagramId;
            var collection = (IList)DiagramActions(model, target);
            if (Items(collection).Any(x => Text(x, "ElementId") == a.ElementId)) throw new InvalidDataException("Destination already owns the presentation action.");
            if (a.TypeValue == "Normal" && a.Type is "File" or "Image")
            {
                string name = a.Content.Substring(12), original = Path.Combine(ActionFolder(model, a.DiagramId), name);
                string folder = ActionFolder(model, target), destination = Path.Combine(folder, name); byte[] bytes = payloads[original];
                if (File.Exists(destination) && !File.ReadAllBytes(destination).SequenceEqual(bytes)) throw new InvalidDataException("Native action migration payload collision.");
                Directory.CreateDirectory(folder); File.WriteAllBytes(destination, bytes); Set(item.Value, "Content", destination);
            }
            if (a.TypeValue == "ExtendedAttribute" && a.ExtendedAttributeId != "" && a.Content != "")
            {
                var definition = Items(Get(Get(model, "ExtendedAttributes"), "Definitions")).SingleOrDefault(d => Text(d, "Id") == a.ExtendedAttributeId);
                if (definition != null && Text(definition, "AttributeType") is "Image" or "FileEmbedded")
                {
                    if (!a.Content.StartsWith("attachment:", StringComparison.Ordinal)) throw new InvalidDataException("Migrated embedded cache lacks an owned attachment reference.");
                    string destination = Path.Combine(AttachmentFolder(model, target, a.ElementId), a.Content.Substring(11));
                    if (!File.Exists(destination)) throw new InvalidDataException("Migrated referenced action attachment is missing.");
                    Set(item.Value, "Content", destination);
                }
            }
            // Keep the actual action object, display name and literal/reference cache. Never refresh or activate it.
            collection.Add(item.Value); progress("native_presentation_migrated:" + a.ElementId);
        }
        foreach (string original in payloads.Keys)
        {
            bool retained = Items(model, "Diagrams").Any(d => Items(DiagramActions(model, Text(d, "Id"))).Any(a =>
                Text(a, "TypeValue") == "Normal" && Text(a, "Type") is "File" or "Image" && string.Equals(Text(a, "Content"), original, StringComparison.OrdinalIgnoreCase)));
            if (!retained) File.Delete(original);
        }
    }
}
