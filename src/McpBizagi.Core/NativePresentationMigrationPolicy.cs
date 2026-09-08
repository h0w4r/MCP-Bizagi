using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

public sealed record NativePresentationMigrationPlan(NativePresentationTransfer[] Transfers,
    NativePresentationAction[] ExpectedActions, NativeAttachmentInfo[] ExpectedFiles,
    IReadOnlyDictionary<string, byte[]> ExpectedEntries)
{
    public NativeFidelityReport? Verification { get; internal set; }
}

/// <summary>Exact action ownership and byte relocation; caches are preserved, not silently recomputed.</summary>
public static class NativePresentationMigrationPolicy
{
    private static bool Entry(string key) => key.EndsWith(".diag!/Actions.xml", StringComparison.OrdinalIgnoreCase) || key.Contains(".diag!/Actions/", StringComparison.OrdinalIgnoreCase);
    private static string Diagram(string key) => key[..key.IndexOf(".diag!/", StringComparison.OrdinalIgnoreCase)];
    private static Dictionary<string, byte[]> Entries(IReadOnlyDictionary<string, byte[]> archive) => archive.Where(p => Entry(p.Key)).ToDictionary(p => p.Key,
        p => p.Key.EndsWith(".diag!/Actions.xml", StringComparison.OrdinalIgnoreCase)
            ? Encoding.UTF8.GetBytes(NativePresentationPolicy.Normalize(Encoding.UTF8.GetString(p.Value), Diagram(p.Key), archive)) : p.Value,
        StringComparer.OrdinalIgnoreCase);

    public static NativePresentationMigrationPlan Prepare(byte[] bytes, EngineReply source, NativeElement[] expected)
    {
        var archive = NativeArchive.ReadEntries(bytes); var entries = Entries(archive);
        var finalGraph = expected.ToDictionary(e => e.Id);
        var originalGraph = source.Elements.ToDictionary(e => e.Id);
        if (!originalGraph.Keys.Order().SequenceEqual(finalGraph.Keys.Order())) throw new InvalidDataException("Action migration cannot change graph identities.");
        var moved = source.Elements.Where(e => e.DiagramId != finalGraph[e.Id].DiagramId).ToDictionary(e => e.Id);
        if (moved.Count == 0) throw new InvalidDataException("Presentation migration requires a cross-diagram move.");
        var docs = source.Elements.Where(e => e.Kind == "Collaboration").ToDictionary(e => e.Id, e =>
            entries.TryGetValue(e.Id + ".diag!/Actions.xml", out var xml) ? NativeMetadataPolicy.Read(Encoding.UTF8.GetString(xml)) : new XDocument(new XElement("DiagramActions")));
        var records = docs.SelectMany(p => p.Value.Root!.Elements("PresentationAction").Select(n => (Node: n, Action: NativePresentationPolicy.ReadAction(n, p.Key)))).ToArray();
        if (records.Select(r => r.Action.ElementId).Distinct().Count() != records.Length) throw new InvalidDataException("Presentation owners are duplicated across native diagrams.");
        var actions = records.Select(r => r.Action).ToArray();
        var observed = source.Presentation ?? throw new InvalidDataException("Missing original native presentation observation.");
        NativePresentationPolicy.Verify(actions, Files(actions, entries), observed);
        var transfers = new List<NativePresentationTransfer>();
        foreach (var record in records.Where(r => moved.ContainsKey(r.Action.ElementId)))
        {
            var a = record.Action;
            if (originalGraph[a.ElementId].DiagramId != a.DiagramId) throw new InvalidDataException("Presentation action is not in its native owner's diagram.");
            string target = finalGraph[a.ElementId].DiagramId;
            foreach (string id in new[] { a.DiagramId, target }) CheckContainer(docs[id].Root!);
            var transfer = new NativePresentationTransfer { SourceAction = JsonSerializer.Deserialize<NativePresentationAction>(JsonSerializer.Serialize(a))!, TargetDiagramId = target };
            transfers.Add(transfer);
        }
        // Copy from the immutable original map. Batch order cannot turn a destination into a new source.
        foreach (var transfer in transfers.Where(t => NativePresentationPolicy.Binary(t.SourceAction)))
        {
            var a = transfer.SourceAction; string original = NativePresentationPolicy.FileKey(a);
            string target = transfer.TargetDiagramId + ".diag!/Actions/" + a.Content[12..];
            byte[] payload = archive[original];
            if (entries.TryGetValue(target, out var existing) && !existing.AsSpan().SequenceEqual(payload))
                throw new InvalidDataException("Presentation migration payload collides with different destination bytes.");
            entries[target] = payload;
        }
        foreach (var transfer in transfers)
        {
            var record = records.Single(r => r.Action.ElementId == transfer.SourceAction.ElementId);
            record.Node.Remove(); docs[transfer.TargetDiagramId].Root!.Add(record.Node);
            record.Action.DiagramId = transfer.TargetDiagramId;
        }
        foreach (string oldFile in transfers.Where(t => NativePresentationPolicy.Binary(t.SourceAction)).Select(t => NativePresentationPolicy.FileKey(t.SourceAction)).Distinct(StringComparer.OrdinalIgnoreCase))
            if (!actions.Any(a => NativePresentationPolicy.Binary(a) && NativePresentationPolicy.FileKey(a).Equals(oldFile, StringComparison.OrdinalIgnoreCase))) entries.Remove(oldFile);
        foreach (var a in actions.Where(NativePresentationPolicy.Binary))
            if (Guid.TryParse(Path.GetFileNameWithoutExtension(a.Content[12..]), out var owner) &&
                !actions.Any(other => other.DiagramId == a.DiagramId && other.ElementId == owner.ToString() && other.Type is "File" or "Image"))
                throw new InvalidDataException("Native persistence would prune a shared GUID-named action payload; rename that reference explicitly before migration.");
        foreach (string id in transfers.SelectMany(t => new[] { t.SourceAction.DiagramId, t.TargetDiagramId }).Distinct())
        {
            // CheckContainer rejects significant mixed content; preserve every actual record in order.
            docs[id].Root!.Nodes().OfType<XText>().Where(t => string.IsNullOrWhiteSpace(t.Value)).Remove();
            entries[id + ".diag!/Actions.xml"] = Encoding.UTF8.GetBytes(docs[id].ToString(SaveOptions.DisableFormatting));
        }
        return new(transfers.ToArray(), actions, Files(actions, entries), entries);
    }

    private static void CheckContainer(XElement root)
    {
        if (root.Name != "DiagramActions" || root.Attributes().Any(a => !a.IsNamespaceDeclaration) ||
            root.Elements().Any(e => e.Name != "PresentationAction") ||
            root.Nodes().Any(n => n is not XElement && (n is not XText t || !string.IsNullOrWhiteSpace(t.Value))))
            throw new InvalidDataException("Unrepresented presentation collection content cannot be relocated safely.");
    }

    private static NativeAttachmentInfo[] Files(NativePresentationAction[] actions, IReadOnlyDictionary<string, byte[]> entries) =>
        actions.Where(NativePresentationPolicy.Binary).Select(a =>
        {
            if (!a.Content.StartsWith("action-file:", StringComparison.Ordinal) || !entries.TryGetValue(NativePresentationPolicy.FileKey(a), out var bytes))
                throw new InvalidDataException("Presentation migration source lacks an owned payload.");
            return new NativeAttachmentInfo { DiagramId = a.DiagramId, ElementId = a.ElementId, FileName = a.Content[12..], Length = bytes.Length, Sha256 = BpmnDocument.Revision(bytes) };
        }).ToArray();

    public static void Project(Dictionary<string, byte[]> original, Dictionary<string, byte[]> actual, NativePresentationMigrationPlan plan,
        EngineReply edited, EngineReply reopened)
    {
        NativePresentationPolicy.Verify(plan.ExpectedActions, plan.ExpectedFiles, edited.Presentation ?? throw new InvalidDataException("Missing migrated action editor evidence."));
        NativePresentationPolicy.Verify(plan.ExpectedActions, plan.ExpectedFiles, reopened.Presentation ?? throw new InvalidDataException("Missing migrated action restart evidence."));
        // Establish exact real final action files BEFORE reverting comparison-only locations.
        // This includes record order, unknown fields, literal caches, and opaque payload bytes.
        var comparison = NativeFidelity.CompareEntries(plan.ExpectedEntries, Entries(actual));
        plan.Verification = comparison;
        if (!comparison.Preserved) throw new InvalidDataException("Presentation migration changed unrequested native action content.");
        foreach (string key in actual.Keys.Where(Entry).ToArray()) actual.Remove(key);
        foreach (var pair in Entries(original)) actual[pair.Key] = pair.Value;
        // Normalize source comparison copies too; no persisted archive is rewritten here.
        foreach (var pair in Entries(original)) original[pair.Key] = pair.Value;
    }
}
