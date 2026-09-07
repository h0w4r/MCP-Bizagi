using System.Text.Json;

/// <summary>Installed special-subprocess lifecycle through the independent official SDK MCP client.</summary>
internal static class NativeSubProcessAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = S(await call(tool, args), "OperationId");
            var response = await wait(id, state); exited(id); receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-subprocesses.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Special subprocesses Ω", "Other diagram" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(graph.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Name") == "Special subprocesses Ω"), "Id");
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string parent = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string diagnostics = S(graph.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Name") == "Other diagram"), "Id");
        string simPool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagnostics && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string simParent = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == simPool), "Id");
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        async Task<JsonElement> Mutate(object[] mutations, string state = "completed")
        {
            var result = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = mutations }, state);
            if (state == "completed") { path = S(result, "outputArtifact"); revision = S(result, "outputRevision"); graph = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray(); }
            return result;
        }
        string Id() => Guid.NewGuid().ToString();
        string transaction = Id(), adHoc = Id(), ordinary = Id(), cancel = Id(), boundary = Id(), task = Id(), toggle = Id(), toggleStart = Id();
        string nestedAdHoc = Id(), nestedTransaction = Id(), nestedCancel = Id(), nestedTask = Id();
        string simStart = Id(), simTransaction = Id(), simAdHoc = Id(), simEnd = Id();
        string condition = "complete > 2 & region == \"日本語 Ω\"";
        var nodes = new List<object>(); int index = 0;
        object Node(string owner, string id, string type, string? kind = null, object? sub = null, object? evt = null, string? mode = null)
        {
            int n = index++;
            return new { Operation = "create", ParentId = owner, ElementId = id, ElementType = type, SubProcessKind = kind, SubProcessProperties = sub, EventProperties = evt, EventMode = mode,
                Name = (kind ?? type) + " " + n + " Ω", Documentation = "Special native subprocess 日本語", Geometry = new { X = 70 + n % 5 * 165, Y = 70 + n / 5 * 120, Width = type == "SubProcess" ? 140 : 80, Height = 60 } };
        }
        nodes.AddRange([Node(parent, transaction, "SubProcess", "Transaction"), Node(parent, adHoc, "SubProcess", "AdHoc", new { AdHocOrdering = "Sequential", AdHocCompletionCondition = condition }),
            Node(parent, ordinary, "SubProcess"), Node(parent, task, "UserTask"), Node(parent, toggle, "SubProcess"),
            Node(transaction, cancel, "CancelEnd"), Node(parent, boundary, "CancelIntermediate", evt: new { AttachedToActivityId = transaction, IsInterrupting = true }, mode: "Boundary"),
            Node(transaction, nestedAdHoc, "SubProcess", "AdHoc", new { AdHocOrdering = "Parallel" }), Node(nestedAdHoc, nestedTask, "ManualTask"),
            Node(adHoc, nestedTransaction, "SubProcess", "Transaction"), Node(nestedTransaction, nestedCancel, "CancelEnd")]);
        var eventContainers = new List<string>();
        foreach (string type in new[] { "ErrorStart", "EscalationStart", "CompensationStart", "MessageStart" })
        {
            string container = Id(); eventContainers.Add(container);
            nodes.Add(Node(ordinary, container, "SubProcess", sub: new { TriggeredByEvent = true }));
            nodes.Add(Node(container, Id(), type, evt: new { IsInterrupting = type is "ErrorStart" or "CompensationStart" }));
        }
        // A separate connected diagram checks honest input-specific simulator diagnostics,
        // not the unsupported execution semantics of the richer modeling palette.
        nodes.AddRange([Node(simParent, simStart, "NoneStart"), Node(simParent, simTransaction, "SubProcess", "Transaction"), Node(simParent, simAdHoc, "SubProcess", "AdHoc"), Node(simParent, simEnd, "NoneEnd")]);
        foreach (var pair in new[] { (simStart, simTransaction), (simTransaction, simAdHoc), (simAdHoc, simEnd) })
            nodes.Add(new { Operation = "create", ParentId = simParent, ElementId = Id(), ElementType = "SequenceFlow", SourceId = pair.Item1, TargetId = pair.Item2,
                Points = new[] { new { X = 100, Y = 100 }, new { X = 200, Y = 100 } } });
        await Mutate(nodes.ToArray());
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        await Mutate([new { Operation = "update", ElementId = toggle, SubProcessProperties = new { TriggeredByEvent = true } },
            Node(toggle, toggleStart, "MessageStart", evt: new { IsInterrupting = false }),
            new { Operation = "update", ElementId = adHoc, SubProcessProperties = new { AdHocOrdering = "Parallel", AdHocCompletionCondition = "" } }]);
        await Mutate([new { Operation = "update", ElementId = adHoc, SubProcessProperties = new { AdHocOrdering = "Sequential", AdHocCompletionCondition = condition } },
            new { Operation = "update", ElementId = nestedAdHoc, SubProcessProperties = new { AdHocOrdering = "Sequential", AdHocCompletionCondition = condition } }]);
        await Mutate([new { Operation = "update", ElementId = transaction, Name = "Transaction rename preserves cancel links" }, new { Operation = "update", ElementId = adHoc, Name = "Ad hoc rename preserves expression" }]);
        // Failure cases are actual native worker executions, followed by successful recovery.
        foreach (var attempt in new[] {
            (Changes: new object[] { new { Operation = "update", ElementId = toggle, SubProcessProperties = new { TriggeredByEvent = false } } }, Error: "event-triggered subprocess"),
            (Changes: new object[] { new { Operation = "update", ElementId = task, SubProcessProperties = new { TriggeredByEvent = false } } }, Error: "native embedded subprocess"),
            (Changes: new object[] { new { Operation = "update", ElementId = transaction, SubProcessProperties = new { AdHocOrdering = "Sequential" } } }, Error: "native AdHoc subprocess"),
            (Changes: new object[] { new { Operation = "update", ElementId = transaction, SubProcessProperties = new { TriggeredByEvent = true } } }, Error: "Transaction or AdHoc"),
            (Changes: new object[] { Node(parent, Id(), "CancelEnd") }, Error: "transaction subprocess"),
            (Changes: new object[] { Node(parent, Id(), "ErrorStart") }, Error: "event-triggered subprocess"),
            (Changes: new object[] { new { Operation = "update", ElementId = boundary, EventProperties = new { AttachedToActivityId = task } } }, Error: "transaction subprocess"),
            (Changes: new object[] { new { Operation = "delete", ElementId = transaction } }, Error: "boundary events"),
            (Changes: new object[] { new { Operation = "create", ParentId = parent, ElementId = Id(), ElementType = "SequenceFlow", SourceId = task, TargetId = toggle, Points = new[] { new { X = 100, Y = 100 }, new { X = 200, Y = 100 } } } }, Error: "Event-triggered subprocesses") })
        {
            var failure = await Mutate(attempt.Changes, "failed");
            if (!S(failure, "Error").Contains(attempt.Error)) throw new InvalidDataException("Special-subprocess rejection had an unrelated cause: " + S(failure, "Error"));
        }
        await Mutate([new { Operation = "delete", ElementId = toggleStart }, new { Operation = "update", ElementId = toggle, SubProcessProperties = new { TriggeredByEvent = false } }]);
        string incoming = Id();
        await Mutate([new { Operation = "create", ParentId = parent, ElementId = incoming, ElementType = "SequenceFlow", SourceId = task, TargetId = toggle,
            Points = new[] { new { X = 100, Y = 100 }, new { X = 200, Y = 100 } } }]);
        var flowFailure = await Mutate([new { Operation = "update", ElementId = toggle, SubProcessProperties = new { TriggeredByEvent = true } }], "failed");
        if (!S(flowFailure, "Error").Contains("Event-triggered subprocesses")) throw new InvalidDataException("Parent flag update did not protect incident sequence flows.");
        await Mutate([new { Operation = "delete", ElementId = incoming }, new { Operation = "update", ElementId = toggle, SubProcessProperties = new { TriggeredByEvent = true } }]);
        // A native clone must preserve type-specific properties and remap cancel boundaries.
        var originalSub = graph.Where(e => S(e, "DiagramId") == diagram && e.GetProperty("SubProcess").ValueKind == JsonValueKind.Object).ToDictionary(e => S(e, "Id"), e => e.GetProperty("SubProcess"));
        var clone = await Op("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new { Changes = new[] { new { Operation = "clone", DiagramId = diagram, Name = "Special subprocess native clone" } } } });
        path = S(clone, "outputArtifact"); revision = S(clone, "outputRevision"); graph = clone.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        var map = clone.GetProperty("edited").GetProperty("DiagramClones")[0].GetProperty("Identities").EnumerateArray().ToDictionary(e => S(e, "SourceId"), e => S(e, "TargetId"));
        foreach (var entry in originalSub)
            if (!JsonElement.DeepEquals(entry.Value, graph.Single(e => S(e, "Id") == map[entry.Key]).GetProperty("SubProcess"))) throw new InvalidDataException("Clone lost subprocess-specific native state.");
        if (S(graph.Single(e => S(e, "Id") == map[boundary]).GetProperty("Event"), "AttachedToActivityId") != map[transaction]) throw new InvalidDataException("Clone lost transaction cancel-boundary mapping.");
        // Persisted tab preferences must accept native subclasses without implying live GUI control.
        var tabs = await Op("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["patch"] = new { OpenedItems = new object[] { new { DiagramId = diagram, SubProcessId = adHoc, IsSelected = true },
                new { DiagramId = diagram, SubProcessId = transaction, IsSelected = false }, new { DiagramId = diagram, SubProcessId = eventContainers[0], IsSelected = false } } } });
        path = S(tabs, "outputArtifact"); revision = S(tabs, "outputRevision");
        await Mutate([
            new { Operation = "update", ElementId = transaction, Geometry = new { X = 70, Y = 500, Width = 140, Height = 60, Expanded = true }, ExpandedSize = new { Width = 650, Height = 400 } },
            new { Operation = "update", ElementId = nestedAdHoc, Geometry = new { X = 40, Y = 40, Width = 140, Height = 60, Expanded = true }, ExpandedSize = new { Width = 450, Height = 240 } },
            new { Operation = "update", ElementId = nestedTask, Geometry = new { X = 60, Y = 60, Width = 110, Height = 60 } },
            new { Operation = "update", ElementId = cancel, Geometry = new { X = 540, Y = 320, Width = 40, Height = 40 } }
        ]);
        await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram });
        foreach (string surface in new[] { transaction, adHoc, ordinary, eventContainers[0] })
            await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram, ["subProcessId"] = surface });
        await Op("native_publish", new() { ["path"] = path, ["format"] = "word", ["title"] = "Special subprocess documentation" });
        var simulation = await Op("native_simulate", new() { ["path"] = path, ["diagramId"] = diagnostics });
        var limitations = simulation.GetProperty("result").GetProperty("SimulationLimitations").EnumerateArray().Where(e => S(e, "Code") == "special_subprocess_simulation_unsupported").ToArray();
        if (limitations.Length != 2 || limitations.Any(e => S(e, "DiagramId") != diagnostics) ||
            !limitations.Select(e => S(e, "ElementId")).Order().SequenceEqual(new[] { simTransaction, simAdHoc }.Order()) ||
            !limitations.Select(e => S(e.GetProperty("SubProcess"), "Kind")).Order().SequenceEqual(new[] { "AdHoc", "Transaction" }))
            throw new InvalidDataException("Special-subprocess simulator limitations lost their actual input identities or native kinds.");
        // Clear durable tabs before explicitly removing the selected containers and children.
        var closed = await Op("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new { OpenedItems = Array.Empty<object>() } });
        path = S(closed, "outputArtifact"); revision = S(closed, "outputRevision");
        await Mutate(new[] { boundary, cancel, nestedTask, nestedAdHoc, transaction, nestedCancel, nestedTransaction, adHoc }.Select(id => (object)new { Operation = "delete", ElementId = id }).ToArray());
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var inspected = await Op("native_inspect", new() { ["path"] = path });
        if (S(inspected, "sourceRevision") != revision) throw new InvalidDataException("Subprocess inspection changed durable native bytes.");
    }
    private static string S(JsonElement element, string key) => element.GetProperty(key).GetString()!;
}
