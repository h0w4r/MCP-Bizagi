using System.Globalization;
using System.Text;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

public static partial class NativeSelectionCopyPolicy
{
    /// <summary>Verify every selected durable record, payload and untouched archive entry.
    /// All projections are in-memory comparison inputs, never rewritten native output.</summary>
    public static NativeFidelityReport Compare(byte[] originalBytes, byte[] resultBytes,
        NativeElement[] before, NativeElement[] reopened, NativeSelectionCopyRequest request, NativeSelectionCopyReceipt receipt)
    {
        var closure = Closure(before, request);
        var allBefore = before.Concat(before.SelectMany(NativeDataFlowPolicy.OwnedNodes)).ToDictionary(e => e.Id);
        var allAfter = reopened.Concat(reopened.SelectMany(NativeDataFlowPolicy.OwnedNodes)).ToDictionary(e => e.Id);
        string targetDiagram = allBefore[request.TargetParentId].DiagramId;
        if (receipt == null || receipt.SourceDiagramId != request.SourceDiagramId || receipt.TargetParentId != request.TargetParentId || receipt.TargetDiagramId != targetDiagram || receipt.Identities == null)
            throw new InvalidDataException("Copy receipt disagrees with the explicit source and destination.");
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var reverse = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var identity in receipt.Identities)
        {
            if (identity == null) throw new InvalidDataException("Null native copy identity.");
            NativeMetadataPolicy.RequireId(identity.SourceId); NativeMetadataPolicy.RequireId(identity.TargetId);
            if (!allBefore.TryGetValue(identity.SourceId, out var old) || allBefore.ContainsKey(identity.TargetId) ||
                !allAfter.TryGetValue(identity.TargetId, out var current) || !map.TryAdd(identity.SourceId, identity.TargetId) ||
                !reverse.TryAdd(identity.TargetId, identity.SourceId) || identity.SourceBpmnId != old.BpmnId || identity.TargetBpmnId != current.BpmnId || old.Kind != current.Kind)
                throw new InvalidDataException("Invalid, reused, duplicated or absent native copy identity.");
        }
        if (!closure.Select(e => e.Id).ToHashSet().SetEquals(map.Keys) || !allAfter.Keys.ToHashSet().SetEquals(allBefore.Keys.Concat(reverse.Keys)))
            throw new InvalidDataException("Native copy identities do not match the full requested closure and original graph.");
        foreach (var pair in map)
        {
            var node = allAfter[pair.Value];
            if (node.DiagramId != targetDiagram || node.ParentId != (map.GetValueOrDefault(allBefore[pair.Key].ParentId) ?? request.TargetParentId))
                throw new InvalidDataException("Copied node did not retain the requested destination or nested containment.");
        }
        if (XpdlDocument.CompareGraph(before, reopened.Where(e => before.Any(b => b.Id == e.Id)).ToArray()).Length != 0)
            throw new InvalidDataException("Native copying modified an original graph element.");
        var (dx, dy) = Offset(before, request);
        var original = NativeArchive.ReadEntries(originalBytes);
        var actual = NativeArchive.ReadEntries(resultBytes);
        var projected = actual.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var ns = XpdlDocument.Namespace;
        string Prefix(string id) => id + ".diag!/";
        byte[] Bytes(XDocument doc) => Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));
        var oldDoc = XpdlDocument.Parse(original[Prefix(request.SourceDiagramId) + "Diagram.xml"]);
        var originalTarget = XpdlDocument.Parse(original[Prefix(receipt.TargetDiagramId) + "Diagram.xml"]);
        var newDoc = XpdlDocument.Parse(actual[Prefix(receipt.TargetDiagramId) + "Diagram.xml"]);

        // Shared exact native containment recognition rejects extension impersonators.
        bool Owned(XElement e) => NativeFidelity.IsNativeNameOwner(e);
        var newRecords = newDoc.Descendants().Where(e => Owned(e) && reverse.ContainsKey((string)e.Attribute("Id")!)).ToArray();
        var sourceRecords = oldDoc.Descendants().Where(e => Owned(e) && map.ContainsKey((string)e.Attribute("Id")!)).ToArray();
        bool durableIdentitiesComplete = newRecords.Select(e => (string)e.Attribute("Id")!).ToHashSet().SetEquals(reverse.Keys) &&
            sourceRecords.Select(e => e.Name + "|" + map[(string)e.Attribute("Id")!]).Order().SequenceEqual(newRecords.Select(e => e.Name + "|" + (string)e.Attribute("Id")!).Order());
        var copyDifferences = new List<NativeDifference>();
        foreach (var node in newRecords)
        {
            string newId = (string)node.Attribute("Id")!, oldId = reverse[newId];
            var old = oldDoc.Descendants(node.Name).Single(e => Owned(e) && (string?)e.Attribute("Id") == oldId);
            // A detached record loses inherited QName bindings and xml:space/lang/base.
            // Equal text is not equal meaning if its destination changes that context.
            if (!InheritedContext(old).SequenceEqual(InheritedContext(node)))
                throw new InvalidDataException("Copy changed inherited XML namespace or language/space/base context.");
            var clone = new XElement(node);
            // Freeze exact native identity/reference slots before detaching nodes from their context.
            foreach (var attribute in node.DescendantsAndSelf().SelectMany(e => e.Attributes()).ToArray())
            {
                var owner = attribute.Parent!;
                bool identity = attribute.Name == "Id" && Owned(owner);
                bool reference = Owned(owner) && owner.Name == ns + "Transition" && (attribute.Name == "From" || attribute.Name == "To") ||
                    owner.Name == ns + "BlockActivity" && attribute.Name == "ActivitySetId" && owner.Parent?.Name == ns + "Activity" && Owned(owner.Parent) ||
                    Owned(owner) && owner.Name == ns + "Association" && (attribute.Name == "Source" || attribute.Name == "Target") ||
                    Owned(owner) && owner.Name == ns + "DataAssociation" && (attribute.Name == "From" || attribute.Name == "To") ||
                    NativeDataFlowPolicy.IsSetReference(owner) && attribute.Name == "ArtifactId" ||
                    NativeEventPayloadPolicy.IsCompensationReference(owner) && attribute.Name == "ActivityId" ||
                    owner.Name == ns + "IntermediateEvent" && attribute.Name == "Target" && (string?)owner.Attribute("IsAttached") == "true" && owner.Parent?.Name == ns + "Event" && owner.Parent.Parent?.Name == ns + "Activity" && Owned(owner.Parent.Parent);
                if ((!identity && !reference) || !reverse.TryGetValue(attribute.Value, out string? id)) continue;
                var indices = new Stack<int>(); XElement cursor = owner;
                while (cursor != node) { indices.Push(cursor.ElementsBeforeSelf().Count()); cursor = cursor.Parent!; }
                XElement target = clone; foreach (int index in indices) target = target.Elements().ElementAt(index);
                target.SetAttributeValue(attribute.Name, id);
            }
            var expected = new XElement(old);
            if (request.ElementIds.Contains(oldId))
            {
                // Translate only the explicitly selected root, never its nested subprocess canvas.
                // Derive the delta from immutable source bounds and the supplied request, not output.
                var coordinates = expected.Element(ns + "NodeGraphicsInfos")?.Elements(ns + "NodeGraphicsInfo").Elements(ns + "Coordinates") ?? [];
                coordinates = coordinates.Concat(expected.Element(ns + "ConnectorGraphicsInfos")?.Elements(ns + "ConnectorGraphicsInfo").Elements(ns + "Coordinates") ?? []);
                foreach (var point in coordinates)
                    foreach (var axis in new[] { (Name: "XCoordinate", Delta: dx), (Name: "YCoordinate", Delta: dy) })
                    {
                        float oldValue = float.Parse(point.Attribute(axis.Name)!.Value, CultureInfo.InvariantCulture);
                        point.SetAttributeValue(axis.Name, (oldValue + axis.Delta).ToString("R", CultureInfo.InvariantCulture));
                    }
                foreach (var graphic in (expected.Element(ns + "NodeGraphicsInfos")?.Elements(ns + "NodeGraphicsInfo") ?? [])
                    .Concat(expected.Element(ns + "ConnectorGraphicsInfos")?.Elements(ns + "ConnectorGraphicsInfo") ?? []))
                    foreach (var axis in new[] { (Name: "TextX", Delta: dx), (Name: "TextY", Delta: dy) })
                        if (graphic.Attribute(axis.Name) is { } value)
                            value.Value = (float.Parse(value.Value, CultureInfo.InvariantCulture) + axis.Delta).ToString("R", CultureInfo.InvariantCulture);
            }
            var differences = XpdlDocument.CompareXml(Wrap(expected), Wrap(clone));
            if (differences.Length != 0) copyDifferences.Add(new(Prefix(receipt.TargetDiagramId) + "Diagram.xml", newId, "copied_record_changed", newId, expected.ToString(), clone.ToString()));
        }
        // Remove only identified additions from the comparison image, retaining all original content.
        var touchedCollections = newRecords.Select(e => e.Parent!).Distinct().ToArray();
        foreach (var node in newRecords.OrderByDescending(e => e.Ancestors().Count())) node.Remove();
        foreach (var collection in touchedCollections.Where(e => e.Document == newDoc && !e.HasElements))
        {
            // Removing added records can leave serializer-only whitespace or a newly materialized
            // collection. Restore only an exactly empty known collection, not arbitrary XML content.
            if (collection.HasAttributes || collection.Nodes().Any(n => n is not XText t || !string.IsNullOrWhiteSpace(t.Value))) continue;
            var owner = collection.Parent!;
            var originalOwner = originalTarget.Descendants(owner.Name).SingleOrDefault(e => (string?)e.Attribute("Id") == (string?)owner.Attribute("Id"));
            if (originalOwner == null) continue;
            var prior = originalOwner.Element(collection.Name);
            if (prior == null) collection.Remove();
            else if (!prior.HasElements && !prior.HasAttributes && prior.Nodes().All(n => n is XText t && string.IsNullOrWhiteSpace(t.Value))) collection.ReplaceNodes(prior.Nodes());
        }
        projected[Prefix(receipt.TargetDiagramId) + "Diagram.xml"] = Bytes(newDoc);
        bool filesEqual = true, attributesEqual = true;
        // Require every original selected payload's destination, not only files that happen to
        // exist in the result. Otherwise a missing copied image could evade an additions scan.
        foreach (var pair in original.Where(p => p.Key.StartsWith(Prefix(request.SourceDiagramId), StringComparison.Ordinal)))
        {
            string suffix = pair.Key[Prefix(request.SourceDiagramId).Length..];
            var parts = suffix.Split('/'); string? destination = null;
            if (parts.Length == 3 && parts[0] == "Files" && map.TryGetValue(parts[1], out string? fileOwner))
                destination = Prefix(receipt.TargetDiagramId) + "Files/" + fileOwner + "/" + parts[2];
            if (parts.Length == 2 && parts[0] == "ImageArtifactImages" && map.TryGetValue(Path.GetFileNameWithoutExtension(parts[1]), out string? imageOwner))
                destination = Prefix(receipt.TargetDiagramId) + "ImageArtifactImages/" + imageOwner + Path.GetExtension(parts[1]);
            if (destination != null) filesEqual &= actual.TryGetValue(destination, out var copiedBytes) && copiedBytes.AsSpan().SequenceEqual(pair.Value);
        }
        string valuesKey = Prefix(receipt.TargetDiagramId) + "ExtendedAttributeValues.xml";
        if (projected.TryGetValue(valuesKey, out var valueBytes))
        {
            var values = NativeMetadataPolicy.Read(Encoding.UTF8.GetString(valueBytes));
            if (values.Root?.Name != "DiagramAttributeValues") throw new InvalidDataException("Unexpected attribute root.");
            var sourceValues = NativeMetadataPolicy.Read(NativeDocumentationPolicy.ValuesContent(Encoding.UTF8.GetString(original[Prefix(request.SourceDiagramId) + "ExtendedAttributeValues.xml"]), request.SourceDiagramId, original));
            var targetValues = NativeMetadataPolicy.Read(NativeDocumentationPolicy.ValuesContent(Encoding.UTF8.GetString(valueBytes), receipt.TargetDiagramId, actual));
            foreach (var identity in map)
            {
                var a = sourceValues.Root!.Elements("ElementAttributeValues").SingleOrDefault(e => (string?)e.Attribute("ElementId") == identity.Key);
                var b = targetValues.Root!.Elements("ElementAttributeValues").SingleOrDefault(e => (string?)e.Attribute("ElementId") == identity.Value);
                if (b != null) b.SetAttributeValue("ElementId", identity.Key);
                bool equal = XNode.DeepEquals(a, b); attributesEqual &= equal;
            }
            foreach (var node in values.Root.Elements("ElementAttributeValues").Where(e => reverse.ContainsKey((string?)e.Attribute("ElementId") ?? "")).ToArray()) node.Remove();
            // Removing the only added records may leave pretty-print whitespace in an
            // originally empty root. Restore only that exact empty known container;
            // attributes, comments, extensions and xml:space remain fully compared.
            var priorValues = NativeMetadataPolicy.Read(Encoding.UTF8.GetString(original[valuesKey]));
            if (priorValues.Root?.Name == values.Root.Name && values.Root.Attributes().All(a => a.IsNamespaceDeclaration) && !values.Root.HasElements &&
                priorValues.Root.Attributes().All(a => a.IsNamespaceDeclaration) && !priorValues.Root.HasElements &&
                values.Root.Nodes().All(n => n is XText t && string.IsNullOrWhiteSpace(t.Value)) &&
                priorValues.Root.Nodes().All(n => n is XText t && string.IsNullOrWhiteSpace(t.Value)))
                values.Root.ReplaceNodes(priorValues.Root.Nodes());
            projected[valuesKey] = Bytes(values);
        }
        else if (original.TryGetValue(Prefix(request.SourceDiagramId) + "ExtendedAttributeValues.xml", out var sourceValueBytes))
        {
            var values = NativeMetadataPolicy.Read(Encoding.UTF8.GetString(sourceValueBytes));
            attributesEqual = !values.Root!.Elements("ElementAttributeValues").Any(e => map.ContainsKey((string?)e.Attribute("ElementId") ?? ""));
        }
        foreach (var pair in actual.Where(p => p.Key.StartsWith(Prefix(receipt.TargetDiagramId) + "Files/", StringComparison.Ordinal)))
        {
            var parts = pair.Key[Prefix(receipt.TargetDiagramId).Length..].Split('/');
            if (parts.Length != 3 || !reverse.TryGetValue(parts[1], out string? owner)) continue;
            string sourceKey = Prefix(request.SourceDiagramId) + "Files/" + owner + "/" + parts[2];
            bool equal = original.TryGetValue(sourceKey, out var bytes) && bytes.AsSpan().SequenceEqual(pair.Value);
            filesEqual &= equal;
            if (!equal) throw new InvalidDataException("Copied file bytes differ or source ownership is absent.");
            projected.Remove(pair.Key);
        }
        foreach (var pair in actual.Where(p => p.Key.StartsWith(Prefix(receipt.TargetDiagramId) + "ImageArtifactImages/", StringComparison.Ordinal)))
        {
            string name = pair.Key[(Prefix(receipt.TargetDiagramId) + "ImageArtifactImages/").Length..];
            if (!reverse.TryGetValue(Path.GetFileNameWithoutExtension(name), out string? owner)) continue;
            string sourceKey = Prefix(request.SourceDiagramId) + "ImageArtifactImages/" + owner + Path.GetExtension(name);
            bool equal = original.TryGetValue(sourceKey, out var bytes) && bytes.AsSpan().SequenceEqual(pair.Value);
            filesEqual &= equal;
            // Retain a failed byte check in durable evidence; never normalize away that failure.
            if (equal) projected.Remove(pair.Key);
        }

        var preserved = NativeFidelity.CompareEntries(original, projected);
        var failures = new List<NativeDifference>(preserved.Differences);
        failures.AddRange(copyDifferences);
        if (!durableIdentitiesComplete) failures.Add(new("copy", "/identities", "copied_records_incomplete", null, "complete source-derived records", "missing or extra"));
        if (!filesEqual) failures.Add(new("copy", "/files", "copied_file_changed", null, "exact source bytes", "missing or changed"));
        if (!attributesEqual) failures.Add(new("copy", "/attributes", "copied_attributes_changed", null, "source attribute values", "missing or changed"));
        return new(preserved.Preserved && durableIdentitiesComplete && filesEqual && attributesEqual && copyDifferences.Count == 0,
            original.Count, actual.Count, preserved.CheckedAtoms + sourceRecords.Length, failures.ToArray());

        // The XML comparator expects a package, but never sees a generated model file.
        byte[] Wrap(XElement record) => Bytes(new XDocument(new XElement(ns + "Package", new XAttribute("Id", "SelectionComparison"),
            new XElement(ns + "PackageHeader", new XElement(ns + "XPDLVersion", "2.2")), new XElement(record))));
    }

    private static string[] InheritedContext(XElement element)
    {
        var attributes = element.AncestorsAndSelf().SelectMany(e => e.Attributes()).Where(a => a.IsNamespaceDeclaration ||
            a.Name == XNamespace.Xml + "space" || a.Name == XNamespace.Xml + "lang").GroupBy(a => a.Name)
            .Select(g => g.Key + "=" + g.First().Value).Order(StringComparer.Ordinal);
        // Relative xml:base values compose through ancestry; retain the complete chain.
        return attributes.Concat(element.AncestorsAndSelf().Reverse().Attributes(XNamespace.Xml + "base").Select(a => "base=" + a.Value)).ToArray();
    }
}
