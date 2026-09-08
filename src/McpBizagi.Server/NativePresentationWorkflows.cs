using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    public OperationView ReadPresentation(string path)
    {
        var input = ReadNative(path);
        return operations.Start("native_presentation_get", async (id, progress, token) =>
        {
            string source = Path.Combine(CreateArtifactDirectory(id), "input.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            var result = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = source }, RunDirectory(id, "reader"), progress, token);
            return new { sourceRevision = input.Revision, result, nativeSourceUnmodified = true };
        });
    }

    public OperationView ApplyPresentation(string path, string expectedRevision, NativePresentationActionChange[] changes)
    {
        NativePresentationPolicy.Validate(changes);
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Native presentation source revision conflict.");
        var captured = JsonSerializer.Deserialize<NativePresentationActionChange[]>(JsonSerializer.Serialize(changes))!;
        return operations.Start("native_presentation_apply", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            File.WriteAllText(Path.Combine(directory, "presentation-request.json"), JsonSerializer.Serialize(captured));
            var before = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = source }, RunDirectory(id, "source-reader"), progress, token);
            var plan = NativePresentationPolicy.Prepare(input.Bytes, before, captured);
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "presentation_save", InputPath = source, OutputPath = output, PresentationChanges = captured }, RunDirectory(id, "editor"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            // Readback comes from a new process; the whole archive must match explicit intent.
            File.WriteAllText(Path.Combine(directory, "presentation-readback.json"), JsonSerializer.Serialize(new { before, edited, reopened }));
            progress("native_presentation_fidelity");
            byte[] bytes = File.ReadAllBytes(output);
            var fidelity = NativePresentationPolicy.Compare(bytes, plan, edited, reopened);
            File.WriteAllText(Path.Combine(directory, "native-fidelity.json"), JsonSerializer.Serialize(fidelity, new JsonSerializerOptions { WriteIndented = true }));
            if (!fidelity.Preserved) throw new InvalidDataException("Presentation fidelity rejected unexplained changes; original retained. Inspect " + directory);
            return new { before, edited, reopened, fidelity, sourceRevision = input.Revision, nativeSourceUnmodified = true,
                requestedChangesVerified = captured.Length, outputArtifact = "artifact:" + id + ":edited.bpm", outputRevision = BpmnDocument.Revision(bytes) };
        });
    }
}
