using System.Text;
using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Derived intent and exact expected BPSim documents for one native containment transaction.</summary>
public sealed record NativeSimulationMigrationPlan(NativeScenarioTransfer[] Transfers,
    NativeSimulationConfiguration[] ExpectedConfigurations, string[] DiscardResultDiagrams)
{
    public static NativeSimulationMigrationPlan Empty => new([], [], []);
}

/// <summary>Preserve configured parameter records and explicit scenario context; never guess unit conversions or overwrite dependencies.</summary>
public static class NativeSimulationMigrationPolicy
{
    private static readonly XNamespace Ns = NativeMetadataPolicy.BpsimNamespace;
    private static string A(XElement e, string name) => (string?)e.Attribute(name) ?? "";
    private static bool Same(XElement a, XElement b) => NativeMetadataPolicy.XmlEquivalent(a.ToString(), b.ToString());
    private static string Key(NativeScenarioMapping m) => m.SourceDiagramId + ":" + m.SourceScenarioId + ":" + m.TargetDiagramId;
    public static void Validate(NativeSimulationMigration? migration)
    {
        if (migration == null) return;
        if (migration.Mappings == null || migration.Mappings.Length is < 1 or > 1000 || migration.Mappings.Any(m => m == null))
            throw new InvalidDataException("Supply 1-1000 explicit scenario mappings.");
        foreach (var m in migration.Mappings)
        {
            NativeMetadataPolicy.RequireId(m.SourceDiagramId); NativeMetadataPolicy.RequireId(m.TargetDiagramId);
            if (string.IsNullOrWhiteSpace(m.SourceScenarioId) || string.IsNullOrWhiteSpace(m.TargetScenarioId) || m.SourceScenarioId.Length > 1000 || m.TargetScenarioId.Length > 1000)
                throw new InvalidDataException("Scenario mappings require nonempty bounded scenario IDs.");
            XmlConvert.VerifyNCName(m.SourceScenarioId); XmlConvert.VerifyNCName(m.TargetScenarioId);
            if (m.SourceDiagramId == m.TargetDiagramId) throw new InvalidDataException("Scenario migration requires distinct diagrams.");
        }
        if (migration.Mappings.Select(Key).Distinct(StringComparer.Ordinal).Count() != migration.Mappings.Length)
            throw new InvalidDataException("A source scenario requires exactly one target scenario per destination diagram.");
    }

    public static NativeSimulationMigrationPlan Prepare(IReadOnlyDictionary<string, byte[]> archive, EngineReply source,
        NativeElement[] expected, NativeSimulationMigration? migration)
    {
        Validate(migration);
        var final = expected.ToDictionary(e => e.Id);
        var moved = source.Elements.Where(e => e.DiagramId != final[e.Id].DiagramId).ToArray();
        if (moved.Length == 0)
        {
            if (migration != null) throw new InvalidDataException("Scenario mappings require a cross-diagram element move.");
            return NativeSimulationMigrationPlan.Empty;
        }
        var metadata = source.Metadata ?? throw new InvalidDataException("Missing native metadata for scenario migration.");
        var original = metadata.Simulations.ToDictionary(s => s.DiagramId, s => NativeMetadataPolicy.Read(s.Xml));
        var projected = original.ToDictionary(p => p.Key, p => new XDocument(p.Value));
        var destinations = new Dictionary<(string DiagramId, string Reference), string>();
        foreach (var element in moved)
            foreach (string reference in new[] { element.Id, element.BpmnId }.Where(s => s != "").Distinct())
                if (!destinations.TryAdd((element.DiagramId, reference), final[element.Id].DiagramId))
                    throw new InvalidDataException("Ambiguous moved simulation reference identity.");
        var maps = (migration?.Mappings ?? []).ToDictionary(Key, StringComparer.Ordinal);
        XElement Scenario(Dictionary<string, XDocument> docs, string diagram, string scenario) =>
            docs.TryGetValue(diagram, out var doc) ? doc.Root!.Elements(Ns + "Scenario").SingleOrDefault(e => A(e, "id") == scenario)
                ?? throw new InvalidDataException("Mapped scenario is absent: " + scenario) : throw new InvalidDataException("Mapped diagram is absent.");
        NativeScenarioMapping Mapping(string diagram, string scenario, string target) => maps.TryGetValue(diagram + ":" + scenario + ":" + target, out var value)
            ? value : throw new InvalidDataException("Configured simulation migration requires a complete explicit scenario correspondence.");

        // Every effective source scenario must have a destination, including
        // scenarios inheriting a moved parameter without a local override.
        IEnumerable<XElement> Chain(string diagram, string id)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (id != "")
            {
                if (!seen.Add(id)) throw new InvalidDataException("Cyclic scenario inheritance.");
                var scenario = Scenario(original, diagram, id); yield return scenario; id = A(scenario, "inherits");
            }
        }
        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (var diagram in original)
            foreach (var scenario in diagram.Value.Root!.Elements(Ns + "Scenario"))
            {
                if (string.IsNullOrWhiteSpace(A(scenario, "id"))) throw new InvalidDataException("Native scenario identity is absent.");
                foreach (var record in Chain(diagram.Key, A(scenario, "id")).SelectMany(s => s.Elements(Ns + "ElementParameters")))
                    if (destinations.TryGetValue((diagram.Key, A(record, "elementRef")), out string? target))
                        required.Add(diagram.Key + ":" + A(scenario, "id") + ":" + target);
            }
        if (required.Count == 0 && migration == null) return NativeSimulationMigrationPlan.Empty;
        if (required.Count != 0 && migration == null)
            throw new NotSupportedException("Cross-diagram migration requires explicit migration of configured element simulation parameters before moving their owners.");
        if (migration == null || required.Any(k => !maps.ContainsKey(k))) throw new InvalidDataException("Configured simulation migration requires complete scenario mappings.");
        var affected = moved.SelectMany(e => new[] { e.DiagramId, final[e.Id].DiagramId }).ToHashSet(StringComparer.Ordinal);
        var resources = metadata.Resources.Select(r => r.BpmnId).ToHashSet(StringComparer.Ordinal);
        foreach (string diagram in migration.Mappings.SelectMany(m => new[] { m.SourceDiagramId, m.TargetDiagramId }).Distinct())
        {
            if (!affected.Contains(diagram) || !original.ContainsKey(diagram)) throw new InvalidDataException("Scenario mapping is outside affected native diagrams.");
            string path = diagram + ".diag!/BPSimData.xml";
            if (!archive.TryGetValue(path, out var bytes) || !NativeMetadataPolicy.XmlEquivalent(Encoding.UTF8.GetString(bytes), original[diagram].ToString()))
                throw new InvalidDataException("Native BPSim source observation does not preserve the durable original document.");
        }
        var transfers = new List<NativeScenarioTransfer>();
        // Capture native-equivalent XML records from immutable observations first;
        // a reverse mapping in the same batch must not consume newly added records.
        var recordsToMove = new List<(NativeScenarioMapping Map, XElement Record)>();
        foreach (var m in migration.Mappings)
        {
            var a = Scenario(original, m.SourceDiagramId, m.SourceScenarioId); var b = Scenario(original, m.TargetDiagramId, m.TargetScenarioId);
            var effectiveIncoming = Chain(m.SourceDiagramId, m.SourceScenarioId).SelectMany(s => s.Elements(Ns + "ElementParameters"))
                .Select(e => A(e, "elementRef")).Where(r => destinations.TryGetValue((m.SourceDiagramId, r), out var d) && d == m.TargetDiagramId).ToHashSet(StringComparer.Ordinal);
            if (b.Elements(Ns + "ElementParameters").Any(e => effectiveIncoming.Contains(A(e, "elementRef"))))
                throw new InvalidDataException("Target scenario already overrides an incoming effective parameter.");
            XElement Context(XElement scenario, XDocument doc) => new(Ns + "Context", new XElement(Ns + "Root", doc.Root!.Attributes()),
                // Only unqualified standard identity fields may differ. A vendor
                // attribute with the same local name is still scenario context.
                new XElement(Ns + "Scenario", scenario.Attributes().Where(v => v.Name.Namespace != XNamespace.None || !new[] { "id", "name", "description", "author", "vendor", "version", "created", "modified", "inherits", "result" }.Contains(v.Name.LocalName)),
                    scenario.Elements().Where(e => e.Name != Ns + "ElementParameters" && e.Name != Ns + "Calendar")));
            if (!Same(Context(a, original[m.SourceDiagramId]), Context(b, original[m.TargetDiagramId])))
                throw new InvalidDataException("Mapped scenario contexts differ; preserve units, global parameters and vendor extensions explicitly before migration.");
            foreach (string link in new[] { "inherits", "result" })
            {
                string oldLink = A(a, link), newLink = A(b, link);
                if (oldLink == "" ? newLink != "" : newLink != Mapping(m.SourceDiagramId, oldLink, m.TargetDiagramId).TargetScenarioId)
                    throw new InvalidDataException("Mapped scenario inheritance or result provenance differs.");
            }
            var moving = a.Elements(Ns + "ElementParameters").Where(e => destinations.TryGetValue((m.SourceDiagramId, A(e, "elementRef")), out var target) && target == m.TargetDiagramId).ToArray();
            var transfer = new NativeScenarioTransfer { Mapping = m, ElementRefs = moving.Select(e => A(e, "elementRef")).ToArray() };
            var targetScenario = Scenario(projected, m.TargetDiagramId, m.TargetScenarioId);
            string[] CopyMissing(string element, string identity, IEnumerable<XElement> candidates)
            {
                var copied = new List<string>();
                foreach (var item in candidates)
                {
                    string id = A(item, identity);
                    var existing = targetScenario.Elements(Ns + element).SingleOrDefault(e => A(e, identity) == id);
                    if (existing != null) { if (!Same(item, existing)) throw new InvalidDataException("Conflicting target scenario dependency: " + id); continue; }
                    if (!migration.CopyMissingDependencies) throw new InvalidDataException("Missing scenario dependency requires explicit CopyMissingDependencies: " + id);
                    Insert(targetScenario, new XElement(item)); copied.Add(id);
                }
                return copied.ToArray();
            }
            // Copy the complete source resource context, not a brittle parser for
            // one expression dialect. Dynamic resource selections remain intact.
            transfer.ResourceRefsToCopy = CopyMissing("ElementParameters", "elementRef", a.Elements(Ns + "ElementParameters").Where(e => resources.Contains(A(e, "elementRef"))));
            transfer.CalendarIdsToCopy = CopyMissing("Calendar", "id", a.Elements(Ns + "Calendar"));
            transfers.Add(transfer); recordsToMove.AddRange(moving.Select(e => (m, e)));
        }
        if (recordsToMove.Count == 0) throw new InvalidDataException("Scenario migration did not select configured moved-element parameters.");
        foreach (var item in recordsToMove)
            Scenario(projected, item.Map.SourceDiagramId, item.Map.SourceScenarioId).Elements(Ns + "ElementParameters").Single(e => A(e, "elementRef") == A(item.Record, "elementRef")).Remove();
        foreach (var item in recordsToMove)
        {
            var target = Scenario(projected, item.Map.TargetDiagramId, item.Map.TargetScenarioId);
            if (target.Elements(Ns + "ElementParameters").Any(e => A(e, "elementRef") == A(item.Record, "elementRef")))
                throw new InvalidDataException("Target scenario already contains a migrated element parameter.");
            Insert(target, new XElement(item.Record));
        }
        var touched = migration.Mappings.SelectMany(m => new[] { m.SourceDiagramId, m.TargetDiagramId }).Distinct().ToArray();
        foreach (string diagram in touched) NativeMetadataPolicy.ValidateSimulation(projected[diagram].ToString());
        if (migration.DiscardSimulationResults)
            foreach (string diagram in affected)
                if (archive.TryGetValue(diagram + ".diag!/BPSimDataResult.xml", out var resultBytes))
                {
                    // Consent to retire results is not consent to discard unknown
                    // archive annotations or an unrepresented result container.
                    var root = NativeMetadataPolicy.Read(Encoding.UTF8.GetString(resultBytes)).Root;
                    if (root?.Name != "ScenarioResults" || root.HasAttributes || root.Nodes().Any(n => n is not XElement && (n is not XText t || !string.IsNullOrWhiteSpace(t.Value))))
                        throw new InvalidDataException("Unrepresented saved-result container cannot be retired safely.");
                    var known = original[diagram].Root!.Elements(Ns + "Scenario").Select(s => A(s, "id")).ToHashSet(StringComparer.Ordinal);
                    foreach (var result in root.Elements())
                        if (result.Name != "Result" || result.Attributes().Count() != 1 || !known.Contains(A(result, "scenarioId")) || result.Nodes().Any(n => n is not XText))
                            throw new InvalidDataException("Unrepresented saved-result record cannot be retired safely.");
                }
        return new(transfers.ToArray(), touched.Select(d => new NativeSimulationConfiguration { DiagramId = d, Xml = projected[d].ToString() }).ToArray(),
            migration.DiscardSimulationResults ? affected.Order(StringComparer.Ordinal).ToArray() : []);
    }

    private static void Insert(XElement scenario, XElement item)
    {
        // Follow the installed serializer's collection order while preserving
        // existing records in each collection and never deleting source context.
        var last = scenario.Elements(item.Name).LastOrDefault();
        if (last != null) { last.AddAfterSelf(item); return; }
        var next = scenario.Elements().FirstOrDefault(e => item.Name == Ns + "ElementParameters" ? e.Name == Ns + "Calendar" || e.Name == Ns + "VendorExtension" : e.Name == Ns + "VendorExtension");
        if (next != null) next.AddBeforeSelf(item); else scenario.Add(item);
    }

    public static void Project(Dictionary<string, byte[]> left, Dictionary<string, byte[]> right, NativeSimulationMigrationPlan plan, EngineReply edited, EngineReply reopened)
    {
        foreach (var configuration in plan.ExpectedConfigurations)
        {
            string key = configuration.DiagramId + ".diag!/BPSimData.xml";
            foreach (var reply in new[] { edited, reopened })
                if (!NativeMetadataPolicy.XmlEquivalent(configuration.Xml, reply.Metadata!.Simulations.Single(s => s.DiagramId == configuration.DiagramId).Xml))
                    throw new InvalidDataException("Native scenario migration differs from the complete expected configuration.");
            if (!right.TryGetValue(key, out var bytes) || !NativeMetadataPolicy.XmlEquivalent(configuration.Xml, Encoding.UTF8.GetString(bytes)))
                throw new InvalidDataException("Durable BPSim migration differs from the expected native records.");
            right[key] = left[key];
        }
        foreach (string diagram in plan.DiscardResultDiagrams)
        {
            string key = diagram + ".diag!/BPSimDataResult.xml";
            if (!right.TryGetValue(key, out var bytes) || !NativeMetadataPolicy.XmlEquivalent(Encoding.UTF8.GetString(bytes), "<ScenarioResults/>"))
                throw new InvalidDataException("Explicit result retirement did not produce an empty durable result set.");
            if (left.TryGetValue(key, out var original)) right[key] = original; else right.Remove(key);
        }
    }
}
