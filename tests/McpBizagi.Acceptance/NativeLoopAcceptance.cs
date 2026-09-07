using System.Text.Json;

/// <summary>Real typed MCP loop mutation, durable restart, diagnostics and failure recovery corpus.</summary>
internal static class NativeLoopAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = (await call(tool, args)).GetProperty("OperationId").GetString()!;
            var response = await wait(id, state); exited(id); receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-loops.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Native loop palette Ω", "Loop diagnostics" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string Diagram(string name) => graph.Single(e => e.GetProperty("Kind").GetString() == "Collaboration" && e.GetProperty("Name").GetString() == name).GetProperty("Id").GetString()!;
        string Process(string diagram)
        {
            string pool = graph.Single(e => e.GetProperty("Kind").GetString() == "Participant" && e.GetProperty("DiagramId").GetString() == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()).GetProperty("Id").GetString()!;
            return graph.Single(e => e.GetProperty("Kind").GetString() == "Process" && e.GetProperty("ParentId").GetString() == pool).GetProperty("Id").GetString()!;
        }
        string palette = Diagram("Native loop palette Ω"), diagnostics = Diagram("Loop diagnostics"), parent = Process(palette), simParent = Process(diagnostics);
        string path = created.GetProperty("outputArtifact").GetString()!, revision = created.GetProperty("outputRevision").GetString()!;
        async Task<JsonElement> Mutate(object[] mutations, string state = "completed")
        {
            var result = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = mutations }, state);
            if (state == "completed") { path = result.GetProperty("outputArtifact").GetString()!; revision = result.GetProperty("outputRevision").GetString()!; graph = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray(); }
            return result;
        }
        async Task ClonePalette(string name)
        {
            var prior = graph.Where(e => e.GetProperty("DiagramId").GetString() == palette && e.GetProperty("ActivityLoop").ValueKind == JsonValueKind.Object)
                .ToDictionary(e => e.GetProperty("Id").GetString()!, e => e.GetProperty("ActivityLoop"));
            var result = await Op("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision,
                ["patch"] = new { Changes = new[] { new { Operation = "clone", DiagramId = palette, Name = name } } } });
            path = result.GetProperty("outputArtifact").GetString()!; revision = result.GetProperty("outputRevision").GetString()!;
            graph = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
            var map = result.GetProperty("edited").GetProperty("DiagramClones")[0].GetProperty("Identities").EnumerateArray()
                .ToDictionary(e => e.GetProperty("SourceId").GetString()!, e => e.GetProperty("TargetId").GetString()!);
            foreach (var pair in prior)
            {
                string target = map[pair.Key];
                var actual = graph.Single(e => e.GetProperty("Id").GetString() == target).GetProperty("ActivityLoop");
                if (!JsonElement.DeepEquals(pair.Value, actual)) throw new InvalidDataException("Native cloning changed a loop configuration.");
            }
        }
        string Id() => Guid.NewGuid().ToString();
        string[] types = ["AbstractTask", "UserTask", "ManualTask", "ServiceTask", "ScriptTask", "SendTask", "ReceiveTask", "BusinessRuleTask", "SubProcess", "CallActivity"];
        string[] ids = types.Select(_ => Id()).ToArray(); string start = Id(), task = Id(), end = Id();
        object Standard(int index) => new { Kind = "Standard", Standard = new { Maximum = index + 3, Counter = index, TestBefore = index % 2 == 0, Condition = "attempt < 3 & region == \"日本語 Ω\"" } };
        object Node(string owner, string id, string type, string name, int x, int y, object? loop = null) => new { Operation = "create", ParentId = owner, ElementId = id, ElementType = type,
            Name = name, Documentation = name + " documentation", Geometry = new { X = x, Y = y, Width = 110, Height = 60 }, ActivityLoop = loop };
        var nodes = types.Select((type, i) => Node(parent, ids[i], type, "Loop " + type, 70 + i % 5 * 155, 70 + i / 5 * 140, Standard(i))).ToList();
        nodes.AddRange([Node(simParent, start, "NoneStart", "Loop start", 80, 100), Node(simParent, task, "UserTask", "Loop task", 260, 100), Node(simParent, end, "NoneEnd", "Loop end", 470, 100)]);
        foreach (var pair in new[] { (start, task), (task, end) }) nodes.Add(new { Operation = "create", ParentId = simParent, ElementId = Id(), ElementType = "SequenceFlow", SourceId = pair.Item1, TargetId = pair.Item2,
            Points = new[] { new { X = 190, Y = 130 }, new { X = 260, Y = 130 } } });
        await Mutate(nodes.ToArray());
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        await ClonePalette("Standard-loop native clone");
        await Mutate([new { Operation = "update", ElementId = ids[0], Name = "Loop name preserves native configuration" }]);
        var failure = await Mutate([new { Operation = "update", ElementId = start, ActivityLoop = Standard(0) }], "failed");
        if (!failure.GetProperty("Error").GetString()!.Contains("requires a native activity")) throw new InvalidDataException("Wrong-kind loop rejected for an unrelated reason.");
        string[] behaviors = ["All", "One", "None", "Complex"];
        await Mutate(ids.Select((id, i) => (object)new { Operation = "update", ElementId = id, ActivityLoop = new { Kind = "MultiInstance", MultiInstance = new { IsSequential = i % 2 == 0,
            Counter = i + 1, Behavior = behaviors[i % 4], CompletionCondition = "complete > 2 & Ω", ComplexCondition = i % 4 == 3 ? "complex < 5 & 日本語" : null } } }).ToArray());
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        await ClonePalette("Multi-instance native clone");
        await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = palette });
        await Op("native_publish", new() { ["path"] = path, ["format"] = "word", ["title"] = "Native loop documentation" });
        // Valid single-task diagram: report native results without claiming expression/iteration execution.
        foreach (var item in new[] {
            (Loop: (object)new { Kind = "Standard", Standard = new { Maximum = 3, Counter = 0, TestBefore = false } }, Code: "standard_loop_simulation_unaccredited"),
            (Loop: (object)new { Kind = "MultiInstance", MultiInstance = new { IsSequential = true, Counter = 3, Behavior = "All" } }, Code: "multi_instance_simulation_unsupported") })
        {
            await Mutate([new { Operation = "update", ElementId = task, ActivityLoop = item.Loop }]);
            var simulation = await Op("native_simulate", new() { ["path"] = path, ["diagramId"] = diagnostics });
            var warning = simulation.GetProperty("result").GetProperty("SimulationLimitations").EnumerateArray().Where(e => e.GetProperty("Code").GetString() == item.Code).ToArray();
            if (warning.Length != 1 || warning[0].GetProperty("ElementId").GetString() != task || warning[0].GetProperty("ActivityLoop").ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Native loop simulation limitations were hidden or lost their actual activity identity.");
        }
        await Mutate(ids.Append(task).Select(id => (object)new { Operation = "update", ElementId = id, ActivityLoop = new { Kind = "None" } }).ToArray());
        await Mutate([new { Operation = "update", ElementId = ids[0], ActivityLoop = new { Kind = "Standard", Standard = new { Maximum = 0, Counter = 0, Condition = (string?)null } } }]);
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var inspected = await Op("native_inspect", new() { ["path"] = path });
        if (inspected.GetProperty("sourceRevision").GetString() != revision) throw new InvalidDataException("Native loop analysis changed the durable model.");
    }
}
