using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Explicit final-state containment and original-preserving native XML relocation checks.</summary>
public static partial class NativeReparentingPolicy
{
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private sealed record Container(string Kind, string Id, string Collection);
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    public static void Validate(NativeReparenting[] changes)
    {
        if (changes == null || changes.Length is < 1 or > 1000 || changes.Any(c => c == null) || changes.Select(c => c.ElementId).Distinct().Count() != changes.Length)
            throw new InvalidDataException("Supply 1-1000 distinct explicit reparenting requests.");
        foreach (var c in changes)
        {
            NativeMetadataPolicy.RequireId(c.ElementId); NativeMetadataPolicy.RequireId(c.ExpectedParentId); NativeMetadataPolicy.RequireId(c.TargetParentId);
            if (c.ExpectedDiagramId != null) NativeMetadataPolicy.RequireId(c.ExpectedDiagramId);
            if (c.TargetDiagramId != null) NativeMetadataPolicy.RequireId(c.TargetDiagramId);
            if ((c.ExpectedDiagramId == null) != (c.TargetDiagramId == null)) throw new InvalidDataException("Supply both source and final target diagram identities.");
            if (c.ExpectedParentId == c.TargetParentId || c.ElementId == c.TargetParentId) throw new InvalidDataException("Reparenting requires a different container, not the element itself.");
            if (c.Position is { } p && (!double.IsFinite(p.X) || !double.IsFinite(p.Y) || Math.Abs(p.X) > 1000000 || Math.Abs(p.Y) > 1000000))
                throw new InvalidDataException("Native coordinates must be finite and within the supported range.");
        }
    }

    public static NativeElement[] Expected(NativeElement[] source, NativeReparenting[] changes)
    {
        Validate(changes);
        var before = source.ToDictionary(e => e.Id); var after = Copy(source).ToDictionary(e => e.Id);
        // Reject inconsistent observations before deriving final containment. A
        // relocation must not silently repair an unrelated stale diagram field.
        foreach (var item in source)
            if (item.ParentId != "" && (!before.TryGetValue(item.ParentId, out var parent) ||
                item.DiagramId != (parent.Kind == "Collaboration" ? parent.Id : parent.DiagramId)))
                throw new InvalidDataException("Source containment has an inconsistent diagram or an absent parent.");
        var selected = changes.Select(c => c.ElementId).ToHashSet(StringComparer.Ordinal);
        foreach (var c in changes)
        {
            if (!before.TryGetValue(c.ElementId, out var item) || item.ParentId != c.ExpectedParentId || !before.TryGetValue(c.TargetParentId, out var target))
                throw new InvalidDataException("Reparenting source, expected parent or target is absent or stale.");
            if (item.Kind is "Collaboration" or "Participant" or "Process" or "Lane" or "Milestone" or "Group" or "HeaderArtifact" or "DataStore" ||
                target.Kind != "Process" && target.SubProcess == null || before[item.ParentId].Kind != "Process" && before[item.ParentId].SubProcess == null)
                throw new NotSupportedException("Move contained native elements between participant processes or embedded subprocesses; diagram-owned catalogs, groups and partitions have separate contracts.");
            string parent = item.ParentId; var seen = new HashSet<string>();
            while (parent != "")
            {
                if (!seen.Add(parent)) throw new InvalidDataException("Cyclic source containment.");
                if (selected.Contains(parent)) throw new InvalidDataException("Select subtree roots without also selecting their descendants.");
                parent = before[parent].ParentId;
            }
            after[item.Id].ParentId = target.Id;
            if (c.Position is { } position)
            {
                if (item.Geometry == null || item.SourceId != "" || item.TargetId != "") throw new InvalidDataException("Position requires a native node, not a connector.");
                after[item.Id].Geometry!.X = (float)position.X; after[item.Id].Geometry!.Y = (float)position.Y;
                if (after[item.Id].ExpandedGeometry is { } expanded) { expanded.X = (float)position.X; expanded.Y = (float)position.Y; }
            }
        }
        foreach (var item in after.Values)
        {
            var seen = new HashSet<string>(); string parent = item.ParentId;
            while (parent != "")
            {
                if (!seen.Add(parent) || !after.TryGetValue(parent, out var owner)) throw new InvalidDataException("Final containment is cyclic or orphaned.");
                if (owner.Kind == "Collaboration") item.DiagramId = owner.Id;
                parent = owner.ParentId;
            }
        }
        foreach (var c in changes)
        {
            var item = before[c.ElementId]; var result = after[c.ElementId];
            if (item.DiagramId != result.DiagramId && (c.ExpectedDiagramId != item.DiagramId || c.TargetDiagramId != result.DiagramId) ||
                c.ExpectedDiagramId != null && (c.ExpectedDiagramId != item.DiagramId || c.TargetDiagramId != result.DiagramId))
                throw new InvalidDataException("Cross-diagram moves require exact explicit source and final target diagram identities.");
        }
        var affected = after.Values.Where(e => e.DiagramId != before[e.Id].DiagramId || selected.Contains(e.Id)).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var item in after.Values)
        {
            // Ports/associations are nested readback records, not top-level graph
            // objects. Their diagram follows their actual native event/activity.
            if (item.DiagramId != before[item.Id].DiagramId && item.DataFlow is { } io)
                foreach (var port in io.Inputs.Concat(io.Outputs).Concat(io.InputAssociations).Concat(io.OutputAssociations)) port.DiagramId = item.DiagramId;
            if (item.Kind is "SequenceFlow" or "Association" && (affected.Contains(item.Id) || affected.Contains(item.SourceId) || affected.Contains(item.TargetId)))
            {
                if (!after.TryGetValue(item.SourceId, out var start) || !after.TryGetValue(item.TargetId, out var end) || start.ParentId != end.ParentId || item.ParentId != start.ParentId ||
                    start.DiagramId != item.DiagramId || end.DiagramId != item.DiagramId)
                    throw new InvalidDataException("Move the complete connection closure; reparenting cannot leave a cross-container flow or association.");
            }
            if (item.Kind == "MessageFlow" && (affected.Contains(item.SourceId) || affected.Contains(item.TargetId)) &&
                (!after.TryGetValue(item.SourceId, out var messageSource) || !after.TryGetValue(item.TargetId, out var messageTarget) ||
                 messageSource.DiagramId != item.DiagramId || messageTarget.DiagramId != item.DiagramId))
                throw new InvalidDataException("Cross-diagram moves cannot leave incident message flows in another diagram.");
            if (item.Event is { } ev)
            {
                foreach (string targetId in new[] { ev.AttachedToActivityId }.Concat(ev.Definitions.Select(d => d.Compensation?.ActivityId ?? "")).Where(id => id != ""))
                    if ((affected.Contains(item.Id) || affected.Contains(targetId)) && (!after.TryGetValue(targetId, out var attached) || attached.ParentId != item.ParentId || attached.DiagramId != item.DiagramId))
                        throw new InvalidDataException("Move the complete boundary/compensation reference closure.");
                after.TryGetValue(item.ParentId, out var owner);
                if (selected.Contains(item.Id) && (item.ElementType == "CancelEnd" && owner?.SubProcess?.Kind != "Transaction" ||
                    ev.Mode == "Start" && (item.ElementType is "ErrorStart" or "EscalationStart" or "CompensationStart" || ev.IsInterrupting == false) && owner?.SubProcess?.TriggeredByEvent != true))
                    throw new InvalidDataException("The event requires its original special subprocess context.");
            }
        }
        return source.Select(e => after[e.Id]).ToArray();
    }

    public static void Verify(NativeElement[] before, NativeElement[] after, NativeReparenting[] changes)
    {
        var expected = Expected(before, changes).ToDictionary(e => e.Id); var actual = after.ToDictionary(e => e.Id);
        if (!expected.Keys.Order().SequenceEqual(actual.Keys.Order())) throw new InvalidDataException("Reparenting changed native identities.");
        foreach (string id in expected.Keys)
            if (JsonSerializer.Serialize(expected[id]) != JsonSerializer.Serialize(actual[id])) throw new InvalidDataException("Reparenting changed unrequested native graph fields: " + id);
    }

    public static NativeFidelityReport Compare(byte[] beforeBytes, byte[] afterBytes, NativeElement[] before, NativeElement[] after, NativeReparenting[] changes,
        EngineReply? sourceReply = null, EngineReply? editedReply = null, EngineReply? reopenedReply = null)
    {
        Verify(before, after, changes);
        if (before.Any(e => after.Single(a => a.Id == e.Id).DiagramId != e.DiagramId))
            return CompareCrossDiagram(beforeBytes, afterBytes, before, after, changes, sourceReply, editedReply, reopenedReply);
        var original = before.ToDictionary(e => e.Id); var expected = Expected(before, changes).ToDictionary(e => e.Id);
        var left = NativeArchive.ReadEntries(beforeBytes).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var right = NativeArchive.ReadEntries(afterBytes).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var group in changes.GroupBy(c => original[c.ElementId].DiagramId))
        {
            string key = group.Key + ".diag!/Diagram.xml";
            var a = NativeMetadataPolicy.Read(Encoding.UTF8.GetString(left[key])); var b = NativeMetadataPolicy.Read(Encoding.UTF8.GetString(right[key]));
            var carriers = group.SelectMany(c =>
            {
                XElement Carrier(XDocument x) => x.Descendants().Single(e => NativeFidelity.IsNativeNameOwner(e) && (string?)e.Attribute("Id") == c.ElementId && e.Name != Ns + "ActivitySet");
                var old = Carrier(a); var current = Carrier(b);
                if (old.Name != current.Name || Key(old) != Destination(original[c.ExpectedParentId], old.Name.LocalName) || Key(current) != Destination(expected[c.TargetParentId], current.Name.LocalName))
                    throw new InvalidDataException("Durable XML containment does not match the explicit native move.");
                var records = new List<(XElement Old, XElement Current)> { (old, current) };
                // Native I/O associations are owned by the activity in memory but serialized
                // beside it in a separate container collection. Relocate their actual records
                // too; do not discard binding payloads or infer them from the visible connector.
                var dataFlow = original[c.ElementId].DataFlow;
                foreach (var binding in (dataFlow?.InputAssociations ?? []).Concat(dataFlow?.OutputAssociations ?? []))
                {
                    XElement Association(XDocument doc) => doc.Descendants(Ns + "DataAssociation").Single(e => NativeFidelity.IsNativeNameOwner(e) && (string?)e.Attribute("Id") == binding.Id);
                    var oldBinding = Association(a); var newBinding = Association(b);
                    if (Key(oldBinding) != Destination(original[c.ExpectedParentId], "DataAssociation") || Key(newBinding) != Destination(expected[c.TargetParentId], "DataAssociation"))
                        throw new InvalidDataException("Durable I/O binding ownership differs from the moved activity.");
                    records.Add((oldBinding, newBinding));
                }
                return records;
            }).ToArray();
            RestoreCollections(a, b, carriers);

            // Native serialization flattens ActivitySets by owning process and traverses them
            // in graph order. Verify their final owners before restoring original comparison order.
            var oldSets = a.Descendants(Ns + "ActivitySet").Where(NativeFidelity.IsNativeNameOwner).ToArray();
            var newSets = b.Descendants(Ns + "ActivitySet").Where(NativeFidelity.IsNativeNameOwner).ToDictionary(e => (string)e.Attribute("Id")!);
            if (!oldSets.Select(e => (string)e.Attribute("Id")!).Order().SequenceEqual(newSets.Keys.Order())) throw new InvalidDataException("Activity-set identities changed during reparenting.");
            foreach (var set in oldSets)
            {
                string id = (string)set.Attribute("Id")!;
                if (Key(set) != new Container("WorkflowProcess", ProcessOf(original, id), "ActivitySets") ||
                    Key(newSets[id]) != new Container("WorkflowProcess", ProcessOf(expected, id), "ActivitySets"))
                    throw new InvalidDataException("A durable subprocess activity set has the wrong process owner.");
            }
            RestoreCollections(a, b, oldSets.Select(s => (Old: s, Current: newSets[(string)s.Attribute("Id")!])).ToArray());
            // The installed serializer also flattens activity/event I/O ports into the
            // participant process, even when their owner is deeply nested. Verify the
            // owner implied by the final graph and retain each actual native port record.
            var ports = before.Where(e => e.DiagramId == group.Key && e.DataFlow != null).SelectMany(owner =>
                owner.DataFlow!.Inputs.Concat(owner.DataFlow.Outputs).Select(port => (Owner: owner.Id, Port: port))).ToArray();
            var portRecords = ports.Select(p =>
            {
                XElement Port(XDocument doc) => doc.Descendants(Ns + p.Port.Kind).Single(e => NativeFidelity.IsNativeNameOwner(e) && (string?)e.Attribute("Id") == p.Port.Id);
                var oldPort = Port(a); var newPort = Port(b);
                if (Key(oldPort) != new Container("WorkflowProcess", ProcessOf(original, p.Owner), "DataInputOutputs") ||
                    Key(newPort) != new Container("WorkflowProcess", ProcessOf(expected, p.Owner), "DataInputOutputs"))
                    throw new InvalidDataException("A durable I/O port has the wrong participant process owner.");
                return (Old: oldPort, Current: newPort);
            }).ToArray();
            RestoreCollections(a, b, portRecords);
            right[key] = Encoding.UTF8.GetBytes(b.ToString(SaveOptions.DisableFormatting));
        }
        var positions = changes.Where(c => c.Position != null).Select(c =>
        {
            var node = expected[c.ElementId];
            return new NativeMutation { Operation = "update", ElementId = node.Id, Geometry = node.Geometry,
                ExpandedSize = node.Geometry!.Expanded && node.ExpandedGeometry != null ? new NativeSize { Width = node.ExpandedGeometry.Width, Height = node.ExpandedGeometry.Height } : null };
        }).ToArray();
        return positions.Length == 0 ? NativeFidelity.CompareEntries(left, right) : NativeMutationFidelity.CompareEntries(left, right, positions, after);
    }

    private static string ProcessOf(Dictionary<string, NativeElement> graph, string id)
    {
        while (graph.TryGetValue(id, out var item)) { if (item.Kind == "Process") return id; id = item.ParentId; }
        throw new InvalidDataException("Subprocess has no participant process owner.");
    }
    private static Container Destination(NativeElement owner, string record)
    {
        string collection = record switch { "Activity" => "Activities", "Transition" => "Transitions", "Artifact" => "Artifacts", "Association" => "Associations",
            "DataObject" => "DataObjects", "DataStoreReference" => "DataStoreReferences", "DataAssociation" => "DataAssociations", _ => throw new NotSupportedException("Unrepresented native relocation record: " + record) };
        if (owner.Kind == "Process" && record is "Artifact" or "Association") return new("Package", owner.DiagramId, collection);
        return new(owner.Kind == "Process" ? "WorkflowProcess" : "ActivitySet", owner.Id, collection);
    }
    private static Container Key(XElement record)
    {
        var collection = record.Parent ?? throw new InvalidDataException("Detached native record.");
        var owner = collection.Parent ?? throw new InvalidDataException("Missing native collection owner.");
        if (collection.Name.Namespace != Ns || owner.Name.Namespace != Ns) throw new InvalidDataException("Unexpected native collection namespace.");
        return new(owner.Name.LocalName, (string?)owner.Attribute("Id") ?? "", collection.Name.LocalName);
    }
    private static XElement Owner(IReadOnlyList<XDocument> docs, Container key) => docs.SelectMany(doc => doc.Descendants(Ns + key.Kind)).Single(e => (string?)e.Attribute("Id") == key.Id && NativeFidelity.IsNativeNameOwner(e));
    private static string RecordKey(XElement e) => e.Name + "|" + ((string?)e.Attribute("Id") ?? throw new InvalidDataException("Unrepresented collection member."));
    private static void Plain(XElement? collection)
    {
        if (collection == null) return;
        if (collection.HasAttributes || collection.AncestorsAndSelf().Any(e => e.Attribute(XNamespace.Xml + "space") != null) ||
            collection.Nodes().Any(n => n is not XElement && (n is not XText t || !string.IsNullOrWhiteSpace(t.Value))))
            throw new InvalidDataException("Reparenting cannot normalize unknown collection attributes, comments or preserved text.");
    }
    private static void RestoreCollections(XDocument before, XDocument after, (XElement Old, XElement Current)[] records)
        => RestoreCollections([before], [after], records);

    // Documents remain separate roots: joining them beneath a synthetic XML root
    // would invalidate the exact native-container provenance checks.
    private static void RestoreCollections(IReadOnlyList<XDocument> before, IReadOnlyList<XDocument> after, (XElement Old, XElement Current)[] records)
    {
        if (records.Length == 0) return;
        var selected = records.ToDictionary(p => RecordKey(p.Old), p => p.Current);
        var touched = records.SelectMany(p => new[] { Key(p.Old), Key(p.Current) }).Distinct().ToArray();
        foreach (var key in touched) { Plain(Owner(before, key).Element(Ns + key.Collection)); Plain(Owner(after, key).Element(Ns + key.Collection)); }
        // Materialize before mutation; never enumerate a lazy axis while removing its nodes.
        foreach (var pair in records) pair.Current.Remove();
        foreach (var key in touched)
        {
            var old = Owner(before, key).Element(Ns + key.Collection); var owner = Owner(after, key); var current = owner.Element(Ns + key.Collection);
            var remaining = current?.Elements().ToArray() ?? [];
            if (!(old?.Elements().Where(e => !selected.ContainsKey(RecordKey(e))).Select(RecordKey) ?? []).SequenceEqual(remaining.Select(RecordKey)))
                throw new InvalidDataException("Unrequested native collection members or order changed.");
            if (old == null) { current?.Remove(); continue; }
            var actual = remaining.ToDictionary(RecordKey);
            if (current == null)
            {
                current = new XElement(old.Name);
                var next = old.ElementsAfterSelf().Select(e => owner.Element(e.Name)).FirstOrDefault(e => e != null);
                if (next == null) owner.Add(current); else next.AddBeforeSelf(current);
            }
            current.RemoveNodes();
            foreach (var element in old.Elements()) current.Add(selected.TryGetValue(RecordKey(element), out var moved) ? moved : actual[RecordKey(element)]);
        }
        // Removing newly introduced wrappers can leave serializer indentation inside an
        // otherwise empty owner. Restore only that layout text, never comments, attributes,
        // significant text or xml:space content; all substantive payload remains compared.
        foreach (var key in touched)
        {
            var oldOwner = Owner(before, key); var newOwner = Owner(after, key);
            if (!oldOwner.HasElements && !newOwner.HasElements &&
                oldOwner.Nodes().All(n => n is XText t && string.IsNullOrWhiteSpace(t.Value)) &&
                newOwner.Nodes().All(n => n is XText t && string.IsNullOrWhiteSpace(t.Value)) &&
                !newOwner.AncestorsAndSelf().Any(e => e.Attribute(XNamespace.Xml + "space") != null))
                newOwner.ReplaceNodes(oldOwner.Nodes().OfType<XText>().Select(t => new XText(t.Value)));
        }
    }
}
