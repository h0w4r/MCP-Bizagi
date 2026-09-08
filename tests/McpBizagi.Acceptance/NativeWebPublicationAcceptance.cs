using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>Real MCP/native Web generation, selected content, attachment bytes and recovery.</summary>
internal static class NativeWebPublicationAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = S(await call(tool, args), "OperationId");
            var response = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-web-publication.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Published process Ω", "Excluded process 日本語" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        var diagrams = graph.Where(e => S(e, "Kind") == "Collaboration").Select(e => S(e, "Id")).ToArray();
        string Process(string diagram)
        {
            string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
            return S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        }
        string task = Guid.NewGuid().ToString(), sub = Guid.NewGuid().ToString(), child = Guid.NewGuid().ToString();
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        var edited = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new object[] {
            new { Operation = "create", ElementId = task, ParentId = Process(diagrams[0]), ElementType = "UserTask", Name = "Review evidence Ω", Documentation = "Root publication description 日本語", Geometry = new { X = 100, Y = 100, Width = 130, Height = 70 } },
            new { Operation = "create", ElementId = sub, ParentId = Process(diagrams[0]), ElementType = "SubProcess", Name = "Nested publication", Documentation = "Nested process documentation", Geometry = new { X = 400, Y = 80, Width = 240, Height = 170 } },
            new { Operation = "create", ElementId = child, ParentId = sub, ElementType = "ManualTask", Name = "Pack parcel 日本語", Documentation = "Nested task documentation Ω", Geometry = new { X = 50, Y = 50, Width = 120, Height = 70 } },
            new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = Process(diagrams[1]), ElementType = "UserTask", Name = "Excluded task sentinel", Documentation = "Excluded description sentinel", Geometry = new { X = 100, Y = 100, Width = 130, Height = 70 } }
        } });
        path = S(edited, "outputArtifact"); revision = S(edited, "outputRevision");
        async Task Attributes(object patch)
        {
            var result = await Op("native_attributes_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = patch });
            path = S(result, "outputArtifact"); revision = S(result, "outputRevision");
        }
        string definitionId = Guid.NewGuid().ToString();
        var definition = new XElement("ExtendedAttribute", new XAttribute("Id", definitionId), new XAttribute("Type", "FileEmbedded"),
            new XAttribute("ExportAsTable", false), new XAttribute("Visible", false), new XElement("Name", "Web evidence attachment"), new XElement("Description", "Own publication corpus"),
            new XElement("Options"), new XElement("TableColumns"), new XElement("ElementTypes", new XElement("AttributeElementType", new XAttribute("Type", "UserTask"))));
        await Attributes(new { Definitions = new[] { new { Id = definitionId, Xml = definition.ToString() } } });
        const string fileName = "Evidence Ω.txt";
        byte[] payload = Encoding.UTF8.GetBytes("Actual native Web attachment bytes 日本語 Ω\n");
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        var values = new XElement("ElementAttributeValues", new XAttribute("ElementId", task), new XElement("Values",
            new XElement("ExtendedAttributeValue", new XAttribute(xsi + "type", "AttachmentAttributeValue"), new XAttribute("Id", definitionId), new XAttribute("Type", "FileEmbedded"),
                new XElement("Content", "attachment:" + fileName), new XElement("DisplayValue", fileName), new XElement("TableValues"), new XElement("DisplayName", fileName))));
        await Attributes(new { Values = new[] { new { DiagramId = diagrams[0], ElementId = task, Xml = values.ToString() } },
            Attachments = new[] { new { DiagramId = diagrams[0], ElementId = task, FileName = fileName, DataBase64 = Convert.ToBase64String(payload) } } });
        Dictionary<string, object?> Args(string[] selected) => new() { ["path"] = path, ["format"] = "web", ["diagramIds"] = selected, ["title"] = "Verified native Web Ω 日本語" };
        await error("native_publish", Args([diagrams[0], diagrams[0]]));
        var rejected = await Op("native_publish", Args([Guid.NewGuid().ToString()]), "failed");
        if (!S(rejected, "Error").Contains("selection", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Web rejection failed for an unrelated reason.");
        var selected = await Op("native_publish", Args([diagrams[0]]));
        Check(selected, [diagrams[0], sub], excluded: true);
        var all = await Op("native_publish", Args([]));
        Check(all, [diagrams[0], diagrams[1], sub], excluded: false);
        // Cancel a real registering publisher, then execute publication again to prove recovery.
        string cancelId = S(await call("native_publish", Args([diagrams[0]])), "OperationId");
        while (true)
        {
            var state = await call("operation_get", new() { ["operationId"] = cancelId });
            string phase = S(state, "Phase");
            if (phase.StartsWith("register:", StringComparison.Ordinal) || phase == "native_registration") break;
            if (S(state, "State") is "completed" or "failed" or "cancelled" or "interrupted") throw new InvalidDataException("No active native publication cancellation boundary observed.");
            await Task.Delay(100);
        }
        await call("operation_cancel", new() { ["operationId"] = cancelId });
        var cancelled = await wait(cancelId, "cancelled"); exited(cancelId);
        receipts.Add(new { tool = "operation_cancel", id = cancelId, response = cancelled });
        Check(await Op("native_publish", Args([diagrams[0]])), [diagrams[0], sub], excluded: true);
        var original = await Op("native_inspect", new() { ["path"] = path });
        if (S(original, "sourceRevision") != revision) throw new InvalidDataException("Web publication changed its original native model.");

        void Check(JsonElement publication, string[] expectedPages, bool excluded)
        {
            var readback = publication.GetProperty("reopened").GetProperty("Publication");
            var web = readback.GetProperty("Web");
            var pages = web.GetProperty("Pages").EnumerateArray().Select(p => S(p, "Id")).ToHashSet();
            if (!pages.SetEquals(expectedPages) || !web.GetProperty("SearchContainerIds").EnumerateArray().Select(p => p.GetString()!).ToHashSet().SetEquals(expectedPages))
                throw new InvalidDataException("Web page/search inventory does not match selected root and nested content.");
            string text = S(readback, "Text");
            foreach (string expected in new[] { "Review evidence Ω", "Pack parcel 日本語", "Root publication description 日本語", "Nested task documentation Ω" })
                if (!text.Contains(expected, StringComparison.Ordinal)) throw new InvalidDataException("Missing Web content: " + expected);
            if (excluded && (text.Contains("Excluded task sentinel", StringComparison.Ordinal) || text.Contains("Excluded description sentinel", StringComparison.Ordinal)))
                throw new InvalidDataException("Unselected diagram content leaked into Web publication.");
            string payloadHash = Convert.ToHexStringLower(SHA256.HashData(payload));
            if (!web.GetProperty("Assets").EnumerateArray().Any(a => S(a, "Path").StartsWith("files/attachments/", StringComparison.Ordinal) && S(a, "Sha256") == payloadHash))
                throw new InvalidDataException("Native Web attachment bytes are missing or changed.");
            if (!publication.GetProperty("nativeSourceUnmodified").GetBoolean()) throw new InvalidDataException("Publication did not retain its original input.");
        }
    }

    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString()!;
}
