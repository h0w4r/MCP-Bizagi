using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    public OperationView Reparent(string path, string expectedRevision, NativeReparenting[] moves, NativeSimulationMigration? simulationMigration = null, bool migratePresentationActions = false)
    {
        NativeReparentingPolicy.Validate(moves);
        NativeSimulationMigrationPolicy.Validate(simulationMigration);
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Native reparenting source revision conflict.");
        var captured = JsonSerializer.Deserialize<NativeReparenting[]>(JsonSerializer.Serialize(moves))!;
        var migration = simulationMigration == null ? null : JsonSerializer.Deserialize<NativeSimulationMigration>(JsonSerializer.Serialize(simulationMigration));
        return operations.Start("native_elements_reparent", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            File.WriteAllText(Path.Combine(directory, "reparenting-request.json"), JsonSerializer.Serialize(captured));
            File.WriteAllText(Path.Combine(directory, "reparenting-request-options.json"), JsonSerializer.Serialize(new { moves = captured, simulationMigration = migration, migratePresentationActions }));
            var before = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = source }, RunDirectory(id, "source-reader"), progress, token);
            var simulation = NativeReparentingPolicy.Preflight(input.Bytes, before, captured, migration, migratePresentationActions);
            var presentation = migratePresentationActions ? NativePresentationMigrationPolicy.Prepare(input.Bytes, before, NativeReparentingPolicy.Expected(before.Elements, captured)) : null;
            File.WriteAllText(Path.Combine(directory, "presentation-migration-plan.json"), JsonSerializer.Serialize(new { migratePresentationActions, presentation?.Transfers, presentation?.ExpectedActions, presentation?.ExpectedFiles }));
            File.WriteAllText(Path.Combine(directory, "simulation-migration-plan.json"), JsonSerializer.Serialize(simulation));
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "reparent_save", InputPath = source, OutputPath = output, Reparentings = captured, PresentationTransfers = presentation?.Transfers ?? [],
                ScenarioTransfers = simulation.Transfers, DiscardScenarioResultDiagrams = simulation.DiscardResultDiagrams },
                RunDirectory(id, "editor"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            var graphDifferences = XpdlDocument.CompareGraph(edited.Elements, reopened.Elements);
            var metadataDifferences = XpdlDocument.CompareMetadata(edited, reopened);
            File.WriteAllText(Path.Combine(directory, "reparenting-readback.json"), JsonSerializer.Serialize(new { before, edited, reopened, graphDifferences, metadataDifferences }));
            // A separate native reader, not editor return values, establishes durable ownership.
            NativeImagePolicy.VerifyRestart(File.ReadAllBytes(output), edited, reopened);
            if (graphDifferences.Length != 0 || metadataDifferences.Length != 0) throw new InvalidDataException("Reparented content changed after native restart; inspect reparenting-readback.json.");
            progress("native_reparenting_fidelity");
            NativeFidelityReport fidelity;
            try { fidelity = NativeReparentingPolicy.Compare(input.Bytes, File.ReadAllBytes(output), before.Elements, reopened.Elements, captured, before, edited, reopened, simulation, presentation); }
            finally { File.WriteAllText(Path.Combine(directory, "presentation-migration-verification.json"), JsonSerializer.Serialize(new { presentation?.Transfers, presentation?.Verification })); }
            File.WriteAllText(Path.Combine(directory, "reparenting-fidelity.json"), JsonSerializer.Serialize(fidelity, new JsonSerializerOptions { WriteIndented = true }));
            if (!fidelity.Preserved) throw new InvalidDataException("Reparenting changed unrequested native content; the original is retained.");
            return new { before, edited, reopened, fidelity, simulationMigration = simulation, presentationMigration = new { presentation?.Transfers, presentation?.Verification }, nativeSourceUnmodified = true, requestedMovesVerified = captured.Length,
                interpretationWarning = "Explicit containment changes are not automatic layout or behavioral/GUI equivalence. Node coordinates are retained unless Position is supplied; subtree coordinates and connector paths are never guessed. Configured parameter migration requires explicit scenario correspondence and matching context. Resource/calendar execution is verified only on the documented corpus; inherited-scenario execution remains unaccredited. Saved-result retirement requires explicit consent. Presentation-action migration is explicit and preserves caches; it does not activate content or accredit playback.",
                outputArtifact = "artifact:" + id + ":edited.bpm", outputRevision = BpmnDocument.Revision(File.ReadAllBytes(output)) };
        });
    }
}
