using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>Real stdio MCP acceptance: resource edits, durable BPSim settings and installed-engine results.</summary>
internal static class NativeMetadataAcceptance
{
    public static async Task Run(string repo, string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        async Task<JsonElement> Operation(string name, Dictionary<string, object?> input, string status = "completed")
        {
            string id = (await call(name, input)).GetProperty("OperationId").GetString()!;
            var state = await wait(id, status); exited(id);
            return status == "completed" ? state.GetProperty("Result") : state;
        }
        File.Copy(Path.Combine(repo, "examples", "minimal.bpmn"), Path.Combine(run, "simulation source.bpmn"));
        var imported = await Operation("native_roundtrip", new() { ["path"] = "simulation source.bpmn", ["modelName"] = "Simulation acceptance" });
        string path = imported.GetProperty("nativeArtifact").GetString()!;
        var read = await Operation("native_metadata_get", new() { ["path"] = path });
        string revision = read.GetProperty("sourceRevision").GetString()!;
        var elements = read.GetProperty("result").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = elements.Single(e => e.GetProperty("Kind").GetString() == "Collaboration").GetProperty("Id").GetString()!;
        string start = elements.Single(e => e.GetProperty("Kind").GetString() == "StartEvent").GetProperty("BpmnId").GetString()!;
        string task = elements.Single(e => e.GetProperty("Kind").GetString()!.EndsWith("Task", StringComparison.Ordinal)).GetProperty("BpmnId").GetString()!;
        string taskId = elements.Single(e => e.GetProperty("BpmnId").GetString() == task).GetProperty("Id").GetString()!;
        string roleId = Guid.NewGuid().ToString(), entityId = Guid.NewGuid().ToString();
        async Task<JsonElement> Apply(object patch)
        {
            var result = await Operation("native_metadata_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = patch });
            if (!result.GetProperty("fidelity").GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Metadata fidelity rejected the change.");
            path = result.GetProperty("outputArtifact").GetString()!; revision = result.GetProperty("outputRevision").GetString()!;
            return result.GetProperty("reopened").GetProperty("Metadata");
        }
        var metadata = await Apply(new
        {
            Resources = new[] {
            new { Id = roleId, Name = "Reviewer Ω", Description = "Approval team", Type = "Role" },
            new { Id = entityId, Name = "Equipment", Description = "Processing resource", Type = "Entity" } }
        });
        string resource = metadata.GetProperty("Resources").EnumerateArray().Single(r => r.GetProperty("Id").GetString() == roleId).GetProperty("BpmnId").GetString()!;
        await Apply(new { Resources = new[] { new { Id = entityId, Name = "Equipment updated", Description = "Updated through MCP", Type = "Role" } } });
        await Apply(new { Resources = new[] { new { Operation = "delete", Id = entityId } } });
        await Apply(new { Assignments = new[] { new { ElementId = taskId, Responsible = new[] { roleId }, Accountable = new[] { roleId }, Consulted = new[] { roleId }, Informed = new[] { roleId } } } });
        await Operation("native_metadata_apply", new()
        {
            ["path"] = path,
            ["expectedRevision"] = revision,
            ["patch"] = new { Resources = new[] { new { Operation = "delete", Id = roleId } } }
        }, "failed");
        XNamespace ns = "http://www.bpsim.org/schemas/1.0";
        XElement Parameter(string name, string type, object value, params object[] attributes) => new(ns + name,
            new XElement(ns + type, new XAttribute("value", value), attributes));
        XElement Scenario(string id, int minutes) => new(ns + "Scenario", new XAttribute("id", id), new XAttribute("name", id), new XAttribute("author", "h0w4r"), new XAttribute("version", "1.0"),
            new XElement(ns + "ScenarioParameters", new XAttribute("replication", 2), new XAttribute("seed", 123), new XAttribute("baseTimeUnit", "min"), new XAttribute("baseCurrencyUnit", "USD"),
                Parameter("Start", "DateTimeParameter", "2026-09-07T00:00:00"), Parameter("Duration", "DurationParameter", "P1D")),
            new XElement(ns + "ElementParameters", new XAttribute("elementRef", start), new XElement(ns + "ControlParameters", Parameter("TriggerCount", "NumericParameter", 12))),
            new XElement(ns + "ElementParameters", new XAttribute("elementRef", task), new XElement(ns + "TimeParameters",
                Parameter("ProcessingTime", "FloatingParameter", minutes))));
        var config = new XElement(ns + "BPSimData", new XAttribute("simulationLevel", "LevelTwo"), Scenario("Scenario_fast", 3), Scenario("Scenario_slow", 9));
        // Explicit empty arrays match the installed native serializer; their presence is not silently waived.
        foreach (var parameters in config.Descendants().Where(e => e.Name == ns + "ScenarioParameters" || e.Name == ns + "ElementParameters"))
            parameters.Add(new XElement(ns + "PropertyParameters"));
        await Apply(new { Simulations = new[] { new { DiagramId = diagram, Xml = config.ToString() } }, DiscardSimulationResults = true });
        // A real native failure must not prevent a subsequent valid operation.
        var bad = new XElement(config); bad.Descendants(ns + "ElementParameters").First().SetAttributeValue("elementRef", "Unknown_element");
        await Operation("native_metadata_apply", new()
        {
            ["path"] = path,
            ["expectedRevision"] = revision,
            ["patch"] = new { Simulations = new[] { new { DiagramId = diagram, Xml = bad.ToString() } } }
        }, "failed");
        var simulation = await Operation("native_simulate", new() { ["path"] = path, ["diagramId"] = diagram, ["scenarioId"] = "Scenario_fast", ["simulationLevel"] = 2 });
        string resultsFile = simulation.GetProperty("result").GetProperty("Artifacts").EnumerateArray().Select(p => p.GetString()!).Single(p => Path.GetFileName(p) == "Results.xml");
        VerifyTiming(resultsFile, 3);
        VerifyStructured(simulation.GetProperty("result"), 1);
        var whatIf = await Operation("native_simulate_what_if", new()
        {
            ["path"] = path,
            ["diagramId"] = diagram,
            ["scenarioIds"] = new[] { "Scenario_fast", "Scenario_slow" },
            ["simulationLevel"] = 2
        });
        var artifacts = whatIf.GetProperty("result").GetProperty("Artifacts").EnumerateArray().Select(p => p.GetString()!).ToArray();
        VerifyStructured(whatIf.GetProperty("result"), 4);
        string[] identities = artifacts.Where(p => p.EndsWith(".identity.xml", StringComparison.Ordinal)).ToArray();
        if (identities.Length != 4) throw new InvalidDataException("Expected two scenarios with two real replications each.");
        foreach (string identity in identities)
        {
            var id = XDocument.Load(identity).Root!;
            VerifyTiming(identity[..^".identity.xml".Length], (string?)id.Attribute("scenarioId") == "Scenario_fast" ? 3 : 9);
        }
        // Level three uses a real constrained native resource, rather than merely storing resource names.
        var constrained = Scenario("Scenario_resources", 3);
        var taskParameters = constrained.Elements(ns + "ElementParameters").Single(e => (string?)e.Attribute("elementRef") == task);
        taskParameters.Add(new XElement(ns + "ResourceParameters", Parameter("Selection", "ExpressionParameter", "bpsim:getResource(\"" + resource + "\",1)")));
        taskParameters.Add(new XElement(ns + "CostParameters", Parameter("FixedCost", "FloatingParameter", 7)));
        constrained.Add(new XElement(ns + "ElementParameters", new XAttribute("elementRef", resource),
            new XElement(ns + "ResourceParameters", Parameter("Quantity", "NumericParameter", 2))));
        var calendar = new XElement(constrained); calendar.SetAttributeValue("id", "Scenario_calendar"); calendar.SetAttributeValue("name", "Scenario_calendar");
        var quantity = calendar.Elements(ns + "ElementParameters").Single(e => (string?)e.Attribute("elementRef") == resource).Element(ns + "ResourceParameters")!.Element(ns + "Quantity")!;
        quantity.ReplaceNodes(new XElement(ns + "NumericParameter", new XAttribute("value", 0)),
            new XElement(ns + "NumericParameter", new XAttribute("value", 2), new XAttribute("validFor", "Calendar_work")));
        calendar.Add(new XElement(ns + "Calendar", new XAttribute("id", "Calendar_work"), new XAttribute("name", "Working day"),
            "BEGIN:VCALENDAR\nBEGIN:VEVENT\nDTSTART:20260907T080000\nDTEND:20260907T170000\nRRULE:FREQ=DAILY;INTERVAL=1\nEND:VEVENT\nPRODID:MCP-Bizagi acceptance\nVERSION:2.0\nEND:VCALENDAR\n"));
        var advanced = new XElement(ns + "BPSimData", new XAttribute("simulationLevel", "LevelFour"), constrained, calendar);
        foreach (var parameters in advanced.Descendants().Where(e => e.Name == ns + "ScenarioParameters" || e.Name == ns + "ElementParameters"))
            parameters.Add(new XElement(ns + "PropertyParameters"));
        await Apply(new { Simulations = new[] { new { DiagramId = diagram, Xml = advanced.ToString() } }, DiscardSimulationResults = true });
        foreach (var scenario in new[] { (Id: "Scenario_resources", Level: 3), (Id: "Scenario_calendar", Level: 4) })
        {
            var result = await Operation("native_simulate", new() { ["path"] = path, ["diagramId"] = diagram, ["scenarioId"] = scenario.Id, ["simulationLevel"] = scenario.Level });
            string file = result.GetProperty("result").GetProperty("Artifacts").EnumerateArray().Select(p => p.GetString()!).Single(p => Path.GetFileName(p) == "Results.xml");
            VerifyTiming(file, 3, resources: true, calendar: scenario.Level == 4);
            VerifyStructured(result.GetProperty("result"), 1);
        }
        await Apply(new { Assignments = new[] { new { ElementId = taskId } } });
        var noop = await Operation("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        if (!noop.GetProperty("fidelity").GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Rich simulation no-op save changed untargeted content.");
        // Return the durable output identity for additional independent corpus checks.
        File.WriteAllText(Path.Combine(run, "metadata-acceptance.json"), JsonSerializer.Serialize(new { path, revision, diagram, start, task, resource, replications = identities.Length }));
    }

    private static void VerifyStructured(JsonElement reply, int count)
    {
        var reports = reply.GetProperty("SimulationReports").EnumerateArray().ToArray();
        if (reports.Length != count) throw new InvalidDataException("Structured MCP simulation reports are missing.");
        foreach (var report in reports)
        {
            var metrics = report.GetProperty("Elements").EnumerateArray().Single(e => e.GetProperty("Kind").GetString() == "Task").GetProperty("Metrics");
            if (metrics.GetProperty("numberOfTokensCompleted").GetString() != "12") throw new InvalidDataException("MCP structured metrics differ from the actual native results.");
        }
    }

    private static void VerifyTiming(string file, int expectedMinutes, bool resources = false, bool calendar = false)
    {
        var xml = XDocument.Load(file);
        // Assert the observed installed-engine schema and quantitative behavior, not just the success envelope.
        var process = xml.Descendants("process").Single(); var task = xml.Descendants("Task").Single();
        double Metric(XElement e, string name) => double.Parse(e.Attribute(name)?.Value ?? throw new InvalidDataException("Missing simulation metric: " + name), CultureInfo.InvariantCulture);
        if (Metric(process, "numberOfProcessesCompleted") != 12 || Metric(task, "numberOfTokensCompleted") != 12 ||
            Metric(task, "averageTimeBusy") != expectedMinutes || Metric(task, "totalTimeBusy") != 12 * expectedMinutes)
            throw new InvalidDataException("Configured trigger count or processing time did not affect real simulation results.");
        if (resources && (Metric(task, "maximumNumberOfResourcesUsed") != 1 || Metric(task, "averageTimeWaitingForResource") <= 0 || Metric(task, "totalCompletionCost") != 84))
            throw new InvalidDataException("Configured resource contention or task costs did not affect the real simulation.");
        if (calendar && Metric(task, "averageTimeWaitingForResource") < 450) throw new InvalidDataException("Calendar did not delay work until the configured 08:00 shift.");
        File.WriteAllText(file + ".verified.json", JsonSerializer.Serialize(new
        {
            completed = 12,
            expectedMinutes,
            resources,
            calendar,
            averageWait = Metric(task, "averageTimeWaitingForResource"),
            completionCost = Metric(task, "totalCompletionCost")
        }));
        Console.WriteLine("simulation_metrics_verified=" + file);
    }
}
