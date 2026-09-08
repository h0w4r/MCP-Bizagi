using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Reverse only verified extraction moves in comparison copies; native files are never rewritten here.</summary>
public static class NativeExtractionPolicy
{
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static XDocument Read(byte[] bytes) => NativeMetadataPolicy.Read(Encoding.UTF8.GetString(bytes));
    private static byte[] Bytes(XDocument xml) => Encoding.UTF8.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
    private static string Prefix(string id) => id + ".diag!/";

    public static void Preflight(byte[] bytes, NativeSubProcessExtraction request, EngineReply source)
    {
        var owner = source.Elements.SingleOrDefault(e => e.Id == request.ElementId);
        if (owner?.Kind != "SubProcess" || owner.SubProcess?.TriggeredByEvent == true)
            throw new InvalidDataException("Extraction requires an ordinary embedded subprocess, not transaction/ad hoc/event-triggered semantics.");
        if (source.Metadata == null || source.Documentation == null) throw new InvalidDataException("Missing native source metadata/documentation evidence.");
        var moved = MovedIds(source.Elements, request.ElementId).ToHashSet(StringComparer.Ordinal);
        var references = source.Elements.Where(e => moved.Contains(e.Id)).SelectMany(e => new[] { e.Id, e.BpmnId }).Where(s => s != "").ToHashSet(StringComparer.Ordinal);
        foreach (var simulation in source.Metadata.Simulations.Where(s => s.DiagramId == owner.DiagramId))
            if (NativeMetadataPolicy.Read(simulation.Xml).Descendants().Attributes().Any(a => a.Name.LocalName == "elementRef" && references.Contains(a.Value)))
                throw new NotSupportedException("Extraction requires an explicit scenario migration for configured child simulation inputs; no simulation content was changed.");
        var entries = NativeArchive.ReadEntries(bytes);
        if (entries.TryGetValue(Prefix(owner.DiagramId) + "Actions.xml", out var actions) && Read(actions).Root?.Elements().Any() == true)
            throw new NotSupportedException("Presentation-action migration requires a separate explicit contract; extraction did not modify the model.");
        var definitions = source.Documentation.Definitions.ToDictionary(d => d.Id, d => NativeMetadataPolicy.Read(d.Xml));
        foreach (var values in source.Documentation.Values.Where(v => v.ElementId == request.ElementId))
            foreach (var value in NativeMetadataPolicy.Read(values.Xml).Root!.Element("Values")?.Elements() ?? [])
            {
                string id = (string?)value.Attribute("Id") ?? "";
                if (!definitions.TryGetValue(id, out var definition) || !definition.Descendants("AttributeElementType").Any(e => (string?)e.Attribute("Type") == "CallActivity"))
                    throw new InvalidDataException("Subprocess-owned attributes must explicitly include CallActivity in their definition scopes before extraction.");
            }
    }

    public static string[] MovedIds(NativeElement[] graph, string sourceId)
    {
        var byId = graph.ToDictionary(e => e.Id);
        bool Inside(NativeElement value)
        {
            var visited = new HashSet<string>(); string parent = value.ParentId;
            while (parent != "")
            {
                if (!visited.Add(parent)) throw new InvalidDataException("Cyclic native containment.");
                if (parent == sourceId) return true;
                parent = byId.TryGetValue(parent, out var owner) ? owner.ParentId : "";
            }
            return false;
        }
        return graph.Where(Inside).Select(e => e.Id).Order(StringComparer.Ordinal).ToArray();
    }

    public static void Verify(NativeElement[] before, NativeElement[] after, NativeSubProcessExtraction request, NativeExtractionReceipt receipt)
    {
        var original = before.ToDictionary(e => e.Id); var result = after.ToDictionary(e => e.Id);
        if (!original.TryGetValue(request.ElementId, out var source) || source.Kind != "SubProcess" || source.SubProcess?.TriggeredByEvent == true ||
            receipt.ElementId != request.ElementId || receipt.SourceDiagramId != source.DiagramId)
            throw new InvalidDataException("Extraction source/receipt is not an ordinary embedded subprocess.");
        var moved = MovedIds(before, request.ElementId).ToHashSet(StringComparer.Ordinal);
        if (!moved.Order().SequenceEqual(receipt.MovedElementIds.Order())) throw new InvalidDataException("Extraction receipt does not cover the exact descendant closure.");
        string[] added = [receipt.TargetDiagramId, receipt.TargetParticipantId, receipt.TargetProcessId];
        foreach (string id in added) NativeMetadataPolicy.RequireId(id);
        if (added.Distinct().Count() != 3 || added.Any(original.ContainsKey) || !result.Keys.Order().SequenceEqual(original.Keys.Concat(added).Order()))
            throw new InvalidDataException("Extraction added or removed unrequested native identities.");
        var diagram = result[receipt.TargetDiagramId]; var participant = result[receipt.TargetParticipantId]; var process = result[receipt.TargetProcessId];
        if (diagram.Kind != "Collaboration" || diagram.Name != request.NewDiagramName || diagram.ParentId != "" ||
            participant.Kind != "Participant" || participant.IsMainParticipant != true || participant.ParentId != diagram.Id ||
            process.Kind != "Process" || process.ParentId != participant.Id || added.Any(id => result[id].DiagramId != diagram.Id))
            throw new InvalidDataException("Extraction did not create the requested native target structure.");
        foreach (var item in before)
        {
            var expected = JsonSerializer.Deserialize<NativeElement>(JsonSerializer.Serialize(item))!;
            if (moved.Contains(item.Id)) { expected.DiagramId = diagram.Id; if (item.ParentId == request.ElementId) expected.ParentId = process.Id; }
            if (item.Id == request.ElementId)
            {
                expected.Kind = "CallActivity"; expected.ElementType = "CallActivity"; expected.SubProcess = null;
                expected.CallReference = new() { CatalogProcessId = process.Id, BpmnName = process.Id };
            }
            if (JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(result[item.Id]))
                throw new InvalidDataException("Extraction changed unrequested native graph fields: " + item.Id);
        }
    }

    public static NativeFidelityReport Compare(byte[] before, byte[] after, NativeSubProcessExtraction request, EngineReply source, EngineReply edited, EngineReply reopened)
    {
        var receipt = edited.Extraction ?? throw new InvalidDataException("Missing native extraction receipt.");
        Verify(source.Elements, reopened.Elements, request, receipt);
        var moved = receipt.MovedElementIds.ToHashSet(StringComparer.Ordinal);
        var left = NativeArchive.ReadEntries(before).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var right = NativeArchive.ReadEntries(after).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        if (source.DiagramState is { } preferences)
        {
            var opened = JsonSerializer.Deserialize<NativeOpenedItem[]>(JsonSerializer.Serialize(preferences.OpenedItems))!;
            bool changed = false;
            foreach (var item in opened.Where(i => i.DiagramId == receipt.SourceDiagramId && (i.SubProcessId == request.ElementId || moved.Contains(i.SubProcessId))))
            {
                changed = true; item.DiagramId = receipt.TargetDiagramId;
                if (item.SubProcessId == request.ElementId) item.SubProcessId = "";
            }
            if (changed) NativeDiagramPolicy.ProjectOpenedItems(left, right, opened,
                reopened.DiagramState ?? throw new InvalidDataException("Missing extraction tab readback."), edited.DiagramState);
        }
        string oldPrefix = Prefix(receipt.SourceDiagramId), newPrefix = Prefix(receipt.TargetDiagramId);
        if (left.Keys.Any(k => k.StartsWith(newPrefix, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Extraction target archive already existed.");
        var originalXml = Read(left[oldPrefix + "Diagram.xml"]); var changedXml = Read(right[oldPrefix + "Diagram.xml"]); var targetXml = Read(right[newPrefix + "Diagram.xml"]);
        XElement Owner(XDocument document, string kind, string id) => document.Descendants(Ns + kind)
            .Single(e => (string?)e.Attribute("Id") == id && NativeFidelity.IsNativeNameOwner(e));
        if ((string?)targetXml.Root?.Attribute("Id") != receipt.TargetDiagramId || (string?)targetXml.Root?.Attribute("Name") != request.NewDiagramName ||
            (string?)Owner(targetXml, "Pool", receipt.TargetParticipantId).Attribute("Process") != receipt.TargetProcessId)
            throw new InvalidDataException("Durable extraction target identity, label or participant/process reference mismatch.");
        Owner(targetXml, "WorkflowProcess", receipt.TargetProcessId);
        var targetIds = moved.Concat(new[] { receipt.TargetDiagramId, receipt.TargetParticipantId, receipt.TargetProcessId }).ToHashSet(StringComparer.Ordinal);
        if (targetXml.Descendants().Where(NativeFidelity.IsNativeNameOwner).Any(e => !targetIds.Contains((string?)e.Attribute("Id") ?? "")))
            throw new InvalidDataException("Unrequested native XML identities appeared in the extracted diagram.");
        var oldActivity = Owner(originalXml, "Activity", request.ElementId); var callActivity = Owner(changedXml, "Activity", request.ElementId);
        var oldSet = Owner(originalXml, "ActivitySet", request.ElementId);
        if (oldSet.Attributes().Any(a => a.Name != "Id" && a.Name != "Name") ||
            oldSet.Elements().Any(e => !new[] { "Associations", "Artifacts", "Activities", "Transitions" }.Contains(e.Name.LocalName) || e.Name.Namespace != Ns))
            throw new InvalidDataException("Subprocess extraction cannot retire unrepresented activity-set semantics.");
        var block = oldActivity.Element(Ns + "BlockActivity") ?? throw new InvalidDataException("Missing source BlockActivity.");
        if (block.Attributes().Count() != 1 || (string?)block.Attribute("ActivitySetId") != request.ElementId || block.Nodes().Any())
            throw new InvalidDataException("Unexpected source block-reference payload.");
        var implementation = callActivity.Element(Ns + "Implementation") ?? throw new InvalidDataException("Missing resulting native call implementation.");
        var subFlow = implementation.Element(Ns + "SubFlow");
        if (implementation.HasAttributes || implementation.Elements().Count() != 1 || subFlow == null || subFlow.Attributes().Count() != 1 ||
            (string?)subFlow.Attribute("Id") != receipt.TargetProcessId || subFlow.Nodes().Any()) throw new InvalidDataException("Unexpected resulting native call-reference payload.");
        implementation.ReplaceWith(new XElement(block));

        // The native serializer flattens nested ActivitySets into the owning WorkflowProcess.
        // Reconstruct only that container in the comparison copy using actual relocated subtrees.
        var oldSets = oldSet.Parent!;
        var processId = (string?)oldSets.Parent?.Attribute("Id") ?? throw new InvalidDataException("Missing source process.");
        var changedProcess = Owner(changedXml, "WorkflowProcess", processId);
        var currentSets = changedProcess.Element(Ns + "ActivitySets");
        RequirePlainCollection(oldSets); if (currentSets != null) RequirePlainCollection(currentSets);
        var remainingIds = oldSets.Elements().Select(e => (string?)e.Attribute("Id") ?? "").Where(id => id != request.ElementId && !moved.Contains(id)).Order(StringComparer.Ordinal);
        if (!remainingIds.SequenceEqual((currentSets?.Elements().Select(e => (string?)e.Attribute("Id") ?? "") ?? []).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Source activity-set collection contains unexpected additions or removals.");
        var restoredSets = new XElement(Ns + "ActivitySets");
        foreach (var set in oldSets.Elements())
        {
            string id = (string?)set.Attribute("Id") ?? throw new InvalidDataException("Missing activity-set identity.");
            if (id == request.ElementId)
            {
                var restored = new XElement(set.Name, set.Attributes().Select(a => new XAttribute(a)));
                RequireElementContent(set);
                foreach (var collection in set.Elements())
                {
                    RequirePlainCollection(collection);
                    var children = new XElement(collection.Name);
                    foreach (var element in collection.Elements())
                    {
                        string elementId = (string?)element.Attribute("Id") ?? throw new InvalidDataException("Unrepresented extracted element.");
                        if (!moved.Contains(elementId)) throw new InvalidDataException("Extracted XML identity is missing from the actual native closure.");
                        children.Add(new XElement(Owner(targetXml, element.Name.LocalName, elementId)));
                    }
                    restored.Add(children);
                }
                restoredSets.Add(restored);
            }
            else restoredSets.Add(new XElement(Owner(moved.Contains(id) ? targetXml : changedXml, "ActivitySet", id)));
        }
        if (currentSets == null)
        {
            // Native collection location is stable; retain its source sibling position in comparison only.
            var next = oldSets.ElementsAfterSelf().FirstOrDefault();
            var nextActual = next == null ? null : changedProcess.Element(next.Name);
            if (nextActual != null) nextActual.AddBeforeSelf(restoredSets); else changedProcess.Add(restoredSets);
        }
        else currentSets.ReplaceWith(restoredSets);
        right[oldPrefix + "Diagram.xml"] = Bytes(changedXml);

        RestoreValues(left, right, oldPrefix, newPrefix, moved, receipt);
        foreach (string key in right.Keys.Where(k => k.StartsWith(newPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            string relative = key[newPrefix.Length..];
            bool attachment = relative.StartsWith("Files/", StringComparison.OrdinalIgnoreCase) && moved.Contains(relative.Split('/').ElementAtOrDefault(1) ?? "");
            bool image = relative.StartsWith("ImageArtifactImages/", StringComparison.OrdinalIgnoreCase) && moved.Contains(Path.GetFileNameWithoutExtension(relative));
            if (attachment || image)
            {
                string previous = oldPrefix + relative;
                if (!left.ContainsKey(previous) || right.ContainsKey(previous)) throw new InvalidDataException("Moved payload source is absent or was duplicated.");
                right.Add(previous, right[key]); right.Remove(key);
            }
        }
        // Only the newly generated native container remains outside the original preservation scope.
        // Every original XML subtree and binary leaf is restored to its old comparison key above.
        foreach (string key in right.Keys.Where(k => k.StartsWith(newPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            string relative = key[newPrefix.Length..];
            if (!new[] { "Diagram.xml", "ExtendedAttributeValues.xml", "Actions.xml", "BPSimData.xml", "BPSimDataResult.xml" }.Contains(relative))
                throw new InvalidDataException("Unexplained generated extraction payload: " + relative);
            if (relative is "Actions.xml" or "BPSimData.xml" or "BPSimDataResult.xml")
            {
                var xml = Read(right[key]);
                if (xml.Root == null || xml.Root.Elements().Any()) throw new InvalidDataException("Extraction added unrequested scenario, result or presentation content.");
            }
            right.Remove(key);
        }
        return NativeFidelity.CompareEntries(left, right);
    }

    private static void RequireElementContent(XElement element)
    {
        if (element.Nodes().Any(n => n is not XElement && (n is not XText t || !string.IsNullOrWhiteSpace(t.Value))) ||
            element.AncestorsAndSelf().Any(e => e.Attribute(XNamespace.Xml + "space") != null))
            throw new InvalidDataException("Extraction cannot retire preserved or unknown collection content.");
    }
    private static void RequirePlainCollection(XElement element)
    {
        if (element.HasAttributes) throw new InvalidDataException("Extraction cannot relocate unknown collection attributes.");
        RequireElementContent(element);
    }

    private static void RestoreValues(Dictionary<string, byte[]> left, Dictionary<string, byte[]> right, string oldPrefix, string newPrefix,
        HashSet<string> moved, NativeExtractionReceipt receipt)
    {
        const string name = "ExtendedAttributeValues.xml";
        var original = Read(left[oldPrefix + name]); var remaining = Read(right[oldPrefix + name]);
        var relocated = NativeMetadataPolicy.Read(NativeDocumentationPolicy.ValuesContent(Encoding.UTF8.GetString(right[newPrefix + name]), receipt.TargetDiagramId, right));
        string Attributes(XElement element) => JsonSerializer.Serialize(element.Attributes().Select(a => new { Name = a.Name.ToString(), a.Value }).OrderBy(a => a.Name));
        if (original.Root?.Name != "DiagramAttributeValues" || remaining.Root?.Name != original.Root.Name || relocated.Root?.Name != original.Root.Name ||
            original.Root.Attributes().Any(a => !a.IsNamespaceDeclaration) || Attributes(original.Root) != Attributes(remaining.Root) || Attributes(original.Root) != Attributes(relocated.Root))
            throw new InvalidDataException("Native value container namespace/attribute contract changed.");
        RequireElementContent(original.Root); RequireElementContent(remaining.Root); RequireElementContent(relocated.Root);
        if (relocated.Root!.Elements().Any(e => !moved.Contains((string?)e.Attribute("ElementId") ?? ""))) throw new InvalidDataException("Unrequested documentation in extracted diagram.");
        var restored = new XElement(original.Root!.Name, original.Root.Attributes().Select(a => new XAttribute(a)));
        foreach (var element in original.Root.Elements())
        {
            string id = (string?)element.Attribute("ElementId") ?? throw new InvalidDataException("Unrepresented native value owner.");
            var source = moved.Contains(id) ? relocated : remaining;
            restored.Add(new XElement(source.Root!.Elements().Single(e => (string?)e.Attribute("ElementId") == id)));
        }
        if (restored.Elements().Count() != remaining.Root!.Elements().Count() + relocated.Root.Elements().Count())
            throw new InvalidDataException("Extra native value containers were added during extraction.");
        right[oldPrefix + name] = Bytes(new XDocument(restored));
    }
}
