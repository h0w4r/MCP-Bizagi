using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Real MCP/installed-manager custom definitions, instances, native .bca and durable native restart.</summary>
internal static class NativeCustomArtifactAcceptance
{
    public static async Task Run(string repo, string run, string stateRoot, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = S(await call(tool, args), "OperationId"); var result = await wait(id, state); exited(id);
            foreach (string file in Directory.GetFiles(Path.Combine(stateRoot, "runs", id), "worker-process.json", SearchOption.AllDirectories))
            {
                string directory = Path.GetDirectoryName(file)!, temp = Path.Combine(directory, "worker-temp-path.txt");
                if (!File.Exists(temp) || !Path.GetFullPath(File.ReadAllText(temp)).TrimEnd(Path.DirectorySeparatorChar).Equals(directory, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Native worker temporary storage did not remain inside its owned operation directory.");
            }
            receipts.Add(new { tool, id, result });
            File.WriteAllText(Path.Combine(run, "native-custom-artifacts.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? result.GetProperty("Result") : result;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Custom artifacts Ω" } });
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(graph.Single(e => S(e, "Kind") == "Collaboration"), "Id");
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string process = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string input = Path.Combine(run, "Custom image Ω.png"); File.Copy(Path.Combine(repo, "tests", "McpBizagi.Acceptance", "Fixtures", "images", "alpha.png"), input);
        string sourceHash = Hash(File.ReadAllBytes(input));
        string alternate = Path.Combine(run, "Replacement palette.gif"); File.Copy(Path.Combine(repo, "tests", "McpBizagi.Acceptance", "Fixtures", "images", "palette.gif"), alternate);
        string alternateHash = Hash(File.ReadAllBytes(alternate));
        object ImageInput() => new { SourcePath = input, ExpectedRevision = sourceHash, AllowPngReencoding = true };
        async Task<JsonElement> Apply(object[] changes, string state = "completed")
        {
            var result = await Op("native_custom_artifacts_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new { Changes = changes } }, state);
            if (state == "completed") { path = S(result, "outputArtifact"); revision = S(result, "outputRevision"); }
            return result;
        }
        async Task<JsonElement> Mutate(object[] changes, string state = "completed")
        {
            var result = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = changes }, state);
            if (state == "completed") { path = S(result, "outputArtifact"); revision = S(result, "outputRevision"); graph = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray(); }
            return result;
        }
        string type = Guid.NewGuid().ToString(), other = Guid.NewGuid().ToString();
        var deniedConversion = await Apply([new { Operation = "create", Id = type, Name = "Not silently normalized", Image = ImageInput() }], "failed");
        if (!S(deniedConversion, "Error").Contains("AllowNativeRasterization", StringComparison.Ordinal)) throw new InvalidDataException("Expected pixel-conversion rejection failed for another reason.");
        var defined = await Apply([
            new { Operation = "create", Id = type, Name = "Risk symbol Ω", Image = ImageInput(), AllowNativeRasterization = true },
            new { Operation = "create", Id = other, Name = "Reusable symbol 日本語", Image = ImageInput(), AllowNativeRasterization = true }
        ]);
        var conversions = defined.GetProperty("edited").GetProperty("CustomArtifactImports").EnumerateArray().ToArray();
        if (conversions.Length != 2 || conversions.Any(r => !r.GetProperty("PixelsChanged").GetBoolean() || !r.GetProperty("RepeatedSerializationStable").GetBoolean() ||
            S(r.GetProperty("Source"), "SourceSha256") != sourceHash)) throw new InvalidDataException("Custom native rasterization receipts lost their source/change/stability evidence.");
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        string sub = Guid.NewGuid().ToString(), root = Guid.NewGuid().ToString(), nested = Guid.NewGuid().ToString();
        object Bounds(int x, int y) => new { X = x, Y = y, Width = 100, Height = 80 };
        await Mutate([
            new { Operation = "create", ElementId = sub, ParentId = process, ElementType = "SubProcess", Name = "Nested custom surface", Geometry = Bounds(240, 140) },
            new { Operation = "create", ElementId = root, ParentId = process, ElementType = "CustomArtifact", Name = "Root symbol", Geometry = Bounds(80, 140), ArtifactProperties = new { CustomArtifactTypeId = type } },
            new { Operation = "create", ElementId = nested, ParentId = sub, ElementType = "CustomArtifact", Name = "Nested symbol", Geometry = Bounds(80, 100), ArtifactProperties = new { CustomArtifactTypeId = type } }
        ]);
        var missing = await Mutate([new { Operation = "update", ElementId = root, ArtifactProperties = new { CustomArtifactTypeId = Guid.NewGuid().ToString() } }], "failed");
        if (!S(missing, "Error").Contains("Unknown model-owned custom artifact definition", StringComparison.Ordinal)) throw new InvalidDataException("Expected missing-reference rejection failed for another reason.");
        await error("native_custom_artifacts_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new { Changes = new[] { new { Operation = "delete", Id = type } } } });
        await Mutate([new { Operation = "update", ElementId = root, ArtifactProperties = new { CustomArtifactTypeId = other } }]);
        var updated = await Apply([new { Operation = "update", Id = type, Name = "Updated referenced definition Ω",
            Image = new { SourcePath = alternate, ExpectedRevision = alternateHash, AllowPngReencoding = true }, AllowNativeRasterization = true }]);
        string updatedPixels = S(updated.GetProperty("reopened").GetProperty("CustomArtifacts").EnumerateArray().Single(d => S(d, "Id") == type).GetProperty("Image"), "PixelSha256");
        if (updatedPixels == S(conversions.Single(d => S(d, "Id") == type).GetProperty("Result").GetProperty("Image"), "PixelSha256"))
            throw new InvalidDataException("Custom image replacement did not change the loaded pixels.");
        string originalPath = path, originalRevision = revision;
        var cloned = await Op("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["patch"] = new { Changes = new[] { new { Operation = "clone", DiagramId = diagram, Name = "Custom clone Ω" } } } });
        path = S(cloned, "outputArtifact"); revision = S(cloned, "outputRevision");
        var map = cloned.GetProperty("edited").GetProperty("DiagramClones")[0].GetProperty("Identities").EnumerateArray().ToDictionary(e => S(e, "SourceId"), e => S(e, "TargetId"));
        graph = cloned.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        foreach (string id in new[] { root, nested })
            if (S(graph.Single(e => S(e, "Id") == id).GetProperty("Artifact"), "CustomArtifactTypeId") != S(graph.Single(e => S(e, "Id") == map[id]).GetProperty("Artifact"), "CustomArtifactTypeId"))
                throw new InvalidDataException("Cloning changed the shared model-owned custom definition reference.");
        var exported = await Op("native_custom_artifacts_export", new() { ["path"] = path, ["definitionIds"] = new[] { type, other } });
        string archive = S(exported, "outputArtifact"), archiveRevision = S(exported, "outputRevision");
        if (Hash(File.ReadAllBytes(S(exported, "artifactPath"))) != archiveRevision) throw new InvalidDataException("Native .bca artifact receipt differs from durable bytes.");
        await error("native_custom_artifacts_import", new() { ["path"] = path, ["expectedRevision"] = revision, ["archivePath"] = archive, ["archiveRevision"] = archiveRevision });
        await Apply([new { Operation = "update", Id = type, Name = "Before explicit .bca replacement", Image = ImageInput(), AllowNativeRasterization = true }]);
        var replaced = await Op("native_custom_artifacts_import", new() { ["path"] = path, ["expectedRevision"] = revision, ["archivePath"] = archive, ["archiveRevision"] = archiveRevision, ["replaceExisting"] = true });
        path = S(replaced, "outputArtifact"); revision = S(replaced, "outputRevision");
        var restored = replaced.GetProperty("reopened").GetProperty("CustomArtifacts").EnumerateArray().Single(d => S(d, "Id") == type);
        if (S(restored, "Name") != "Updated referenced definition Ω" || S(restored.GetProperty("Image"), "PixelSha256") != updatedPixels)
            throw new InvalidDataException("Explicit .bca replacement did not restore the exported name and image.");
        var fresh = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Independent .bca destination" } });
        var imported = await Op("native_custom_artifacts_import", new() { ["path"] = S(fresh, "outputArtifact"), ["expectedRevision"] = S(fresh, "outputRevision"), ["archivePath"] = archive, ["archiveRevision"] = archiveRevision });
        if (imported.GetProperty("reopened").GetProperty("CustomArtifacts").GetArrayLength() != 2) throw new InvalidDataException("Independent model import lost custom definitions.");
        foreach (bool inside in new[] { false, true })
        {
            var render = await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram, ["subProcessId"] = inside ? sub : "" });
            string surface = inside ? sub : diagram; var result = render.GetProperty("result");
            var rendered = result.GetProperty("RenderedImages").EnumerateArray().Where(e => S(e, "SurfaceId") == surface).ToArray();
            if (rendered.Length != 1 || S(rendered[0], "ElementId") != (inside ? nested : root)) throw new InvalidDataException("Custom native rendering lacks exact surface pixel evidence.");
            var svg = System.Xml.Linq.XDocument.Load(result.GetProperty("Artifacts").EnumerateArray().Select(e => e.GetString()!).Single(p => p.EndsWith(surface + ".svg", StringComparison.Ordinal)));
            var shape = svg.Descendants().Single(e => (string?)e.Attribute("data-element-id") == (inside ? nested : root));
            string uri = shape.Descendants("{http://www.w3.org/2000/svg}image").Single().Attributes().Single(a => a.Name.LocalName == "href").Value;
            if (Hash(Convert.FromBase64String(uri[(uri.IndexOf(',') + 1)..])) != S(rendered[0], "EmbeddedSha256")) throw new InvalidDataException("SVG custom image hash does not match its native pixel receipt.");
        }
        await Mutate(new[] { root, nested, map[root], map[nested] }.Select(id => (object)new { Operation = "delete", ElementId = id }).ToArray());
        var removed = await Apply([new { Operation = "delete", Id = type }, new { Operation = "delete", Id = other }]);
        if (removed.GetProperty("reopened").GetProperty("CustomArtifacts").GetArrayLength() != 0) throw new InvalidDataException("Deleted custom definition resurrected on native reopen.");
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var intact = await Op("native_inspect", new() { ["path"] = originalPath });
        if (S(intact, "sourceRevision") != originalRevision || Hash(File.ReadAllBytes(input)) != sourceHash || Hash(File.ReadAllBytes(alternate)) != alternateHash)
            throw new InvalidDataException("Custom lifecycle modified an original source.");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString()!;
}
