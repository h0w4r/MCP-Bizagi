using System.Text.Json;

/// <summary>Real diagram lifecycle through MCP, not an adapter or fabricated native-file test.</summary>
internal static class NativeDiagramAcceptance
{
    public static async Task Run(string repo, string run, string? input, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, bool configuredSimulation = false)
    {
        async Task<JsonElement> Operation(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = (await call(tool, args)).GetProperty("OperationId").GetString()!;
            var result = await wait(id, state); exited(id); return state == "completed" ? result.GetProperty("Result") : result;
        }
        var rejected = new List<JsonElement>();
        async Task ExpectFailure(Dictionary<string, object?> args, string reason)
        {
            var failure = await Operation("native_diagrams_apply", args, "failed");
            if (!(failure.GetProperty("Error").GetString() ?? "").Contains(reason, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Lifecycle negative case failed for an unrelated reason: " + failure);
            rejected.Add(failure);
        }
        string path;
        if (input == null)
        {
            File.Copy(Path.Combine(repo, "examples", "collaboration-nested.bpmn"), Path.Combine(run, "diagram source.bpmn"));
            path = (await Operation("native_roundtrip", new() { ["path"] = "diagram source.bpmn", ["modelName"] = "Diagram lifecycle" })).GetProperty("nativeArtifact").GetString()!;
        }
        else { path = Path.Combine(run, "diagram source.bpm"); File.Copy(input, path); }
        var baseline = await Operation("native_diagrams_get", new() { ["path"] = path });
        string revision = baseline.GetProperty("sourceRevision").GetString()!;
        string original = baseline.GetProperty("result").GetProperty("DiagramState").GetProperty("Diagrams")[0].GetProperty("Id").GetString()!;
        async Task<JsonElement> Apply(object patch)
        {
            var result = await Operation("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = patch });
            if (!result.GetProperty("fidelity").GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Diagram fidelity failed.");
            path = result.GetProperty("outputArtifact").GetString()!; revision = result.GetProperty("outputRevision").GetString()!;
            return result;
        }
        string createdId = Guid.NewGuid().ToString();
        var created = await Apply(new { Changes = new[] { new { Operation = "create", DiagramId = createdId, Name = "New diagram Ω" } } });
        var ordered = await Apply(new { OpenedItems = new[] { new { DiagramId = createdId, IsSelected = true }, new { DiagramId = original, IsSelected = false } } });
        var renamed = await Apply(new { Changes = new[] { new { Operation = "rename", DiagramId = createdId, Name = "Renamed diagram Ω" } } });
        await ExpectFailure(new() { ["path"] = path, ["expectedRevision"] = revision,
            ["patch"] = new { Changes = new[] { new { Operation = "create", DiagramId = Guid.NewGuid().ToString(), Name = "Renamed diagram Ω" } } } }, "Diagram names must be unique");
        await ExpectFailure(new() { ["path"] = path, ["expectedRevision"] = revision,
            ["patch"] = new { Changes = new[] { new { Operation = "delete", DiagramId = createdId } } } }, "Supply complete OpenedItems");
        var cloned = await Apply(new { Changes = new[] { new { Operation = "clone", DiagramId = original, Name = "Copied diagram 日本語" } } });
        string cloneId = cloned.GetProperty("edited").GetProperty("DiagramClones")[0].GetProperty("TargetId").GetString()!;
        var clonedElements = cloned.GetProperty("reopened").GetProperty("Elements").EnumerateArray().Where(e => e.GetProperty("DiagramId").GetString() == cloneId).ToArray();
        var inner = clonedElements.FirstOrDefault(e => e.GetProperty("Kind").GetString() == "SubProcess");
        var tabs = new List<object> { new { DiagramId = cloneId, IsSelected = true }, new { DiagramId = original, IsSelected = false } };
        if (inner.ValueKind != JsonValueKind.Undefined) tabs.Add(new { DiagramId = cloneId, SubProcessId = inner.GetProperty("Id").GetString(), IsSelected = false });
        var retabbed = await Apply(new { OpenedItems = tabs });
        var noop = await Operation("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        path = noop.GetProperty("outputArtifact").GetString()!; revision = noop.GetProperty("outputRevision").GetString()!;
        var rendered = await Operation("native_render_svg", new() { ["path"] = path, ["diagramId"] = cloneId });
        var simulations = new List<JsonElement>();
        if (configuredSimulation)
        {
            // This optional corpus is the durable output of NativeMetadataAcceptance, not invented engine data.
            foreach (var scenario in new[] { (Id: "Scenario_resources", Level: 3), (Id: "Scenario_calendar", Level: 4) })
            {
                var simulation = await Operation("native_simulate", new() { ["path"] = path, ["diagramId"] = cloneId, ["scenarioId"] = scenario.Id, ["simulationLevel"] = scenario.Level });
                var metrics = simulation.GetProperty("result").GetProperty("SimulationReports")[0].GetProperty("Elements").EnumerateArray()
                    .Single(e => e.GetProperty("Kind").GetString() == "Task").GetProperty("Metrics");
                double Metric(string name) => double.Parse(metrics.GetProperty(name).GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                if (Metric("numberOfTokensCompleted") != 12 || Metric("averageTimeBusy") != 3 || Metric("totalCompletionCost") != 84 ||
                    Metric("maximumNumberOfResourcesUsed") != 1 || Metric("averageTimeWaitingForResource") <= 0 || scenario.Level == 4 && Metric("averageTimeWaitingForResource") < 450)
                    throw new InvalidDataException("Cloned configured simulation lost quantitative resource/calendar behavior.");
                simulations.Add(simulation);
            }
        }
        var deleted = await Apply(new { Changes = new[] { new { Operation = "delete", DiagramId = cloneId }, new { Operation = "delete", DiagramId = createdId } },
            OpenedItems = Array.Empty<object>() });
        if (deleted.GetProperty("reopened").GetProperty("DiagramState").GetProperty("Diagrams").GetArrayLength() != baseline.GetProperty("result").GetProperty("DiagramState").GetProperty("Diagrams").GetArrayLength())
            throw new InvalidDataException("Diagram deletion changed the original diagram count.");
        var remaining = deleted.GetProperty("reopened").GetProperty("DiagramState").GetProperty("Diagrams").EnumerateArray().Select(d =>
            new { Operation = "delete", DiagramId = d.GetProperty("Id").GetString() }).ToArray();
        await ExpectFailure(new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new { Changes = remaining, OpenedItems = Array.Empty<object>() } }, "final diagram");
        var recovered = await Operation("native_diagrams_get", new() { ["path"] = path });
        if (recovered.GetProperty("sourceRevision").GetString() != revision) throw new InvalidDataException("Rejected lifecycle write changed the valid output.");
        File.WriteAllText(Path.Combine(run, "diagram-acceptance.json"), JsonSerializer.Serialize(new { path, revision, baseline, created, ordered, renamed, cloned, retabbed, noop, rendered, simulations, deleted, rejected, recovered }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
