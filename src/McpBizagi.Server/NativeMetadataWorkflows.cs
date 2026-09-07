using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    public OperationView ReadMetadata(string path)
    {
        var input = ReadNative(path);
        return operations.Start("native_metadata_get", async (id, progress, token) =>
        {
            string source = Path.Combine(CreateArtifactDirectory(id), "input.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            var result = await Execute(new EngineRequest { OperationId = id, Action = "metadata_read", InputPath = source }, RunDirectory(id, "reader"), progress, token);
            return new { sourceRevision = input.Revision, result, nativeSourceUnmodified = true };
        });
    }

    public OperationView ApplyMetadata(string path, string expectedRevision, NativeMetadataPatch patch)
    {
        NativeMetadataPolicy.Validate(patch);
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Revision conflict: native file changed since inspection.");
        return operations.Start("native_metadata_apply", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            File.WriteAllText(Path.Combine(directory, "metadata-request.json"), JsonSerializer.Serialize(patch));
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "metadata_save", InputPath = source, OutputPath = output, MetadataPatch = patch },
                RunDirectory(id, "editor"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "metadata_read", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            File.WriteAllText(Path.Combine(directory, "metadata-readback.json"), JsonSerializer.Serialize(new { edited = edited.Metadata, reopened = reopened.Metadata }));
            progress("native_metadata_fidelity");
            var fidelity = NativeMetadataPolicy.Compare(input.Bytes, File.ReadAllBytes(output), patch, reopened.Metadata ?? throw new InvalidDataException("Missing native metadata readback."));
            File.WriteAllText(Path.Combine(directory, "native-fidelity.json"), JsonSerializer.Serialize(fidelity, new JsonSerializerOptions { WriteIndented = true }));
            if (!fidelity.Preserved) throw new InvalidDataException("Native metadata fidelity rejected unexplained changes; original untouched. Inspect " + directory);
            return new
            {
                edited,
                reopened,
                fidelity,
                nativeSourceUnmodified = true,
                requestedChangesVerified = patch.Resources.Length + patch.Simulations.Length + patch.Assignments.Length,
                outputArtifact = "artifact:" + id + ":edited.bpm",
                outputRevision = BpmnDocument.Revision(File.ReadAllBytes(output))
            };
        });
    }

    public OperationView RunWhatIf(string path, string diagramId, string[] scenarioIds, int simulationLevel)
    {
        NativeMetadataPolicy.RequireId(diagramId);
        if (scenarioIds == null || scenarioIds.Length is < 1 or > 1000 || scenarioIds.Any(string.IsNullOrWhiteSpace) || scenarioIds.Distinct().Count() != scenarioIds.Length || simulationLevel is < 1 or > 4)
            throw new InvalidDataException("Supply 1-1000 distinct explicit scenario IDs and simulation level 1-4.");
        var input = ReadNative(path);
        return operations.Start("native_simulate_what_if", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            var result = await Execute(new EngineRequest
            {
                OperationId = id,
                Action = "what_if",
                InputPath = source,
                OutputPath = Path.Combine(directory, "results"),
                DiagramId = diagramId,
                ScenarioIds = scenarioIds,
                SimulationLevel = simulationLevel
            }, RunDirectory(id, "analyzer"), progress, token);
            if (!input.Bytes.SequenceEqual(File.ReadAllBytes(source))) throw new InvalidDataException("What-if changed its input snapshot.");
            return new
            {
                sourceRevision = input.Revision,
                result,
                nativeSourceUnmodified = true,
                warning = "Actual installed-engine replication artifacts. Results are not saved into the source model; level-specific engine filtering applies. Inspect result.SimulationLimitations for input-specific native semantics; an empty list is not a complete semantic-support assessment."
            };
        });
    }
}
