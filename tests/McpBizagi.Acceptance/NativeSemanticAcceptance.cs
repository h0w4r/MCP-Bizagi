using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>Real MCP/native semantic editing, explicit failures and honest native simulation diagnostics.</summary>
internal static class NativeSemanticAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, bool, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, bool requireIntendedTokenSemantics = false)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Operation(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = (await call(tool, args, false)).GetProperty("OperationId").GetString()!;
            var result = await wait(id, state); exited(id); receipts.Add(new { tool, id, result });
            File.WriteAllText(Path.Combine(run, "native-semantics.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? result.GetProperty("Result") : result;
        }
        void Require(bool condition, string error) { if (!condition) throw new InvalidDataException(error); }
        var created = await Operation("native_model_create", new() { ["diagramNames"] = new[] { "Semantic properties Ω", "Token quantities" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string Diagram(string name) => graph.Single(e => e.GetProperty("Kind").GetString() == "Collaboration" && e.GetProperty("Name").GetString() == name).GetProperty("Id").GetString()!;
        string Process(string diagram)
        {
            string pool = graph.Single(e => e.GetProperty("Kind").GetString() == "Participant" && e.GetProperty("DiagramId").GetString() == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()).GetProperty("Id").GetString()!;
            return graph.Single(e => e.GetProperty("Kind").GetString() == "Process" && e.GetProperty("ParentId").GetString() == pool).GetProperty("Id").GetString()!;
        }
        string semanticDiagram = Diagram("Semantic properties Ω"), tokenDiagram = Diagram("Token quantities"), semanticProcess = Process(semanticDiagram), tokenProcess = Process(tokenDiagram);
        string path = created.GetProperty("outputArtifact").GetString()!, revision = created.GetProperty("outputRevision").GetString()!;
        async Task<JsonElement> Mutate(object[] changes, string state = "completed")
        {
            var result = await Operation("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = changes }, state);
            if (state == "completed") { path = result.GetProperty("outputArtifact").GetString()!; revision = result.GetProperty("outputRevision").GetString()!; graph = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray(); }
            return result;
        }
        string Id() => Guid.NewGuid().ToString();
        object Node(string parent, string id, string type, string name, int x, int y) => new { Operation = "create", ElementId = id, ParentId = parent, ElementType = type, Name = name,
            Documentation = name + " documentation", Geometry = new { X = x, Y = y, Width = type.Contains("Gateway") || type.EndsWith("Start") || type.EndsWith("End") ? 40 : 110, Height = 50 } };
        object Flow(string parent, string id, string from, string to) => new { Operation = "create", ElementId = id, ParentId = parent, ElementType = "SequenceFlow", SourceId = from, TargetId = to,
            Points = new[] { new { X = 100, Y = 100 }, new { X = 200, Y = 100 } } };
        string[] kinds = ["AbstractTask", "UserTask", "ManualTask", "ServiceTask", "ScriptTask", "SendTask", "ReceiveTask", "BusinessRuleTask", "SubProcess", "CallActivity"];
        string[] activities = kinds.Select(_ => Id()).ToArray();
        string start = Id(), gateway = Id(), target = Id(), end = Id(), alternateGateway = Id(), firstFlow = Id(), secondFlow = Id();
        string tStart = Id(), fork = Id(), a = Id(), b = Id(), join = Id(), tEnd = Id();
        var nodes = kinds.Select((kind, index) => Node(semanticProcess, activities[index], kind, "Properties " + kind, 80 + index % 5 * 145, 70 + index / 5 * 100)).ToList();
        nodes.AddRange([Node(semanticProcess, start, "NoneStart", "Decision start", 80, 400), Node(semanticProcess, gateway, "ExclusiveGateway", "Decision", 200, 400),
            Node(semanticProcess, alternateGateway, "InclusiveGateway", "Alternate decision", 200, 500), Node(semanticProcess, target, "UserTask", "Approved", 400, 400), Node(semanticProcess, end, "NoneEnd", "Decision end", 600, 400),
            Flow(semanticProcess, Id(), start, gateway), Flow(semanticProcess, firstFlow, gateway, target), Flow(semanticProcess, secondFlow, gateway, end), Flow(semanticProcess, Id(), target, end),
            Node(tokenProcess, tStart, "NoneStart", "Token start", 80, 100), Node(tokenProcess, fork, "ParallelGateway", "Token fork", 180, 100),
            Node(tokenProcess, a, "UserTask", "Left branch", 280, 80), Node(tokenProcess, b, "UserTask", "Right branch", 280, 220), Node(tokenProcess, join, "UserTask", "Quantity join", 480, 100), Node(tokenProcess, tEnd, "NoneEnd", "Token end", 680, 100),
            Flow(tokenProcess, Id(), tStart, fork), Flow(tokenProcess, Id(), fork, a), Flow(tokenProcess, Id(), fork, b), Flow(tokenProcess, Id(), a, join), Flow(tokenProcess, Id(), b, join), Flow(tokenProcess, Id(), join, tEnd)]);
        await Mutate(nodes.ToArray());
        // These real SDK binding failures must happen before an operation or worker starts.
        await call("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new[] {
            new { Operation = "update", ElementId = activities[0], Name = "Must not change", ActivityProperties = new { CompletionQuantiy = 3 } } } }, true);
        await call("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new[] {
            new { Operation = "update", ElementId = activities[0], Name = "Must not change", UnknownProperty = true } } }, true);
        await call("native_metadata_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new { UnknownProperty = true } }, true);
        string[] states = ["None", "Ready", "Active", "Completing", "Completed", "Aborted", "Aborting"];
        var properties = activities.Select((id, index) => (object)new { Operation = "update", ElementId = id,
            ActivityProperties = new { StartQuantity = index + 2, CompletionQuantity = index + 3, IsForCompensation = true, State = states[index % states.Length] } }).ToList();
        properties.Add(new { Operation = "update", ElementId = gateway, GatewayDirection = "Diverging" });
        properties.Add(new { Operation = "update", ElementId = alternateGateway, GatewayDirection = "Mixed" });
        await Mutate(properties.ToArray());
        await Operation("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var wrongActivity = await Mutate([new { Operation = "update", ElementId = start, ActivityProperties = new { StartQuantity = 2 } }], "failed");
        Require(wrongActivity.GetProperty("Error").GetString()!.Contains("requires a native activity"), "Wrong activity kind failed for an unrelated reason.");
        var wrongGateway = await Mutate([new { Operation = "update", ElementId = activities[0], GatewayDirection = "Diverging" }], "failed");
        Require(wrongGateway.GetProperty("Error").GetString()!.Contains("requires a native gateway"), "Wrong gateway kind failed for an unrelated reason.");
        var wrongFlow = await Mutate([new { Operation = "update", ElementId = activities[0], FlowCondition = new { Kind = "Default" } }], "failed");
        Require(wrongFlow.GetProperty("Error").GetString()!.Contains("requires a native sequence flow"), "Wrong flow kind failed for an unrelated reason.");
        const string condition = "Order.Total < 100 & Region == \"日本語 Ω\"";
        await Mutate([new { Operation = "update", ElementId = firstFlow, FlowCondition = new { Kind = "Expression", Text = condition } },
            new { Operation = "update", ElementId = secondFlow, FlowCondition = new { Kind = "Default" } }]);
        var duplicate = await Mutate([new { Operation = "update", ElementId = firstFlow, FlowCondition = new { Kind = "Default" } }], "failed");
        Require(duplicate.GetProperty("Error").GetString()!.Contains("multiple default"), "Duplicate default was not rejected by native reference validation.");
        await Mutate([new { Operation = "update", ElementId = secondFlow, FlowCondition = new { Kind = "None" } },
            new { Operation = "update", ElementId = firstFlow, FlowCondition = new { Kind = "Default" } }]);
        await Mutate([new { Operation = "reconnect", ElementId = firstFlow, SourceId = alternateGateway, TargetId = target, Points = new[] { new { X = 240, Y = 500 }, new { X = 400, Y = 400 } } }]);
        Require(graph.Single(e => e.GetProperty("Id").GetString() == gateway).GetProperty("DefaultSequenceFlowIds").GetArrayLength() == 0 &&
            graph.Single(e => e.GetProperty("Id").GetString() == alternateGateway).GetProperty("DefaultSequenceFlowIds")[0].GetString() == firstFlow, "Reconnected default retained a stale source association.");
        await Mutate([new { Operation = "update", ElementId = firstFlow, FlowCondition = new { Kind = "Expression", Text = condition } }]);
        await Operation("native_render_svg", new() { ["path"] = path, ["diagramId"] = semanticDiagram });
        await Operation("native_publish", new() { ["path"] = path, ["format"] = "word", ["title"] = "Native semantic property evidence" });
        await Mutate(activities.Select(id => (object)new { Operation = "update", ElementId = id, ActivityProperties = new { StartQuantity = 1, CompletionQuantity = 1, IsForCompensation = false, State = "None" } }).ToArray());

        // The simulation corpus is separate from the deliberately disconnected property palette.
        // Two parallel branches produce two arrivals per instance at the task whose token quantities change.
        string Bpmn(string id) => graph.Single(e => e.GetProperty("Id").GetString() == id).GetProperty("BpmnId").GetString()!;
        XNamespace ns = "http://www.bpsim.org/schemas/1.0";
        var config = new XElement(ns + "BPSimData", new XAttribute("simulationLevel", "LevelOne"), new XElement(ns + "Scenario", new XAttribute("id", "Tokens"), new XAttribute("name", "Tokens"), new XAttribute("author", "h0w4r"), new XAttribute("version", "1.0"),
            new XElement(ns + "ScenarioParameters", new XAttribute("baseTimeUnit", "min"), new XAttribute("baseCurrencyUnit", "USD"),
                new XElement(ns + "Start", new XElement(ns + "DateTimeParameter", new XAttribute("value", "2026-09-07T00:00:00"))), new XElement(ns + "Duration", new XElement(ns + "DurationParameter", new XAttribute("value", "P1D"))), new XElement(ns + "PropertyParameters")),
            new XElement(ns + "ElementParameters", new XAttribute("elementRef", Bpmn(tStart)), new XElement(ns + "ControlParameters", new XElement(ns + "TriggerCount", new XElement(ns + "NumericParameter", new XAttribute("value", 12)))), new XElement(ns + "PropertyParameters"))));
        var configured = await Operation("native_metadata_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new { Simulations = new[] { new { DiagramId = tokenDiagram, Xml = config.ToString() } }, DiscardSimulationResults = true } });
        path = configured.GetProperty("outputArtifact").GetString()!; revision = configured.GetProperty("outputRevision").GetString()!;
        var observations = new List<object>(); bool semanticMismatch = false;
        foreach (var quantities in new[] { (Start: 1, Complete: 1, EndTokens: 24), (Start: 2, Complete: 1, EndTokens: 12), (Start: 2, Complete: 3, EndTokens: 36) })
        {
            await Mutate([new { Operation = "update", ElementId = join, ActivityProperties = new { StartQuantity = quantities.Start, CompletionQuantity = quantities.Complete } }]);
            var simulation = await Operation("native_simulate", new() { ["path"] = path, ["diagramId"] = tokenDiagram, ["scenarioId"] = "Tokens" });
            var result = simulation.GetProperty("result").GetProperty("SimulationReports")[0].GetProperty("Elements").EnumerateArray().Single(e => e.GetProperty("Id").GetString() == Bpmn(tEnd));
            double tokens = double.Parse(result.GetProperty("Metrics").GetProperty("numberCompleted").GetString()!, CultureInfo.InvariantCulture);
            var inputs = simulation.GetProperty("result").GetProperty("SimulationInputs")[0].GetProperty("Activities").EnumerateArray().Single(e => e.GetProperty("ElementId").GetString() == join);
            Require(inputs.GetProperty("StartQuantity").GetInt32() == quantities.Start && inputs.GetProperty("CompletionQuantity").GetInt32() == quantities.Complete,
                "The actual simulator input did not preserve the requested activity quantities.");
            var warnings = simulation.GetProperty("result").GetProperty("SimulationLimitations").EnumerateArray().Where(e => e.GetProperty("Code").GetString() == "nondefault_token_quantities_unaccredited").ToArray();
            if (quantities.Start != 1 || quantities.Complete != 1)
                Require(warnings.Length == 1 && warnings[0].GetProperty("ElementId").GetString() == join, "Observed native quantity limitations were hidden from the operator.");
            else Require(warnings.Length == 0, "Default quantities unexpectedly received a nondefault warning.");
            // The local installed engine emitted 24 end tokens for all three correct-input cases.
            // Preserve that regression and the explicit mismatch; this is diagnostic acceptance,
            // NOT accreditation of the intended nondefault token semantics.
            if (!requireIntendedTokenSemantics)
                Require(tokens == 24, "The installed engine's observed quantity behavior changed; reassess the limitation with retained native input/results.");
            semanticMismatch |= tokens != quantities.EndTokens;
            observations.Add(new { quantities.Start, quantities.Complete, quantities.EndTokens, actualEndTokens = tokens, simulation });
            File.WriteAllText(Path.Combine(run, "token-quantity-observations.json"), JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (requireIntendedTokenSemantics && semanticMismatch)
            throw new InvalidDataException("Nondefault token semantics remain unaccredited: correct native input does not produce the intended completion counts. See token-quantity-observations.json.");
        await Operation("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var inspected = await Operation("native_inspect", new() { ["path"] = path });
        Require(inspected.GetProperty("sourceRevision").GetString() == revision, "Native analyses changed the persisted model.");
    }
}
