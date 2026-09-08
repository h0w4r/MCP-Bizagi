using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

/// <summary>Independent installed-command extraction; no successful assertion can bypass native fidelity gates.</summary>
internal static class NativeRefactoringAcceptance
{
    private static string S(JsonElement element, string name) => element.GetProperty(name).GetString()!;
    public static async Task RunExisting(string run, string input, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        // Focused regression over a previously authored real model; every extraction still traverses MCP and fresh workers.
        string path = Path.Combine(run, "Existing extraction source Ω.bpm"); File.Copy(Path.GetFullPath(input), path);
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> arguments)
        {
            string id = S(await call(tool, arguments), "OperationId"); var response = await wait(id, "completed"); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-refactoring-existing.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return response.GetProperty("Result");
        }
        var inspected = await Op("native_inspect", new() { ["path"] = path }); string revision = S(inspected, "sourceRevision");
        var candidates = inspected.GetProperty("result").GetProperty("Elements").EnumerateArray().Where(e => S(e, "Kind") == "SubProcess" &&
            !e.GetProperty("SubProcess").GetProperty("TriggeredByEvent").GetBoolean()).ToArray();
        if (candidates.Length == 0) throw new InvalidDataException("The supplied real model has no ordinary embedded subprocesses to extract.");
        for (int index = 0; index < candidates.Length; index++)
        {
            var extracted = await Op("native_subprocess_extract", new() { ["path"] = path, ["expectedRevision"] = revision,
                ["extraction"] = new { ElementId = S(candidates[index], "Id"), NewDiagramName = "Verified extraction Ω " + index } });
            if (!extracted.GetProperty("fidelity").GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Extraction fidelity was not accredited.");
            await Op("native_save_copy", new() { ["path"] = S(extracted, "outputArtifact"), ["expectedRevision"] = S(extracted, "outputRevision") });
        }
        if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() != revision) throw new InvalidDataException("Focused extraction modified its source.");
    }

    public static async Task Run(string repo, string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = S(await call(tool, args), "OperationId"); var response = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-refactoring.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Extraction source Ω", "Unrelated diagram Ω" } });
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(graph.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Name") == "Extraction source Ω"), "Id");
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string process = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string sub = Guid.NewGuid().ToString(), child = Guid.NewGuid().ToString(), nested = Guid.NewGuid().ToString(), task = Guid.NewGuid().ToString();
        var seeded = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new object[] {
            new { Operation = "create", ElementId = sub, ParentId = process, ElementType = "SubProcess", Name = "Extract 日本語 Ω",
                Geometry = new { X = 100, Y = 100, Width = 260, Height = 200 }, Documentation = "Preserve original subprocess description Ω" },
            new { Operation = "create", ElementId = child, ParentId = sub, ElementType = "UserTask", Name = "Direct child Ω",
                Geometry = new { X = 40, Y = 50, Width = 120, Height = 70 } },
            new { Operation = "create", ElementId = nested, ParentId = sub, ElementType = "SubProcess", Name = "Nested embedded Ω",
                Geometry = new { X = 220, Y = 50, Width = 200, Height = 140 } },
            new { Operation = "create", ElementId = task, ParentId = nested, ElementType = "ManualTask", Name = "Nested preserved task Ω",
                Geometry = new { X = 40, Y = 40, Width = 120, Height = 70 } }
        } });
        path = S(seeded, "outputArtifact"); revision = S(seeded, "outputRevision");
        async Task Patch(string tool, object patch)
        {
            var result = await Op(tool, new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = patch });
            path = S(result, "outputArtifact"); revision = S(result, "outputRevision");
        }
        string role = Guid.NewGuid().ToString(), textId = Guid.NewGuid().ToString(), fileId = Guid.NewGuid().ToString();
        await Patch("native_metadata_apply", new { Resources = new[] { new { Id = role, Name = "Extraction reviewer Ω", Type = "Role" } } });
        await Patch("native_metadata_apply", new { Assignments = new[] { new { ElementId = child, Responsible = new[] { role }, Accountable = new[] { role }, Consulted = new[] { role }, Informed = new[] { role } } } });
        XElement Definition(string id, string type) => new("ExtendedAttribute", new XAttribute("Id", id), new XAttribute("Type", type),
            new XAttribute("ExportAsTable", false), new XAttribute("Visible", false), new XElement("Name", "Extraction " + type + " Ω"),
            new XElement("Options"), new XElement("TableColumns"), new XElement("ElementTypes", new XElement("AttributeElementType", new XAttribute("Type", "UserTask"))));
        await Patch("native_attributes_apply", new { Definitions = new[] { new { Id = textId, Xml = Definition(textId, "Text").ToString() }, new { Id = fileId, Xml = Definition(fileId, "FileEmbedded").ToString() } } });
        const string fileName = "Extracted bytes Ω.xml"; byte[] attachment = Encoding.UTF8.GetBytes("Preserve the complete attachment 日本語 Ω\n");
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        var values = new XElement("ElementAttributeValues", new XAttribute("ElementId", child), new XElement("Values",
            new XElement("ExtendedAttributeValue", new XAttribute("Id", textId), new XAttribute("Type", "Text"), new XElement("Content", "Preserve extended text 日本語 Ω"), new XElement("TableValues")),
            new XElement("ExtendedAttributeValue", new XAttribute(xsi + "type", "AttachmentAttributeValue"), new XAttribute("Id", fileId), new XAttribute("Type", "FileEmbedded"),
                new XElement("Content", "attachment:" + fileName), new XElement("DisplayValue", fileName), new XElement("TableValues"), new XElement("DisplayName", fileName))));
        await Patch("native_attributes_apply", new { Values = new[] { new { DiagramId = diagram, ElementId = child, Xml = values.ToString() } },
            Attachments = new[] { new { DiagramId = diagram, ElementId = child, FileName = fileName, DataBase64 = Convert.ToBase64String(attachment) } } });
        string image = Guid.NewGuid().ToString(), imagePath = Path.Combine(run, "Extraction image Ω.png");
        byte[] imageBytes = File.ReadAllBytes(Path.Combine(repo, "tests", "McpBizagi.Acceptance", "Fixtures", "images", "alpha.png"));
        File.WriteAllBytes(imagePath, imageBytes);
        var pictured = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new[] {
            new { Operation = "create", ElementId = image, ParentId = nested, ElementType = "ImageArtifact", Name = "Nested image Ω",
                Geometry = new { X = 180, Y = 40, Width = 64, Height = 48 }, ArtifactProperties = new { Image = new { SourcePath = imagePath,
                    ExpectedRevision = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant(), AllowPngReencoding = true } } } } });
        path = S(pictured, "outputArtifact"); revision = S(pictured, "outputRevision");
        string outside = Guid.NewGuid().ToString(), innerFlow = Guid.NewGuid().ToString(), outerFlow = Guid.NewGuid().ToString();
        var connected = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new object[] {
            new { Operation = "create", ElementId = outside, ParentId = process, ElementType = "UserTask", Name = "Outside counterpart Ω", Geometry = new { X = 500, Y = 150, Width = 130, Height = 70 } },
            new { Operation = "create", ElementId = innerFlow, ParentId = sub, ElementType = "SequenceFlow", SourceId = child, TargetId = nested,
                Name = "Moved internal flow Ω", Points = new[] { new { X = 160, Y = 85 }, new { X = 220, Y = 85 } } },
            new { Operation = "create", ElementId = outerFlow, ParentId = process, ElementType = "SequenceFlow", SourceId = sub, TargetId = outside,
                Name = "Preserved outer flow Ω", Points = new[] { new { X = 360, Y = 200 }, new { X = 500, Y = 185 } } } } });
        path = S(connected, "outputArtifact"); revision = S(connected, "outputRevision");
        string unrelated = S(graph.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Name") == "Unrelated diagram Ω"), "Id");
        await Patch("native_diagrams_apply", new { OpenedItems = new[] {
            new { DiagramId = diagram, SubProcessId = "", IsSelected = false }, new { DiagramId = diagram, SubProcessId = sub, IsSelected = true },
            new { DiagramId = diagram, SubProcessId = nested, IsSelected = false }, new { DiagramId = unrelated, SubProcessId = "", IsSelected = false } } });
        await error("native_subprocess_extract", new() { ["path"] = path, ["expectedRevision"] = new string('0', 64),
            ["extraction"] = new { ElementId = sub, NewDiagramName = "Rejected stale request" } });
        var extracted = await Op("native_subprocess_extract", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["extraction"] = new { ElementId = sub, NewDiagramName = "Reusable 日本語 Ω" } });
        var after = extracted.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string newDiagram = S(after.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Name") == "Reusable 日本語 Ω"), "Id");
        if (S(after.Single(e => S(e, "Id") == sub), "Kind") != "CallActivity" ||
            new[] { child, nested, task, image }.Any(id => S(after.Single(e => S(e, "Id") == id), "DiagramId") != newDiagram))
            throw new InvalidDataException("Real extraction did not preserve child identities in the new native diagram.");
        var documented = extracted.GetProperty("reopened").GetProperty("Documentation");
        var file = documented.GetProperty("Attachments").EnumerateArray().Single(f => S(f, "ElementId") == child && S(f, "FileName") == fileName);
        if (S(file, "DiagramId") != newDiagram || S(file, "Sha256") != Convert.ToHexString(SHA256.HashData(attachment)).ToLowerInvariant())
            throw new InvalidDataException("Native attachment was not relocated byte-exactly.");
        var assignment = extracted.GetProperty("reopened").GetProperty("Metadata").GetProperty("Assignments").EnumerateArray().Single(a => S(a, "ElementId") == child);
        foreach (string field in new[] { "Responsible", "Accountable", "Consulted", "Informed" })
            if (!assignment.GetProperty(field).EnumerateArray().Select(v => v.GetString()).SequenceEqual(new[] { role })) throw new InvalidDataException("Extraction changed RACI: " + field);
        var imageBefore = pictured.GetProperty("reopened").GetProperty("ImageFiles").EnumerateArray().Single(f => S(f, "ElementId") == image);
        var imageAfter = extracted.GetProperty("reopened").GetProperty("ImageFiles").EnumerateArray().Single(f => S(f, "ElementId") == image);
        if (S(imageAfter, "DiagramId") != newDiagram || S(imageBefore, "Sha256") != S(imageAfter, "Sha256")) throw new InvalidDataException("Nested image bytes changed during extraction.");
        var tabs = extracted.GetProperty("reopened").GetProperty("DiagramState").GetProperty("OpenedItems").EnumerateArray().ToArray();
        if (tabs.Length != 4 || S(tabs[1], "DiagramId") != newDiagram || S(tabs[1], "SubProcessId") != "" || !tabs[1].GetProperty("IsSelected").GetBoolean() ||
            S(tabs[2], "DiagramId") != newDiagram || S(tabs[2], "SubProcessId") != nested || S(tabs[0], "DiagramId") != diagram || S(tabs[3], "DiagramId") != unrelated)
            throw new InvalidDataException("Persisted tab order/selection/reference remapping failed.");
        if (S(after.Single(e => S(e, "Id") == innerFlow), "ParentId") == sub || S(after.Single(e => S(e, "Id") == outerFlow), "SourceId") != sub)
            throw new InvalidDataException("Internal or incident external flow references were changed incorrectly.");
        await Op("native_save_copy", new() { ["path"] = S(extracted, "outputArtifact"), ["expectedRevision"] = S(extracted, "outputRevision") });
        await Op("native_render_svg", new() { ["path"] = S(extracted, "outputArtifact"), ["diagramId"] = newDiagram, ["subProcessId"] = nested });
        // Reuse the unchanged source to independently exercise a nested extraction command context.
        var nestedExtraction = await Op("native_subprocess_extract", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["extraction"] = new { ElementId = nested, NewDiagramName = "Nested reusable Ω" } });
        var nestedGraph = nestedExtraction.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        if (S(nestedGraph.Single(e => S(e, "Id") == sub), "Kind") != "SubProcess" || S(nestedGraph.Single(e => S(e, "Id") == nested), "Kind") != "CallActivity" ||
            S(nestedGraph.Single(e => S(e, "Id") == task), "DiagramId") == diagram || S(nestedGraph.Single(e => S(e, "Id") == child), "DiagramId") != diagram)
            throw new InvalidDataException("Nested extraction did not preserve surrounding native containment.");
        await Op("native_subprocess_extract", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["extraction"] = new { ElementId = child, NewDiagramName = "Not a subprocess" } }, "failed");
        var source = await Op("native_inspect", new() { ["path"] = path });
        if (S(source, "sourceRevision") != revision) throw new InvalidDataException("Original source changed during extraction.");
    }
}
