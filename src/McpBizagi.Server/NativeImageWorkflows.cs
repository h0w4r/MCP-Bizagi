using System.Text.RegularExpressions;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    private byte[] ReadImage(NativeImageImport input)
    {
        NativeImagePolicy.Validate(input); byte[] bytes;
        if (input.SourcePath.StartsWith("artifact:", StringComparison.Ordinal))
        {
            // A single typed export is reusable; no arbitrary state paths or attachment/log references.
            var match = Regex.Match(input.SourcePath, @"\Aartifact:([0-9a-f]{32}):image\.bin\z");
            if (!match.Success) throw new InvalidDataException("Invalid native image artifact reference.");
            var operation = operations.Get(match.Groups[1].Value);
            if (operation.State != "completed" || operation.Kind != "native_image_export") throw new InvalidDataException("Only completed native image exports may be reused.");
            bytes = new WorkspaceFiles(RunDirectory(match.Groups[1].Value, "artifacts")).Read("image.bin");
        }
        else bytes = files.Read(input.SourcePath);
        if (bytes.LongLength is <= 0 or > NativeImagePolicy.MaxSourceBytes || BpmnDocument.Revision(bytes) != input.ExpectedRevision)
            throw new IOException("Image source revision or size differs from the requested snapshot.");
        return bytes;
    }
    public OperationView ExportImage(string path, string diagramId, string elementId)
    {
        NativeMetadataPolicy.RequireId(diagramId); NativeMetadataPolicy.RequireId(elementId);
        var input = ReadNative(path);
        return operations.Start("native_image_export", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "image.bin");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            var readback = await Execute(new EngineRequest { OperationId = id, Action = "image_export", InputPath = source, OutputPath = output,
                DiagramId = diagramId, ImageElementId = elementId }, RunDirectory(id, "reader"), progress, token);
            var entries = NativeArchive.ReadEntries(input.Bytes); NativeImagePolicy.VerifyFiles(entries, readback);
            var file = readback.ImageFiles.Single(f => f.ElementId == elementId && f.DiagramId == diagramId);
            byte[] bytes = await File.ReadAllBytesAsync(output, token);
            if (!bytes.SequenceEqual(entries[NativeImagePolicy.Entry(file)])) throw new InvalidDataException("Native image export differs from the exact original payload.");
            // .bin is deliberately neutral: existing native containers may carry non-PNG payloads.
            return new { sourceRevision = input.Revision, file, image = readback.Elements.Single(e => e.Id == elementId).Artifact!.Image,
                artifactPath = output, outputArtifact = "artifact:" + id + ":image.bin", outputRevision = BpmnDocument.Revision(bytes), nativeSourceUnmodified = true };
        });
    }
}
