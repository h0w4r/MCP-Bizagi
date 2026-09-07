using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    public OperationView ReadDiagrams(string path)
    {
        var input = ReadNative(path);
        return operations.Start("native_diagrams_get", async (id, progress, token) =>
        {
            string source = Path.Combine(CreateArtifactDirectory(id), "input.bpm"); await File.WriteAllBytesAsync(source, input.Bytes, token);
            var result = await Execute(new EngineRequest { OperationId = id, Action = "diagrams_read", InputPath = source }, RunDirectory(id, "reader"), progress, token);
            return new { sourceRevision = input.Revision, result, nativeSourceUnmodified = true };
        });
    }
    public OperationView ApplyDiagrams(string path, string expectedRevision, NativeDiagramPatch patch)
    {
        NativeDiagramPolicy.Validate(patch); var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Revision conflict: native model changed.");
        return operations.Start("native_diagrams_apply", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            File.WriteAllText(Path.Combine(directory, "diagram-request.json"), JsonSerializer.Serialize(patch));
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "diagrams_save", InputPath = source, OutputPath = output, DiagramPatch = patch }, RunDirectory(id, "editor"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "diagrams_read", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            File.WriteAllText(Path.Combine(directory, "diagram-readback.json"), JsonSerializer.Serialize(new { edited, reopened }));
            progress("native_diagram_fidelity");
            var fidelity = NativeDiagramPolicy.Compare(input.Bytes, File.ReadAllBytes(output), patch, edited, reopened);
            File.WriteAllText(Path.Combine(directory, "native-fidelity.json"), JsonSerializer.Serialize(fidelity, new JsonSerializerOptions { WriteIndented = true }));
            if (!fidelity.Preserved) throw new InvalidDataException("Diagram lifecycle fidelity rejected unexplained changes; inspect " + directory);
            return new { edited, reopened, fidelity, nativeSourceUnmodified = true, outputArtifact = "artifact:" + id + ":edited.bpm", outputRevision = BpmnDocument.Revision(File.ReadAllBytes(output)) };
        });
    }
}
