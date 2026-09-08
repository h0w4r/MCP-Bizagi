using System.Text;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

public static partial class NativeReparentingPolicy
{
    public static NativeSimulationMigrationPlan Preflight(byte[] bytes, EngineReply source, NativeReparenting[] changes, NativeSimulationMigration? simulationMigration = null, bool migratePresentationActions = false)
    {
        var expected = Expected(source.Elements, changes).ToDictionary(e => e.Id);
        var moved = source.Elements.Where(e => e.DiagramId != expected[e.Id].DiagramId).ToArray();
        if (moved.Length == 0)
        {
            if (migratePresentationActions) throw new InvalidDataException("Presentation migration requires a cross-diagram move.");
            if (simulationMigration != null) throw new InvalidDataException("Scenario migration requires a cross-diagram move.");
            return NativeSimulationMigrationPlan.Empty;
        }
        if (source.Metadata == null || source.Documentation == null || source.DiagramState == null)
            throw new InvalidDataException("Cross-diagram migration requires native metadata, documentation and tab evidence.");
        var entries = NativeArchive.ReadEntries(bytes);
        var simulation = NativeSimulationMigrationPolicy.Prepare(entries, source, expected.Values.ToArray(), simulationMigration);
        foreach (string diagram in moved.SelectMany(e => new[] { e.DiagramId, expected[e.Id].DiagramId }).Distinct())
            foreach (string name in new[] { "Actions.xml", "BPSimDataResult.xml" })
                if (entries.TryGetValue(diagram + ".diag!/" + name, out var value) && NativeMetadataPolicy.Read(Encoding.UTF8.GetString(value)).Root?.Elements().Any() == true &&
                    (name != "BPSimDataResult.xml" || !simulation.DiscardResultDiagrams.Contains(diagram)) && (name != "Actions.xml" || !migratePresentationActions))
                    throw new NotSupportedException("Cross-diagram migration cannot silently retire or invalidate presentation actions or simulation results.");
        return simulation;
    }

    private static NativeFidelityReport CompareCrossDiagram(byte[] beforeBytes, byte[] afterBytes, NativeElement[] before, NativeElement[] after,
        NativeReparenting[] changes, EngineReply? sourceReply, EngineReply? editedReply, EngineReply? reopenedReply, NativeSimulationMigrationPlan? simulation, NativePresentationMigrationPlan? presentation)
    {
        var original = before.ToDictionary(e => e.Id); var expected = Expected(before, changes).ToDictionary(e => e.Id);
        var moved = before.Where(e => e.DiagramId != expected[e.Id].DiagramId).ToDictionary(e => e.Id, e => (Source: e.DiagramId, Target: expected[e.Id].DiagramId));
        var left = NativeArchive.ReadEntries(beforeBytes).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var right = NativeArchive.ReadEntries(afterBytes).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        if (presentation != null) NativePresentationMigrationPolicy.Project(left, right, presentation, editedReply ?? throw new InvalidDataException("Missing action editor evidence."), reopenedReply ?? throw new InvalidDataException("Missing action restart evidence."));
        if (!left.Keys.Where(k => k.EndsWith(".diag!/Diagram.xml", StringComparison.OrdinalIgnoreCase)).Order().SequenceEqual(right.Keys.Where(k => k.EndsWith(".diag!/Diagram.xml", StringComparison.OrdinalIgnoreCase)).Order()))
            throw new InvalidDataException("Cross-diagram reparenting cannot create or remove diagrams.");
        var a = before.Where(e => e.Kind == "Collaboration").ToDictionary(e => e.Id, e => NativeMetadataPolicy.Read(Encoding.UTF8.GetString(left[e.Id + ".diag!/Diagram.xml"])));
        var b = a.Keys.ToDictionary(id => id, id => NativeMetadataPolicy.Read(Encoding.UTF8.GetString(right[id + ".diag!/Diagram.xml"])));
        XElement Find(Dictionary<string, XDocument> docs, string diagram, string kind, string id) => docs[diagram].Descendants(Ns + kind)
            .Single(e => (string?)e.Attribute("Id") == id && NativeFidelity.IsNativeNameOwner(e));

        var records = new List<(XElement Old, XElement Current)>();
        foreach (var c in changes)
        {
            var old = a[original[c.ElementId].DiagramId].Descendants().Single(e => (string?)e.Attribute("Id") == c.ElementId && e.Name != Ns + "ActivitySet" && NativeFidelity.IsNativeNameOwner(e));
            var current = Find(b, expected[c.ElementId].DiagramId, old.Name.LocalName, c.ElementId);
            if (Key(old) != Destination(original[c.ExpectedParentId], old.Name.LocalName) || Key(current) != Destination(expected[c.TargetParentId], current.Name.LocalName))
                throw new InvalidDataException("Cross-diagram carrier ownership differs from the native request.");
            records.Add((old, current));
            foreach (var binding in (original[c.ElementId].DataFlow?.InputAssociations ?? []).Concat(original[c.ElementId].DataFlow?.OutputAssociations ?? []))
            {
                var oldBinding = Find(a, original[c.ElementId].DiagramId, "DataAssociation", binding.Id);
                var newBinding = Find(b, expected[c.ElementId].DiagramId, "DataAssociation", binding.Id);
                if (Key(oldBinding) != Destination(original[c.ExpectedParentId], "DataAssociation") || Key(newBinding) != Destination(expected[c.TargetParentId], "DataAssociation"))
                    throw new InvalidDataException("Cross-diagram I/O binding ownership differs from its owner.");
                records.Add((oldBinding, newBinding));
            }
        }
        RestoreCollections(a.Values.ToArray(), b.Values.ToArray(), records.ToArray());

        var oldSets = a.Values.SelectMany(d => d.Descendants(Ns + "ActivitySet")).Where(NativeFidelity.IsNativeNameOwner).ToArray();
        var newSets = b.Values.SelectMany(d => d.Descendants(Ns + "ActivitySet")).Where(NativeFidelity.IsNativeNameOwner).ToDictionary(e => (string)e.Attribute("Id")!);
        if (!oldSets.Select(s => (string)s.Attribute("Id")!).Order().SequenceEqual(newSets.Keys.Order())) throw new InvalidDataException("Cross-diagram activity-set identities changed.");
        foreach (var set in oldSets)
        {
            string id = (string)set.Attribute("Id")!;
            if (Key(set) != new Container("WorkflowProcess", ProcessOf(original, id), "ActivitySets") || Key(newSets[id]) != new Container("WorkflowProcess", ProcessOf(expected, id), "ActivitySets"))
                throw new InvalidDataException("Cross-diagram subprocess process ownership differs.");
        }
        RestoreCollections(a.Values.ToArray(), b.Values.ToArray(), oldSets.Select(s => (Old: s, Current: newSets[(string)s.Attribute("Id")!])).ToArray());
        var ports = before.Where(e => e.DataFlow != null).SelectMany(e => e.DataFlow!.Inputs.Concat(e.DataFlow.Outputs).Select(p => (Owner: e.Id, Port: p))).ToArray();
        var portRecords = ports.Select(p =>
        {
            var old = Find(a, original[p.Owner].DiagramId, p.Port.Kind, p.Port.Id);
            var current = Find(b, expected[p.Owner].DiagramId, p.Port.Kind, p.Port.Id);
            if (Key(old) != new Container("WorkflowProcess", ProcessOf(original, p.Owner), "DataInputOutputs") || Key(current) != new Container("WorkflowProcess", ProcessOf(expected, p.Owner), "DataInputOutputs"))
                throw new InvalidDataException("Cross-diagram I/O port ownership differs.");
            return (Old: old, Current: current);
        }).ToArray();
        RestoreCollections(a.Values.ToArray(), b.Values.ToArray(), portRecords);
        foreach (var pair in b) right[pair.Key + ".diag!/Diagram.xml"] = Encoding.UTF8.GetBytes(pair.Value.ToString(SaveOptions.DisableFormatting));

        RestoreCrossDiagramValues(left, right, moved);
        foreach (var item in moved)
        {
            string oldPrefix = item.Value.Source + ".diag!/", newPrefix = item.Value.Target + ".diag!/";
            bool Payload(string relative) => relative.StartsWith("Files/" + item.Key + "/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("ImageArtifactImages/", StringComparison.OrdinalIgnoreCase) && Path.GetFileNameWithoutExtension(relative) == item.Key;
            var sources = left.Keys.Where(k => k.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase) && Payload(k[oldPrefix.Length..])).ToArray();
            foreach (string key in sources)
            {
                string target = newPrefix + key[oldPrefix.Length..];
                if (right.ContainsKey(key) || left.ContainsKey(target) || !right.TryGetValue(target, out var payload)) throw new InvalidDataException("Cross-diagram payload is missing, duplicated or overwrote existing content.");
                right.Add(key, payload); right.Remove(target);
            }
        }
        var state = sourceReply?.DiagramState ?? throw new InvalidDataException("Missing original native tab snapshot.");
        var opened = Copy(state.OpenedItems); bool tabsChanged = false;
        foreach (var tab in opened)
            if (moved.TryGetValue(tab.SubProcessId, out var relocation) && tab.DiagramId == relocation.Source)
            { tab.DiagramId = relocation.Target; tabsChanged = true; }
        if (tabsChanged) NativeDiagramPolicy.ProjectOpenedItems(left, right, opened,
            reopenedReply?.DiagramState ?? throw new InvalidDataException("Missing native tab restart snapshot."), editedReply?.DiagramState);
        if (simulation is { Transfers.Length: > 0 }) NativeSimulationMigrationPolicy.Project(left, right, simulation,
            editedReply ?? throw new InvalidDataException("Missing native editor migration evidence."), reopenedReply ?? throw new InvalidDataException("Missing native restart migration evidence."));
        var positions = changes.Where(c => c.Position != null).Select(c => new NativeMutation { Operation = "update", ElementId = c.ElementId,
            Geometry = expected[c.ElementId].Geometry, ExpandedSize = expected[c.ElementId].Geometry!.Expanded && expected[c.ElementId].ExpandedGeometry is { } e ? new() { Width = e.Width, Height = e.Height } : null }).ToArray();
        return positions.Length == 0 ? NativeFidelity.CompareEntries(left, right) : NativeMutationFidelity.CompareEntries(left, right, positions, before.Select(e => { var value = Copy(expected[e.Id]); value.DiagramId = e.DiagramId; return value; }).ToArray());
    }

    private static void RestoreCrossDiagramValues(Dictionary<string, byte[]> left, Dictionary<string, byte[]> right, Dictionary<string, (string Source, string Target)> moved)
    {
        var diagrams = moved.Values.SelectMany(v => new[] { v.Source, v.Target }).Distinct().ToArray();
        string KeyFor(string id) => id + ".diag!/ExtendedAttributeValues.xml";
        XDocument Read(Dictionary<string, byte[]> entries, string id) => NativeMetadataPolicy.Read(NativeDocumentationPolicy.ValuesContent(Encoding.UTF8.GetString(entries[KeyFor(id)]), id, entries));
        var old = diagrams.ToDictionary(id => id, id => Read(left, id)); var current = diagrams.ToDictionary(id => id, id => Read(right, id));
        // Preserve each actual owner record and its unknown fields, requiring
        // exact final diagram membership before reversing only the declared move.
        var originals = old.SelectMany(p => p.Value.Root!.Elements().Select(e => (Diagram: p.Key, Node: e))).ToArray();
        var actual = current.SelectMany(p => p.Value.Root!.Elements().Select(e => (Diagram: p.Key, Node: e))).ToDictionary(p => (string)p.Node.Attribute("ElementId")!);
        if (!originals.Select(p => (string)p.Node.Attribute("ElementId")!).Order().SequenceEqual(actual.Keys.Order())) throw new InvalidDataException("Native attribute-owner identity set changed.");
        foreach (var pair in old)
        {
            var root = pair.Value.Root!; var observed = current[pair.Key].Root!;
            if (root.Name != "DiagramAttributeValues" || observed.Name != root.Name || root.Attributes().Any(a => !a.IsNamespaceDeclaration) ||
                !XNode.DeepEquals(new XElement(root.Name, root.Attributes()), new XElement(observed.Name, observed.Attributes())))
                throw new InvalidDataException("Native attribute container changed.");
            foreach (var container in new[] { root, observed })
                if (container.Nodes().Any(n => n is not XElement && (n is not XText t || !string.IsNullOrWhiteSpace(t.Value))) || container.AncestorsAndSelf().Any(e => e.Attribute(XNamespace.Xml + "space") != null))
                    throw new InvalidDataException("Unknown attribute collection content cannot be normalized during migration.");
        }
        foreach (var item in originals)
        {
            string id = (string)item.Node.Attribute("ElementId")!;
            string target = moved.TryGetValue(id, out var relocation) ? relocation.Target : item.Diagram;
            if (actual[id].Diagram != target) throw new InvalidDataException("Native attribute owner moved to an unrequested diagram.");
        }
        foreach (string diagram in diagrams)
        {
            var root = current[diagram].Root!; root.RemoveNodes();
            foreach (var owner in originals.Where(p => p.Diagram == diagram)) root.Add(new XElement(actual[(string)owner.Node.Attribute("ElementId")!].Node));
            left[KeyFor(diagram)] = Encoding.UTF8.GetBytes(old[diagram].ToString(SaveOptions.DisableFormatting));
            right[KeyFor(diagram)] = Encoding.UTF8.GetBytes(current[diagram].ToString(SaveOptions.DisableFormatting));
        }
    }
}
