using System.Text.Json;

/// <summary>Real MCP artifact lifecycle across native persistence, new workers, cloning and installed rendering.</summary>
internal static class NativeArtifactAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = S(await call(tool, args), "OperationId"); var response = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-artifacts.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Artifact lifecycle Ω" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(graph.Single(e => S(e, "Kind") == "Collaboration"), "Id");
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string process = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        async Task<JsonElement> Mutate(object[] mutations, string state = "completed")
        {
            var result = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = mutations }, state);
            if (state == "completed") { path = S(result, "outputArtifact"); revision = S(result, "outputRevision"); graph = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray(); }
            return result;
        }
        string Id() => Guid.NewGuid().ToString();
        string annotation = Id(), formatted = Id(), header = Id(), group = Id(), sub = Id(), nested = Id(), nestedFormatted = Id(), task = Id(), association = Id();
        const string plain = "ANNOTATION-Ω 日本語\nSecond line", rich = "<p><b>FORMATTED-Ω</b> 日本語</p>";
        object Bounds(int x, int y, int w = 130, int h = 65, bool expanded = false) => new { X = x, Y = y, Width = w, Height = h, Expanded = expanded };
        object Node(string id, string owner, string type, object geometry, string? text = null, string? name = null) =>
            new { Operation = "create", ElementId = id, ParentId = owner, ElementType = type, Geometry = geometry, Name = name,
                ArtifactProperties = text == null ? null : new { Text = text } };
        await Mutate([
            Node(sub, process, "SubProcess", Bounds(70, 280), name: "Nested artifacts"),
            Node(task, process, "UserTask", Bounds(80, 90), name: "Artifact reference"),
            Node(annotation, process, "TextAnnotation", Bounds(250, 90), plain),
            Node(formatted, process, "FormattedTextArtifact", Bounds(420, 90), rich),
            Node(header, process, "HeaderArtifact", Bounds(300, 200, 350, 175)),
            Node(group, diagram, "Group", Bounds(50, 60, 650, 320, true), name: "Group Ω"),
            Node(nested, sub, "TextAnnotation", Bounds(70, 80), "NESTED-ANNOTATION-Ω"),
            Node(nestedFormatted, sub, "FormattedTextArtifact", Bounds(250, 80), "<p>NESTED-FORMATTED-Ω</p>"),
            new { Operation = "create", ElementId = association, ParentId = process, ElementType = "Association", SourceId = task, TargetId = annotation,
                Points = new[] { new { X = 180, Y = 110 }, new { X = 250, Y = 110 } } }
        ]);
        JsonElement Element(string id) => graph.Single(e => S(e, "Id") == id);
        if (S(Element(annotation).GetProperty("Artifact"), "Text") != plain || S(Element(formatted).GetProperty("Artifact"), "Text") != rich)
            throw new InvalidDataException("Artifact content did not survive native persistence.");
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        // These are actual worker failures, not only preflight parameter rejections.
        await Mutate([new { Operation = "delete", ElementId = annotation }], "failed");
        await Mutate([new { Operation = "update", ElementId = task, ArtifactProperties = new { Text = "wrong kind" } }], "failed");
        await Mutate([Node(Id(), process, "Group", Bounds(60, 70, 130, 90, true), name: "Wrong owner")], "failed");
        await Mutate([new { Operation = "update", ElementId = annotation, Geometry = Bounds(900, 900) }], "failed");
        await Mutate([
            new { Operation = "update", ElementId = annotation, ArtifactProperties = new { Text = "UPDATED-ANNOTATION-Ω" }, Documentation = "Annotation documentation" },
            new { Operation = "update", ElementId = formatted, ArtifactProperties = new { Text = "<p><i>UPDATED-FORMATTED-Ω</i></p>" }, Documentation = "Formatted documentation" },
            new { Operation = "update", ElementId = group, Name = "Renamed group Ω", Geometry = Bounds(45, 55, 660, 330, true) },
            new { Operation = "update", ElementId = header, Documentation = "Header documentation", Geometry = Bounds(320, 195, 350, 175) },
            new { Operation = "update", ElementId = nested, ArtifactProperties = new { Text = "UPDATED-NESTED-Ω" } }
        ]);
        await Mutate([new { Operation = "update", ElementId = task, Name = "Unrelated change preserves artifacts" }]);
        // Artifact handles are protocol references, not filesystem paths. Preserve their native SHA-256 receipt.
        string originalPath = path, originalHash = revision;
        var originals = graph.Where(e => e.GetProperty("Artifact").ValueKind == JsonValueKind.Object).ToDictionary(e => S(e, "Id"), e => e.GetProperty("Artifact").Clone());
        var clone = await Op("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["patch"] = new { Changes = new[] { new { Operation = "clone", DiagramId = diagram, Name = "Cloned artifacts Ω" } } } });
        path = S(clone, "outputArtifact"); revision = S(clone, "outputRevision"); graph = clone.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        var map = clone.GetProperty("edited").GetProperty("DiagramClones")[0].GetProperty("Identities").EnumerateArray().ToDictionary(e => S(e, "SourceId"), e => S(e, "TargetId"));
        foreach (var item in originals)
        {
            if (!JsonElement.DeepEquals(Element(item.Key).GetProperty("Artifact"), item.Value)) throw new InvalidDataException("Clone mutated original artifact properties.");
            var copied = Element(map[item.Key]).GetProperty("Artifact");
            foreach (string field in new[] { "Type", "Text", "TextFormat" })
                if (!JsonElement.DeepEquals(copied.GetProperty(field), item.Value.GetProperty(field))) throw new InvalidDataException("Clone changed artifact content.");
            if (item.Value.GetProperty("HeaderDiagramId").ValueKind == JsonValueKind.String && S(copied, "HeaderDiagramId") != map[diagram])
                throw new InvalidDataException("Clone did not remap the header context.");
        }
        async Task Render(string subProcess = "")
        {
            var rendered = await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram, ["subProcessId"] = subProcess });
            string surface = subProcess == "" ? diagram : subProcess;
            var artifacts = rendered.GetProperty("result").GetProperty("Artifacts").EnumerateArray().Select(a => a.GetString()!).ToArray();
            // A successful rendering of the wrong surface must not pass a nested-artifact test.
            foreach (string extension in new[] { ".svg", ".png" })
                if (artifacts.Count(a => a.EndsWith(surface + extension, StringComparison.Ordinal)) != 1)
                    throw new InvalidDataException("Native rendering did not return the explicitly requested surface.");
        }
        await Render(); await Render(sub);
        await Op("native_publish", new() { ["path"] = path, ["format"] = "word", ["title"] = "Native artifacts publication" });
        await Mutate([new { Operation = "update", ElementId = annotation, ArtifactProperties = new { Text = "" } },
            new { Operation = "update", ElementId = formatted, ArtifactProperties = new { Text = "" } }]);
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        await Mutate(new[] { association, annotation, formatted, header, group, nested, nestedFormatted, task, sub }.Select(id => (object)new { Operation = "delete", ElementId = id }).ToArray());
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var inspect = await Op("native_inspect", new() { ["path"] = originalPath });
        if (S(inspect, "sourceRevision") != originalHash) throw new InvalidDataException("Artifact operations modified their source bytes.");
    }
    private static string S(JsonElement element, string name) => element.GetProperty(name).GetString()!;
}
