using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

/// <summary>Real MCP-authored native multidiagram inputs; no production migration policy calls.</summary>
internal static class NativePresentationMigrationAcceptance
{
    private static string S(JsonElement e, string key) => e.GetProperty(key).GetString()!;
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> input, string state = "completed")
        {
            string id = S(await call(tool, input), "OperationId"); var result = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, result });
            File.WriteAllText(Path.Combine(run, "native-action-migration.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? result.GetProperty("Result") : result;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Action source Ω", "Action target 日本語" } });
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        var initial = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string Diagram(string name) => S(initial.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Name") == name), "Id");
        string Process(string d)
        {
            string pool = S(initial.Single(e => S(e, "DiagramId") == d && S(e, "Kind") == "Participant" && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
            return S(initial.Single(e => S(e, "ParentId") == pool && S(e, "Kind") == "Process"), "Id");
        }
        string from = Diagram("Action source Ω"), to = Diagram("Action target 日本語"), sourceProcess = Process(from), targetProcess = Process(to);
        string Id() => Guid.NewGuid().ToString();
        string root = Id(), nested = Id(), shared = Id(), retained = Id(); string[] owners = Enumerable.Range(0, 8).Select(_ => Id()).ToArray();
        async Task<JsonElement> Write(string tool, string key, object value)
        {
            var result = await Op(tool, new() { ["path"] = path, ["expectedRevision"] = revision, [key] = value });
            path = S(result, "outputArtifact"); revision = S(result, "outputRevision"); return result;
        }
        object Node(string id, string parent, string kind, int x, int y) => new { Operation = "create", ElementId = id, ParentId = parent, ElementType = kind,
            Name = "Action owner Ω " + x, Documentation = "Original description Ω", Geometry = new { X = x, Y = y, Width = 130, Height = 80 } };
        var nodes = new List<object> { Node(root, sourceProcess, "SubProcess", 100, 100), Node(nested, root, "SubProcess", 100, 200),
            Node(shared, sourceProcess, "UserTask", 500, 100), Node(retained, targetProcess, "UserTask", 500, 100) };
        nodes.AddRange(owners.Select((id, i) => Node(id, i < 4 ? root : nested, "UserTask", 100 + i % 4 * 160, 350)));
        var authored = await Write("native_mutate", "mutations", nodes.ToArray());
        string elementType = S(authored.GetProperty("reopened").GetProperty("Elements").EnumerateArray().Single(e => S(e, "Id") == owners[5]), "ElementType");
        var render = await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = from });
        byte[] png = File.ReadAllBytes(render.GetProperty("result").GetProperty("Artifacts").EnumerateArray().Select(e => e.GetString()!).Single(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)));
        byte[] file = Encoding.UTF8.GetBytes("Own shared action payload Ω: opaque, not XML.");
        string fileId = Id(), imageId = Id(), linkId = Id();
        object Definition(string id, string type) => new { Id = id, Xml = new XElement("ExtendedAttribute", new XAttribute("Id", id), new XAttribute("Type", type),
            new XAttribute("ExportAsTable", false), new XAttribute("Visible", false), new XElement("Name", "Migration " + type), new XElement("Description", "Own definition"),
            new XElement("Options"), new XElement("TableColumns"), new XElement("ElementTypes", new XElement("AttributeElementType", new XAttribute("Type", elementType)))).ToString() };
        object Value(string owner, string id, string type, string content, bool embedded)
        {
            var value = new XElement("ExtendedAttributeValue", new XAttribute("Id", id), new XAttribute("Type", type), new XElement("Content", content),
                new XElement("DisplayValue", content), new XElement("TableValues"));
            if (embedded) { value.SetAttributeValue(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance"), "AttachmentAttributeValue"); value.Element("DisplayValue")!.Value = content[11..]; value.Add(new XElement("DisplayName", content[11..])); }
            return new { DiagramId = from, ElementId = owner, Xml = new XElement("ElementAttributeValues", new XAttribute("ElementId", owner), new XElement("Values", value)).ToString() };
        }
        await Write("native_attributes_apply", "patch", new { Definitions = new[] { Definition(fileId, "FileEmbedded"), Definition(imageId, "Image"), Definition(linkId, "FileLinked") },
            Values = new[] { Value(owners[5], fileId, "FileEmbedded", "attachment:Reference Ω.xml", true), Value(owners[6], imageId, "Image", "attachment:Reference Ω.png", true),
                Value(owners[7], linkId, "FileLinked", "reference/not-opened.txt", false) },
            Attachments = new[] { new { DiagramId = from, ElementId = owners[5], FileName = "Reference Ω.xml", DataBase64 = Convert.ToBase64String(file) },
                new { DiagramId = from, ElementId = owners[6], FileName = "Reference Ω.png", DataBase64 = Convert.ToBase64String(png) } } });
        object Action(string diagram, string owner, string type, string content = "", string value = "Normal", string attribute = "", byte[]? bytes = null) => new
        { Action = new { DiagramId = diagram, ElementId = owner, Type = type, TypeValue = value, ExtendedAttributeId = attribute, DisplayName = "Migration Ω " + type, Content = content }, DataBase64 = bytes == null ? "" : Convert.ToBase64String(bytes) };
        await Write("native_presentation_apply", "changes", new[] { Action(from, owners[0], "Text", "Literal Ω"), Action(from, owners[1], "Link", "https://example.invalid/inert"),
            Action(from, owners[2], "File", "action-file:Evidence Ω.xml", bytes: file), Action(from, owners[3], "Image", "action-file:Diagram Ω.png", bytes: png),
            Action(from, owners[4], "Text", value: "Description"), Action(from, owners[5], "File", value: "ExtendedAttribute", attribute: fileId),
            Action(from, owners[6], "Image", value: "ExtendedAttribute", attribute: imageId), Action(from, owners[7], "File", value: "ExtendedAttribute", attribute: linkId),
            Action(from, shared, "File", "action-file:Evidence Ω.xml", bytes: file), Action(to, retained, "File", "action-file:Evidence Ω.xml", bytes: Encoding.UTF8.GetBytes("Different target bytes")) });
        // A subsequent native owner edit leaves a deliberately stale action cache to preserve during migration.
        await Write("native_mutate", "mutations", new[] { new { Operation = "update", ElementId = owners[4], Documentation = "Changed owner description; keep action cache Ω" } });
        object[] Move(bool reverse = false) => [new { ElementId = root, ExpectedParentId = reverse ? targetProcess : sourceProcess, TargetParentId = reverse ? sourceProcess : targetProcess,
            ExpectedDiagramId = reverse ? to : from, TargetDiagramId = reverse ? from : to }];
        var denied = await Op("native_elements_reparent", new() { ["path"] = path, ["expectedRevision"] = revision, ["moves"] = Move() }, "failed");
        if (!S(denied, "Error").Contains("presentation", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Missing consent failed for an unrelated reason.");
        var collision = await Op("native_elements_reparent", new() { ["path"] = path, ["expectedRevision"] = revision, ["moves"] = Move(), ["migratePresentationActions"] = true }, "failed");
        if (!S(collision, "Error").Contains("collid", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Destination collision failed for an unrelated reason.");
        await Write("native_presentation_apply", "changes", new[] { Action(to, retained, "Text", "Unrelated target action Ω") });
        string originalPath = path, originalRevision = revision;
        var forward = await Op("native_elements_reparent", new() { ["path"] = path, ["expectedRevision"] = revision, ["moves"] = Move(), ["migratePresentationActions"] = true });
        path = S(forward, "outputArtifact"); revision = S(forward, "outputRevision");
        var before = forward.GetProperty("before"); var after = forward.GetProperty("reopened");
        var movedOwners = owners.Append(root).Append(nested).ToHashSet();
        void Verify(JsonElement first, JsonElement second, bool reverse)
        {
            foreach (string field in new[] { "Actions", "Files" })
            {
                var expected = first.GetProperty("Presentation").GetProperty(field).EnumerateArray().Select(e => JsonNode.Parse(e.GetRawText())!).ToDictionary(e => e["ElementId"]!.GetValue<string>());
                foreach (var pair in expected.Where(p => movedOwners.Contains(p.Key))) pair.Value["DiagramId"] = reverse ? from : to;
                var actual = second.GetProperty("Presentation").GetProperty(field).EnumerateArray().ToDictionary(e => S(e, "ElementId"));
                if (!expected.Keys.Order().SequenceEqual(actual.Keys.Order())) throw new InvalidDataException("Action or payload identity set changed during migration.");
                foreach (var pair in expected) if (!JsonNode.DeepEquals(pair.Value, JsonNode.Parse(actual[pair.Key].GetRawText()))) throw new InvalidDataException("Action cache/bytes changed during migration: " + pair.Key);
            }
        }
        Verify(before, after, false);
        var stale = after.GetProperty("Presentation").GetProperty("Actions").EnumerateArray().Single(e => S(e, "ElementId") == owners[4]);
        if (S(stale, "Content") != "Original description Ω") throw new InvalidDataException("Migration implicitly refreshed a stale description cache.");
        using (var outer = ZipFile.OpenRead(forward.GetProperty("edited").GetProperty("Artifacts")[0].GetString()!))
            foreach (string diagram in new[] { from, to })
            {
                using var stream = outer.GetEntry(diagram + ".diag")!.Open(); using var inner = new ZipArchive(stream);
                using var input = inner.GetEntry("Actions/Evidence Ω.xml")!.Open(); using var bytes = new MemoryStream(); input.CopyTo(bytes);
                if (!bytes.ToArray().SequenceEqual(file)) throw new InvalidDataException("Shared source/destination action payload was not retained exactly.");
            }
        var exported = await Op("native_attachment_export", new() { ["path"] = path, ["diagramId"] = to, ["elementId"] = owners[5], ["fileName"] = "Reference Ω.xml" });
        if (!File.ReadAllBytes(S(exported, "artifactPath")).SequenceEqual(file)) throw new InvalidDataException("Migrated referenced attachment bytes differ.");
        var noop = await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision }); path = S(noop, "outputArtifact"); revision = S(noop, "outputRevision");
        var backward = await Op("native_elements_reparent", new() { ["path"] = path, ["expectedRevision"] = revision, ["moves"] = Move(true), ["migratePresentationActions"] = true });
        path = S(backward, "outputArtifact"); revision = S(backward, "outputRevision"); Verify(after, backward.GetProperty("reopened"), true);
        var original = await Op("native_presentation_get", new() { ["path"] = originalPath });
        if (S(original, "sourceRevision") != originalRevision) throw new InvalidDataException("Migration modified the source model.");
        await Op("native_presentation_get", new() { ["path"] = path });
    }
}
