using System.Text.Json;

/// <summary>Real native palette, boundary lifecycle, clone, rendering and failure recovery through typed MCP.</summary>
internal static class NativeEventAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = (await call(tool, args)).GetProperty("OperationId").GetString()!;
            var response = await wait(id, state); exited(id); receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-events.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Event palette Ω", "Other container" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = graph.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Name") == "Event palette Ω").GetProperty("Id").GetString()!;
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string parent = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string otherParent = S(graph.First(e => S(e, "Kind") == "Process" && S(e, "DiagramId") != diagram), "Id");
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        async Task<JsonElement> Mutate(object[] mutations, string state = "completed")
        {
            var result = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = mutations }, state);
            if (state == "completed") { path = S(result, "outputArtifact"); revision = S(result, "outputRevision"); graph = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray(); }
            return result;
        }
        string Id() => Guid.NewGuid().ToString();
        string task = Id(), replacement = Id(), foreign = Id(), subprocess = Id(), nestedTask = Id(), nestedBoundary = Id();
        var nodes = new List<object>(); int index = 0;
        object Node(string owner, string id, string type, string? mode = null, object? properties = null)
        {
            int n = index++;
            return new { Operation = "create", ParentId = owner, ElementId = id, ElementType = type, EventMode = mode, EventProperties = properties,
                Name = type + " " + mode + " Ω", Documentation = "Native event corpus 日本語", Geometry = new { X = 70 + n % 8 * 145, Y = 70 + n / 8 * 115, Width = type.EndsWith("Task") ? 110 : 40, Height = 40 } };
        }
        nodes.AddRange([Node(parent, task, "UserTask"), Node(parent, replacement, "UserTask"), Node(otherParent, foreign, "UserTask"), Node(parent, subprocess, "SubProcess"), Node(subprocess, nestedTask, "UserTask")]);
        foreach (string type in new[] { "NoneStart", "MessageStart", "TimerStart", "ConditionalStart", "SignalStart", "MultipleStart", "ParallelMultipleStart",
            "NoneEnd", "MessageEnd", "TerminateEnd", "EscalationEnd", "ErrorEnd", "CompensationEnd", "SignalEnd", "MultipleEnd", "EventBasedGatewayExclusive", "EventBasedGatewayParallel" }) nodes.Add(Node(parent, Id(), type));
        foreach (string type in new[] { "MessageIntermediate", "TimerIntermediate", "ConditionalIntermediate", "LinkIntermediate", "SignalIntermediate", "MultipleIntermediate", "ParallelMultipleIntermediate" }) nodes.Add(Node(parent, Id(), type, "Catch"));
        foreach (string type in new[] { "NoneIntermediate", "MessageIntermediate", "EscalationIntermediate", "LinkIntermediate", "CompensationIntermediate", "SignalIntermediate", "MultipleIntermediate" }) nodes.Add(Node(parent, Id(), type, "Throw"));
        var boundaryIds = new List<string>();
        foreach (string type in new[] { "MessageIntermediate", "TimerIntermediate", "EscalationIntermediate", "ConditionalIntermediate", "ErrorIntermediate", "CompensationIntermediate", "SignalIntermediate", "MultipleIntermediate", "ParallelMultipleIntermediate" })
        {
            string id = Id(); boundaryIds.Add(id); nodes.Add(Node(parent, id, type, "Boundary", new { AttachedToActivityId = task, IsInterrupting = true }));
        }
        nodes.Add(Node(subprocess, nestedBoundary, "TimerIntermediate", "Boundary", new { AttachedToActivityId = nestedTask, IsInterrupting = false }));
        await Mutate(nodes.ToArray());
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var configurable = boundaryIds.Where((_, i) => i != 4 && i != 5).ToArray();
        string messageStart = S(graph.Single(e => S(e, "DiagramId") == diagram && S(e, "ElementType") == "MessageStart"), "Id");
        // Exercise each interruption-capable trigger, not only the timer example.
        await Mutate(configurable.Select(id => (object)new { Operation = "update", ElementId = id, EventProperties = new { IsInterrupting = false } })
            .Concat([new { Operation = "update", ElementId = messageStart, EventProperties = new { IsInterrupting = true } }]).ToArray());
        string timer = boundaryIds[1];
        await Mutate([new { Operation = "update", ElementId = timer, EventProperties = new { AttachedToActivityId = replacement, IsInterrupting = false } }]);
        await Mutate([new { Operation = "update", ElementId = timer, Name = "Boundary rename retains references Ω" }]);
        foreach (var attempt in new[] {
            (Changes: new object[] { new { Operation = "delete", ElementId = task } }, Error: "boundary events"),
            (Changes: new object[] { new { Operation = "update", ElementId = timer, EventProperties = new { AttachedToActivityId = foreign } } }, Error: "same flow container"),
            (Changes: new object[] { new { Operation = "update", ElementId = boundaryIds[4], EventProperties = new { IsInterrupting = false } } }, Error: "noninterrupting"),
            (Changes: new object[] { new { Operation = "update", ElementId = task, EventProperties = new { IsInterrupting = false } } }, Error: "start or boundary"),
            (Changes: new object[] { new { Operation = "update", ElementId = messageStart, EventProperties = new { IsInterrupting = false } } }, Error: "event-triggered subprocess"),
            (Changes: new object[] { new { Operation = "create", ParentId = parent, ElementId = Id(), ElementType = "SequenceFlow", SourceId = task, TargetId = timer, Points = new[] { new { X = 100, Y = 100 }, new { X = 200, Y = 100 } } } }, Error: "flow direction") })
        {
            var failure = await Mutate(attempt.Changes, "failed");
            if (!S(failure, "Error").Contains(attempt.Error)) throw new InvalidDataException("Event failure occurred for an unrelated reason: " + S(failure, "Error"));
        }
        await Mutate([new { Operation = "update", ElementId = timer, EventProperties = new { AttachedToActivityId = task, IsInterrupting = true } }]);
        var beforeClone = graph.Where(e => S(e, "DiagramId") == diagram && e.GetProperty("Event").ValueKind == JsonValueKind.Object).ToArray();
        var clone = await Op("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["patch"] = new { Changes = new[] { new { Operation = "clone", DiagramId = diagram, Name = "Native event clone" } } } });
        path = S(clone, "outputArtifact"); revision = S(clone, "outputRevision"); graph = clone.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        var map = clone.GetProperty("edited").GetProperty("DiagramClones")[0].GetProperty("Identities").EnumerateArray().ToDictionary(e => S(e, "SourceId"), e => S(e, "TargetId"));
        foreach (var original in beforeClone)
        {
            var expected = original.GetProperty("Event"); var actual = graph.Single(e => S(e, "Id") == map[S(original, "Id")]).GetProperty("Event");
            if (S(expected, "Mode") != S(actual, "Mode") || !JsonElement.DeepEquals(expected.GetProperty("DefinitionKinds"), actual.GetProperty("DefinitionKinds")) ||
                !JsonElement.DeepEquals(expected.GetProperty("IsInterrupting"), actual.GetProperty("IsInterrupting")) ||
                S(expected, "AttachedToActivityId") is var target && target != "" && S(actual, "AttachedToActivityId") != map[target])
                throw new InvalidDataException("Native clone lost an event mode, definition kind, interruption flag or remapped boundary attachment.");
        }
        await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram });
        await Op("native_publish", new() { ["path"] = path, ["format"] = "word", ["title"] = "Native events and boundaries" });
        await Mutate(boundaryIds.Select(id => (object)new { Operation = "delete", ElementId = id }).Concat([new { Operation = "delete", ElementId = task }, new { Operation = "delete", ElementId = nestedBoundary }, new { Operation = "delete", ElementId = nestedTask }, new { Operation = "delete", ElementId = subprocess }]).ToArray());
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var inspected = await Op("native_inspect", new() { ["path"] = path });
        if (S(inspected, "sourceRevision") != revision) throw new InvalidDataException("Event inspection changed durable source bytes.");
    }
    private static string S(JsonElement element, string key) => element.GetProperty(key).GetString()!;
}
