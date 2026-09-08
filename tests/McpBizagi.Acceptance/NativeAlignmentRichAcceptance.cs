using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>Native MCP nested selection, embedded-byte fidelity, manual labels and active CEF cancellation.</summary>
internal static class NativeAlignmentRichAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        var records = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> input, string state = "completed")
        {
            string id = S(await call(tool, input), "OperationId");
            var result = await wait(id, state); exited(id);
            records.Add(new { tool, id, result });
            File.WriteAllText(Path.Combine(run, "native-alignment-rich.json"), JsonSerializer.Serialize(records));
            return state == "completed" ? result.GetProperty("Result") : result;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Nested alignment Ω", "Untouched 日本語" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(graph.First(e => S(e, "Kind") == "Collaboration"), "Id");
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string process = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string outer = Guid.NewGuid().ToString(), inner = Guid.NewGuid().ToString(), a = Guid.NewGuid().ToString(), b = Guid.NewGuid().ToString(), flow = Guid.NewGuid().ToString();
        object Node(string type, string id, string parent, int x, int y) => new { Operation = "create", ElementType = type, ElementId = id, ParentId = parent,
            Name = "Nested " + type + " Ω", Geometry = new { X = x, Y = y, Width = 100, Height = 60 } };
        var seeded = await Op("native_mutate", new() { ["path"] = S(created, "outputArtifact"), ["expectedRevision"] = S(created, "outputRevision"), ["mutations"] = new object[]
        {
            Node("SubProcess", outer, process, 260, 85), Node("SubProcess", inner, outer, 30, 30),
            Node("UserTask", a, inner, 30, 30), Node("ManualTask", b, inner, 230, 90),
            new { Operation = "create", ElementType = "SequenceFlow", ElementId = flow, ParentId = inner, SourceId = a, TargetId = b,
                Points = new[] { new { X = 130, Y = 60 }, new { X = 230, Y = 120 } } }
        } });
        string path = S(seeded, "outputArtifact"), revision = S(seeded, "outputRevision");
        // Author an actual embedded attribute using the same public transaction as an operator.
        string attribute = Guid.NewGuid().ToString();
        var definition = new XElement("ExtendedAttribute", new XAttribute("Id", attribute), new XAttribute("Type", "FileEmbedded"), new XAttribute("ExportAsTable", false), new XAttribute("Visible", false),
            new XElement("Name", "Alignment evidence Ω"), new XElement("Description", "Own preservation corpus"), new XElement("Options"), new XElement("TableColumns"),
            new XElement("ElementTypes", new XElement("AttributeElementType", new XAttribute("Type", "UserTask"))));
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        var value = new XElement("ExtendedAttributeValue", new XAttribute(xsi + "type", "AttachmentAttributeValue"), new XAttribute("Id", attribute), new XAttribute("Type", "FileEmbedded"),
            new XElement("Content", "attachment:Nested Ω.txt"), new XElement("DisplayValue", "Nested Ω.txt"), new XElement("TableValues"), new XElement("DisplayName", "Nested Ω.txt"));
        var values = new XElement("ElementAttributeValues", new XAttribute("ElementId", a), new XElement("Values", value));
        byte[] content = Encoding.UTF8.GetBytes("Own alignment preservation bytes Ω 日本語\n");
        var rich = await Op("native_attributes_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new
        {
            Definitions = new[] { new { Id = attribute, Xml = definition.ToString() } },
            Values = new[] { new { DiagramId = diagram, ElementId = a, Xml = values.ToString() } },
            Attachments = new[] { new { DiagramId = diagram, ElementId = a, FileName = "Nested Ω.txt", DataBase64 = Convert.ToBase64String(content) } }
        } });
        path = S(rich, "outputArtifact"); revision = S(rich, "outputRevision");
        Dictionary<string, object?> Args(string source, string hash) => new() { ["path"] = source, ["expectedRevision"] = hash,
            ["alignment"] = new { DiagramId = diagram, SubProcessId = inner, Mode = "Bottom", ElementIds = new[] { a, b } } };
        var aligned = await Op("native_elements_align", Args(path, revision));
        var exported = await Op("native_attachment_export", new() { ["path"] = S(aligned, "outputArtifact"), ["diagramId"] = diagram, ["elementId"] = a, ["fileName"] = "Nested Ω.txt" });
        if (!File.ReadAllBytes(S(exported, "artifactPath")).SequenceEqual(content)) throw new InvalidDataException("Nested alignment lost embedded bytes.");
        // A nonzero manual rectangle must remain an explicit intent, never be reset as automatic.
        var labeled = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new[]
        {
            new { Operation = "update", ElementId = a, Style = new { LabelBounds = new { X = 35, Y = 40, Width = 80, Height = 40 } } }
        } });
        var manual = await Op("native_elements_align", Args(S(labeled, "outputArtifact"), S(labeled, "outputRevision")));
        var bounds = manual.GetProperty("reopened").GetProperty("Elements").EnumerateArray().Single(e => S(e, "Id") == a).GetProperty("Style").GetProperty("LabelBounds");
        if (bounds.GetProperty("X").GetDouble() != 35 || bounds.GetProperty("Y").GetDouble() != 100 || bounds.GetProperty("Width").GetDouble() != 80 || bounds.GetProperty("Height").GetDouble() != 40)
            throw new InvalidDataException("Native manual label size/offset did not survive alignment and readback.");

        // Cancel only after observing the actual offscreen phase through MCP, not a sleep.
        string cancelId = S(await call("native_elements_align", Args(path, revision)), "OperationId");
        var timer = System.Diagnostics.Stopwatch.StartNew(); string last = "";
        while (true)
        {
            var state = await call("operation_get", new() { ["operationId"] = cancelId });
            string phase = S(state, "Phase");
            if (phase != last) { Console.WriteLine($"alignment cancellation phase={phase} elapsed={timer.Elapsed}"); last = phase; }
            if (phase.StartsWith("native_render_surface:", StringComparison.Ordinal) || phase.StartsWith("native_editor_", StringComparison.Ordinal)) break;
            if (S(state, "State") is "completed" or "failed" or "cancelled" or "interrupted") throw new InvalidDataException("Did not observe a live CEF cancellation boundary.");
            await Task.Delay(100);
        }
        await call("operation_cancel", new() { ["operationId"] = cancelId });
        var cancelled = await wait(cancelId, "cancelled"); exited(cancelId);
        records.Add(new { tool = "operation_cancel", id = cancelId, result = cancelled });
        await Op("native_elements_align", Args(path, revision));
        File.WriteAllText(Path.Combine(run, "nested-attachment-sha256.txt"), Convert.ToHexStringLower(SHA256.HashData(content)));
    }
    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString()!;
}
