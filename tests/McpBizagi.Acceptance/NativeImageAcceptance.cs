using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Actual SDK-to-installed-engine image lifecycle; authored inputs are not engine doubles.</summary>
internal static class NativeImageAcceptance
{
    public static async Task Run(string repo, string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = S(await call(tool, args), "OperationId"); var response = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-images.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        string fixtureRoot = Path.Combine(repo, "tests", "McpBizagi.Acceptance", "Fixtures", "images"), inputs = Path.Combine(run, "Image inputs Ω");
        Directory.CreateDirectory(inputs);
        var corpus = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtureRoot, "manifest.json"))).RootElement.EnumerateArray().ToArray();
        foreach (var source in corpus)
        {
            string name = S(source, "fileName"); byte[] bytes = File.ReadAllBytes(Path.Combine(fixtureRoot, name));
            if (Hash(bytes) != S(source, "sha256")) throw new InvalidDataException("Authored image corpus hash differs from its manifest.");
            File.WriteAllBytes(Path.Combine(inputs, name), bytes);
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Image lifecycle Ω" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(graph.Single(e => S(e, "Kind") == "Collaboration"), "Id");
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string process = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        async Task<JsonElement> Mutate(object[] changes, string state = "completed")
        {
            var result = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = changes }, state);
            if (state == "completed") { path = S(result, "outputArtifact"); revision = S(result, "outputRevision"); graph = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray(); }
            return result;
        }
        object Bounds(int x, int y) => new { X = x, Y = y, Width = 100, Height = 75 };
        object Input(string file, int? frame = null) => new { SourcePath = Path.Combine(inputs, file), ExpectedRevision = S(corpus.Single(c => S(c, "fileName") == file), "sha256"), AllowPngReencoding = true, FrameIndex = frame };
        string sub = Guid.NewGuid().ToString(), task = Guid.NewGuid().ToString();
        var images = corpus.Select((source, index) => new { Id = Guid.NewGuid().ToString(), Source = source, Frame = source.GetProperty("frames").GetArrayLength() - 1, Nested = index >= 4 }).ToArray();
        var changes = new List<object> {
            new { Operation = "create", ElementId = sub, ParentId = process, ElementType = "SubProcess", Name = "Nested image surface", Geometry = Bounds(80, 280) },
            new { Operation = "create", ElementId = task, ParentId = process, ElementType = "UserTask", Name = "Unrelated task", Geometry = Bounds(210, 280) }
        };
        changes.AddRange(images.Select((image, index) => (object)new { Operation = "create", ElementId = image.Id, ParentId = image.Nested ? sub : process,
            ElementType = "ImageArtifact", Name = "Image " + S(image.Source, "fileName"), Geometry = Bounds(80 + index % 4 * 140, 100),
            ArtifactProperties = new { Image = Input(S(image.Source, "fileName"), image.Frame) } }));
        var imported = await Mutate(changes.ToArray());
        foreach (var image in images)
        {
            var actual = graph.Single(e => S(e, "Id") == image.Id).GetProperty("Artifact").GetProperty("Image");
            var expected = image.Source.GetProperty("frames")[image.Frame];
            if (actual.GetProperty("Width").GetInt32() != expected.GetProperty("width").GetInt32() || actual.GetProperty("Height").GetInt32() != expected.GetProperty("height").GetInt32() ||
                actual.GetProperty("HasTransparency").GetBoolean() != expected.GetProperty("hasTransparency").GetBoolean())
                throw new InvalidDataException("Image dimensions/alpha differ from the independently authored source.");
            // JPEG decoder rounding is codec-specific. All lossless inputs require a cross-library exact pixel fingerprint.
            if (!S(image.Source, "fileName").EndsWith(".jpg", StringComparison.Ordinal) && S(actual, "PixelSha256") != S(expected, "pixelSha256"))
                throw new InvalidDataException("Lossless image pixels differ from independent Pillow fixture evidence: " + S(image.Source, "fileName"));
        }
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        string alpha = images.Single(i => S(i.Source, "fileName") == "alpha.png").Id;
        var exported = await Op("native_image_export", new() { ["path"] = path, ["diagramId"] = diagram, ["elementId"] = alpha });
        if (Hash(File.ReadAllBytes(S(exported, "artifactPath"))) != S(exported, "outputRevision")) throw new InvalidDataException("Export receipt differs from durable bytes.");
        await Mutate([new { Operation = "update", ElementId = alpha, ArtifactProperties = new { Image = new { SourcePath = S(exported, "outputArtifact"), ExpectedRevision = S(exported, "outputRevision"), AllowPngReencoding = true } } }]);
        await Mutate([new { Operation = "update", ElementId = task, Name = "Unrelated edit preserves every image" }]);
        await Mutate([new { Operation = "update", ElementId = alpha, ArtifactProperties = new { Image = Input("animated.gif") } }], "failed");
        await Mutate([new { Operation = "update", ElementId = alpha, ArtifactProperties = new { Image = Input("multipage.tiff", 99) } }], "failed");
        await Mutate([new { Operation = "update", ElementId = task, ArtifactProperties = new { Image = Input("alpha.png") } }], "failed");
        string corrupt = Path.Combine(inputs, "corrupt.png"); File.WriteAllText(corrupt, "not an image");
        await Mutate([new { Operation = "update", ElementId = alpha, ArtifactProperties = new { Image = new { SourcePath = corrupt, ExpectedRevision = Hash(File.ReadAllBytes(corrupt)), AllowPngReencoding = true } } }], "failed");
        await error("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new[] {
            new { Operation = "update", ElementId = alpha, ArtifactProperties = new { Image = new { SourcePath = Path.Combine(inputs, "alpha.png"), ExpectedRevision = new string('0', 64), AllowPngReencoding = true } } } } });
        await Mutate([new { Operation = "update", ElementId = alpha, ArtifactProperties = new { Image = Input("multipage.tiff", 0) }, Geometry = Bounds(90, 110) }]);
        if (S(graph.Single(e => S(e, "Id") == alpha).GetProperty("Artifact").GetProperty("Image"), "PixelSha256") !=
            S(corpus.Single(c => S(c, "fileName") == "multipage.tiff").GetProperty("frames")[0], "pixelSha256"))
            throw new InvalidDataException("Selected TIFF frame pixels differ from independent fixture evidence.");
        string originalPath = path, originalRevision = revision;
        var clone = await Op("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["patch"] = new { Changes = new[] { new { Operation = "clone", DiagramId = diagram, Name = "Cloned image surface Ω" } } } });
        path = S(clone, "outputArtifact"); revision = S(clone, "outputRevision");
        var map = clone.GetProperty("edited").GetProperty("DiagramClones")[0].GetProperty("Identities").EnumerateArray().ToDictionary(e => S(e, "SourceId"), e => S(e, "TargetId"));
        var files = clone.GetProperty("reopened").GetProperty("ImageFiles").EnumerateArray().ToArray();
        foreach (var image in images)
            if (S(files.Single(f => S(f, "ElementId") == image.Id), "Sha256") != S(files.Single(f => S(f, "ElementId") == map[image.Id]), "Sha256"))
                throw new InvalidDataException("Cloned native image file bytes changed.");
        async Task Render(bool nested)
        {
            var render = await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram, ["subProcessId"] = nested ? sub : "" });
            var result = render.GetProperty("result"); string surface = nested ? sub : diagram;
            string svgPath = result.GetProperty("Artifacts").EnumerateArray().Select(e => e.GetString()!).Single(p => p.EndsWith(surface + ".svg", StringComparison.Ordinal));
            var svg = System.Xml.Linq.XDocument.Load(svgPath);
            var evidence = result.GetProperty("RenderedImages").EnumerateArray().Where(e => S(e, "SurfaceId") == surface).ToArray();
            var expected = images.Where(i => i.Nested == nested).ToArray();
            if (evidence.Length != expected.Length) throw new InvalidDataException("SVG pixel receipts do not cover the requested image surface.");
            foreach (var image in expected)
            {
                var receipt = evidence.Single(e => S(e, "ElementId") == image.Id);
                var shape = svg.Descendants().Single(e => (string?)e.Attribute("data-element-id") == image.Id);
                var picture = shape.Descendants("{http://www.w3.org/2000/svg}image").Single();
                string uri = picture.Attributes().Single(a => a.Name.LocalName == "href").Value;
                byte[] embedded = Convert.FromBase64String(uri[(uri.IndexOf(',') + 1)..]);
                if (Hash(embedded) != S(receipt, "EmbeddedSha256") || !JsonElement.DeepEquals(receipt.GetProperty("Image"),
                    graph.Single(e => S(e, "Id") == image.Id).GetProperty("Artifact").GetProperty("Image")))
                    throw new InvalidDataException("Durable SVG image bytes or decoded pixels differ from native readback.");
            }
        }
        await Render(false); await Render(true);
        await Op("native_publish", new() { ["path"] = path, ["format"] = "word", ["title"] = "Native image publication Ω" });
        await Mutate(images.Select(i => (object)new { Operation = "delete", ElementId = i.Id }).ToArray());
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var intact = await Op("native_inspect", new() { ["path"] = originalPath });
        if (S(intact, "sourceRevision") != originalRevision) throw new InvalidDataException("Image lifecycle modified its original source.");
        foreach (var source in corpus)
            if (Hash(File.ReadAllBytes(Path.Combine(inputs, S(source, "fileName")))) != S(source, "sha256")) throw new InvalidDataException("Image source bytes were modified.");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString()!;
}
