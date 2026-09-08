using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>Independent MCP client; native models and images are produced by the installed engine.</summary>
internal static class NativePresentationAcceptance
{
    public static async Task Run(string repo, string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error)
    {
        string S(JsonElement e, string p) => e.GetProperty(p).GetString()!;
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> input, string state = "completed")
        {
            string id = S(await call(tool, input), "OperationId"); var response = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-presentation.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var bpmn = XDocument.Load(Path.Combine(repo, "examples", "minimal.bpmn"));
        bpmn.Descendants().Single(e => e.Name.LocalName == "task").AddFirst(new XElement(bpmn.Root!.Name.Namespace + "documentation", "Description Ω 日本語"));
        bpmn.Save(Path.Combine(run, "presentation Ω.bpmn"));
        var imported = await Op("native_roundtrip", new() { ["path"] = "presentation Ω.bpmn", ["modelName"] = "Presentation Ω" });
        string path = S(imported, "nativeArtifact");
        var read = await Op("native_presentation_get", new() { ["path"] = path });
        string revision = S(read, "sourceRevision");
        var graph = read.GetProperty("result").GetProperty("Elements").EnumerateArray().ToArray();
        var task = graph.Single(e => S(e, "Kind").EndsWith("Task", StringComparison.Ordinal));
        string owner = S(task, "Id"), diagram = S(task, "DiagramId");
        string start = S(graph.Single(e => S(e, "Kind") == "StartEvent"), "Id"), end = S(graph.Single(e => S(e, "Kind") == "EndEvent"), "Id");
        object Change(string id, string type, string content = "", string source = "Normal", string attribute = "", byte[]? bytes = null) => new
        { Action = new { DiagramId = diagram, ElementId = id, Type = type, TypeValue = source, ExtendedAttributeId = attribute, DisplayName = "Action Ω " + type, Content = content }, DataBase64 = bytes == null ? "" : Convert.ToBase64String(bytes) };
        object Delete(string id) => new { Operation = "delete", Action = new { DiagramId = diagram, ElementId = id } };
        async Task<JsonElement> Apply(object[] changes, string state = "completed")
        {
            var response = await Op("native_presentation_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["changes"] = changes }, state);
            if (state == "completed")
            {
                if (!response.GetProperty("fidelity").GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Presentation fidelity failed.");
                path = S(response, "outputArtifact"); revision = S(response, "outputRevision");
            }
            return response;
        }
        void AssertContent(JsonElement response, string id, string expected)
        {
            var a = response.GetProperty("reopened").GetProperty("Presentation").GetProperty("Actions").EnumerateArray().Single(e => S(e, "ElementId") == id);
            if (S(a, "Content") != expected) throw new InvalidDataException("Unexpected actual native action content.");
        }
        // Literal link/text are never opened. Description is resolved by the native service.
        var scalars = await Apply([Change(owner, "Text", "Literal Ω\nC:\\not-a-file"), Change(start, "Link", "https://example.invalid/inert-action"), Change(end, "None")]);
        AssertContent(scalars, owner, "Literal Ω\nC:\\not-a-file");
        var description = await Apply([Change(owner, "Text", source: "Description")]);
        AssertContent(description, owner, S(task, "Documentation"));

        var rendered = await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram });
        byte[] png = File.ReadAllBytes(rendered.GetProperty("result").GetProperty("Artifacts").EnumerateArray().Select(e => e.GetString()!).Single(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)));
        byte[] file = Encoding.UTF8.GetBytes("Opaque action bytes Ω, deliberately not XML.");
        var binaries = await Apply([Change(start, "File", "action-file:Evidence Ω.xml", bytes: file), Change(end, "Image", "action-file:Diagram Ω.png", bytes: png)]);
        void AssertFile(JsonElement result, string id, byte[] bytes)
        {
            var f = result.GetProperty("reopened").GetProperty("Presentation").GetProperty("Files").EnumerateArray().Single(e => S(e, "ElementId") == id);
            if (S(f, "Sha256") != Convert.ToHexStringLower(SHA256.HashData(bytes)) || f.GetProperty("Length").GetInt64() != bytes.Length) throw new InvalidDataException("Action payload bytes changed.");
            // Independently decode the durable native archive; do not trust server-side hashing alone.
            using var outer = ZipFile.OpenRead(result.GetProperty("edited").GetProperty("Artifacts")[0].GetString()!);
            using var stream = outer.GetEntry(diagram + ".diag")!.Open(); using var nested = new ZipArchive(stream);
            using var input = nested.GetEntry("Actions/" + S(f, "FileName"))!.Open(); using var memory = new MemoryStream(); input.CopyTo(memory);
            if (!memory.ToArray().SequenceEqual(bytes)) throw new InvalidDataException("Durable action archive bytes differ.");
        }
        AssertFile(binaries, start, file); AssertFile(binaries, end, png);
        await Apply([Change(end, "Image", "action-file:invalid.png", bytes: file)], "failed");
        var recovered = await Op("native_presentation_get", new() { ["path"] = path });
        if (S(recovered, "sourceRevision") != revision) throw new InvalidDataException("Failed edit changed source.");
        var noop = await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        path = S(noop, "outputArtifact"); revision = S(noop, "outputRevision");
        // Native referenced content is derived from explicit, real attribute definitions and values.
        string attr = Guid.NewGuid().ToString();
        var definition = new XElement("ExtendedAttribute", new XAttribute("Id", attr), new XAttribute("Type", "Text"), new XAttribute("ExportAsTable", false), new XAttribute("Visible", false),
            new XElement("Name", "Action reference Ω"), new XElement("Description", "Own native reference"), new XElement("Options"), new XElement("TableColumns"),
            new XElement("ElementTypes", new XElement("AttributeElementType", new XAttribute("Type", S(task, "ElementType")))));
        var values = new XElement("ElementAttributeValues", new XAttribute("ElementId", owner), new XElement("Values", new XElement("ExtendedAttributeValue", new XAttribute("Id", attr), new XAttribute("Type", "Text"),
            new XElement("Content", "Referenced Ω"), new XElement("DisplayValue", "Referenced Ω"), new XElement("TableValues"))));
        var metadata = await Op("native_attributes_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new
        { Definitions = new[] { new { Id = attr, Xml = definition.ToString() } }, Values = new[] { new { DiagramId = diagram, ElementId = owner, Xml = values.ToString() } } } });
        path = S(metadata, "outputArtifact"); revision = S(metadata, "outputRevision");
        var referenced = await Apply([Change(owner, "Text", source: "ExtendedAttribute", attribute: attr)]);
        AssertContent(referenced, owner, "Referenced Ω");
        // Exercise every allowed reference type against real persisted native values.
        var referenceKinds = new[] { (Kind: "LongText", Action: "Text", Content: "Long reference Ω\nSecond line"),
            (Kind: "Number", Action: "Text", Content: "42.5"), (Kind: "Date", Action: "Text", Content: "2026-09-08T00:00:00"),
            (Kind: "Link", Action: "Link", Content: "https://example.invalid/referenced"), (Kind: "FileLinked", Action: "File", Content: "reference/not-opened.txt"),
            (Kind: "FileEmbedded", Action: "File", Content: "attachment:Reference Ω.xml"), (Kind: "Image", Action: "Image", Content: "attachment:Reference Ω.png") };
        var referenceIds = referenceKinds.ToDictionary(k => k.Kind, _ => Guid.NewGuid().ToString());
        var moreDefinitions = referenceKinds.Select(k =>
        {
            var d = new XElement(definition); d.SetAttributeValue("Id", referenceIds[k.Kind]); d.SetAttributeValue("Type", k.Kind);
            d.Element("Name")!.Value = "Reference " + k.Kind + " Ω"; return new { Id = referenceIds[k.Kind], Xml = d.ToString() };
        }).ToArray();
        foreach (var k in referenceKinds)
        {
            var v = new XElement("ExtendedAttributeValue", new XAttribute("Id", referenceIds[k.Kind]), new XAttribute("Type", k.Kind),
                new XElement("Content", k.Content), new XElement("DisplayValue", k.Content), new XElement("TableValues"));
            if (k.Kind is "FileEmbedded" or "Image")
            {
                v.SetAttributeValue(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance"), "AttachmentAttributeValue");
                v.Element("DisplayValue")!.Value = k.Content[11..]; v.Add(new XElement("DisplayName", k.Content[11..]));
            }
            values.Element("Values")!.Add(v);
        }
        var moreMetadata = await Op("native_attributes_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new
        {
            Definitions = moreDefinitions, Values = new[] { new { DiagramId = diagram, ElementId = owner, Xml = values.ToString() } },
            Attachments = new[] { new { DiagramId = diagram, ElementId = owner, FileName = "Reference Ω.xml", DataBase64 = Convert.ToBase64String(file) },
                new { DiagramId = diagram, ElementId = owner, FileName = "Reference Ω.png", DataBase64 = Convert.ToBase64String(png) } }
        } });
        path = S(moreMetadata, "outputArtifact"); revision = S(moreMetadata, "outputRevision");
        foreach (var k in referenceKinds)
        {
            var r = await Apply([Change(owner, k.Action, source: "ExtendedAttribute", attribute: referenceIds[k.Kind])]);
            AssertContent(r, owner, k.Content); AssertFile(r, start, file); AssertFile(r, end, png);
        }
        var referencedNoop = await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        path = S(referencedNoop, "outputArtifact"); revision = S(referencedNoop, "outputRevision");
        await Apply([Change(owner, "Image", source: "ExtendedAttribute", attribute: attr)], "failed");
        await Apply([Change(Guid.NewGuid().ToString(), "Text", "Missing owner")], "failed");
        await Apply([Change(owner, "File", "action-file:Evidence Ω.xml", bytes: Encoding.UTF8.GetBytes("must not overwrite another owner"))], "failed");
        await error("native_presentation_apply", new() { ["path"] = path, ["expectedRevision"] = new string('0', 64), ["changes"] = new[] { Change(owner, "Text", "stale") } });
        await error("native_presentation_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["changes"] = new[] { Change(owner, "File", "action-file:../outside", bytes: file) } });
        byte[] replacement = Encoding.UTF8.GetBytes("Replacement bytes Ω");
        var updated = await Apply([Change(start, "File", "action-file:Evidence Ω.xml", bytes: replacement)]);
        AssertFile(updated, start, replacement); AssertFile(updated, end, png);
        var deleted = await Apply([Delete(start), Delete(end), Delete(owner)]);
        if (deleted.GetProperty("reopened").GetProperty("Presentation").GetProperty("Actions").GetArrayLength() != 0 ||
            deleted.GetProperty("reopened").GetProperty("Presentation").GetProperty("Files").GetArrayLength() != 0) throw new InvalidDataException("Presentation deletion did not survive restart.");
        await Apply([Delete(owner)], "failed");
        await Apply([Change(owner, "Text", "Recovered after deletion Ω")]);
    }
}
