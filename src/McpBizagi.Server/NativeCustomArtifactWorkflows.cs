using System.Text.Json;
using System.Text.RegularExpressions;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    public OperationView ApplyCustomArtifacts(string path, string expectedRevision, NativeCustomArtifactPatch patch)
    {
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Native model revision conflict.");
        NativeCustomArtifactPolicy.Preflight(input.Bytes, patch);
        var captured = JsonSerializer.Deserialize<NativeCustomArtifactPatch>(JsonSerializer.Serialize(patch))!;
        var images = captured.Changes.Where(c => c.Image != null).ToDictionary(c => c.Id, c => ReadImage(c.Image!));
        if (images.Values.Sum(b => b.LongLength) > 128L * 1024 * 1024) throw new InvalidDataException("Custom image batch exceeds the aggregate input bound.");
        return operations.Start("native_custom_artifacts_apply", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            foreach (var change in captured.Changes.Where(c => c.Image != null))
            {
                string file = Path.Combine(directory, "custom-source-" + change.Id + ".bin");
                await File.WriteAllBytesAsync(file, images[change.Id], token); change.Image!.SourcePath = file;
            }
            File.WriteAllText(Path.Combine(directory, "custom-request.json"), JsonSerializer.Serialize(captured));
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "custom_save", InputPath = source, OutputPath = output,
                CustomArtifactPatch = captured }, RunDirectory(id, "editor"), progress, token);
            return await VerifyCustomResult(id, input.Bytes, output, captured, edited, progress, token);
        });
    }
    private async Task<object> VerifyCustomResult(string id, byte[] original, string output, NativeCustomArtifactPatch patch, EngineReply edited,
        Action<string> progress, CancellationToken token)
    {
        var reopened = await Execute(new EngineRequest { OperationId = id, Action = "inspect", InputPath = output }, RunDirectory(id, "reader"), progress, token);
        byte[] bytes = await File.ReadAllBytesAsync(output, token);
        var fidelity = NativeCustomArtifactPolicy.Compare(original, bytes, patch, edited, reopened);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!, "custom-fidelity.json"), JsonSerializer.Serialize(fidelity, new JsonSerializerOptions { WriteIndented = true }));
        if (!fidelity.Preserved) throw new InvalidDataException("Unexplained native custom artifact changes; original retained. Inspect custom-fidelity.json.");
        return new { edited, reopened, fidelity, outputArtifact = "artifact:" + id + ":edited.bpm", outputRevision = BpmnDocument.Revision(bytes),
            nativeSourceUnmodified = true, warnings = edited.CustomArtifactImports.Where(r => r.PixelsChanged).Select(r => new { code = "acknowledged_native_custom_rasterization", r.Id, source = r.Source.Image, result = r.Result.Image }).ToArray() };
    }
    private byte[] ReadCustomArchive(string path, string revision)
    {
        byte[] bytes;
        if (path.StartsWith("artifact:", StringComparison.Ordinal))
        {
            var match = Regex.Match(path, @"\Aartifact:([0-9a-f]{32}):custom-artifacts\.bca\z");
            if (!match.Success) throw new InvalidDataException("Invalid custom artifact archive reference.");
            var operation = operations.Get(match.Groups[1].Value);
            if (operation.State != "completed" || operation.Kind != "native_custom_artifacts_export") throw new InvalidDataException("Only completed native .bca exports may be reused.");
            bytes = new WorkspaceFiles(RunDirectory(match.Groups[1].Value, "artifacts")).Read("custom-artifacts.bca");
        }
        else bytes = files.Read(path);
        if (BpmnDocument.Revision(bytes) != revision) throw new IOException("Custom archive source revision conflict.");
        NativeCustomArtifactArchive.Read(bytes); return bytes;
    }
    private static NativeCustomArtifactPatch ImportPatch(byte[] model, byte[] bca, bool replaceExisting, bool allowNativeRasterization)
    {
        var original = NativeCustomArtifactPolicy.Catalog(NativeArchive.ReadEntries(model));
        var changes = NativeCustomArtifactArchive.Read(bca).Select(p =>
        {
            if (original.ContainsKey(p.Id) && !replaceExisting) throw new InvalidDataException("Custom definition already exists; replacement requires explicit acknowledgement.");
            return new NativeCustomArtifactChange { Operation = original.ContainsKey(p.Id) ? "update" : "create", Id = p.Id, Name = p.Name,
                Image = new NativeImageImport { SourcePath = "captured-native-bca-sidecar", ExpectedRevision = BpmnDocument.Revision(p.SidecarPng), AllowPngReencoding = true }, AllowNativeRasterization = allowNativeRasterization };
        }).ToArray();
        return new NativeCustomArtifactPatch { Changes = changes };
    }
    public OperationView ImportCustomArtifacts(string path, string expectedRevision, string archivePath, string archiveRevision, bool replaceExisting, bool allowNativeRasterization)
    {
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Native model revision conflict.");
        byte[] bca = ReadCustomArchive(archivePath, archiveRevision); var patch = ImportPatch(input.Bytes, bca, replaceExisting, allowNativeRasterization);
        NativeCustomArtifactPolicy.Preflight(input.Bytes, patch);
        return operations.Start("native_custom_artifacts_import", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), archive = Path.Combine(directory, "input.bca"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token); await File.WriteAllBytesAsync(archive, bca, token);
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "custom_import", InputPath = source, OutputPath = output,
                CustomArtifactArchivePath = archive, ReplaceCustomArtifacts = replaceExisting, AllowCustomArtifactRasterization = allowNativeRasterization }, RunDirectory(id, "importer"), progress, token);
            return await VerifyCustomResult(id, input.Bytes, output, patch, edited, progress, token);
        });
    }
    public OperationView ExportCustomArtifacts(string path, string[] definitionIds)
    {
        var input = ReadNative(path); var catalog = NativeCustomArtifactPolicy.Catalog(NativeArchive.ReadEntries(input.Bytes));
        if (definitionIds == null || definitionIds.Length is < 1 or > 100 || definitionIds.Distinct().Count() != definitionIds.Length || definitionIds.Any(id => !catalog.ContainsKey(id)))
            throw new InvalidDataException("Select 1-100 distinct existing native custom definitions.");
        var capturedIds = definitionIds.ToArray();
        return operations.Start("native_custom_artifacts_export", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), archive = Path.Combine(directory, "custom-artifacts.bca"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            var exported = await Execute(new EngineRequest { OperationId = id, Action = "custom_export", InputPath = source, OutputPath = archive,
                CustomArtifactIds = capturedIds }, RunDirectory(id, "exporter"), progress, token);
            byte[] bca = await File.ReadAllBytesAsync(archive, token); var payloads = NativeCustomArtifactArchive.Read(bca);
            if (!payloads.Select(p => p.Id).Order().SequenceEqual(capturedIds.Order()) || payloads.Any(p => p.Name != catalog[p.Id].Name || !p.EmbeddedPng.SequenceEqual(catalog[p.Id].EmbeddedPng)))
                throw new InvalidDataException("Native .bca export differs from the original model-owned definitions.");
            // Re-import with the actual installed manager in another process, persist, then open in a third.
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "custom_import", InputPath = source, OutputPath = output,
                CustomArtifactArchivePath = archive, ReplaceCustomArtifacts = true }, RunDirectory(id, "importer"), progress, token);
            var verified = await VerifyCustomResult(id, input.Bytes, output, ImportPatch(input.Bytes, bca, true, false), edited, progress, token);
            return new { exported, verification = verified, outputArtifact = "artifact:" + id + ":custom-artifacts.bca", artifactPath = archive,
                outputRevision = BpmnDocument.Revision(bca), sourceRevision = input.Revision, nativeSourceUnmodified = true };
        });
    }
}
