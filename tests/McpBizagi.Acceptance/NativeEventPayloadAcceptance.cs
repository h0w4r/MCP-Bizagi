using System.Text.Json;

/// <summary>Real definition payload lifecycle and reference preservation through the official MCP SDK client.</summary>
internal static class NativeEventPayloadAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = S(await call(tool, args), "OperationId"); var response = await wait(id, state); exited(id); receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-event-payloads.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Event payloads Ω", "Other scope" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(graph.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Name") == "Event payloads Ω"), "Id");
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string parent = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string foreignParent = S(graph.First(e => S(e, "Kind") == "Process" && S(e, "DiagramId") != diagram), "Id");
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        async Task<JsonElement> Mutate(object[] mutations, string state = "completed")
        {
            var result = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = mutations }, state);
            if (state == "completed") { path = S(result, "outputArtifact"); revision = S(result, "outputRevision"); graph = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray(); }
            return result;
        }
        string Id() => Guid.NewGuid().ToString();
        string task = Id(), replacement = Id(), foreign = Id(), sub = Id(), nestedTask = Id(), nestedEvent = Id();
        string name = "Payload 日本語 Ω", condition = "amount > 2 & region == \"日本語\"";
        string errorCode = "E_日本語_Ω", escalationCode = "ESC_Ω";
        var ids = new List<(string Id, string Kind)>(); int index = 0;
        object[] Payload(string kind, bool clear = false) => kind switch {
            "Timer" => [new { Kind = kind, Timer = new { Kind = clear ? "None" : "Cycle", Text = clear ? "" : "R3/PT5M" } }],
            "Conditional" => [new { Kind = kind, Name = clear ? "" : name, Condition = clear ? "" : condition }],
            "Error" => [new { Kind = kind, ErrorCode = clear ? "" : errorCode }],
            "Escalation" => [new { Kind = kind, EscalationCode = clear ? "" : escalationCode }],
            "Compensation" => [new { Kind = kind, Compensation = new { ActivityId = clear ? "" : task, WaitForCompletion = !clear } }],
            "StartMultiple" => Payload("Message", clear).Concat(Payload("Timer", clear)).Concat(Payload("Conditional", clear)).Concat(Payload("Signal", clear)).ToArray(),
            "BoundaryMultiple" => Payload("Message", clear).Concat(Payload("Timer", clear)).Concat(Payload("Conditional", clear)).Concat(Payload("Compensation", clear)).ToArray(),
            "Multiple" => Payload("Message", clear).Concat(Payload("Error", clear)).Concat(Payload("Signal", clear)).Concat(Payload("Compensation", clear)).ToArray(),
            _ => [new { Kind = kind, Name = clear ? "" : name }] };
        object Node(string owner, string id, string type, string? mode = null, object? evt = null, object[]? payload = null, object? subprocess = null)
        {
            int n = index++;
            return new { Operation = "create", ParentId = owner, ElementId = id, ElementType = type, EventMode = mode, EventProperties = evt, EventPayloads = payload,
                SubProcessProperties = subprocess, Name = "Event " + type + " " + n, Geometry = new { X = 70 + n % 7 * 155, Y = 70 + n / 7 * 115, Width = 90, Height = 60 } };
        }
        var nodes = new List<object> { Node(parent, task, "UserTask"), Node(parent, replacement, "ManualTask"), Node(foreignParent, foreign, "UserTask"), Node(parent, sub, "SubProcess"), Node(sub, nestedTask, "UserTask") };
        foreach (var item in new[] { ("MessageStart", "Message", ""), ("MessageIntermediate", "Message", "Catch"), ("MessageIntermediate", "Message", "Throw"), ("MessageEnd", "Message", ""),
            ("TimerStart", "Timer", ""), ("TimerIntermediate", "Timer", "Catch"), ("ConditionalStart", "Conditional", ""), ("ConditionalIntermediate", "Conditional", "Catch"),
            ("LinkIntermediate", "Link", "Catch"), ("LinkIntermediate", "Link", "Throw"), ("SignalStart", "Signal", ""), ("SignalIntermediate", "Signal", "Catch"), ("SignalIntermediate", "Signal", "Throw"), ("SignalEnd", "Signal", ""),
            ("ErrorEnd", "Error", ""), ("EscalationIntermediate", "Escalation", "Throw"), ("EscalationEnd", "Escalation", ""), ("CompensationIntermediate", "Compensation", "Throw"), ("CompensationEnd", "Compensation", ""),
            ("MultipleStart", "Multiple", ""), ("ParallelMultipleStart", "Multiple", ""), ("ParallelMultipleIntermediate", "Multiple", "Catch"), ("MultipleIntermediate", "Multiple", "Catch"), ("MultipleIntermediate", "Multiple", "Throw"), ("MultipleEnd", "Multiple", "") })
        {
            // The installed factory uses different definition sets for start, boundary and other multiple events.
            string payloadKind = item.Item2 == "Multiple" && item.Item1.EndsWith("Start") ? "StartMultiple" : item.Item2;
            string id = Id(); ids.Add((id, payloadKind)); nodes.Add(Node(parent, id, item.Item1, item.Item3 == "" ? null : item.Item3, payload: Payload(payloadKind)));
        }
        foreach (string kind in new[] { "Message", "Timer", "Conditional", "Error", "Escalation", "Signal", "Compensation", "Multiple", "ParallelMultiple" })
        {
            string payloadKind = kind is "Multiple" or "ParallelMultiple" ? "BoundaryMultiple" : kind;
            string id = Id(); ids.Add((id, payloadKind)); nodes.Add(Node(parent, id, kind + "Intermediate", "Boundary", new { AttachedToActivityId = replacement, IsInterrupting = true }, Payload(payloadKind)));
        }
        nodes.Add(Node(sub, nestedEvent, "CompensationEnd", payload: [new { Kind = "Compensation", Compensation = new { ActivityId = nestedTask, WaitForCompletion = true } }]));
        // Context-specific start definitions use independent event-triggered containers.
        foreach (string kind in new[] { "Error", "Escalation", "Compensation" })
        {
            string container = Id(), child = Id();
            nodes.Add(Node(sub, container, "SubProcess", subprocess: new { TriggeredByEvent = true }));
            nodes.Add(Node(container, child, "ManualTask"));
            nodes.Add(Node(container, Id(), kind + "Start", evt: new { IsInterrupting = kind != "Escalation" },
                payload: kind == "Compensation" ? [new { Kind = kind, Compensation = new { ActivityId = child, WaitForCompletion = true } }] : Payload(kind)));
        }
        await Mutate(nodes.ToArray());
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        // Unrelated renames must retain complete definitions, not just their displayed kind.
        await Mutate(ids.Select(e => (object)new { Operation = "update", ElementId = e.Id, Name = "Preserved payload " + e.Id[..8] }).ToArray());
        // Replace nonempty payloads through MCP, exercising exact field projection rather than only creation/clear.
        name = "Changed 日本語 Ω"; condition = "amount < 9 & region != \"other\""; errorCode = "E_CHANGED_Ω"; escalationCode = "ESC_CHANGED_日本語";
        await Mutate(ids.Select(e => (object)new { Operation = "update", ElementId = e.Id, EventPayloads = Payload(e.Kind) }).ToArray());
        await Mutate(ids.Where(e => e.Kind == "Timer").Select(e => (object)new { Operation = "update", ElementId = e.Id, EventPayloads = new object[] { new { Kind = "Timer", Timer = new { Kind = "Date", Text = "2026-09-07T10:00:00" } } } }).ToArray());
        // Sparse definition patches must retain the other definitions and unrequested fields.
        await Mutate([new { Operation = "update", ElementId = ids.First(e => e.Kind == "StartMultiple").Id,
            EventPayloads = new object[] { new { Kind = "Conditional", Name = "One changed field" } } }]);
        string comp = ids.First(e => e.Kind == "Compensation").Id;
        foreach (var attempt in new[] {
            (Changes: new object[] { new { Operation = "delete", ElementId = task } }, Error: "compensation event references"),
            (Changes: new object[] { new { Operation = "update", ElementId = task, EventPayloads = Payload("Message") } }, Error: "requires a native event"),
            (Changes: new object[] { new { Operation = "update", ElementId = ids[0].Id, EventPayloads = Payload("Timer") } }, Error: "absent or ambiguous"),
            (Changes: new object[] { new { Operation = "update", ElementId = comp, EventPayloads = new object[] { new { Kind = "Compensation", Compensation = new { ActivityId = foreign } } } } }, Error: "same flow container") })
        {
            var failure = await Mutate(attempt.Changes, "failed");
            if (!S(failure, "Error").Contains(attempt.Error)) throw new InvalidDataException("Payload failure occurred for an unrelated reason: " + S(failure, "Error"));
        }
        await Mutate([new { Operation = "update", ElementId = comp, EventPayloads = new object[] { new { Kind = "Compensation", Compensation = new { ActivityId = replacement, WaitForCompletion = false } } } }]);
        var before = graph.Where(e => S(e, "DiagramId") == diagram && e.GetProperty("Event").ValueKind == JsonValueKind.Object).ToDictionary(e => S(e, "Id"), e => e.GetProperty("Event").GetProperty("Definitions"));
        var clone = await Op("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new { Changes = new[] { new { Operation = "clone", DiagramId = diagram, Name = "Payload clone" } } } });
        path = S(clone, "outputArtifact"); revision = S(clone, "outputRevision"); graph = clone.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        var map = clone.GetProperty("edited").GetProperty("DiagramClones")[0].GetProperty("Identities").EnumerateArray().ToDictionary(e => S(e, "SourceId"), e => S(e, "TargetId"));
        foreach (var original in before)
        {
            var actual = graph.Single(e => S(e, "Id") == map[original.Key]).GetProperty("Event").GetProperty("Definitions");
            foreach (var definition in original.Value.EnumerateArray())
            {
                var copy = actual.EnumerateArray().Single(d => S(d, "Kind") == S(definition, "Kind"));
                if (S(definition, "Kind") != "Compensation") { if (!JsonElement.DeepEquals(definition, copy)) throw new InvalidDataException("Native cloning changed an event payload."); }
                else
                {
                    var prior = definition.GetProperty("Compensation"); var current = copy.GetProperty("Compensation");
                    if (!JsonElement.DeepEquals(prior.GetProperty("WaitForCompletion"), current.GetProperty("WaitForCompletion")) ||
                        S(current, "ActivityId") != (S(prior, "ActivityId") == "" ? "" : map[S(prior, "ActivityId")])) throw new InvalidDataException("Native cloning lost compensation behavior or target mapping.");
                }
            }
        }
        await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram });
        await Op("native_publish", new() { ["path"] = path, ["format"] = "word", ["title"] = "Event payload documentation" });
        await Mutate(ids.Select(e => (object)new { Operation = "update", ElementId = e.Id, EventPayloads = Payload(e.Kind, true) }).ToArray());
        await Mutate([new { Operation = "delete", ElementId = task }, new { Operation = "delete", ElementId = nestedEvent }, new { Operation = "delete", ElementId = nestedTask }]);
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var inspected = await Op("native_inspect", new() { ["path"] = path });
        if (S(inspected, "sourceRevision") != revision) throw new InvalidDataException("Payload inspection changed durable model bytes.");
    }
    private static string S(JsonElement e, string key) => e.GetProperty(key).GetString()!;
}
