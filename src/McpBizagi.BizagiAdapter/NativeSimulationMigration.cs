using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private void MigrateScenarioParameters(object model, NativeScenarioTransfer[] transfers, string[] discardResults, Action<string> progress)
    {
        var diagrams = Items(model, "Diagrams").ToDictionary(d => Text(d, "Id"), StringComparer.Ordinal);
        object Scenario(string diagram, string scenario) => Items(Get(diagrams[diagram], "BPSimData"), "Scenarios").Single(s => Text(s, "Id") == scenario);
        var graph = Graph(model).ToArray();
        var resources = Items(model, "Resources").Select(r => Text(r, "BpmnId")).ToHashSet(StringComparer.Ordinal);
        var scheduled = new List<(IList Source, IList Target, object Value)>();
        var copies = new List<(IList Target, object Value, string Identity)>();
        foreach (var transfer in transfers)
        {
            var m = transfer.Mapping;
            object source = Scenario(m.SourceDiagramId, m.SourceScenarioId), target = Scenario(m.TargetDiagramId, m.TargetScenarioId);
            var from = (IList)Get(source, "ElementParameters"); var to = (IList)Get(target, "ElementParameters");
            foreach (string reference in transfer.ElementRefs)
            {
                if (!graph.Any(e => e.DiagramId == m.TargetDiagramId && (Text(e.Value, "BpmnId") == reference || Text(e.Value, "Id") == reference)))
                    throw new InvalidDataException("Migrated scenario parameter has no element in its final native diagram.");
                object value = Items(from).Single(e => Text(e, "ElementRef") == reference);
                if (scheduled.Any(s => ReferenceEquals(s.Value, value))) throw new InvalidDataException("Native scenario record was selected twice.");
                scheduled.Add((from, to, value));
            }
            foreach (string reference in transfer.ResourceRefsToCopy)
            {
                if (!resources.Contains(reference)) throw new InvalidDataException("Scenario dependency is not a native model resource.");
                copies.Add((to, CopySimulationValue(Items(from).Single(e => Text(e, "ElementRef") == reference)), "ElementRef"));
            }
            var targetCalendars = (IList)Get(target, "Calendars");
            foreach (string id in transfer.CalendarIdsToCopy)
                copies.Add((targetCalendars, CopySimulationValue(Items(source, "Calendars").Single(c => Text(c, "Id") == id)), "Id"));
        }
        // Capture all native source objects before touching any list: bidirectional
        // and multi-diagram batches must not consume newly introduced records.
        foreach (var item in copies)
        {
            string id = Text(item.Value, item.Identity);
            if (Items(item.Target).Any(e => Text(e, item.Identity) == id)) throw new InvalidDataException("Native scenario dependency destination is no longer empty.");
            item.Target.Add(item.Value); progress("native_scenario_dependency:" + id);
        }
        foreach (var item in scheduled) item.Source.Remove(item.Value);
        foreach (var item in scheduled)
        {
            string reference = Text(item.Value, "ElementRef");
            if (Items(item.Target).Any(e => Text(e, "ElementRef") == reference)) throw new InvalidDataException("Native scenario destination already contains this parameter.");
            item.Target.Add(item.Value); progress("native_scenario_parameter_migrated:" + reference);
        }
        foreach (string id in discardResults)
        {
            foreach (object scenario in Items(Get(diagrams[id], "BPSimData"), "Scenarios")) Set(scenario, "SimulationResult", "");
            progress("native_scenario_results_retired:" + id);
        }
    }

    private static object CopySimulationValue(object value)
    {
        // Use the installed native type's XML contract, not a handwritten DTO.
        // The whole-document gate will independently verify every copied field.
        var serializer = new XmlSerializer(value.GetType());
        serializer.UnknownAttribute += (_, e) => throw new InvalidDataException("Unknown native dependency attribute: " + e.Attr.Name);
        serializer.UnknownElement += (_, e) => throw new InvalidDataException("Unknown native dependency element: " + e.Element.Name);
        using var bytes = new MemoryStream(); serializer.Serialize(bytes, value); bytes.Position = 0;
        using var reader = XmlReader.Create(bytes, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
        return serializer.Deserialize(reader) ?? throw new InvalidDataException("Native dependency copy is absent.");
    }
}
