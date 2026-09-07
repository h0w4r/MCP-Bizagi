using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>Quantitative native reusable-subprocess black-box semantics and linked-model publication.</summary>
internal static class NativeCallBehaviorAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Operation(string tool, Dictionary<string, object?> args)
        {
            string id = (await call(tool, args)).GetProperty("OperationId").GetString()!;
            var result = await wait(id, "completed"); exited(id); receipts.Add(new { tool, id, result });
            File.WriteAllText(Path.Combine(run, "native-call-behavior.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return result.GetProperty("Result");
        }
        var created = await Operation("native_model_create", new() { ["diagramNames"] = new[] { "Invoke service", "Fulfil service" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string Diagram(string name) => graph.Single(e => e.GetProperty("Kind").GetString() == "Collaboration" && e.GetProperty("Name").GetString() == name).GetProperty("Id").GetString()!;
        string Process(string diagram)
        {
            string pool = graph.Single(e => e.GetProperty("Kind").GetString() == "Participant" && e.GetProperty("DiagramId").GetString() == diagram && e.GetProperty("IsMainParticipant").ValueKind == JsonValueKind.False).GetProperty("Id").GetString()!;
            return graph.Single(e => e.GetProperty("Kind").GetString() == "Process" && e.GetProperty("ParentId").GetString() == pool).GetProperty("Id").GetString()!;
        }
        string callerDiagram = Diagram("Invoke service"), serviceDiagram = Diagram("Fulfil service"), callerProcess = Process(callerDiagram), serviceProcess = Process(serviceDiagram);
        string rootStart = Guid.NewGuid().ToString(), rootCall = Guid.NewGuid().ToString(), rootEnd = Guid.NewGuid().ToString();
        string serviceStart = Guid.NewGuid().ToString(), serviceTask = Guid.NewGuid().ToString(), serviceEnd = Guid.NewGuid().ToString();
        object Node(string parent, string id, string type, string name, int x, int size) => new { Operation = "create", ElementId = id, ParentId = parent, ElementType = type,
            Name = name, Documentation = name + " documentation", Geometry = new { X = x, Y = 100, Width = size, Height = size == 30 ? 30 : 65 } };
        object Flow(string parent, string from, string to, int x1, int x2) => new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = parent,
            ElementType = "SequenceFlow", SourceId = from, TargetId = to, Points = new[] { new { X = x1, Y = 115 }, new { X = x2, Y = 115 } } };
        var edited = await Operation("native_mutate", new() { ["path"] = created.GetProperty("outputArtifact").GetString(), ["expectedRevision"] = created.GetProperty("outputRevision").GetString(),
            ["mutations"] = new object[] {
                Node(callerProcess, rootStart, "NoneStart", "Request service", 80, 30),
                new { Operation = "create", ElementId = rootCall, ParentId = callerProcess, ElementType = "CallActivity", Name = "Invoke fulfilment", Documentation = "Execute the linked native fulfilment process.",
                    CallTarget = new { ProcessId = serviceProcess }, Geometry = new { X = 220, Y = 100, Width = 110, Height = 65 } },
                Node(callerProcess, rootEnd, "NoneEnd", "Request complete", 450, 30), Flow(callerProcess, rootStart, rootCall, 110, 220), Flow(callerProcess, rootCall, rootEnd, 330, 450),
                Node(serviceProcess, serviceStart, "NoneStart", "Service start", 80, 30), Node(serviceProcess, serviceTask, "UserTask", "Perform linked service Ω", 220, 110),
                Node(serviceProcess, serviceEnd, "NoneEnd", "Service complete", 450, 30), Flow(serviceProcess, serviceStart, serviceTask, 110, 220), Flow(serviceProcess, serviceTask, serviceEnd, 330, 450)
            } });
        string path = edited.GetProperty("outputArtifact").GetString()!;
        var validation = await Operation("native_validate", new() { ["path"] = path });
        if (validation.GetProperty("result").GetProperty("Validation").GetArrayLength() != 0) throw new InvalidDataException("The explicit linked-process corpus must pass native validation without findings.");
        var saved = edited.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string Bpmn(string id) => saved.Single(e => e.GetProperty("Id").GetString() == id).GetProperty("BpmnId").GetString()!;

        // Modeler documents reusable subprocesses as black boxes, even with a valid local target.
        // Configure the call shape itself; never inline linked service elements to counterfeit native behavior.
        XNamespace ns = "http://www.bpsim.org/schemas/1.0";
        XElement Parameter(string name, string type, object value) => new(ns + name, new XElement(ns + type, new XAttribute("value", value)));
        XElement Scenario(string id, int minutes) => new(ns + "Scenario", new XAttribute("id", id), new XAttribute("name", id), new XAttribute("author", "h0w4r"), new XAttribute("version", "1.0"),
            new XElement(ns + "ScenarioParameters", new XAttribute("replication", 2), new XAttribute("seed", 123), new XAttribute("baseTimeUnit", "min"), new XAttribute("baseCurrencyUnit", "USD"),
                Parameter("Start", "DateTimeParameter", "2026-09-07T00:00:00"), Parameter("Duration", "DurationParameter", "P1D")),
            new XElement(ns + "ElementParameters", new XAttribute("elementRef", Bpmn(rootStart)), new XElement(ns + "ControlParameters", Parameter("TriggerCount", "NumericParameter", 12))),
            new XElement(ns + "ElementParameters", new XAttribute("elementRef", Bpmn(rootCall)), new XElement(ns + "TimeParameters", Parameter("ProcessingTime", "FloatingParameter", minutes))));
        var config = new XElement(ns + "BPSimData", new XAttribute("simulationLevel", "LevelTwo"), Scenario("Call_fast", 3), Scenario("Call_slow", 9));
        foreach (var parameters in config.Descendants().Where(e => e.Name == ns + "ScenarioParameters" || e.Name == ns + "ElementParameters")) parameters.Add(new XElement(ns + "PropertyParameters"));
        var configured = await Operation("native_metadata_apply", new() { ["path"] = path, ["expectedRevision"] = edited.GetProperty("outputRevision").GetString(),
            ["patch"] = new { Simulations = new[] { new { DiagramId = callerDiagram, Xml = config.ToString() } }, DiscardSimulationResults = true } });
        path = configured.GetProperty("outputArtifact").GetString()!;

        void Verify(JsonElement result, int expectedReports)
        {
            var reply = result.GetProperty("result");
            var limitation = reply.GetProperty("SimulationLimitations").EnumerateArray().Single();
            if (limitation.GetProperty("Code").GetString() != "reusable_subprocess_black_box" || limitation.GetProperty("ElementId").GetString() != rootCall ||
                limitation.GetProperty("DiagramId").GetString() != callerDiagram || limitation.GetProperty("BpmnId").GetString() != Bpmn(rootCall) ||
                limitation.GetProperty("CallReference").GetProperty("CatalogProcessId").GetString() != serviceProcess)
                throw new InvalidDataException("Simulation did not expose the exact input call's black-box limitation and original target.");
            var reports = reply.GetProperty("SimulationReports").EnumerateArray().ToArray();
            if (reports.Length != expectedReports) throw new InvalidDataException("Not every actual native replication was returned.");
            foreach (var report in reports)
            {
                int minutes = report.GetProperty("ScenarioId").GetString() == "Call_fast" ? 3 : report.GetProperty("ScenarioId").GetString() == "Call_slow" ? 9 : throw new InvalidDataException("Unexpected native scenario identity.");
                var elements = report.GetProperty("Elements").EnumerateArray().ToArray();
                double Metric(JsonElement element, string name) => double.Parse(element.GetProperty("Metrics").GetProperty(name).GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);
                var end = elements.Single(e => e.GetProperty("Id").GetString() == Bpmn(rootEnd));
                // Bizagi emits a generated Task for each black-box invocation. Correlate the full
                // generated identity to the actual call, not a task name or an arbitrary result row.
                string blackBoxId = Bpmn(rootCall) + "_UnreferencedCallInstance(" + Bpmn(rootCall) + ")";
                var blackBox = elements.Single(e => e.GetProperty("Id").GetString() == blackBoxId);
                if (Metric(end, "numberCompleted") != 12 || Metric(blackBox, "numberOfTokensCompleted") != 12 ||
                    Metric(blackBox, "averageTimeBusy") != minutes || Metric(blackBox, "totalTimeBusy") != 12 * minutes ||
                    elements.Any(e => e.GetProperty("Id").GetString() == Bpmn(serviceTask)))
                    throw new InvalidDataException("Actual native results differ from configured black-box duration/count or unexpectedly claim linked service execution.");
            }
        }
        Verify(await Operation("native_simulate", new() { ["path"] = path, ["diagramId"] = callerDiagram, ["scenarioId"] = "Call_fast", ["simulationLevel"] = 2 }), 1);
        Verify(await Operation("native_simulate_what_if", new() { ["path"] = path, ["diagramId"] = callerDiagram, ["scenarioIds"] = new[] { "Call_fast", "Call_slow" }, ["simulationLevel"] = 2 }), 4);
        await Operation("native_render_svg", new() { ["path"] = path, ["diagramId"] = callerDiagram });
        foreach (string format in new[] { "word", "pdf" })
        {
            var publication = await Operation("native_publish", new() { ["path"] = path, ["format"] = format, ["title"] = "Linked native service documentation" });
            string text = System.Text.RegularExpressions.Regex.Replace(publication.GetProperty("reopened").GetProperty("Publication").GetProperty("Text").GetString()!, @"\s+", " ");
            foreach (string expected in new[] { "Invoke fulfilment", "Execute the linked native fulfilment process.", "Fulfil service", "Perform linked service Ω" })
                if (!text.Contains(expected, StringComparison.Ordinal)) throw new InvalidDataException("Fresh publication reader did not find the call/target documentation: " + expected);
        }
        var final = await Operation("native_inspect", new() { ["path"] = path });
        if (final.GetProperty("sourceRevision").GetString() != configured.GetProperty("outputRevision").GetString()) throw new InvalidDataException("Call behavior analysis changed the source artifact.");
        var finalCall = final.GetProperty("result").GetProperty("Elements").EnumerateArray().Single(e => e.GetProperty("Id").GetString() == rootCall);
        if (finalCall.GetProperty("CallReference").GetProperty("CatalogProcessId").GetString() != serviceProcess) throw new InvalidDataException("Simulation or publication cleared the durable call target.");
    }
}
