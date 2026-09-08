using System.Text;
using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

public sealed record NativeSavedSimulationPlan(string DiagramId, string ScenarioId,
    IReadOnlyDictionary<string, string> ExpectedTargetResults, NativeSavedSimulationResult[] ExpectedReadback,
    NativeSavedSimulationResult? PreviousResult);

/// <summary>Permit only the chosen actual result; preserve all other saved results and native archive content.</summary>
public static class NativeSavedSimulationPolicy
{
    private static readonly XNamespace Bp = NativeMetadataPolicy.BpsimNamespace;
    private static string ResultKey(string diagram) => diagram + ".diag!/BPSimDataResult.xml";
    private static string[] ScenarioIds(IReadOnlyDictionary<string, byte[]> entries, string diagram)
    {
        if (!entries.TryGetValue(diagram + ".diag!/BPSimData.xml", out var bytes)) throw new InvalidDataException("Simulation diagram is absent.");
        var document = NativeMetadataPolicy.Read(Encoding.UTF8.GetString(bytes));
        if (document.Root?.Name != Bp + "BPSimData") throw new InvalidDataException("Unsupported native scenario configuration.");
        var ids = document.Root.Elements(Bp + "Scenario").Select(s => (string?)s.Attribute("id") ?? "").ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct().Count() != ids.Length) throw new InvalidDataException("Native scenarios require unique identities.");
        return ids;
    }

    public static void ValidateTarget(byte[] archive, string diagramId, string scenarioId)
    {
        NativeMetadataPolicy.RequireId(diagramId);
        if (string.IsNullOrWhiteSpace(scenarioId) || scenarioId.Length > 1000) throw new InvalidDataException("Saving results requires an explicit bounded scenario ID.");
        XmlConvert.VerifyNCName(scenarioId);
        var entries = NativeArchive.ReadEntries(archive);
        if (!ScenarioIds(entries, diagramId).Contains(scenarioId)) throw new InvalidDataException("Saved-result scenario is absent from the requested diagram.");
        // Reject unrepresented result records before paying for native simulation.
        ReadResults(entries, diagramId);
    }

    private static Dictionary<string, string> ReadResults(IReadOnlyDictionary<string, byte[]> entries, string diagram)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!entries.TryGetValue(ResultKey(diagram), out var bytes)) return result;
        using var input = new MemoryStream(bytes, writable: false);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = NativeSimulationResultPayload.MaximumBytes });
        var doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace); var root = doc.Root;
        bool Unknown(XNode n) => n is not XElement && (n is not XText t || !string.IsNullOrWhiteSpace(t.Value));
        if (root?.Name != "ScenarioResults" || root.HasAttributes || doc.Nodes().Any(Unknown) || root.Nodes().Any(Unknown))
            throw new InvalidDataException("Unrepresented saved-result container cannot be changed safely.");
        var known = ScenarioIds(entries, diagram);
        foreach (var record in root.Elements())
        {
            string id = (string?)record.Attribute("scenarioId") ?? "";
            if (record.Name != "Result" || record.Attributes().Count() != 1 || !known.Contains(id) ||
                record.Nodes().Any(n => n is not XText) || string.IsNullOrEmpty(record.Value) || !result.TryAdd(id, record.Value))
                throw new InvalidDataException("Unrepresented or duplicate saved-result record cannot be changed safely.");
        }
        return result;
    }

    /// <summary>Discard consent never authorizes losing unrepresented container data.</summary>
    public static void ValidateResultRetirement(IReadOnlyDictionary<string, byte[]> entries, string diagram) => ReadResults(entries, diagram);

    public static NativeSavedSimulationPlan Prepare(byte[] archive, string diagramId, string scenarioId, byte[] resultBytes)
    {
        ValidateTarget(archive, diagramId, scenarioId);
        string xml = NativeSimulationResultPayload.Read(resultBytes);
        var entries = NativeArchive.ReadEntries(archive); var expected = ReadResults(entries, diagramId);
        var previous = expected.TryGetValue(scenarioId, out var old) ? NativeSimulationResultPayload.Describe(diagramId, scenarioId, old) : null;
        expected[scenarioId] = xml;
        var readback = new List<NativeSavedSimulationResult>();
        foreach (string key in entries.Keys.Where(k => k.EndsWith(".diag!/BPSimData.xml", StringComparison.Ordinal)))
        {
            string diagram = key[..key.IndexOf(".diag!/", StringComparison.Ordinal)];
            foreach (var item in diagram == diagramId ? expected : ReadResults(entries, diagram))
                readback.Add(NativeSimulationResultPayload.Describe(diagram, item.Key, item.Value));
        }
        return new(diagramId, scenarioId, expected, readback.ToArray(), previous);
    }

    public static NativeFidelityReport Compare(byte[] before, byte[] after, NativeSavedSimulationPlan plan, EngineReply edited, EngineReply reopened)
    {
        var left = NativeArchive.ReadEntries(before).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var right = NativeArchive.ReadEntries(after).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var actual = ReadResults(right, plan.DiagramId);
        if (actual.Count != plan.ExpectedTargetResults.Count || plan.ExpectedTargetResults.Any(p => !actual.TryGetValue(p.Key, out var value) || value != p.Value))
            throw new InvalidDataException("Durable native saved results differ from the exact requested record and retained results.");
        foreach (var reply in new[] { edited, reopened })
        {
            var records = reply.SavedSimulationResults;
            if (records.Length != plan.ExpectedReadback.Length || plan.ExpectedReadback.Any(e => records.Count(r =>
                r.DiagramId == e.DiagramId && r.ScenarioId == e.ScenarioId && r.Sha256 == e.Sha256 && r.CharacterCount == e.CharacterCount) != 1))
                throw new InvalidDataException("Native saved-result property readback differs from durable intent.");
        }
        // Result ordering is not semantic; every keyed opaque payload above is
        // exact. Only this verified leaf is projected in comparison copies.
        string key = ResultKey(plan.DiagramId);
        if (left.TryGetValue(key, out var original)) right[key] = original; else right.Remove(key);
        return NativeFidelity.CompareEntries(left, right);
    }
}
