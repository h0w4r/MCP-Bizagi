using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

/// <summary>Actual MCP-to-installed-engine acceptance; no adapter calls or synthetic native models.</summary>
internal static class NativeDocumentationAcceptance
{
    public static async Task Run(string repo, string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        async Task<JsonElement> Operation(string name, Dictionary<string, object?> input, string status = "completed")
        {
            string id = (await call(name, input)).GetProperty("OperationId").GetString()!;
            var result = await wait(id, status); exited(id);
            return status == "completed" ? result.GetProperty("Result") : result;
        }
        File.Copy(Path.Combine(repo, "examples", "minimal.bpmn"), Path.Combine(run, "attribute source.bpmn"));
        var imported = await Operation("native_roundtrip", new() { ["path"] = "attribute source.bpmn", ["modelName"] = "Attribute acceptance" });
        string path = imported.GetProperty("nativeArtifact").GetString()!;
        var read = await Operation("native_attributes_get", new() { ["path"] = path });
        string revision = read.GetProperty("sourceRevision").GetString()!;
        var elements = read.GetProperty("result").GetProperty("Elements").EnumerateArray().ToArray();
        var task = elements.Single(e => e.GetProperty("Kind").GetString()!.EndsWith("Task", StringComparison.Ordinal));
        string element = task.GetProperty("Id").GetString()!, diagram = task.GetProperty("DiagramId").GetString()!, elementType = task.GetProperty("ElementType").GetString()!;
        async Task<JsonElement> Apply(object patch)
        {
            var result = await Operation("native_attributes_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = patch });
            if (!result.GetProperty("fidelity").GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Documentation fidelity failed.");
            path = result.GetProperty("outputArtifact").GetString()!; revision = result.GetProperty("outputRevision").GetString()!;
            return result.GetProperty("reopened").GetProperty("Documentation");
        }
        string[] types = ["Text", "LongText", "Number", "Date", "Combo", "Radio", "Check", "Link", "FileLinked", "FileEmbedded", "Image", "Table"];
        var ids = types.ToDictionary(t => t, _ => Guid.NewGuid().ToString());
        XElement Definition(string type) => new("ExtendedAttribute", new XAttribute("Id", ids[type]), new XAttribute("Type", type), new XAttribute("ExportAsTable", false), new XAttribute("Visible", false),
            new XElement("Name", "Evidence " + type + " Ω"), new XElement("Description", "Own native attribute acceptance"),
            new XElement("Options", type is "Combo" or "Radio" or "Check" ? new[] { new XElement("string", "First"), new XElement("string", "Second") } : Array.Empty<XElement>()),
            new XElement("TableColumns"), new XElement("ElementTypes", new XElement("AttributeElementType", new XAttribute("Type", elementType))));
        var definitions = types.Select(Definition).ToArray();
        string textColumnId = Guid.NewGuid().ToString(), numberColumnId = Guid.NewGuid().ToString();
        XElement Column(string id, string type, string name) => new("ColumnAttribute", new XAttribute("Id", id), new XAttribute("Type", type),
            new XAttribute("ExportAsTable", false), new XAttribute("Visible", false), new XAttribute("ModificationDate", XmlConvert.ToString(new DateTime(2000, 1, 1), XmlDateTimeSerializationMode.Local)),
            new XElement("Name", name), new XElement("Description", "Own table column"), new XElement("Options"), new XElement("TableColumns"), new XElement("ElementTypes"));
        definitions.Single(d => (string?)d.Attribute("Type") == "Table").Element("TableColumns")!.Add(Column(textColumnId, "Text", "Item"), Column(numberColumnId, "Number", "Amount"));
        var created = await Apply(new { Definitions = definitions.Select(d => new { Id = (string)d.Attribute("Id")!, Xml = d.ToString() }).ToArray() });
        if (created.GetProperty("Definitions").GetArrayLength() != types.Length) throw new InvalidDataException("Definition count mismatch.");
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        XElement Value(string type, string content) => new("ExtendedAttributeValue", new XAttribute("Id", ids[type]), new XAttribute("Type", type),
            new XElement("Content", content), new XElement("DisplayValue", content), new XElement("TableValues"));
        var values = new XElement("ElementAttributeValues", new XAttribute("ElementId", element), new XElement("Values",
            Value("Text", "Unicode Ω 日本語"), Value("LongText", "First line\nSecond line"), Value("Number", "42.5"), Value("Date", "2026-09-07T00:00:00"),
            Value("Combo", "First"), Value("Radio", "Second"), Value("Check", "First;Second"), Value("Link", "https://example.com/model-documentation"),
            Value("FileLinked", "documentation/reference.txt"), Value("Table", "Rows")));
        XElement Cell(string id, string type, string content) { var value = Value(type, content); value.SetAttributeValue("Id", id); return value; }
        values.Element("Values")!.Elements().Single(e => (string?)e.Attribute("Type") == "Table").Element("TableValues")!.Add(
            new XElement("RowValues", Cell(textColumnId, "Text", "First row Ω"), Cell(numberColumnId, "Number", "7.5")),
            new XElement("RowValues", Cell(textColumnId, "Text", "Second row 日本語"), Cell(numberColumnId, "Number", "12")));
        var scalar = await Apply(new { Values = new[] { new { DiagramId = diagram, ElementId = element, Xml = values.ToString() } } });
        // Use a deliberately non-XML .xml payload: native attachments are bytes, not archive metadata.
        byte[] attachment = Encoding.UTF8.GetBytes("Own embedded evidence Ω. Not an XML document.\n");
        var fileValue = Value("FileEmbedded", "attachment:Evidence Ω.xml");
        fileValue.SetAttributeValue(xsi + "type", "AttachmentAttributeValue");
        fileValue.Element("DisplayValue")!.Value = "Evidence Ω.xml";
        fileValue.Add(new XElement("DisplayName", "Evidence Ω.xml"));
        values.Element("Values")!.Add(fileValue);
        // Use an actual installed-renderer PNG of our own process, not a fabricated image result.
        var rendered = await Operation("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram });
        string png = rendered.GetProperty("result").GetProperty("Artifacts").EnumerateArray().Select(a => a.GetString()!).Single(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
        byte[] imageBytes = File.ReadAllBytes(png);
        var imageValue = Value("Image", "attachment:Own diagram.png");
        imageValue.SetAttributeValue(xsi + "type", "AttachmentAttributeValue"); imageValue.Element("DisplayValue")!.Value = "Own diagram.png";
        imageValue.Add(new XElement("DisplayName", "Own diagram.png")); values.Element("Values")!.Add(imageValue);
        var embedded = await Apply(new
        {
            Values = new[] { new { DiagramId = diagram, ElementId = element, Xml = values.ToString() } },
            Attachments = new[] { new { DiagramId = diagram, ElementId = element, FileName = "Evidence Ω.xml", DataBase64 = Convert.ToBase64String(attachment) },
                new { DiagramId = diagram, ElementId = element, FileName = "Own diagram.png", DataBase64 = Convert.ToBase64String(imageBytes) } }
        });
        if (embedded.GetProperty("Attachments").GetArrayLength() != 2) throw new InvalidDataException("File and image attachments were not read back.");
        var exported = await Operation("native_attachment_export", new() { ["path"] = path, ["diagramId"] = diagram, ["elementId"] = element, ["fileName"] = "Evidence Ω.xml" });
        if (!File.ReadAllBytes(exported.GetProperty("artifactPath").GetString()!).SequenceEqual(attachment)) throw new InvalidDataException("Native file export changed bytes.");
        await Operation("native_attachment_export", new() { ["path"] = path, ["diagramId"] = diagram, ["elementId"] = element, ["fileName"] = "missing.txt" }, "failed");
        // Unknown definitions must fail in the real worker and leave it recoverable.
        var bad = new XElement(definitions[0]); bad.Add(new XElement("UnsupportedOwnField", "must not disappear"));
        await Operation("native_attributes_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new { Definitions = new[] { new { Id = ids["Text"], Xml = bad.ToString() } } } }, "failed");
        var recovered = await Operation("native_attributes_get", new() { ["path"] = path });
        var renamed = new XElement(definitions[0]); renamed.Element("Name")!.Value = "Updated attribute Ω";
        await Apply(new { Definitions = new[] { new { Id = ids["Text"], Xml = renamed.ToString() } } });
        // A native no-op save and an unrelated name edit must preserve every attribute and embedded byte.
        var noop = await Operation("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        path = noop.GetProperty("outputArtifact").GetString()!; revision = noop.GetProperty("outputRevision").GetString()!;
        var nameEdit = await Operation("native_apply_changes", new() { ["path"] = path, ["expectedRevision"] = revision, ["changes"] = new[] { new { ElementId = element, Name = "Documented task Ω" } } });
        path = nameEdit.GetProperty("outputArtifact").GetString()!; revision = nameEdit.GetProperty("outputRevision").GetString()!;
        // Replace bytes without changing the reference, then require explicit deletion of both value and file.
        byte[] replacementBytes = Encoding.UTF8.GetBytes("Updated own embedded bytes Ω\n");
        await Apply(new { Attachments = new[] { new { DiagramId = diagram, ElementId = element, FileName = "Evidence Ω.xml", DataBase64 = Convert.ToBase64String(replacementBytes) } } });
        await Operation("native_attributes_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new { Definitions = new[] { new { Id = ids["Text"], Operation = "delete" } } } }, "failed");
        fileValue.Remove();
        var cleared = await Apply(new
        {
            Values = new[] { new { DiagramId = diagram, ElementId = element, Xml = values.ToString() } },
            Attachments = new[] { new { Operation = "delete", DiagramId = diagram, ElementId = element, FileName = "Evidence Ω.xml" } }
        });
        if (cleared.GetProperty("Attachments").GetArrayLength() != 1 || cleared.GetProperty("Attachments")[0].GetProperty("FileName").GetString() != "Own diagram.png") throw new InvalidDataException("File deletion must preserve the unrelated image attachment.");
        values.Element("Values")!.Elements().Single(e => (string?)e.Attribute("Id") == ids["Text"]).Remove();
        await Apply(new { Values = new[] { new { DiagramId = diagram, ElementId = element, Xml = values.ToString() } } });
        var deleted = await Apply(new { Definitions = new[] { new { Id = ids["Text"], Operation = "delete" } } });
        if (deleted.GetProperty("Definitions").GetArrayLength() != 11) throw new InvalidDataException("Definition deletion did not survive reload.");
        File.WriteAllText(Path.Combine(run, "documentation-acceptance.json"), JsonSerializer.Serialize(new { path, revision, types, created, scalar, embedded, exported, recovered, noop, nameEdit, cleared, deleted }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
