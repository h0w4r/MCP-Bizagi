using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

/// <summary>All directed task/gateway pairs through the real MCP client, installed command and fresh process readers.</summary>
internal static class NativeConversionAcceptance
{
    // Independent wire DTO and palette: acceptance never calls the production policy or adapter.
    private sealed record Conversion(string ElementId, string ExpectedType, string TargetType);
    private static readonly string[] Tasks = ["AbstractTask", "UserTask", "ManualTask", "ServiceTask", "ScriptTask", "SendTask", "ReceiveTask", "BusinessRuleTask"];
    private static readonly string[] Gateways = ["ExclusiveGateway", "InclusiveGateway", "ParallelGateway", "ComplexGateway", "EventBasedGateway", "EventBasedGatewayExclusive", "EventBasedGatewayParallel"];
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error, bool taskToCall = false)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = S(await call(tool, args), "OperationId"); var response = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-conversions.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = taskToCall ? new[] { "Native conversion Ω", "Explicit call target Ω" } : new[] { "Native conversion Ω" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(graph.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Name") == "Native conversion Ω"), "Id");
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string process = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        var conversions = new List<Conversion>(); var mutations = new List<object>();
        mutations.Add(new { Operation = "update", ElementId = pool, Geometry = new { X = 30, Y = 30, Width = 2000, Height = 2400 } });
        int index = 0;
        foreach (var family in taskToCall ? new[] { Tasks } : new[] { Tasks, Gateways })
            foreach (string source in family)
                foreach (string target in taskToCall ? new[] { "CallActivity" } : family.Where(t => t != source))
                {
                    string id = Guid.NewGuid().ToString(); int x = 100 + index % 8 * 210, y = 100 + index / 8 * 140; index++;
                    conversions.Add(new(id, source, target));
                    mutations.Add(new { Operation = "create", ElementId = id, ParentId = process, ElementType = source,
                        Name = source + " to " + target + " Ω", Documentation = "Conversion must preserve this documentation Ω",
                        Geometry = new { X = x, Y = y, Width = 140, Height = 70 },
                        ActivityProperties = family == Tasks ? new { StartQuantity = 2, CompletionQuantity = 3 } : null,
                        ActivityLoop = family != Tasks ? null : index % 2 == 0
                            ? (object)new { Kind = "Standard", Standard = new { Maximum = 4, Counter = 1, TestBefore = true, Condition = "attempt < 4 & 日本語 Ω" } }
                            : new { Kind = "MultiInstance", MultiInstance = new { IsSequential = true, Counter = 3, Behavior = "All", CompletionCondition = "complete > 2 & Ω" } },
                        Style = new { FontSize = 12, Bold = true, LabelBounds = new { X = x + 2, Y = y + 2, Width = 130, Height = 40 } } });
                }
        string sub = Guid.NewGuid().ToString(), nested = Guid.NewGuid().ToString();
        mutations.Add(new { Operation = "create", ElementId = sub, ParentId = process, ElementType = "SubProcess", Name = "Nested Ω", Geometry = new { X = 100, Y = 2100, Width = 400, Height = 200 } });
        mutations.Add(new { Operation = "create", ElementId = nested, ParentId = sub, ElementType = "UserTask", Name = "Nested task Ω", Geometry = new { X = 50, Y = 50, Width = 140, Height = 70 } });
        conversions.Add(new(nested, "UserTask", taskToCall ? "CallActivity" : "ManualTask"));
        // Incident sequence flows exercise native endpoint rewiring, not just disconnected palette objects.
        mutations.Add(new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = process, ElementType = "SequenceFlow",
            SourceId = conversions[0].ElementId, TargetId = conversions[1].ElementId, Name = "Preserved connection Ω",
            Points = new[] { new { X = 240, Y = 135 }, new { X = 310, Y = 135 } } });
        if (taskToCall)
        {
            // Associations create actual native activity I/O identities. Boundary
            // and compensation events retain object references across replacement.
            foreach (var scope in new[] { (Parent: process, Task: conversions[1].ElementId), (Parent: sub, Task: nested) })
            {
                string data = Guid.NewGuid().ToString();
                mutations.Add(new { Operation = "create", ElementId = data, ParentId = scope.Parent, ElementType = "DataObject", Name = "Call input 日本語",
                    DataProperties = new { State = "Ready Ω", IsCollection = true }, Geometry = new { X = 250, Y = 50, Width = 60, Height = 60 } });
                mutations.Add(new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = scope.Parent, ElementType = "Association",
                    SourceId = data, TargetId = scope.Task, Points = new[] { new { X = 250, Y = 80 }, new { X = 190, Y = 80 } } });
                mutations.Add(new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = scope.Parent, ElementType = "TimerIntermediate", EventMode = "Boundary",
                    EventProperties = new { AttachedToActivityId = scope.Task, IsInterrupting = true }, Geometry = new { X = 155, Y = 110, Width = 30, Height = 30 } });
                mutations.Add(new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = scope.Parent, ElementType = "CompensationEnd", Name = "Compensate Ω",
                    EventPayloads = new[] { new { Kind = "Compensation", Compensation = new { ActivityId = scope.Task, WaitForCompletion = true } } },
                    Geometry = new { X = 350, Y = 120, Width = 30, Height = 30 } });
            }
        }
        var seeded = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = mutations.ToArray() });
        path = S(seeded, "outputArtifact"); revision = S(seeded, "outputRevision");
        // Rich content is authored through native MCP tools, never injected into a fabricated ZIP.
        string documentedTask = conversions.Single(c => c.ExpectedType == "UserTask" && c.TargetType == (taskToCall ? "CallActivity" : "ServiceTask") && c.ElementId != nested).ElementId;
        string role = Guid.NewGuid().ToString(), textDefinition = Guid.NewGuid().ToString(), fileDefinition = Guid.NewGuid().ToString();
        async Task Patch(string tool, object patch)
        {
            var result = await Op(tool, new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = patch });
            path = S(result, "outputArtifact"); revision = S(result, "outputRevision");
        }
        await Patch("native_metadata_apply", new { Resources = new[] { new { Id = role, Name = "Conversion reviewer Ω", Type = "Role" } } });
        await Patch("native_metadata_apply", new { Assignments = new[] { new { ElementId = documentedTask, Responsible = new[] { role }, Accountable = new[] { role }, Consulted = new[] { role }, Informed = new[] { role } } } });
        XElement Definition(string id, string type) => new("ExtendedAttribute", new XAttribute("Id", id), new XAttribute("Type", type),
            new XAttribute("ExportAsTable", false), new XAttribute("Visible", false), new XElement("Name", "Conversion " + type + " Ω"),
            new XElement("Description", "Must survive native type changes"), new XElement("Options"), new XElement("TableColumns"),
            new XElement("ElementTypes", Tasks.Concat(taskToCall ? new[] { "CallActivity" } : []).Select(t => new XElement("AttributeElementType", new XAttribute("Type", t)))));
        var textXml = Definition(textDefinition, "Text");
        if (taskToCall) textXml.Element("ElementTypes")!.Elements().Where(e => (string?)e.Attribute("Type") == "CallActivity").Remove();
        await Patch("native_attributes_apply", new { Definitions = new[] { new { Id = textDefinition, Xml = textXml.ToString() }, new { Id = fileDefinition, Xml = Definition(fileDefinition, "FileEmbedded").ToString() } } });
        const string fileName = "Conversion evidence Ω.xml";
        byte[] payload = Encoding.UTF8.GetBytes("Own non-XML attachment bytes 日本語 Ω\n");
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        var values = new XElement("ElementAttributeValues", new XAttribute("ElementId", documentedTask), new XElement("Values",
            new XElement("ExtendedAttributeValue", new XAttribute("Id", textDefinition), new XAttribute("Type", "Text"),
                new XElement("Content", "Preserve 日本語 Ω"), new XElement("DisplayValue", "Preserve 日本語 Ω"), new XElement("TableValues")),
            new XElement("ExtendedAttributeValue", new XAttribute(xsi + "type", "AttachmentAttributeValue"), new XAttribute("Id", fileDefinition), new XAttribute("Type", "FileEmbedded"),
                new XElement("Content", "attachment:" + fileName), new XElement("DisplayValue", fileName), new XElement("TableValues"), new XElement("DisplayName", fileName))));
        await Patch("native_attributes_apply", new { Values = new[] { new { DiagramId = diagram, ElementId = documentedTask, Xml = values.ToString() } },
            Attachments = new[] { new { DiagramId = diagram, ElementId = documentedTask, FileName = fileName, DataBase64 = Convert.ToBase64String(payload) } } });
        if (taskToCall)
        {
            // A real native definition initially excludes the destination type.
            // Verify rejection and explicitly broaden it through MCP before retrying.
            var scopeError = await error("native_elements_convert", new() { ["path"] = path, ["expectedRevision"] = revision, ["changes"] = conversions.ToArray() });
            if (!scopeError.GetRawText().Contains("definition scopes", StringComparison.Ordinal)) throw new InvalidDataException("Wrong attribute-scope rejection cause.");
            await Patch("native_attributes_apply", new { Definitions = new[] { new { Id = textDefinition, Xml = Definition(textDefinition, "Text").ToString() } } });
        }
        var baseline = await Op("native_attributes_get", new() { ["path"] = path });
        var beforeGraph = baseline.GetProperty("result").GetProperty("Elements");
        var beforeDocumentation = baseline.GetProperty("result").GetProperty("Documentation");
        var metadataBaseline = await Op("native_metadata_get", new() { ["path"] = path });
        var beforeMetadata = metadataBaseline.GetProperty("result").GetProperty("Metadata");
        async Task VerifyRichContent(JsonElement result)
        {
            var after = result.GetProperty("reopened");
            // Independent wire-level assertions supplement, rather than reuse, the production gate.
            foreach (var node in beforeGraph.EnumerateArray())
            {
                var actual = after.GetProperty("Elements").EnumerateArray().Single(e => S(e, "Id") == S(node, "Id"));
                foreach (string field in new[] { "ActivityLoop", "ActivityProperties", "Documentation", "Style", "ParentId", "SourceRef", "TargetRef", "DataFlow", "Event", "DefaultSequenceFlowIds", "Geometry", "Points" })
                    if (!JsonElement.DeepEquals(node.GetProperty(field), actual.GetProperty(field))) throw new InvalidDataException("Conversion changed rich graph content: " + field);
            }
            // Specialized reads populate documentation and metadata; an ordinary graph inspection
            // intentionally omits those snapshots. Never accept null-versus-null as preservation.
            var documentationRead = await Op("native_attributes_get", new() { ["path"] = path });
            var metadataRead = await Op("native_metadata_get", new() { ["path"] = path });
            if (beforeDocumentation.ValueKind != JsonValueKind.Object || beforeMetadata.ValueKind != JsonValueKind.Object ||
                !DocumentationEquivalent(beforeDocumentation, documentationRead.GetProperty("result").GetProperty("Documentation")) ||
                !JsonElement.DeepEquals(beforeMetadata, metadataRead.GetProperty("result").GetProperty("Metadata")))
                throw new InvalidDataException("Conversion changed native attributes, attachment metadata or RACI.");
            var exported = await Op("native_attachment_export", new() { ["path"] = path, ["diagramId"] = diagram, ["elementId"] = documentedTask, ["fileName"] = fileName });
            if (!File.ReadAllBytes(S(exported, "artifactPath")).SequenceEqual(payload)) throw new InvalidDataException("Conversion changed embedded attachment bytes.");
        }
        string original = path, originalRevision = revision;
        var converted = await Op("native_elements_convert", new() { ["path"] = path, ["expectedRevision"] = revision, ["changes"] = conversions.ToArray() });
        path = S(converted, "outputArtifact"); revision = S(converted, "outputRevision");
        if (converted.GetProperty("requestedChangesVerified").GetInt32() != (taskToCall ? 9 : 99)) throw new InvalidDataException("Missing directed conversion pairs.");
        await VerifyRichContent(converted);
        var reverse = conversions.Select(c => new Conversion(c.ElementId, c.TargetType, c.ExpectedType)).ToArray();
        // A stale type and a stale revision must be rejected through actual MCP preflight.
        var revisionError = await error("native_elements_convert", new() { ["path"] = path, ["expectedRevision"] = originalRevision,
            ["changes"] = taskToCall ? conversions.ToArray() : reverse });
        if (!revisionError.GetRawText().Contains("Revision conflict", StringComparison.Ordinal)) throw new InvalidDataException("Wrong stale-revision rejection cause.");
        await error("native_elements_convert", new() { ["path"] = path, ["expectedRevision"] = revision, ["changes"] = conversions.ToArray() });
        await error("native_elements_convert", new() { ["path"] = path, ["expectedRevision"] = revision, ["changes"] = new[] { new { ElementId = reverse[0].ElementId, ExpectedType = "UserTask", TargetType = "ParallelGateway" } } });
        if (taskToCall)
        {
            // Conversion must not secretly extract another process or bind a target.
            var convertedGraph = converted.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
            if (!graph.Where(e => S(e, "Kind") == "Collaboration").Select(e => S(e, "Id")).Order().SequenceEqual(
                convertedGraph.Where(e => S(e, "Kind") == "Collaboration").Select(e => S(e, "Id")).Order()))
                throw new InvalidDataException("Task-to-call created or removed a diagram.");
            foreach (var change in conversions)
            {
                var element = convertedGraph.Single(e => S(e, "Id") == change.ElementId);
                var reference = element.GetProperty("CallReference");
                if (S(element, "Kind") != "CallActivity" || new[] { "CatalogProcessId", "BpmnName", "BpmnNamespace" }.Any(f => S(reference, f) != "") ||
                    reference.GetProperty("External").ValueKind != JsonValueKind.Null)
                    throw new InvalidDataException("Task-to-call did not produce an explicit unbound call.");
            }
            await error("native_elements_convert", new() { ["path"] = path, ["expectedRevision"] = revision, ["changes"] = reverse });
            string targetDiagram = S(graph.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Name") == "Explicit call target Ω"), "Id");
            string targetPool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == targetDiagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
            string targetProcess = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == targetPool), "Id");
            // Binding uses the existing typed mutation contract, in its own durable transaction.
            foreach (string binding in new[] { targetProcess, "" })
            {
                var bound = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision,
                    ["mutations"] = new[] { documentedTask, nested }.Select(id => new { Operation = "update", ElementId = id, CallTarget = new { ProcessId = binding } }).ToArray() });
                path = S(bound, "outputArtifact"); revision = S(bound, "outputRevision");
                foreach (string id in new[] { documentedTask, nested })
                    if (S(bound.GetProperty("reopened").GetProperty("Elements").EnumerateArray().Single(e => S(e, "Id") == id).GetProperty("CallReference"), "CatalogProcessId") != binding)
                        throw new InvalidDataException("Explicit call binding did not survive a fresh worker.");
                await VerifyRichContent(bound);
            }
        }
        else
        {
            var restored = await Op("native_elements_convert", new() { ["path"] = path, ["expectedRevision"] = revision, ["changes"] = reverse });
            path = S(restored, "outputArtifact"); revision = S(restored, "outputRevision");
            await VerifyRichContent(restored);
        }
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram, ["subProcessId"] = sub });
        var untouched = await Op("native_inspect", new() { ["path"] = original });
        if (S(untouched, "sourceRevision") != originalRevision) throw new InvalidDataException("Conversion changed the original model.");
    }
    private static bool DocumentationEquivalent(JsonElement before, JsonElement after)
    {
        // Native persistence updates definition audit timestamps even when values are unchanged.
        // Verify real timestamps, then compare every other snapshot field without a production policy call.
        var left = JsonNode.Parse(before.GetRawText())!; var right = JsonNode.Parse(after.GetRawText())!;
        foreach (var side in new[] { left, right })
            foreach (var definition in side["Definitions"]!.AsArray())
            {
                var xml = XElement.Parse(definition!["Xml"]!.GetValue<string>());
                if (xml.Name != "ExtendedAttribute" || (string?)xml.Attribute("Id") != definition["Id"]!.GetValue<string>()) return false;
                var date = xml.Attribute("ModificationDate");
                if (date == null || !DateTimeOffset.TryParse(date.Value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out _)) return false;
                date.Remove(); definition["Xml"] = xml.ToString(SaveOptions.DisableFormatting);
            }
        return JsonNode.DeepEquals(left, right);
    }
    private static string S(JsonElement e, string name) => e.GetProperty(name).GetString()!;
}
