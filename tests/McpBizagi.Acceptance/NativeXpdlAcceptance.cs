using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>Independent MCP/native XPDL exchange with Unicode, selected diagrams and explicit losses.</summary>
internal static class NativeXpdlAcceptance
{
    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString()!;
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = S(await call(tool, args), "OperationId"); var response = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-xpdl.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        // Unicode labels must not be routed through the installed ASCII stream overload.
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Exchange Ω 日本語 1", "Exchange Ω 日本語 2" } });
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string[] diagrams = graph.Where(e => S(e, "Kind") == "Collaboration").Select(e => S(e, "Id")).ToArray();
        string pool = S(graph.First(e => S(e, "Kind") == "Participant" && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string process = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string task = Guid.NewGuid().ToString(), gateway = Guid.NewGuid().ToString(), sub = Guid.NewGuid().ToString(), nested = Guid.NewGuid().ToString();
        var mutations = new object[] {
            new { Operation = "create", ElementId = task, ParentId = process, ElementType = "UserTask", Name = "Review 日本語 Ω", Documentation = "COMPLEX description 日本語 Ω",
                Geometry = new { X = 100, Y = 100, Width = 140, Height = 80 } },
            new { Operation = "create", ElementId = gateway, ParentId = process, ElementType = "ExclusiveGateway", Name = "Decision Ω",
                Geometry = new { X = 320, Y = 110, Width = 50, Height = 50 } },
            new { Operation = "create", ElementId = sub, ParentId = process, ElementType = "SubProcess", Name = "Nested 日本語",
                Geometry = new { X = 400, Y = 80, Width = 240, Height = 170 } },
            new { Operation = "create", ElementId = nested, ParentId = sub, ElementType = "ManualTask", Name = "Nested task Ω",
                Geometry = new { X = 50, Y = 50, Width = 120, Height = 70 } },
            new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = process, ElementType = "SequenceFlow", SourceId = task, TargetId = gateway,
                Name = "Route Ω", Points = new[] { new { X = 240, Y = 140 }, new { X = 320, Y = 135 } } }
        };
        var seeded = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = mutations });
        path = S(seeded, "outputArtifact"); revision = S(seeded, "outputRevision");
        async Task Patch(string tool, object patch)
        {
            var result = await Op(tool, new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = patch });
            path = S(result, "outputArtifact"); revision = S(result, "outputRevision");
        }
        string role = Guid.NewGuid().ToString(), textDefinition = Guid.NewGuid().ToString(), fileDefinition = Guid.NewGuid().ToString();
        await Patch("native_metadata_apply", new { Resources = new[] { new { Id = role, Name = "Exchange reviewer Ω", Type = "Role" } } });
        await Patch("native_metadata_apply", new { Assignments = new[] { new { ElementId = task, Responsible = new[] { role }, Accountable = new[] { role }, Consulted = new[] { role }, Informed = new[] { role } } } });
        XElement Definition(string id, string type) => new("ExtendedAttribute", new XAttribute("Id", id), new XAttribute("Type", type),
            new XAttribute("ExportAsTable", false), new XAttribute("Visible", false), new XElement("Name", "Exchange " + type + " Ω"),
            new XElement("Description", "Native XPDL fidelity evidence"), new XElement("Options"), new XElement("TableColumns"),
            new XElement("ElementTypes", new XElement("AttributeElementType", new XAttribute("Type", "UserTask"))));
        await Patch("native_attributes_apply", new { Definitions = new[] { new { Id = textDefinition, Xml = Definition(textDefinition, "Text").ToString() }, new { Id = fileDefinition, Xml = Definition(fileDefinition, "FileEmbedded").ToString() } } });
        const string fileName = "Exchange evidence Ω.xml"; byte[] payload = Encoding.UTF8.GetBytes("Own attachment bytes 日本語 Ω\n");
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        var values = new XElement("ElementAttributeValues", new XAttribute("ElementId", task), new XElement("Values",
            new XElement("ExtendedAttributeValue", new XAttribute("Id", textDefinition), new XAttribute("Type", "Text"),
                new XElement("Content", "Preserve 日本語 Ω"), new XElement("DisplayValue", "Preserve 日本語 Ω"), new XElement("TableValues")),
            new XElement("ExtendedAttributeValue", new XAttribute(xsi + "type", "AttachmentAttributeValue"), new XAttribute("Id", fileDefinition), new XAttribute("Type", "FileEmbedded"),
                new XElement("Content", "attachment:" + fileName), new XElement("DisplayValue", fileName), new XElement("TableValues"), new XElement("DisplayName", fileName))));
        await Patch("native_attributes_apply", new { Values = new[] { new { DiagramId = diagrams[0], ElementId = task, Xml = values.ToString() } },
            Attachments = new[] { new { DiagramId = diagrams[0], ElementId = task, FileName = fileName, DataBase64 = Convert.ToBase64String(payload) } } });
        var exportArgs = new Dictionary<string, object?> { ["path"] = path, ["expectedRevision"] = revision, ["diagramIds"] = diagrams, ["acknowledgeFormatLimits"] = true };
        await error("native_xpdl_export", new(exportArgs) { ["acknowledgeFormatLimits"] = false });
        await error("native_xpdl_export", new(exportArgs) { ["expectedRevision"] = new string('0', 64) });
        await error("native_xpdl_export", new(exportArgs) { ["diagramIds"] = new[] { diagrams[0], diagrams[0] } });
        var exported = await Op("native_xpdl_export", exportArgs);
        var files = exported.GetProperty("files").EnumerateArray().ToArray();
        if (files.Length != 2 || files.Select(f => S(f, "artifact")).Distinct().Count() != 2) throw new InvalidDataException("XPDL artifact collision/count failure.");
        if (string.IsNullOrWhiteSpace(S(exported, "warning"))) throw new InvalidDataException("Missing exchange-loss warning.");
        var reopened = exported.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        foreach (string elementId in new[] { task, gateway, sub, nested })
            if (!reopened.Any(e => S(e, "Id") == elementId)) throw new InvalidDataException("Known native XPDL element missing: " + elementId);
        if (S(reopened.Single(e => S(e, "Id") == task), "Name") != "Review 日本語 Ω") throw new InvalidDataException("Unicode task text was corrupted.");
        var exportedDocumentation = exported.GetProperty("exported").GetProperty("Documentation");
        var reopenedDocumentation = exported.GetProperty("reopened").GetProperty("Documentation");
        var originalAttachment = exportedDocumentation.GetProperty("Attachments").EnumerateArray().Single(f => S(f, "FileName") == fileName);
        if (S(originalAttachment, "Sha256") != Hash(payload) || originalAttachment.GetProperty("Length").GetInt64() != payload.Length)
            throw new InvalidDataException("Rich source attachment was not actually loaded by the exporter.");
        if (reopenedDocumentation.GetProperty("Attachments").GetArrayLength() != 0 ||
            !exported.GetProperty("nativeRoundtripComparison").GetProperty("Differences").EnumerateArray().Any(d =>
                S(d, "Classification") == "entry_removed" && S(d, "Entry").EndsWith("/" + fileName, StringComparison.Ordinal)))
            throw new InvalidDataException("Native XPDL embedded-byte loss was not reported explicitly.");
        var nativeDefinitions = reopenedDocumentation.GetProperty("Definitions").EnumerateArray().Select(d => XDocument.Parse(S(d, "Xml")).Root!).ToArray();
        if (nativeDefinitions.Length != 2 || nativeDefinitions.Any(d => (string?)d.Attribute("Type") != "LongText"))
            throw new InvalidDataException("Unexpected native XPDL extended-attribute projection; revalidate the evidence.");
        var nativeValues = reopenedDocumentation.GetProperty("Values").EnumerateArray().Where(v => S(v, "ElementId") == task).Select(v => XDocument.Parse(S(v, "Xml")));
        if (!nativeValues.SelectMany(v => v.Descendants("Content")).Any(v => v.Value == "Preserve 日本語 Ω")) throw new InvalidDataException("Unicode extended value missing.");
        var assignment = exported.GetProperty("reopened").GetProperty("Metadata").GetProperty("Assignments").EnumerateArray().Single(a => S(a, "ElementId") == task);
        foreach (string field in new[] { "Responsible", "Accountable", "Consulted", "Informed" })
            if (!assignment.GetProperty(field).EnumerateArray().Select(v => v.GetString()).SequenceEqual(new[] { role })) throw new InvalidDataException("XPDL RACI mismatch: " + field);
        // The installed importer changes uppercase COMPLEX even in user text; require explicit reporting, not a hidden equivalence claim.
        if (exported.GetProperty("graphDifferences").GetArrayLength() == 0 && S(reopened.Single(e => S(e, "Id") == task), "Documentation") != "COMPLEX description 日本語 Ω")
            throw new InvalidDataException("Native text normalization was not reported.");
        object[] inputs = files.Select(f => (object)new { Path = S(f, "artifact"), ExpectedRevision = S(f, "revision") }).ToArray();
        var imported = await Op("native_xpdl_import", new() { ["inputs"] = inputs, ["acknowledgeFormatLimits"] = true, ["modelName"] = "Imported 日本語 Ω" });
        if (imported.GetProperty("differences").GetArrayLength() != 2) throw new InvalidDataException("Missing input-specific XPDL XML differences.");
        await Op("native_save_copy", new() { ["path"] = S(imported, "outputArtifact"), ["expectedRevision"] = S(imported, "outputRevision") });
        await error("native_xpdl_import", new() { ["inputs"] = new[] { new { Path = S(files[0], "artifact"), ExpectedRevision = new string('0', 64) } }, ["acknowledgeFormatLimits"] = true });
        // Duplicate native document IDs in two distinct captured files must not replace or merge a diagram.
        string firstExport = exported.GetProperty("exported").GetProperty("ExchangeFiles")[0].GetProperty("Path").GetString()!;
        byte[] duplicateBytes = File.ReadAllBytes(firstExport);
        foreach (string name in new[] { "duplicate-one.xpdl", "duplicate-two.xpdl" }) File.WriteAllBytes(Path.Combine(run, name), duplicateBytes);
        await Op("native_xpdl_import", new() { ["inputs"] = new[] { new { Path = "duplicate-one.xpdl", ExpectedRevision = Hash(duplicateBytes) },
            new { Path = "duplicate-two.xpdl", ExpectedRevision = Hash(duplicateBytes) } }, ["acknowledgeFormatLimits"] = true }, "failed");
        var subset = await Op("native_xpdl_export", new(exportArgs) { ["diagramIds"] = new[] { diagrams[0] } });
        if (subset.GetProperty("files").GetArrayLength() != 1 || subset.GetProperty("nativeRoundtripComparison").GetProperty("Preserved").GetBoolean())
            throw new InvalidDataException("Omitted source diagram was not disclosed by native archive comparison.");
        // Real preflight failures do not open the native worker or dereference external XML entities.
        const string ns = "http://www.wfmc.org/2009/XPDL2.2";
        foreach (var item in new[] { ("invalid-version.xpdl", $"<Package xmlns='{ns}'><PackageHeader><XPDLVersion>2.1</XPDLVersion></PackageHeader></Package>"),
            ("prohibited-dtd.xpdl", "<!DOCTYPE Package [<!ENTITY text 'unused'>]>" + $"<Package xmlns='{ns}'><PackageHeader><XPDLVersion>2.2</XPDLVersion></PackageHeader></Package>") })
        {
            byte[] bytes = Encoding.UTF8.GetBytes(item.Item2); File.WriteAllBytes(Path.Combine(run, item.Item1), bytes);
            await error("native_xpdl_import", new() { ["inputs"] = new[] { new { Path = item.Item1, ExpectedRevision = Hash(bytes) } }, ["acknowledgeFormatLimits"] = true });
        }
        await Op("native_xpdl_export", new(exportArgs) { ["diagramIds"] = new[] { Guid.NewGuid().ToString() } }, "failed");
        // A valid operation after a native failure demonstrates that the isolated worker is recoverable.
        var inspected = await Op("native_inspect", new() { ["path"] = path });
        if (S(inspected, "sourceRevision") != revision) throw new InvalidDataException("Original native bytes changed during interchange.");
    }
}
