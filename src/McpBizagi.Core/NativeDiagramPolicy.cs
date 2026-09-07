using System.Text;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Whole-document lifecycle verification; comparison projections never write the native ZIP.</summary>
public static class NativeDiagramPolicy
{
    private static readonly XNamespace Xpdl = "http://www.wfmc.org/2009/XPDL2.2";
    public static void Validate(NativeDiagramPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (patch.Changes == null || patch.Changes.Length > 100 || patch.Changes.Length == 0 && patch.OpenedItems == null)
            throw new InvalidDataException("Supply diagram changes or an explicit opened-item list.");
        var ids = new HashSet<string>();
        foreach (var c in patch.Changes)
        {
            if (c == null) throw new InvalidDataException("Diagram changes cannot contain null entries.");
            NativeMetadataPolicy.RequireId(c.DiagramId);
            if (!ids.Add(c.DiagramId) || c.Operation is not "create" and not "rename" and not "delete" and not "clone") throw new InvalidDataException("Duplicate diagram target or unsupported operation.");
            if (c.Operation == "delete" ? c.Name != null : string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 120)
                throw new InvalidDataException("Create, clone and rename need a name; deletion must not contain one.");
        }
        if (patch.OpenedItems is { } opened)
        {
            if (opened.Length > 1000 || opened.Any(i => i == null) || opened.Count(i => i.IsSelected) > 1 || opened.Select(i => (i.DiagramId, i.SubProcessId)).Distinct().Count() != opened.Length)
                throw new InvalidDataException("Opened items must be unique, bounded and have at most one selected item.");
            foreach (var item in opened) { NativeMetadataPolicy.RequireId(item.DiagramId); if (item.SubProcessId != "") NativeMetadataPolicy.RequireId(item.SubProcessId); }
        }
    }
    private static XDocument Read(byte[] bytes) => NativeMetadataPolicy.Read(Encoding.UTF8.GetString(bytes));
    private static byte[] Bytes(XDocument doc) => Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));
    private static string Prefix(string id) => id + ".diag!/";
    internal static Dictionary<string, string> Diagrams(IReadOnlyDictionary<string, byte[]> entries)
    {
        var result = new Dictionary<string, string>();
        foreach (var pair in entries.Where(p => p.Key.EndsWith(".diag!/Diagram.xml", StringComparison.OrdinalIgnoreCase)))
        {
            var root = Read(pair.Value).Root;
            if (root?.Name != Xpdl + "Package") throw new InvalidDataException("Missing native diagram package.");
            string id = (string?)root.Attribute("Id") ?? ""; NativeMetadataPolicy.RequireId(id);
            if (pair.Key != Prefix(id) + "Diagram.xml" || !result.TryAdd(id, (string?)root.Attribute("Name") ?? ""))
                throw new InvalidDataException("Native diagram archive identity is ambiguous or does not match its package.");
        }
        return result;
    }

    public static NativeFidelityReport Compare(byte[] before, byte[] after, NativeDiagramPatch patch, EngineReply edited, EngineReply reopened)
    {
        Validate(patch);
        var left = NativeArchive.ReadEntries(before).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var right = NativeArchive.ReadEntries(after).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var oldDiagrams = Diagrams(left); var newDiagrams = Diagrams(right);
        var expected = new Dictionary<string, string>(oldDiagrams); var created = new HashSet<string>(); var deleted = new HashSet<string>();
        if (edited.DiagramClones.Length != patch.Changes.Count(c => c.Operation == "clone")) throw new InvalidDataException("Clone receipts do not cover the request.");
        foreach (var c in patch.Changes)
        {
            switch (c.Operation)
            {
                case "create":
                    if (!expected.TryAdd(c.DiagramId, c.Name!)) throw new InvalidDataException("Created diagram already existed.");
                    created.Add(c.DiagramId);
                    var nodes = reopened.Elements.Where(e => e.DiagramId == c.DiagramId).ToArray();
                    if (nodes.Count(e => e.Kind == "Collaboration") != 1 || nodes.Count(e => e.Kind == "Participant") != 2 || nodes.Count(e => e.Kind == "Process") != 2 || nodes.Length != 5 ||
                        reopened.Scenarios.Count(s => s.DiagramId == c.DiagramId) != 1)
                        throw new InvalidDataException("Created diagram did not retain its native empty-document defaults.");
                    break;
                case "rename":
                    if (!expected.ContainsKey(c.DiagramId)) throw new InvalidDataException("Rename source is absent.");
                    expected[c.DiagramId] = c.Name!; break;
                case "delete":
                    if (!expected.Remove(c.DiagramId)) throw new InvalidDataException("Deleted diagram was absent.");
                    deleted.Add(c.DiagramId); break;
                case "clone":
                    if (!oldDiagrams.ContainsKey(c.DiagramId)) throw new InvalidDataException("Clone source is absent.");
                    var receipt = edited.DiagramClones.Single(r => r.SourceId == c.DiagramId);
                    NativeMetadataPolicy.RequireId(receipt.TargetId);
                    if (!expected.TryAdd(receipt.TargetId, c.Name!)) throw new InvalidDataException("Clone identity was reused.");
                    var cloneReport = CompareClone(left, right, receipt, c.Name!, reopened.Elements);
                    if (!cloneReport.Preserved) return cloneReport;
                    created.Add(receipt.TargetId); break;
            }
        }
        if (expected.Count == 0 || expected.Count != newDiagrams.Count || expected.Any(p => !newDiagrams.TryGetValue(p.Key, out var name) || name != p.Value))
            throw new InvalidDataException("Durable diagram set/name differs from the explicit request.");
        var state = reopened.DiagramState ?? throw new InvalidDataException("Missing independent diagram snapshot.");
        if (state.Diagrams.Length != expected.Count || state.Diagrams.Select(d => d.Id).Distinct().Count() != state.Diagrams.Length || state.Diagrams.Any(d => !expected.TryGetValue(d.Id, out var name) || name != d.Name))
            throw new InvalidDataException("Fresh-worker diagram snapshot differs from durable XML.");
        if (patch.OpenedItems is { } opened)
        {
            if (!opened.Select(ItemKey).SequenceEqual(state.OpenedItems.Select(ItemKey))) throw new InvalidDataException("Native tab preferences did not survive restart.");
            var scope = state.PreferenceEntries;
            if (scope.Length is < 1 or > 2 || scope.Distinct(StringComparer.OrdinalIgnoreCase).Count() != scope.Length ||
                !scope.Contains("Users/Default/UserPreferences.xml", StringComparer.OrdinalIgnoreCase) ||
                !scope.SequenceEqual(edited.DiagramState?.PreferenceEntries ?? [], StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Native preference write scope is absent or changed across workers.");
            foreach (string key in scope)
            {
                var parts = key.Split('/');
                if (parts.Length != 3 || parts[0] != "Users" || parts[2] != "UserPreferences.xml" || string.IsNullOrWhiteSpace(parts[1]) ||
                    parts[1] is "." or ".." || parts[1].IndexOfAny(['\\', ':']) >= 0 || !left.ContainsKey(key) || !right.ContainsKey(key))
                    throw new InvalidDataException("Preference projection requires existing, confined archive entries.");
                var a = Read(left[key]); var b = Read(right[key]);
                if (a.Root?.Name != "UserPreferences" || b.Root?.Name != "UserPreferences") throw new InvalidDataException("Unexpected preference document root.");
                var x = a.Root.Element("OpenedItems"); var y = b.Root.Element("OpenedItems");
                if (x == null || y == null || a.Root.Elements("OpenedItems").Count() != 1 || b.Root.Elements("OpenedItems").Count() != 1) throw new InvalidDataException("Missing or duplicate native opened-item preferences.");
                XElement wanted = OpenedXml(opened);
                // Compare semantics without ignoring unknown nodes, namespaces, comments or xml:space.
                if (!NativeFidelity.CompareEntries(new Dictionary<string, byte[]> { ["tabs.xml"] = Bytes(new XDocument(wanted)) },
                    new Dictionary<string, byte[]> { ["tabs.xml"] = Bytes(new XDocument(y)) }).Preserved)
                    throw new InvalidDataException("Durable opened-item preferences differ from the complete requested list.");
                y.ReplaceWith(new XElement(x)); right[key] = Bytes(b);
            }
        }
        foreach (string id in deleted) foreach (string key in left.Keys.Where(k => k.StartsWith(Prefix(id), StringComparison.OrdinalIgnoreCase)).ToArray()) left.Remove(key);
        foreach (string id in created) foreach (string key in right.Keys.Where(k => k.StartsWith(Prefix(id), StringComparison.OrdinalIgnoreCase)).ToArray()) right.Remove(key);
        foreach (var change in patch.Changes.Where(c => c.Operation == "rename"))
        {
            string key = Prefix(change.DiagramId) + "Diagram.xml"; var original = Read(left[key]); var resulting = Read(right[key]);
            ProjectDerivedDescription(original, resulting, change.Name!); right[key] = Bytes(resulting);
        }
        var names = patch.Changes.Where(c => c.Operation == "rename").Select(c => new ExpectedNativeName(c.DiagramId, c.Name!)).ToArray();
        var report = NativeFidelity.CompareEntries(left, right, names);
        return report with { Differences = report.Differences.Concat(patch.Changes.Select(c => new NativeDifference("request", c.Operation, "verified_diagram_lifecycle", c.DiagramId, null, "fresh-worker and durable container checked"))).ToArray() };
    }
    private static string ItemKey(NativeOpenedItem i) => i.DiagramId + ":" + i.SubProcessId + ":" + i.IsSelected;
    private static void ProjectDerivedDescription(XDocument source, XDocument target, string requestedName)
    {
        // Native PackageHeader.Description mirrors the diagram name; Documentation is a different field.
        var a = source.Root?.Element(Xpdl + "PackageHeader")?.Element(Xpdl + "Description");
        var b = target.Root?.Element(Xpdl + "PackageHeader")?.Element(Xpdl + "Description");
        if (a == null && b == null) return;
        if (a == null || b == null || a.Value != (string?)source.Root?.Attribute("Name") || b.Value != requestedName ||
            a.Nodes().Any(n => n is not XText) || b.Nodes().Any(n => n is not XText))
            throw new InvalidDataException("Derived diagram description does not match the explicit name change.");
        // Keep attributes and surrounding unknown payload visible to the whole-document comparator.
        b.Value = a.Value;
    }
    private static XElement OpenedXml(NativeOpenedItem[] items) => new("OpenedItems", items.Select(i => new XElement("ModelItem",
        new XAttribute("ItemType", i.SubProcessId == "" ? "Diagram" : "Subprocess"), new XAttribute("DiagramId", i.DiagramId),
        i.SubProcessId == "" ? null : new XAttribute("SubProcessId", i.SubProcessId), new XAttribute("IsSelected", i.IsSelected))));

    private static NativeFidelityReport CompareClone(IReadOnlyDictionary<string, byte[]> original, IReadOnlyDictionary<string, byte[]> result,
        NativeDiagramClone clone, string name, NativeElement[] reopened)
    {
        // Require a bijection over the durable source identities, not merely a self-consistent worker receipt.
        foreach (var identity in clone.Identities)
        {
            NativeMetadataPolicy.RequireId(identity.SourceId); NativeMetadataPolicy.RequireId(identity.TargetId);
            if (identity.SourceId == identity.TargetId) throw new InvalidDataException("Clone reused a source identity.");
        }
        var reverse = clone.Identities.ToDictionary(i => i.TargetId, i => i.SourceId);
        if (!reverse.TryGetValue(clone.TargetId, out var root) || root != clone.SourceId || reverse.Values.Distinct().Count() != reverse.Count)
            throw new InvalidDataException("Clone identity mapping is incomplete or ambiguous.");
        var nodes = reopened.Where(e => e.DiagramId == clone.TargetId).ToArray();
        if (nodes.Length != reverse.Count || nodes.Any(e => !reverse.ContainsKey(e.Id))) throw new InvalidDataException("Clone readback does not cover every mapped native identity.");
        var left = original.Where(p => p.Key.StartsWith(Prefix(clone.SourceId), StringComparison.OrdinalIgnoreCase)).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        string sourceXml = Prefix(clone.SourceId) + "Diagram.xml";
        var durableIds = Read(left[sourceXml]).Descendants().Where(NativeFidelity.IsNativeNameOwner).Select(e => (string)e.Attribute("Id")!).ToHashSet();
        if (!durableIds.SetEquals(reverse.Values)) throw new InvalidDataException("Clone receipt does not cover all durable source identities.");
        var existingIds = original.Where(p => p.Key.EndsWith(".diag!/Diagram.xml", StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => Read(p.Value).Descendants().Where(NativeFidelity.IsNativeNameOwner).Select(e => (string)e.Attribute("Id")!)).ToHashSet();
        if (reverse.Keys.Any(existingIds.Contains)) throw new InvalidDataException("Clone reused an existing model identity.");
        var right = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var bpmn = clone.Identities.Where(i => i.TargetBpmnId != "").GroupBy(i => i.TargetBpmnId).ToDictionary(g => g.Key, g => g.Select(i => i.SourceBpmnId).Distinct().Single());
        foreach (var pair in result.Where(p => p.Key.StartsWith(Prefix(clone.TargetId), StringComparison.OrdinalIgnoreCase)))
        {
            string suffix = pair.Key[Prefix(clone.TargetId).Length..];
            if (suffix.StartsWith("Files/", StringComparison.Ordinal))
            {
                var parts = suffix.Split('/'); if (parts.Length >= 3 && reverse.TryGetValue(parts[1], out var owner)) parts[1] = owner;
                right.Add(Prefix(clone.SourceId) + string.Join('/', parts), pair.Value); continue;
            }
            byte[] data = pair.Value;
            if (suffix == "Diagram.xml")
            {
                var doc = Read(data);
                foreach (var node in doc.Descendants().Where(e => e.Name.Namespace == Xpdl))
                    foreach (var attr in node.Attributes().Where(a => a.Name.Namespace == XNamespace.None))
                    {
                        bool owner = NativeFidelity.IsNativeNameOwner(node);
                        bool identity = attr.Name == "Id" && owner;
                        bool reference = owner && (node.Name.LocalName, attr.Name.LocalName) is ("Pool", "Process") or ("Lane", "ParentPool") or ("Milestone", "ParentPool") or ("Transition", "From") or ("Transition", "To") or ("MessageFlow", "Source") or ("MessageFlow", "Target");
                        reference |= node.Name == Xpdl + "BlockActivity" && attr.Name == "ActivitySetId" && node.Parent?.Name == Xpdl + "Activity" && NativeFidelity.IsNativeNameOwner(node.Parent);
                        reference |= NativeCallFidelity.IsCallReference(node) && attr.Name == "Id";
                        if ((identity || reference) && reverse.TryGetValue(attr.Value, out var id)) attr.Value = id;
                    }
                if ((string?)doc.Root?.Attribute("Name") != name) throw new InvalidDataException("Clone name does not match its request.");
                var source = Read(left[Prefix(clone.SourceId) + suffix]);
                ProjectDerivedDescription(source, doc, name);
                doc.Root!.SetAttributeValue("Name", source.Root!.Attribute("Name")!.Value);
                data = Bytes(doc);
            }
            else if (suffix == "ExtendedAttributeValues.xml")
            {
                var doc = NativeMetadataPolicy.Read(NativeDocumentationPolicy.ValuesContent(Encoding.UTF8.GetString(data), clone.TargetId, result));
                if (doc.Root?.Name != "DiagramAttributeValues") throw new InvalidDataException("Unexpected native attribute values root.");
                foreach (var value in doc.Root.Elements("ElementAttributeValues")) if (reverse.TryGetValue((string?)value.Attribute("ElementId") ?? "", out var id)) value.SetAttributeValue("ElementId", id);
                data = Bytes(doc);
            }
            else if (suffix == "BPSimData.xml")
            {
                var doc = Read(data);
                XNamespace ns = NativeMetadataPolicy.BpsimNamespace;
                if (doc.Root?.Name != ns + "BPSimData") throw new InvalidDataException("Unexpected native simulation root.");
                foreach (var element in doc.Root.Elements(ns + "Scenario").Elements(ns + "ElementParameters"))
                    if (bpmn.TryGetValue((string?)element.Attribute("elementRef") ?? "", out var id)) element.SetAttributeValue("elementRef", id);
                data = Bytes(doc);
            }
            right.Add(Prefix(clone.SourceId) + suffix, data);
        }
        // Normalize source attachment scratch paths before comparing to the clone's mapped owners.
        string valuesKey = Prefix(clone.SourceId) + "ExtendedAttributeValues.xml";
        if (left.TryGetValue(valuesKey, out var values)) left[valuesKey] = Encoding.UTF8.GetBytes(NativeDocumentationPolicy.ValuesContent(Encoding.UTF8.GetString(values), clone.SourceId, original));
        return NativeFidelity.CompareEntries(left, right);
    }
}
