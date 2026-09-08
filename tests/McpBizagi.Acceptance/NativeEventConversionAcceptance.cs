using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Xml.Linq;

/// <summary>Independent event conversion wire client: no production policy, fixture archive, or adapter calls.</summary>
internal static class NativeEventConversionAcceptance
{
    private sealed record Conversion(string ElementId, string ExpectedType, string TargetType, string ExpectedEventMode);
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = S(await call(tool, args), "OperationId"); var response = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-event-conversions.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Native event conversion Ω" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(graph.Single(e => S(e, "Kind") == "Collaboration"), "Id");
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string process = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        string sub = Guid.NewGuid().ToString(), transaction = Guid.NewGuid().ToString();
        var changes = new List<Conversion>(); var nodes = new List<object>
        {
            new { Operation = "update", ElementId = pool, Geometry = new { X = 30, Y = 30, Width = 3000, Height = 5000 } },
            new { Operation = "create", ElementId = sub, ParentId = process, ElementType = "SubProcess", Name = "Event context Ω", SubProcessProperties = new { TriggeredByEvent = true }, Geometry = new { X = 100, Y = 100, Width = 200, Height = 150 } },
            new { Operation = "create", ElementId = transaction, ParentId = process, ElementType = "SubProcess", SubProcessKind = "Transaction", Name = "Transaction Ω", Geometry = new { X = 500, Y = 100, Width = 200, Height = 150 } }
        };
        var families = new[]
        {
            (Mode: "Start", Types: new[] { "NoneStart", "MessageStart", "TimerStart", "ConditionalStart", "SignalStart", "MultipleStart", "ParallelMultipleStart", "ErrorStart", "EscalationStart", "CompensationStart" }),
            (Mode: "End", Types: new[] { "NoneEnd", "MessageEnd", "TerminateEnd", "EscalationEnd", "ErrorEnd", "CompensationEnd", "SignalEnd", "MultipleEnd", "CancelEnd" }),
            (Mode: "Catch", Types: new[] { "MessageIntermediate", "TimerIntermediate", "ConditionalIntermediate", "LinkIntermediate", "SignalIntermediate", "MultipleIntermediate", "ParallelMultipleIntermediate" }),
            (Mode: "Throw", Types: new[] { "NoneIntermediate", "MessageIntermediate", "EscalationIntermediate", "LinkIntermediate", "CompensationIntermediate", "SignalIntermediate", "MultipleIntermediate" }),
            (Mode: "Boundary", Types: new[] { "MessageIntermediate", "TimerIntermediate", "EscalationIntermediate", "ConditionalIntermediate", "ErrorIntermediate", "CompensationIntermediate", "SignalIntermediate", "MultipleIntermediate", "ParallelMultipleIntermediate", "CancelIntermediate" })
        };
        int index = 0;
        foreach (var family in families)
            foreach (string source in family.Types)
                foreach (string target in family.Types.Where(t => t != source))
                {
                    string id = Guid.NewGuid().ToString(); int x = 70 + index % 12 * 110, y = 60 + index / 12 * 80; index++;
                    changes.Add(new(id, source, target, family.Mode));
                    nodes.Add(new { Operation = "create", ElementId = id, ParentId = family.Mode == "Start" ? sub : family.Mode == "End" ? transaction : process,
                        ElementType = source, EventMode = family.Mode is "Start" or "End" ? null : family.Mode,
                        EventProperties = family.Mode == "Boundary" ? new { AttachedToActivityId = transaction, IsInterrupting = true } : null,
                        Name = source + " -> " + target + " " + family.Mode + " Ω", Documentation = "Retain event documentation 日本語 Ω",
                        Geometry = new { X = x, Y = y, Width = 40, Height = 40 }, Style = new { FontSize = 12, Bold = true, LabelBounds = new { X = x, Y = y + 42, Width = 100, Height = 25 } } });
                }
        // Real incident connections exercise endpoint replacement. Role never changes.
        string sourceNode = changes.First(c => c.ExpectedEventMode == "Catch").ElementId;
        string targetNode = changes.First(c => c.ExpectedEventMode == "Throw").ElementId;
        nodes.Add(new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = process, ElementType = "SequenceFlow", SourceId = sourceNode, TargetId = targetNode,
            Points = new[] { new { X = 110, Y = 450 }, new { X = 200, Y = 450 } }, Name = "Unchanged endpoints Ω" });
        // Native event I/O survives same-role replacement: catch produces and
        // throw consumes data. These associations exercise actual generated ports.
        string data = Guid.NewGuid().ToString();
        nodes.Add(new { Operation = "create", ElementId = data, ParentId = process, ElementType = "DataObject", Name = "Event data 日本語", Geometry = new { X = 1600, Y = 400, Width = 60, Height = 70 } });
        nodes.Add(new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = process, ElementType = "Association", SourceId = sourceNode, TargetId = data,
            Points = new[] { new { X = 110, Y = 450 }, new { X = 1600, Y = 450 } } });
        nodes.Add(new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = process, ElementType = "Association", SourceId = data, TargetId = targetNode,
            Points = new[] { new { X = 1660, Y = 450 }, new { X = 200, Y = 450 } } });
        foreach (var extra in new[] { (Type: "TimerStart", Target: "SignalStart", Mode: "Start", Parent: process),
            (Type: "TimerIntermediate", Target: "SignalIntermediate", Mode: "Boundary", Parent: process),
            (Type: "MessageIntermediate", Target: "SignalIntermediate", Mode: "Catch", Parent: sub) })
        {
            string id = Guid.NewGuid().ToString();
            changes.Add(new(id, extra.Type, extra.Target, extra.Mode));
            nodes.Add(new { Operation = "create", ElementId = id, ParentId = extra.Parent, ElementType = extra.Type, EventMode = extra.Mode == "Start" ? null : extra.Mode,
                Name = "Extra root/nested noninterrupting coverage Ω", Geometry = new { X = 1800, Y = 600 + changes.Count * 2, Width = 40, Height = 40 },
                EventProperties = extra.Mode == "Boundary" ? new { AttachedToActivityId = transaction, IsInterrupting = false } : null });
        }
        var seed = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = nodes.ToArray() });
        path = S(seed, "outputArtifact"); revision = S(seed, "outputRevision");
        string original = path, originalRevision = revision;

        var stale = await error("native_elements_convert", new() { ["path"] = path, ["expectedRevision"] = "stale", ["changes"] = changes.ToArray() });
        if (!stale.GetRawText().Contains("Revision conflict", StringComparison.Ordinal)) throw new InvalidDataException("Wrong stale conversion rejection.");
        await error("native_elements_convert", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["changes"] = new[] { new { ElementId = sourceNode, ExpectedType = "MessageIntermediate", TargetType = "TimerIntermediate" } } });

        // A configured payload must cause an explicit refusal, then an explicit
        // native clear may enable the operation. No archive is fabricated or patched.
        string configuredId = changes.First(c => c.ExpectedType == "TimerStart").ElementId;
        async Task SetTimer(string kind, string text)
        {
            var result = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision,
                ["mutations"] = new[] { new { Operation = "update", ElementId = configuredId, EventPayloads = new[] { new { Kind = "Timer", Timer = new { Kind = kind, Text = text } } } } } });
            path = S(result, "outputArtifact"); revision = S(result, "outputRevision");
        }
        await SetTimer("Cycle", "R3/PT1M");
        var configured = await error("native_elements_convert", new() { ["path"] = path, ["expectedRevision"] = revision, ["changes"] = changes.ToArray() });
        if (!configured.GetRawText().Contains("definition payloads", StringComparison.Ordinal)) throw new InvalidDataException("Wrong configured payload rejection.");
        await SetTimer("None", "");

        // Attribute applicability and opaque attachments are tested on an event,
        // not inferred from the task conversion corpus.
        string definition = Guid.NewGuid().ToString(), documented = sourceNode;
        string fileName = "Event conversion Ω.xml";
        byte[] attachment = Encoding.UTF8.GetBytes("Opaque own bytes 日本語 Ω\n");
        async Task ApplyAttributes(object patch)
        {
            var result = await Op("native_attributes_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = patch });
            path = S(result, "outputArtifact"); revision = S(result, "outputRevision");
        }
        var definitionXml = new XElement("ExtendedAttribute", new XAttribute("Id", definition), new XAttribute("Type", "FileEmbedded"),
            new XAttribute("Visible", false), new XAttribute("ExportAsTable", false), new XElement("Name", "Event evidence Ω"), new XElement("Description", "Preserve event-owned attachment"),
            new XElement("Options"), new XElement("TableColumns"), new XElement("ElementTypes", families.SelectMany(f => f.Types).Distinct().Select(t => new XElement("AttributeElementType", new XAttribute("Type", t)))));
        await ApplyAttributes(new { Definitions = new[] { new { Id = definition, Xml = definitionXml.ToString() } } });
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        var values = new XElement("ElementAttributeValues", new XAttribute("ElementId", documented), new XElement("Values",
            new XElement("ExtendedAttributeValue", new XAttribute(xsi + "type", "AttachmentAttributeValue"), new XAttribute("Id", definition), new XAttribute("Type", "FileEmbedded"),
                new XElement("Content", "attachment:" + fileName), new XElement("DisplayValue", fileName), new XElement("TableValues"), new XElement("DisplayName", fileName))));
        await ApplyAttributes(new { Values = new[] { new { DiagramId = diagram, ElementId = documented, Xml = values.ToString() } },
            Attachments = new[] { new { DiagramId = diagram, ElementId = documented, FileName = fileName, DataBase64 = Convert.ToBase64String(attachment) } } });
        var documentation = (await Op("native_attributes_get", new() { ["path"] = path })).GetProperty("result").GetProperty("Documentation");

        foreach (var invalid in new[]
        {
            (Change: new Conversion(changes.Last(c => c.ExpectedType == "TimerIntermediate" && c.ExpectedEventMode == "Boundary").ElementId,
                "TimerIntermediate", "ErrorIntermediate", "Boundary"), Cause: "noninterrupting"),
            (Change: new Conversion(changes.Last(c => c.ExpectedType == "TimerStart").ElementId,
                "TimerStart", "ErrorStart", "Start"), Cause: "event-triggered subprocess")
        })
        {
            // These requests reach the actual worker and fail against native
            // context, rather than treating a host-side fixture as recovery proof.
            var rejected = await Op("native_elements_convert", new() { ["path"] = path, ["expectedRevision"] = revision, ["changes"] = new[] { invalid.Change } }, "failed");
            if (!S(rejected, "Error").Contains(invalid.Cause, StringComparison.Ordinal)) throw new InvalidDataException("Wrong native event context rejection.");
        }

        var baseline = await Op("native_inspect", new() { ["path"] = path });
        var expectedGraph = baseline.GetProperty("result").GetProperty("Elements");
        foreach (var batch in new[] { changes.ToArray(), changes.Select(c => new Conversion(c.ElementId, c.TargetType, c.ExpectedType, c.ExpectedEventMode)).ToArray() })
        {
            var result = await Op("native_elements_convert", new() { ["path"] = path, ["expectedRevision"] = revision, ["changes"] = batch });
            path = S(result, "outputArtifact"); revision = S(result, "outputRevision");
            if (!result.GetProperty("fidelity").GetProperty("Preserved").GetBoolean() || result.GetProperty("requestedChangesVerified").GetInt32() != batch.Length)
                throw new InvalidDataException("Event conversion did not verify the complete native batch.");
            var actual = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToDictionary(e => S(e, "Id"));
            foreach (var old in expectedGraph.EnumerateArray())
            {
                var a = JsonNode.Parse(old.GetRawText())!; var b = JsonNode.Parse(actual[S(old, "Id")].GetRawText())!;
                var change = batch.SingleOrDefault(c => c.ElementId == S(old, "Id"));
                if (change != null)
                {
                    if (b["ElementType"]!.GetValue<string>() != change.TargetType || b["Event"]!["Mode"]!.GetValue<string>() != change.ExpectedEventMode)
                        throw new InvalidDataException("Wrong event kind or mode after native restart.");
                    foreach (var item in new[] { a, b })
                    {
                        item.AsObject().Remove("ElementType");
                        foreach (string key in new[] { "Definitions", "DefinitionKinds", "IsParallelMultiple" }) item["Event"]!.AsObject().Remove(key);
                    }
                }
                if (!JsonNode.DeepEquals(a, b)) throw new InvalidDataException("Event conversion changed a common field, reference or unrelated graph element.");
            }
            var attributes = await Op("native_attributes_get", new() { ["path"] = path });
            if (!NativeConversionAcceptance.DocumentationEquivalent(documentation, attributes.GetProperty("result").GetProperty("Documentation")))
                throw new InvalidDataException("Event conversion changed event-owned attributes or attachment metadata.");
            var exported = await Op("native_attachment_export", new() { ["path"] = path, ["diagramId"] = diagram, ["elementId"] = documented, ["fileName"] = fileName });
            if (!File.ReadAllBytes(S(exported, "artifactPath")).SequenceEqual(attachment)) throw new InvalidDataException("Event conversion changed embedded file bytes.");
        }
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram, ["subProcessId"] = sub });
        var unchanged = await Op("native_inspect", new() { ["path"] = original });
        if (S(unchanged, "sourceRevision") != originalRevision) throw new InvalidDataException("Event conversion changed the original file.");
    }
    private static string S(JsonElement element, string key) => element.GetProperty(key).GetString()!;
}
