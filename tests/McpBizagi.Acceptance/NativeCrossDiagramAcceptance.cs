using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

/// <summary>MCP-authored inputs for independent cross-diagram durable verification.</summary>
internal sealed record CrossDiagramSeed(string Path, string Revision, string Diagram, string Process,
    string Root, string Nested, string Child, string Leaf, string Image, string FileName, byte[] Payload);

/// <summary>Real engine evidence; does not reference any production fidelity or relocation policy.</summary>
internal static class NativeCrossDiagramAcceptance
{
    private static string S(JsonElement e, string name) => e.GetProperty(name).GetString()!;
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited,
        Func<string, Dictionary<string, object?>, Task<JsonElement>> error, CrossDiagramSeed seed)
    {
        string path = seed.Path, revision = seed.Revision;
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> input, string state = "completed")
        {
            string id = S(await call(tool, input), "OperationId"); var response = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-cross-diagram.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        async Task<JsonElement> Write(string tool, string field, object value)
        {
            var result = await Op(tool, new() { ["path"] = path, ["expectedRevision"] = revision, [field] = value });
            path = S(result, "outputArtifact"); revision = S(result, "outputRevision"); return result;
        }
        var inspection = await Op("native_inspect", new() { ["path"] = path });
        var initial = inspection.GetProperty("result").GetProperty("Elements").EnumerateArray().ToArray();
        string targetDiagram = S(initial.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Name") == "Cross target Ω"), "Id");
        string targetPool = S(initial.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == targetDiagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string targetProcess = S(initial.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == targetPool), "Id");
        string target = Guid.NewGuid().ToString(), retained = Guid.NewGuid().ToString();
        object Move(string id, string from, string to, string sourceDiagram, string destinationDiagram) => new {
            ElementId = id, ExpectedParentId = from, TargetParentId = to, ExpectedDiagramId = sourceDiagram, TargetDiagramId = destinationDiagram };
        object[] Forward() => [Move(seed.Root, seed.Process, target, seed.Diagram, targetDiagram)];
        await Write("native_mutate", "mutations", new object[] {
            new { Operation = "create", ElementId = target, ParentId = targetProcess, ElementType = "SubProcess", Name = "Destination container 日本語",
                Geometry = new { X = 100, Y = 100, Width = 140, Height = 90 } },
            new { Operation = "create", ElementId = retained, ParentId = target, ElementType = "ManualTask", Name = "Destination content remains Ω",
                Documentation = "Unrelated destination documentation 日本語", Geometry = new { X = 500, Y = 100, Width = 140, Height = 90 } }
        });
        // Configured simulation is deliberately NOT migrated by this initial
        // contract: prove explicit rejection, then explicitly replace it in this
        // synthetic operator-owned model. Preserve the configured original.
        var rejected = await Op("native_elements_reparent", new() { ["path"] = path, ["expectedRevision"] = revision, ["moves"] = Forward() }, "failed");
        if (!S(rejected, "Error").Contains("simulation", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Configured scenario failed for an unrelated reason.");
        await Write("native_metadata_apply", "patch", new { Simulations = new[] { new { DiagramId = seed.Diagram,
            Xml = "<BPSimData xmlns='http://www.bpsim.org/schemas/1.0' simulationLevel='LevelTwo'/>" } }, DiscardSimulationResults = true });
        string originalPath = path, originalRevision = revision;
        await error("native_elements_reparent", new() { ["path"] = path, ["expectedRevision"] = new string('0', 64), ["moves"] = Forward() });
        var unacknowledged = await Op("native_elements_reparent", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["moves"] = new[] { new { ElementId = seed.Root, ExpectedParentId = seed.Process, TargetParentId = target } } }, "failed");
        if (!S(unacknowledged, "Error").Contains("diagram identities", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Missing migration intent failed for an unrelated reason.");

        var forward = await Write("native_elements_reparent", "moves", Forward());
        var before = forward.GetProperty("before"); var after = forward.GetProperty("reopened");
        var graph = before.GetProperty("Elements").EnumerateArray().ToDictionary(e => S(e, "Id"));
        bool Descendant(string id)
        {
            while (id != "") { if (id == seed.Root) return true; id = S(graph[id], "ParentId"); }
            return false;
        }
        var moved = graph.Keys.Where(Descendant).ToHashSet(StringComparer.Ordinal);
        // Compare the full observed graph, changing only explicit ownership and
        // nested port diagram IDs. Array traversal order is not element identity.
        var expected = graph.ToDictionary(p => p.Key, p => JsonNode.Parse(p.Value.GetRawText())!);
        foreach (string id in moved)
        {
            expected[id]["DiagramId"] = targetDiagram;
            if (id == seed.Root) expected[id]["ParentId"] = target;
            if (expected[id]["DataFlow"] is JsonObject io)
                foreach (string field in new[] { "Inputs", "Outputs", "InputAssociations", "OutputAssociations" })
                    foreach (var port in io[field]!.AsArray()) port!["DiagramId"] = targetDiagram;
        }
        var observed = after.GetProperty("Elements").EnumerateArray().ToDictionary(e => S(e, "Id"));
        if (!expected.Keys.Order().SequenceEqual(observed.Keys.Order())) throw new InvalidDataException("Cross-diagram move changed element identities.");
        foreach (string id in expected.Keys)
            if (!JsonNode.DeepEquals(expected[id], JsonNode.Parse(observed[id].GetRawText()))) throw new InvalidDataException("Cross-diagram move changed an unrelated graph field: " + id);
        var originalImage = await Op("native_image_export", new() { ["path"] = originalPath, ["diagramId"] = seed.Diagram, ["elementId"] = seed.Image });
        var exportedImage = await Op("native_image_export", new() { ["path"] = path, ["diagramId"] = targetDiagram, ["elementId"] = seed.Image });
        if (!File.ReadAllBytes(S(originalImage, "artifactPath")).SequenceEqual(File.ReadAllBytes(S(exportedImage, "artifactPath")))) throw new InvalidDataException("Image bytes changed during migration.");
        var attachment = await Op("native_attachment_export", new() { ["path"] = path, ["diagramId"] = targetDiagram, ["elementId"] = seed.Child, ["fileName"] = seed.FileName });
        if (!File.ReadAllBytes(S(attachment, "artifactPath")).SequenceEqual(seed.Payload)) throw new InvalidDataException("Embedded bytes changed during migration.");
        string Metadata(JsonElement reply) => JsonSerializer.Serialize(reply.GetProperty("Metadata").EnumerateObject().ToDictionary(p => p.Name,
            p => p.Name == "Assignments" ? (object)p.Value.EnumerateArray().OrderBy(e => S(e, "ElementId"), StringComparer.Ordinal).ToArray() : p.Value));
        if (Metadata(before) != Metadata(after)) throw new InvalidDataException("Cross-diagram move changed RACI or unrequested metadata.");
        // Native documentation reads already resolve archive-owned attachments
        // into stable attachment: names. Compare complete records and definitions,
        // not just a convenient text value or the embedded byte count.
        var documentation = JsonNode.Parse(before.GetProperty("Documentation").GetRawText())!;
        var actualDocumentation = JsonNode.Parse(after.GetProperty("Documentation").GetRawText())!;
        foreach (var snapshot in new[] { documentation, actualDocumentation })
            foreach (var definition in snapshot["Definitions"]!.AsArray())
            {
                // The native loader refreshes this specific audit timestamp.
                // Retain its observed values in the receipts; compare all other
                // definition content, including ModifiedBy, without projection.
                var xml = XDocument.Parse(definition!["Xml"]!.GetValue<string>());
                if (xml.Root?.Name != "ExtendedAttribute" || (string?)xml.Root.Attribute("Id") != definition["Id"]!.GetValue<string>() ||
                    !DateTimeOffset.TryParse((string?)xml.Root.Attribute("ModificationDate"), out _))
                    throw new InvalidDataException("Unrecognized native definition timestamp scope.");
                xml.Root.Attribute("ModificationDate")!.Remove(); definition["Xml"] = xml.ToString();
            }
        foreach (string field in new[] { "Values", "Attachments" })
        {
            foreach (var entry in documentation[field]!.AsArray())
                if (moved.Contains(entry!["ElementId"]!.GetValue<string>())) entry["DiagramId"] = targetDiagram;
            string Key(JsonNode? entry) => entry!["ElementId"]!.GetValue<string>() + ":" + (entry["FileName"]?.GetValue<string>() ?? "");
            documentation[field] = new JsonArray(documentation[field]!.AsArray().OrderBy(Key, StringComparer.Ordinal).Select(e => e!.DeepClone()).ToArray());
            actualDocumentation[field] = new JsonArray(actualDocumentation[field]!.AsArray().OrderBy(Key, StringComparer.Ordinal).Select(e => e!.DeepClone()).ToArray());
        }
        if (!JsonNode.DeepEquals(documentation, actualDocumentation)) throw new InvalidDataException("Extended definitions, values or attachment records changed beyond diagram ownership.");
        var tabs = JsonNode.Parse(before.GetProperty("DiagramState").GetProperty("OpenedItems").GetRawText())!.AsArray();
        foreach (var tab in tabs) if (moved.Contains(tab!["SubProcessId"]!.GetValue<string>())) tab["DiagramId"] = targetDiagram;
        if (!JsonNode.DeepEquals(tabs, JsonNode.Parse(after.GetProperty("DiagramState").GetProperty("OpenedItems").GetRawText()))) throw new InvalidDataException("Persisted tab identity, order or selection changed.");
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = targetDiagram, ["subProcessId"] = seed.Nested });

        // Exercise a standalone leaf and a binary-backed artifact separately:
        // moving a whole subprocess alone does not cover these native routes.
        await Write("native_elements_reparent", "moves", new[] { Move(seed.Leaf, seed.Nested, seed.Process, targetDiagram, seed.Diagram) });
        await Write("native_elements_reparent", "moves", new[] { Move(seed.Leaf, seed.Process, seed.Nested, seed.Diagram, targetDiagram) });
        await Write("native_elements_reparent", "moves", new[] { Move(seed.Image, seed.Nested, seed.Process, targetDiagram, seed.Diagram) });
        await Write("native_elements_reparent", "moves", new[] { Move(seed.Image, seed.Process, seed.Nested, seed.Diagram, targetDiagram) });
        var backward = await Write("native_elements_reparent", "moves", new[] { Move(seed.Root, target, seed.Process, targetDiagram, seed.Diagram) });
        var restored = backward.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToDictionary(e => S(e, "Id"));
        foreach (var pair in graph)
            if (!JsonElement.DeepEquals(pair.Value, restored[pair.Key])) throw new InvalidDataException("Round-trip graph differs: " + pair.Key);
        var reopened = await Op("native_inspect", new() { ["path"] = path });
        if (S(reopened, "sourceRevision") != revision) throw new InvalidDataException("Fresh reader revision differs.");
        foreach (var original in new[] { (originalPath, originalRevision), (seed.Path, seed.Revision) })
        {
            var read = await Op("native_inspect", new() { ["path"] = original.Item1 });
            if (S(read, "sourceRevision") != original.Item2) throw new InvalidDataException("Migration changed an original input.");
        }
        File.WriteAllText(Path.Combine(run, "cross-diagram-summary.json"), JsonSerializer.Serialize(new {
            movedElements = moved.Count, originalRevision, finalRevision = revision, attachmentSha256 = Convert.ToHexString(SHA256.HashData(seed.Payload)).ToLowerInvariant(),
            configuredSimulationMigration = "NOT_IMPLEMENTED_EXPLICIT_REJECTION_VERIFIED", guiCompatibility = "NOT_ACCREDITED"
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
