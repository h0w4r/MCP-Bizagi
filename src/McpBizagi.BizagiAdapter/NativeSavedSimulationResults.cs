using System.Text;
using System.Xml;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private NativeSavedSimulationResult[] SavedSimulationResults(object model) => Items(model, "Diagrams")
        .SelectMany(diagram => Items(Get(diagram, "BPSimData"), "Scenarios")
            .Where(s => !string.IsNullOrEmpty(Text(s, "SimulationResult")))
            .Select(s => NativeSimulationResultPayload.Describe(Text(diagram, "Id"), Text(s, "Id"), Text(s, "SimulationResult"))))
        .ToArray();

    private void SaveSimulationResult(object model, NativeSimulationResultWrite request, Action<string> progress)
    {
        var file = new FileInfo(request.ResultPath);
        if (!file.Exists || file.Length == 0 || file.Length > NativeSimulationResultPayload.MaximumBytes)
            throw new InvalidDataException("Current-operation simulation result artifact is missing or too large.");
        byte[] bytes = File.ReadAllBytes(request.ResultPath);
        if (NativeSimulationResultPayload.Hash(bytes) != request.ResultFileSha256)
            throw new InvalidDataException("Simulation result artifact changed before native persistence.");
        string xml = NativeSimulationResultPayload.Read(bytes);
        object diagram = Items(model, "Diagrams").Single(d => Text(d, "Id") == request.DiagramId);
        object scenario = Items(Get(diagram, "BPSimData"), "Scenarios").Single(s => Text(s, "Id") == request.ScenarioId);
        // Match the native SimulationManager completion setter. This model was
        // freshly loaded from the original, not mutated by the simulator pipeline.
        Set(scenario, "SimulationResult", xml);
        progress("native_saved_simulation_result:" + request.ScenarioId);
    }

    private string[] ReadSavedSimulationResult(object model, EngineRequest request, Action<string> progress)
    {
        object diagram = Items(model, "Diagrams").Single(d => Text(d, "Id") == request.DiagramId);
        object scenario = Items(Get(diagram, "BPSimData"), "Scenarios").Single(s => Text(s, "Id") == request.ScenarioId);
        string xml = Text(scenario, "SimulationResult");
        if (string.IsNullOrEmpty(xml)) throw new InvalidDataException("The selected native scenario has no saved simulation result.");
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = NativeSimulationResultPayload.MaximumBytes });
        var document = new XmlDocument { XmlResolver = null }; document.Load(reader);
        if (document.DocumentElement?.Name != "model" || document.DocumentElement.NamespaceURI != "")
            throw new InvalidDataException("Unknown saved native simulation result schema.");
        Directory.CreateDirectory(request.OutputPath);
        string output = Path.Combine(request.OutputPath, "Results.xml");
        // The native property is a Unicode string. Export with a matching UTF-8
        // declaration, rather than writing UTF-8 bytes under a stale Latin-1 label.
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false) })) document.Save(writer);
        progress("native_saved_simulation_result_read:" + request.ScenarioId);
        return new[] { output };
    }
}
