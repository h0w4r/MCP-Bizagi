using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>Independent MCP-authored rich native reparenting and durable readback, never policy-only proof.</summary>
internal static class NativeReparentingAcceptance
{
    private static string S(JsonElement e, string name) => e.GetProperty(name).GetString()!;
    private static string Id() => Guid.NewGuid().ToString();
    public static async Task Run(string repo, string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> input, string state = "completed")
        {
            string id = S(await call(tool, input), "OperationId"); var response = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-reparenting.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Reparent source Ω", "Unrelated 日本語" } });
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        var initial = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(initial.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Name") == "Reparent source Ω"), "Id");
        string pool = S(initial.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string process = S(initial.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string a = Id(), b = Id(), child = Id(), nested = Id(), leaf = Id(), flow = Id(), data = Id(), association = Id(), boundary = Id(), otherPool = Id(), otherProcess = Id();
        object Node(string id, string parent, string type, int x, int y) => new { Operation = "create", ElementId = id, ParentId = parent, ElementType = type,
            Name = type + " 日本語 Ω", Geometry = new { X = x, Y = y, Width = 140, Height = 90 } };
        var seeded = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new object[] {
            Node(a, process, "SubProcess", 100, 100), Node(b, process, "SubProcess", 500, 100),
            new { Operation = "create", ElementId = otherPool, ParentId = diagram, ProcessId = otherProcess, ElementType = "Participant", Name = "Other owner Ω",
                Geometry = new { X = 2000, Y = 0, Width = 1000, Height = 700 } },
            Node(child, a, "UserTask", 100, 100), Node(nested, a, "SubProcess", 350, 100), Node(leaf, nested, "ManualTask", 60, 70),
            new { Operation = "create", ElementId = data, ParentId = a, ElementType = "DataObject", Name = "Preserved input Ω",
                DataProperties = new { State = "Waiting 日本語", IsCollection = true }, Geometry = new { X = 100, Y = 260, Width = 60, Height = 60 } },
            new { Operation = "create", ElementId = flow, ParentId = a, ElementType = "SequenceFlow", SourceId = child, TargetId = nested,
                Points = new[] { new { X = 240, Y = 145 }, new { X = 350, Y = 145 } } },
            new { Operation = "create", ElementId = association, ParentId = a, ElementType = "Association", SourceId = data, TargetId = child,
                Points = new[] { new { X = 130, Y = 260 }, new { X = 170, Y = 190 } } },
            new { Operation = "create", ElementId = boundary, ParentId = a, ElementType = "TimerIntermediate", EventMode = "Boundary",
                EventProperties = new { AttachedToActivityId = child }, Geometry = new { X = 170, Y = 175, Width = 30, Height = 30 } }
        } });
        path = S(seeded, "outputArtifact"); revision = S(seeded, "outputRevision");
        async Task<JsonElement> Patch(string tool, object patch)
        {
            var result = await Op(tool, new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = patch });
            path = S(result, "outputArtifact"); revision = S(result, "outputRevision"); return result;
        }
        string role = Id(), textId = Id(), fileId = Id();
        await Patch("native_metadata_apply", new { Resources = new[] { new { Id = role, Name = "Reparent reviewer Ω", Type = "Role" } } });
        await Patch("native_metadata_apply", new { Assignments = new[] { new { ElementId = child, Responsible = new[] { role }, Accountable = new[] { role }, Consulted = new[] { role }, Informed = new[] { role } } } });
        XElement Definition(string id, string type) => new("ExtendedAttribute", new XAttribute("Id", id), new XAttribute("Type", type), new XAttribute("ExportAsTable", false),
            new XAttribute("Visible", false), new XElement("Name", type + " Ω"), new XElement("Options"), new XElement("TableColumns"),
            new XElement("ElementTypes", new XElement("AttributeElementType", new XAttribute("Type", "UserTask"))));
        await Patch("native_attributes_apply", new { Definitions = new[] { new { Id = textId, Xml = Definition(textId, "Text").ToString() }, new { Id = fileId, Xml = Definition(fileId, "FileEmbedded").ToString() } } });
        const string fileName = "Reparent preserved Ω.xml"; byte[] payload = Encoding.UTF8.GetBytes("Retain original embedded bytes 日本語 Ω\n");
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        var values = new XElement("ElementAttributeValues", new XAttribute("ElementId", child), new XElement("Values",
            new XElement("ExtendedAttributeValue", new XAttribute("Id", textId), new XAttribute("Type", "Text"), new XElement("Content", "Reparent text 日本語"), new XElement("TableValues")),
            new XElement("ExtendedAttributeValue", new XAttribute(xsi + "type", "AttachmentAttributeValue"), new XAttribute("Id", fileId), new XAttribute("Type", "FileEmbedded"),
                new XElement("Content", "attachment:" + fileName), new XElement("DisplayValue", fileName), new XElement("TableValues"), new XElement("DisplayName", fileName))));
        await Patch("native_attributes_apply", new { Values = new[] { new { DiagramId = diagram, ElementId = child, Xml = values.ToString() } },
            Attachments = new[] { new { DiagramId = diagram, ElementId = child, FileName = fileName, DataBase64 = Convert.ToBase64String(payload) } } });
        string image = Id(), imagePath = Path.Combine(run, "Reparent image Ω.png");
        byte[] imageBytes = File.ReadAllBytes(Path.Combine(repo, "tests/McpBizagi.Acceptance/Fixtures/images/alpha.png")); File.WriteAllBytes(imagePath, imageBytes);
        var pictured = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new[] {
            new { Operation = "create", ElementId = image, ParentId = nested, ElementType = "ImageArtifact", Name = "Retained nested image Ω",
                Geometry = new { X = 260, Y = 70, Width = 64, Height = 48 }, ArtifactProperties = new { Image = new { SourcePath = imagePath,
                    ExpectedRevision = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant(), AllowPngReencoding = true } } } } });
        path = S(pictured, "outputArtifact"); revision = S(pictured, "outputRevision");
        // Configure a child by its durable BPMN identity; same-diagram moves must retain this XML.
        string childBpmn = S(pictured.GetProperty("reopened").GetProperty("Elements").EnumerateArray().Single(e => S(e, "Id") == child), "BpmnId");
        XNamespace sim = "http://www.bpsim.org/schemas/1.0";
        var config = new XElement(sim + "BPSimData", new XAttribute("simulationLevel", "LevelTwo"), new XElement(sim + "Scenario", new XAttribute("id", "Reparent_scenario"),
            new XAttribute("name", "Retained scenario Ω"), new XAttribute("author", "h0w4r"), new XAttribute("version", "1.0"),
            new XElement(sim + "ScenarioParameters", new XAttribute("replication", 1), new XAttribute("seed", 7), new XAttribute("baseTimeUnit", "min"), new XAttribute("baseCurrencyUnit", "USD"), new XElement(sim + "PropertyParameters")),
            new XElement(sim + "ElementParameters", new XAttribute("elementRef", childBpmn), new XElement(sim + "TimeParameters",
                new XElement(sim + "ProcessingTime", new XElement(sim + "FloatingParameter", new XAttribute("value", 3)))), new XElement(sim + "PropertyParameters"))));
        await Patch("native_metadata_apply", new { Simulations = new[] { new { DiagramId = diagram, Xml = config.ToString() } }, DiscardSimulationResults = true });
        await Patch("native_diagrams_apply", new { OpenedItems = new[] { new { DiagramId = diagram, SubProcessId = a, IsSelected = true }, new { DiagramId = diagram, SubProcessId = nested, IsSelected = false } } });
        string originalPath = path, originalRevision = revision;
        string[] selected = [child, nested, flow, data, association, boundary];
        object[] Moves(string from, string to) => selected.Select(id => (object)new { ElementId = id, ExpectedParentId = from, TargetParentId = to }).ToArray();
        await error("native_elements_reparent", new() { ["path"] = path, ["expectedRevision"] = new string('0', 64), ["moves"] = Moves(a, b) });
        var incomplete = await Op("native_elements_reparent", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["moves"] = new[] { new { ElementId = child, ExpectedParentId = a, TargetParentId = b } } }, "failed");
        if (!S(incomplete, "Error").Contains("closure", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Incomplete selection failed for an unrelated reason.");
        async Task<JsonElement> Move(object[] moves)
        {
            var result = await Op("native_elements_reparent", new() { ["path"] = path, ["expectedRevision"] = revision, ["moves"] = moves });
            path = S(result, "outputArtifact"); revision = S(result, "outputRevision"); return result;
        }
        var forward = await Move(Moves(a, b));
        var after = forward.GetProperty("reopened");
        foreach (string id in selected)
            if (S(after.GetProperty("Elements").EnumerateArray().Single(e => S(e, "Id") == id), "ParentId") != b) throw new InvalidDataException("Moved root has the wrong actual native parent.");
        var attachment = after.GetProperty("Documentation").GetProperty("Attachments").EnumerateArray().Single(e => S(e, "ElementId") == child && S(e, "FileName") == fileName);
        if (S(attachment, "Sha256") != Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant()) throw new InvalidDataException("Attachment bytes changed.");
        // Assignment rows follow graph traversal, so a containment move changes their
        // observation order. Compare keyed rows without relaxing any row's ordered RACI
        // values or the remaining metadata (including exact scenario XML).
        string Metadata(JsonElement observation) => JsonSerializer.Serialize(observation.GetProperty("Metadata").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Name == "Assignments"
                ? (object)p.Value.EnumerateArray().OrderBy(e => S(e, "ElementId"), StringComparer.Ordinal).ToArray() : p.Value));
        if (Metadata(after) != Metadata(forward.GetProperty("before"))) throw new InvalidDataException("Configured scenario or RACI changed during same-diagram reparenting.");
        var backward = await Move(Moves(b, a));
        // Moving the whole rich subtree to another participant also relocates flattened native ActivitySets.
        var crossProcess = await Move([new { ElementId = a, ExpectedParentId = process, TargetParentId = otherProcess, Position = new { X = 2150, Y = 150 } }]);
        // A root artifact is stored in the diagram-wide XML list and assigned to a pool by
        // native geometry. Exercise that route explicitly instead of assuming subtree proof covers it.
        await Move([new { ElementId = image, ExpectedParentId = nested, TargetParentId = otherProcess, Position = new { X = 2250, Y = 300 } }]);
        await Move([new { ElementId = image, ExpectedParentId = otherProcess, TargetParentId = nested, Position = new { X = 260, Y = 70 } }]);
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram, ["subProcessId"] = nested });
        var returned = await Move([new { ElementId = a, ExpectedParentId = otherProcess, TargetParentId = process, Position = new { X = 100, Y = 100 } }]);
        var cycle = await Op("native_elements_reparent", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["moves"] = new[] { new { ElementId = a, ExpectedParentId = process, TargetParentId = nested } } }, "failed");
        if (!S(cycle, "Error").Contains("cyclic", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Cyclic target failed for an unrelated reason.");
        var recovered = await Op("native_inspect", new() { ["path"] = path });
        if (S(recovered, "sourceRevision") != revision) throw new InvalidDataException("Failure modified the latest native artifact.");
        var original = await Op("native_inspect", new() { ["path"] = originalPath });
        if (S(original, "sourceRevision") != originalRevision) throw new InvalidDataException("Reparenting changed the original rich source.");
    }
}
