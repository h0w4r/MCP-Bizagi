using System.Text.Json;

/// <summary>Create real native models without BPMN input, then edit and use the durable output through MCP.</summary>
internal static class NativeModelCreationAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        async Task<JsonElement> Operation(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = (await call(tool, args)).GetProperty("OperationId").GetString()!;
            var result = await wait(id, state); exited(id); return state == "completed" ? result.GetProperty("Result") : result;
        }
        var single = await Operation("native_model_create", new() { ["diagramNames"] = new[] { "Single native Ω" } });
        var multiple = await Operation("native_model_create", new() { ["diagramNames"] = new[] { "First diagram Ω", "Second 日本語" } });
        string path = multiple.GetProperty("outputArtifact").GetString()!, revision = multiple.GetProperty("outputRevision").GetString()!;
        var initial = multiple.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = multiple.GetProperty("reopened").GetProperty("DiagramState").GetProperty("OpenedItems")[0].GetProperty("DiagramId").GetString()!;
        var pool = initial.Single(e => e.GetProperty("Kind").GetString() == "Participant" && e.GetProperty("IsMainParticipant").ValueKind == JsonValueKind.False && e.GetProperty("DiagramId").GetString() == diagram);
        string parent = initial.Single(e => e.GetProperty("Kind").GetString() == "Process" && e.GetProperty("ParentId").GetString() == pool.GetProperty("Id").GetString()).GetProperty("Id").GetString()!;
        string start = Guid.NewGuid().ToString(), task = Guid.NewGuid().ToString(), end = Guid.NewGuid().ToString(), firstFlow = Guid.NewGuid().ToString(), secondFlow = Guid.NewGuid().ToString();
        object Node(string type, string id, string name, int x, int width, int height) => new { Operation = "create", ElementId = id, ParentId = parent, ElementType = type, Name = name,
            Geometry = new { X = x, Y = 100, Width = width, Height = height } };
        object Flow(string id, string from, string to, int x1, int y1, int x2, int y2) => new { Operation = "create", ElementId = id, ParentId = parent,
            ElementType = "SequenceFlow", SourceId = from, TargetId = to, Points = new[] { new { X = x1, Y = y1 }, new { X = x2, Y = y2 } } };
        var edited = await Operation("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new[] {
            Node("NoneStart", start, "Start", 100, 30, 30), Node("UserTask", task, "Native review Ω", 230, 100, 60), Node("NoneEnd", end, "End", 440, 30, 30),
            Flow(firstFlow, start, task, 130, 115, 230, 130), Flow(secondFlow, task, end, 330, 130, 440, 115) } });
        string editedPath = edited.GetProperty("outputArtifact").GetString()!;
        var rendered = await Operation("native_render_svg", new() { ["path"] = editedPath, ["diagramId"] = diagram });
        var simulation = await Operation("native_simulate", new() { ["path"] = editedPath, ["diagramId"] = diagram });
        string taskBpmnId = edited.GetProperty("reopened").GetProperty("Elements").EnumerateArray().Single(e => e.GetProperty("Id").GetString() == task).GetProperty("BpmnId").GetString()!;
        // Native simulation can add its own black-box task for an empty process. Match the actual
        // requested task by durable BPMN identity rather than assuming every Task metric is a user task.
        var tasks = simulation.GetProperty("result").GetProperty("SimulationReports")[0].GetProperty("Elements").EnumerateArray()
            .Where(e => e.GetProperty("Kind").GetString() == "Task" && e.GetProperty("Id").GetString() == taskBpmnId).ToArray();
        if (tasks.Length != 1 || !double.TryParse(tasks[0].GetProperty("Metrics").GetProperty("numberOfTokensCompleted").GetString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double count) || count != 1000)
            throw new InvalidDataException("New native model did not execute the added task in the real simulator.");
        var failed = await Operation("native_diagrams_apply", new() { ["path"] = editedPath, ["expectedRevision"] = edited.GetProperty("outputRevision").GetString(),
            ["patch"] = new { Changes = new[] { new { Operation = "delete", DiagramId = diagram } } } }, "failed");
        if (!failed.GetProperty("Error").GetString()!.Contains("Supply complete OpenedItems")) throw new InvalidDataException("New model failure path failed for the wrong reason.");
        var recovered = await Operation("native_inspect", new() { ["path"] = editedPath });
        if (recovered.GetProperty("sourceRevision").GetString() != edited.GetProperty("outputRevision").GetString()) throw new InvalidDataException("Rejected operation changed the new model.");
        var original = await Operation("native_inspect", new() { ["path"] = path });
        if (original.GetProperty("sourceRevision").GetString() != revision) throw new InvalidDataException("Subsequent editing modified the original blank artifact.");
        // Remove the final members too: absent/empty native collections must not obstruct legitimate lifecycle edits.
        var cleared = await Operation("native_mutate", new() { ["path"] = editedPath, ["expectedRevision"] = edited.GetProperty("outputRevision").GetString(),
            ["mutations"] = new[] { firstFlow, secondFlow, start, task, end }.Select(id => new { Operation = "delete", ElementId = id }).ToArray() });
        var comparison = await call("native_compare", new() { ["path"] = path, ["otherPath"] = cleared.GetProperty("outputArtifact").GetString() });
        if (!comparison.GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Cleared native model differs from its original blank state.");
        File.WriteAllText(Path.Combine(run, "model-creation-acceptance.json"), JsonSerializer.Serialize(new { single, multiple, edited, rendered, simulation, failed, recovered, original, cleared, comparison }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
