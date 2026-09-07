using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    public OperationView ExportAttachment(string path, string diagramId, string elementId, string fileName)
    {
        NativeMetadataPolicy.RequireId(diagramId); NativeMetadataPolicy.RequireId(elementId); NativeDocumentationPolicy.RequireFileName(fileName);
        var input = ReadNative(path);
        return operations.Start("native_attachment_export", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), exported = Path.Combine(directory, "attachment.bin");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            var result = await Execute(new EngineRequest
            {
                OperationId = id,
                Action = "documentation_read",
                InputPath = source,
                OutputPath = exported,
                Attachment = new NativeAttachmentInfo { DiagramId = diagramId, ElementId = elementId, FileName = fileName }
            }, RunDirectory(id, "reader"), progress, token);
            var attachment = result.Documentation!.Attachments.Single(a => a.DiagramId == diagramId && a.ElementId == elementId && a.FileName == fileName);
            byte[] bytes = await File.ReadAllBytesAsync(exported, token);
            // Compare the native-loaded file with the original archive payload and durable export, independently.
            byte[] original = NativeArchive.ReadEntries(input.Bytes)[diagramId + ".diag!/Files/" + elementId + "/" + fileName];
            if (!bytes.SequenceEqual(original) || bytes.LongLength != attachment.Length || BpmnDocument.Revision(bytes) != attachment.Sha256) throw new InvalidDataException("Exported attachment bytes do not match native readback and original archive.");
            return new { sourceRevision = input.Revision, attachment, artifactPath = exported, nativeSourceUnmodified = true, verifiedBytes = bytes.LongLength };
        });
    }

    public OperationView ReadDocumentation(string path)
    {
        var input = ReadNative(path);
        return operations.Start("native_attributes_get", async (id, progress, token) =>
        {
            string source = Path.Combine(CreateArtifactDirectory(id), "input.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            var result = await Execute(new EngineRequest { OperationId = id, Action = "documentation_read", InputPath = source }, RunDirectory(id, "reader"), progress, token);
            return new { sourceRevision = input.Revision, result, nativeSourceUnmodified = true };
        });
    }

    public OperationView ApplyDocumentation(string path, string expectedRevision, NativeDocumentationPatch patch)
    {
        NativeDocumentationPolicy.Validate(patch);
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Revision conflict: native file changed since inspection.");
        return operations.Start("native_attributes_apply", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            File.WriteAllText(Path.Combine(directory, "documentation-request.json"), JsonSerializer.Serialize(patch));
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "documentation_save", InputPath = source, OutputPath = output, DocumentationPatch = patch }, RunDirectory(id, "editor"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "documentation_read", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            File.WriteAllText(Path.Combine(directory, "documentation-readback.json"), JsonSerializer.Serialize(new { edited.Documentation, reopened = reopened.Documentation }));
            progress("native_documentation_fidelity");
            var fidelity = NativeDocumentationPolicy.Compare(input.Bytes, File.ReadAllBytes(output), patch, reopened.Documentation ?? throw new InvalidDataException("Missing native documentation readback."));
            File.WriteAllText(Path.Combine(directory, "native-fidelity.json"), JsonSerializer.Serialize(fidelity, new JsonSerializerOptions { WriteIndented = true }));
            if (!fidelity.Preserved) throw new InvalidDataException("Native documentation fidelity rejected unexplained changes; original untouched. Inspect " + directory);
            return new { edited, reopened, fidelity, nativeSourceUnmodified = true, outputArtifact = "artifact:" + id + ":edited.bpm", outputRevision = BpmnDocument.Revision(File.ReadAllBytes(output)) };
        });
    }
}
