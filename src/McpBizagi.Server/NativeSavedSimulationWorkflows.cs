using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    private async Task<object> PersistSimulationResult(string id, byte[] original, string source, string diagramId,
        string scenarioId, EngineReply simulation, Action<string> progress, CancellationToken token)
    {
        string directory = CreateArtifactDirectory(id), output = Path.Combine(directory, "edited.bpm");
        string expectedPath = Path.GetFullPath(Path.Combine(directory, "results", "Results.xml"));
        string resultPath = simulation.Artifacts.Single(p => string.Equals(Path.GetFullPath(p), expectedPath, StringComparison.OrdinalIgnoreCase));
        var file = new FileInfo(resultPath);
        if (!file.Exists || file.Length > NativeSimulationResultPayload.MaximumBytes) throw new InvalidDataException("Current native result artifact is absent or too large.");
        byte[] bytes = await File.ReadAllBytesAsync(resultPath, token);
        var plan = NativeSavedSimulationPolicy.Prepare(original, diagramId, scenarioId, bytes);
        var write = new NativeSimulationResultWrite { DiagramId = diagramId, ScenarioId = scenarioId,
            ResultPath = resultPath, ResultFileSha256 = NativeSimulationResultPayload.Hash(bytes) };
        // A new editor loads the original source. The analyzer model may contain
        // transient simulation projections and must never become the saved copy.
        var edited = await Execute(new EngineRequest { OperationId = id, Action = "simulation_result_save", InputPath = source,
            OutputPath = output, SimulationResultWrite = write }, RunDirectory(id, "result-editor"), progress, token);
        var reopened = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = output },
            RunDirectory(id, "result-reader"), progress, token);
        if (!original.SequenceEqual(File.ReadAllBytes(source)) || NativeSimulationResultPayload.Hash(File.ReadAllBytes(resultPath)) != write.ResultFileSha256)
            throw new InvalidDataException("Captured source or actual result artifact changed during persistence.");
        byte[] saved = File.ReadAllBytes(output);
        progress("native_saved_result_fidelity");
        var fidelity = NativeSavedSimulationPolicy.Compare(original, saved, plan, edited, reopened);
        File.WriteAllText(Path.Combine(directory, "saved-simulation-result.json"), JsonSerializer.Serialize(new { write, plan,
            editorReadback = edited.SavedSimulationResults, reopenedReadback = reopened.SavedSimulationResults, fidelity }, new JsonSerializerOptions { WriteIndented = true }));
        if (!fidelity.Preserved) throw new InvalidDataException("Saving simulation results changed unrequested native content; original retained.");
        return new { diagramId, scenarioId, plan.PreviousResult, resultFileSha256 = write.ResultFileSha256,
            edited, reopened, fidelity, outputArtifact = "artifact:" + id + ":edited.bpm",
            outputRevision = BpmnDocument.Revision(saved), nativeSourceUnmodified = true };
    }
}
