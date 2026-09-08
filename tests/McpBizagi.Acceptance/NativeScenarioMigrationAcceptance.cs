using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

/// <summary>Independent MCP/native scenario migration with before/after real resource and calendar simulations.</summary>
internal static class NativeScenarioMigrationAcceptance
{
    private static string S(JsonElement e, string name) => e.GetProperty(name).GetString()!;
    public static async Task Run(string repo, string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, bool saveResults = false,
        Func<string, Dictionary<string, object?>, Task<JsonElement>>? reject = null)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string name, Dictionary<string, object?> input, string state = "completed")
        {
            string id = S(await call(name, input), "OperationId"); var response = await wait(id, state); exited(id);
            receipts.Add(new { tool = name, id, response });
            File.WriteAllText(Path.Combine(run, "native-scenario-migration.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        File.Copy(Path.Combine(repo, "examples/minimal.bpmn"), Path.Combine(run, "Scenario migration Ω.bpmn"));
        var imported = await Op("native_roundtrip", new() { ["path"] = "Scenario migration Ω.bpmn", ["modelName"] = "Scenario migration Ω" });
        string path = S(imported, "nativeArtifact");
        var read = await Op("native_metadata_get", new() { ["path"] = path }); string revision = S(read, "sourceRevision");
        var graph = read.GetProperty("result").GetProperty("Elements").EnumerateArray().ToArray();
        string sourceDiagram = S(graph.Single(e => S(e, "Kind") == "Collaboration"), "Id");
        var task = graph.Single(e => S(e, "Kind").EndsWith("Task", StringComparison.Ordinal));
        string process = S(task, "ParentId"), taskBpmn = S(task, "BpmnId");
        string sourceProcessBpmn = S(graph.Single(e => S(e, "Id") == process), "BpmnId");
        string start = S(graph.Single(e => S(e, "Kind") == "StartEvent"), "BpmnId");
        string[] selected = graph.Where(e => S(e, "ParentId") == process && S(e, "Kind") != "Lane").Select(e => S(e, "Id")).ToArray();
        async Task<JsonElement> Write(string tool, object patch)
        {
            var result = await Op(tool, new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = patch });
            path = S(result, "outputArtifact"); revision = S(result, "outputRevision"); return result;
        }
        string targetDiagram = Guid.NewGuid().ToString(), unrelatedDiagram = Guid.NewGuid().ToString();
        var diagrams = await Write("native_diagrams_apply", new { Changes = new[] {
            new { Operation = "create", DiagramId = targetDiagram, Name = "Scenario destination 日本語" },
            new { Operation = "create", DiagramId = unrelatedDiagram, Name = "Untouched scenario diagram" } } });
        graph = diagrams.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == targetDiagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string targetProcess = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string targetProcessBpmn = S(graph.Single(e => S(e, "Id") == targetProcess), "BpmnId");
        string role = Guid.NewGuid().ToString();
        var resourceResult = await Write("native_metadata_apply", new { Resources = new[] { new { Id = role, Name = "Migration reviewer Ω", Type = "Role" } },
            Assignments = new[] { new { ElementId = S(task, "Id"), Responsible = new[] { role }, Accountable = new[] { role }, Consulted = new[] { role }, Informed = new[] { role } } } });
        string resource = S(resourceResult.GetProperty("reopened").GetProperty("Metadata").GetProperty("Resources").EnumerateArray().Single(r => S(r, "Id") == role), "BpmnId");
        XNamespace ns = "http://www.bpsim.org/schemas/1.0";
        XElement Parameter(string name, string type, object value, params object[] attributes) => new(ns + name, new XElement(ns + type, new XAttribute("value", value), attributes));
        XElement Globals() => new(ns + "ScenarioParameters", new XAttribute("replication", 1), new XAttribute("seed", 123), new XAttribute("baseTimeUnit", "min"), new XAttribute("baseCurrencyUnit", "USD"),
            Parameter("Start", "DateTimeParameter", "2026-09-07T00:00:00"), Parameter("Duration", "DurationParameter", "P1D"), new XElement(ns + "PropertyParameters"));
        XElement Scene(string id, bool configured, bool calendar = false) => new(ns + "Scenario", new XAttribute("id", id), new XAttribute("name", id),
            new XAttribute("author", "h0w4r"), new XAttribute("version", "1.0"), Globals(), configured ? new object[] {
                new XElement(ns + "ElementParameters", new XAttribute("elementRef", start), new XElement(ns + "ControlParameters", Parameter("TriggerCount", "NumericParameter", 12)), new XElement(ns + "PropertyParameters")),
                new XElement(ns + "ElementParameters", new XAttribute("elementRef", taskBpmn), new XElement(ns + "TimeParameters", Parameter("ProcessingTime", "FloatingParameter", 3)),
                    new XElement(ns + "ResourceParameters", Parameter("Selection", "ExpressionParameter", "bpsim:getResource(\"" + resource + "\",1)")),
                    new XElement(ns + "CostParameters", Parameter("FixedCost", "FloatingParameter", 7)), new XElement(ns + "PropertyParameters")),
                new XElement(ns + "ElementParameters", new XAttribute("elementRef", resource), new XElement(ns + "ResourceParameters", new XElement(ns + "Quantity",
                    new XElement(ns + "NumericParameter", new XAttribute("value", calendar ? 0 : 2)), calendar ? new XElement(ns + "NumericParameter", new XAttribute("value", 2), new XAttribute("validFor", "Work")) : null)),
                    new XElement(ns + "PropertyParameters"))
            } : [], calendar ? new XElement(ns + "Calendar", new XAttribute("id", "Work"), new XAttribute("name", "Working day Ω"),
                "BEGIN:VCALENDAR\nBEGIN:VEVENT\nDTSTART:20260907T080000\nDTEND:20260907T170000\nRRULE:FREQ=DAILY;INTERVAL=1\nEND:VEVENT\nPRODID:MCP-Bizagi acceptance\nVERSION:2.0\nEND:VCALENDAR\n") : null);
        XElement Root(params XElement[] scenes) => new(ns + "BPSimData", new XAttribute("simulationLevel", "LevelFour"), scenes);
        var inherited = Scene("Inherited", false); inherited.SetAttributeValue("inherits", "Resources");
        var targetInherited = Scene("TargetInherited", false); targetInherited.SetAttributeValue("inherits", "TargetResources");
        var config = Root(Scene("Resources", true), Scene("Calendar", true, true), inherited);
        var targetConfig = Root(Scene("TargetResources", false), Scene("TargetCalendar", false), targetInherited);
        await Write("native_metadata_apply", new { Simulations = new[] { new { DiagramId = sourceDiagram, Xml = config.ToString() }, new { DiagramId = targetDiagram, Xml = targetConfig.ToString() } }, DiscardSimulationResults = true });
        string originalPath = path, originalRevision = revision;
        var expectedSaved = new Dictionary<(string Diagram, string Scenario), string>();
        void VerifySaved(JsonElement saved, Dictionary<(string Diagram, string Scenario), string> expected)
        {
            // Decode the actual V5 archive independently of the production policy.
            string file = saved.GetProperty("edited").GetProperty("Artifacts").EnumerateArray().Single().GetString()!;
            using var zip = ZipFile.OpenRead(file);
            var actual = new Dictionary<(string Diagram, string Scenario), string>();
            foreach (var entry in zip.Entries.Where(e => e.Name.EndsWith(".diag", StringComparison.OrdinalIgnoreCase)))
            {
                using var nestedStream = entry.Open(); using var nested = new ZipArchive(nestedStream, ZipArchiveMode.Read);
                var results = nested.GetEntry("BPSimDataResult.xml"); if (results == null) continue;
                using var xmlStream = results.Open(); var xml = XDocument.Load(xmlStream);
                foreach (var record in xml.Root!.Elements("Result")) actual.Add((Path.GetFileNameWithoutExtension(entry.Name), (string)record.Attribute("scenarioId")!), record.Value);
            }
            if (actual.Count != expected.Count || expected.Any(e => !actual.TryGetValue(e.Key, out var value) || value != e.Value))
                throw new InvalidDataException("Independent durable saved-result payloads changed.");
            foreach (string observation in new[] { "edited", "reopened" })
            {
                var records = saved.GetProperty(observation).GetProperty("SavedSimulationResults").EnumerateArray().ToArray();
                if (records.Length != expected.Count || expected.Any(e => records.Count(r => S(r, "DiagramId") == e.Key.Diagram && S(r, "ScenarioId") == e.Key.Scenario &&
                    S(r, "Sha256") == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(e.Value))).ToLowerInvariant() && r.GetProperty("CharacterCount").GetInt32() == e.Value.Length) != 1))
                    throw new InvalidDataException("Independent native saved-result property readback changed.");
            }
        }
        if (saveResults)
        {
            if (reject == null) throw new InvalidOperationException("Saved-result acceptance requires protocol-rejection verification.");
            await reject("native_simulate", new() { ["path"] = path, ["diagramId"] = sourceDiagram, ["saveResultsAsNativeCopy"] = true });
            await reject("native_simulate", new() { ["path"] = path, ["diagramId"] = sourceDiagram, ["scenarioId"] = "Missing", ["saveResultsAsNativeCopy"] = true });
            // Also traverse the actual native failure path, then recover with
            // the same untouched model rather than substituting a fake result.
            await Op("native_simulate", new() { ["path"] = path, ["diagramId"] = sourceDiagram, ["scenarioId"] = "Missing" }, "failed");
        }
        async Task Simulate(string diagram, string scenario, int level)
        {
            var result = await Op("native_simulate", new() { ["path"] = path, ["diagramId"] = diagram, ["scenarioId"] = scenario, ["simulationLevel"] = level, ["saveResultsAsNativeCopy"] = saveResults });
            string file = result.GetProperty("result").GetProperty("Artifacts").EnumerateArray().Select(e => e.GetString()!).Single(p => Path.GetFileName(p) == "Results.xml");
            NativeMetadataAcceptance.VerifyTiming(file, 3, resources: true, calendar: level == 4,
                processBpmnId: diagram == sourceDiagram ? sourceProcessBpmn : targetProcessBpmn, taskBpmnId: taskBpmn);
            if (saveResults)
            {
                var xml = new XmlDocument { XmlResolver = null }; xml.Load(file);
                expectedSaved[(diagram, scenario)] = xml.OuterXml;
                var saved = result.GetProperty("savedNative"); VerifySaved(saved, expectedSaved);
                path = S(saved, "outputArtifact"); revision = S(saved, "outputRevision");
                var historical = await Op("native_simulation_results_get", new() { ["path"] = path, ["diagramId"] = diagram, ["scenarioId"] = scenario });
                string exported = historical.GetProperty("result").GetProperty("Artifacts").EnumerateArray().Single().GetString()!;
                if (!XNode.DeepEquals(XDocument.Parse(xml.OuterXml).Root, XDocument.Load(exported).Root) || S(historical, "sourceRevision") != revision)
                    throw new InvalidDataException("Historical native result export differs from the persisted simulation.");
                NativeMetadataAcceptance.VerifyTiming(exported, 3, resources: true, calendar: level == 4,
                    processBpmnId: diagram == sourceDiagram ? sourceProcessBpmn : targetProcessBpmn, taskBpmnId: taskBpmn);
                var report = historical.GetProperty("result").GetProperty("SimulationReports").EnumerateArray().Single();
                var metrics = report.GetProperty("Elements").EnumerateArray().Single(e => S(e, "Id") == taskBpmn && S(e, "Kind") == "Task").GetProperty("Metrics");
                var taskXml = XDocument.Load(exported).Descendants("element").Single(e => (string?)e.Attribute("name") == taskBpmn).Element("Task")!;
                if (S(report, "ScenarioId") != scenario || taskXml.Attributes().Any(a => S(metrics, a.Name.ToString()) != a.Value))
                    throw new InvalidDataException("Structured historical MCP metrics differ from the actual saved native XML.");
            }
        }
        await Simulate(sourceDiagram, "Resources", 3); await Simulate(sourceDiagram, "Calendar", 4);
        if (saveResults)
        {
            // Replacing one existing result must retain the second scenario's opaque result exactly.
            await Simulate(sourceDiagram, "Resources", 3);
            var savedCopy = await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
            VerifySaved(savedCopy, expectedSaved);
            originalPath = path; originalRevision = revision;
            var blocked = await Op("native_metadata_apply", new() { ["path"] = path, ["expectedRevision"] = revision,
                ["patch"] = new { Simulations = new[] { new { DiagramId = sourceDiagram, Xml = config.ToString() } } } }, "failed");
            if (!S(blocked, "Error").Contains("saved results", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Configuration replacement failed for an unrelated reason.");
            // Exercise existing explicit replacement/discard on a separate copy.
            // Keep the genuine result-bearing input for subsequent migration tests.
            var discarded = await Op("native_metadata_apply", new() { ["path"] = path, ["expectedRevision"] = revision,
                ["patch"] = new { Simulations = new[] { new { DiagramId = sourceDiagram, Xml = config.ToString() } }, DiscardSimulationResults = true } });
            VerifySaved(discarded, new());
        }
        object Map(string from, string to, bool reverse = false) => new { SourceDiagramId = reverse ? targetDiagram : sourceDiagram, SourceScenarioId = from,
            TargetDiagramId = reverse ? sourceDiagram : targetDiagram, TargetScenarioId = to };
        object[] Mappings(bool reverse) => reverse ? [Map("TargetResources", "Resources", true), Map("TargetCalendar", "Calendar", true), Map("TargetInherited", "Inherited", true)]
            : [Map("Resources", "TargetResources"), Map("Calendar", "TargetCalendar"), Map("Inherited", "TargetInherited")];
        Dictionary<string, object?> Request(bool reverse, object? migration) => new() { ["path"] = path, ["expectedRevision"] = revision, ["simulationMigration"] = migration,
            ["moves"] = selected.Select(id => new { ElementId = id, ExpectedParentId = reverse ? targetProcess : process, TargetParentId = reverse ? process : targetProcess,
                ExpectedDiagramId = reverse ? targetDiagram : sourceDiagram, TargetDiagramId = reverse ? sourceDiagram : targetDiagram }).ToArray() };
        async Task Rejected(object? migration, string message)
        {
            var failed = await Op("native_elements_reparent", Request(false, migration), "failed");
            if (!S(failed, "Error").Contains(message, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Scenario migration rejected for an unrelated reason: " + S(failed, "Error"));
        }
        await Rejected(null, "simulation");
        await Rejected(new { Mappings = Mappings(false), CopyMissingDependencies = false }, "CopyMissingDependencies");
        await Rejected(new { Mappings = Mappings(false).Take(2).ToArray(), CopyMissingDependencies = true }, "complete scenario");
        if (saveResults) await Rejected(new { Mappings = Mappings(false), CopyMissingDependencies = true }, "simulation results");
        var forward = await Op("native_elements_reparent", Request(false, new { Mappings = Mappings(false), CopyMissingDependencies = true, DiscardSimulationResults = saveResults }));
        if (saveResults) { expectedSaved.Clear(); VerifySaved(forward, expectedSaved); }
        path = S(forward, "outputArtifact"); revision = S(forward, "outputRevision");
        if (saveResults)
            await Op("native_simulation_results_get", new() { ["path"] = path, ["diagramId"] = targetDiagram, ["scenarioId"] = "TargetResources" }, "failed");
        var before = forward.GetProperty("before").GetProperty("Metadata").GetProperty("Simulations").EnumerateArray().ToDictionary(e => S(e, "DiagramId"), e => XDocument.Parse(S(e, "Xml")));
        var after = forward.GetProperty("reopened").GetProperty("Metadata").GetProperty("Simulations").EnumerateArray().ToDictionary(e => S(e, "DiagramId"), e => XDocument.Parse(S(e, "Xml")));
        XElement Find(Dictionary<string, XDocument> docs, string diagram, string scenario) => docs[diagram].Descendants(ns + "Scenario").Single(e => (string?)e.Attribute("id") == scenario);
        foreach (var pair in new[] { ("Resources", "TargetResources"), ("Calendar", "TargetCalendar") })
        {
            var old = Find(before, sourceDiagram, pair.Item1); var current = Find(after, targetDiagram, pair.Item2);
            foreach (var record in old.Elements().Where(e => e.Name == ns + "ElementParameters" || e.Name == ns + "Calendar"))
            {
                string identity = record.Name == ns + "Calendar" ? "id" : "elementRef";
                var found = current.Elements(record.Name).Single(e => (string?)e.Attribute(identity) == (string?)record.Attribute(identity));
                if (!XNode.DeepEquals(record, found)) throw new InvalidDataException("Native migrated parameter or copied dependency changed its content.");
            }
            var retained = Find(after, sourceDiagram, pair.Item1).Elements(ns + "ElementParameters").ToArray();
            if (retained.Length != 1 || (string?)retained[0].Attribute("elementRef") != resource) throw new InvalidDataException("Source parameters were not moved exactly; resource context must remain.");
        }
        if ((string?)Find(after, targetDiagram, "TargetInherited").Attribute("inherits") != "TargetResources" ||
            !XNode.DeepEquals(before[unrelatedDiagram], after[unrelatedDiagram])) throw new InvalidDataException("Inheritance or unrelated configuration changed.");
        await Simulate(targetDiagram, "TargetResources", 3); await Simulate(targetDiagram, "TargetCalendar", 4);
        var noOp = await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        if (saveResults) VerifySaved(noOp, expectedSaved);
        var backward = await Op("native_elements_reparent", Request(true, new { Mappings = Mappings(true), CopyMissingDependencies = false, DiscardSimulationResults = saveResults }));
        if (saveResults) { expectedSaved.Clear(); VerifySaved(backward, expectedSaved); }
        path = S(backward, "outputArtifact"); revision = S(backward, "outputRevision");
        var restored = backward.GetProperty("reopened").GetProperty("Metadata").GetProperty("Simulations").EnumerateArray().Single(e => S(e, "DiagramId") == sourceDiagram);
        // Native collection order is preserved for existing target records; the
        // inverse appends returning parameters after retained resource context.
        foreach (var scene in before[sourceDiagram].Descendants(ns + "Scenario"))
        {
            var actual = XDocument.Parse(S(restored, "Xml")).Descendants(ns + "Scenario").Single(e => (string?)e.Attribute("id") == (string?)scene.Attribute("id"));
            foreach (var expected in scene.Elements(ns + "ElementParameters"))
                if (!XNode.DeepEquals(expected, actual.Elements(ns + "ElementParameters").Single(e => (string?)e.Attribute("elementRef") == (string?)expected.Attribute("elementRef"))))
                    throw new InvalidDataException("Inverse migration changed a source parameter.");
        }
        var original = await Op("native_inspect", new() { ["path"] = originalPath });
        if (S(original, "sourceRevision") != originalRevision) throw new InvalidDataException("Scenario migration modified the original input.");
    }
}
