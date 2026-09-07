using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    public OperationView CreateModel(string[] diagramNames)
    {
        var patch = NativeModelCreation.Prepare(diagramNames);
        return operations.Start("native_model_create", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), output = Path.Combine(directory, "model.bpm"), stability = Path.Combine(directory, "stability.bpm");
            File.WriteAllText(Path.Combine(directory, "creation-request.json"), JsonSerializer.Serialize(patch));
            var created = await Execute(new EngineRequest { OperationId = id, Action = "create_save", OutputPath = output, DiagramPatch = patch }, RunDirectory(id, "creator"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "diagrams_read", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            byte[] durable = File.ReadAllBytes(output);
            File.WriteAllText(Path.Combine(directory, "creation-readback.json"), JsonSerializer.Serialize(new { created, reopened }));
            NativeModelCreation.Verify(durable, patch, created, reopened);
            // A new document must also be a stable native input, not just a one-time serializable object graph.
            await Execute(new EngineRequest { OperationId = id, Action = "edit_save", InputPath = output, OutputPath = stability }, RunDirectory(id, "stability-writer"), progress, token);
            progress("native_creation_stability");
            var fidelity = NativeFidelity.Compare(durable, File.ReadAllBytes(stability));
            File.WriteAllText(Path.Combine(directory, "native-fidelity.json"), JsonSerializer.Serialize(fidelity));
            if (!fidelity.Preserved) throw new InvalidDataException("New native model is not stable under no-op persistence; inspect " + directory);
            if (!durable.AsSpan().SequenceEqual(File.ReadAllBytes(output))) throw new IOException("Creation artifact changed during verification.");
            return new { created, reopened, fidelity, outputArtifact = "artifact:" + id + ":model.bpm", outputRevision = BpmnDocument.Revision(durable),
                warning = "Created by the installed native model constructor and domain defaults, without BPMN import. Model name/path is file context, not a claimed persistent model identity. Live desktop and visual compatibility remain separate." };
        });
    }
}
