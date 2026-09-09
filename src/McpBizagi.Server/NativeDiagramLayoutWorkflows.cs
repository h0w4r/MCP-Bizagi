using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    public OperationView LayoutDiagram(string path, string expectedRevision, NativeDiagramLayoutRequest layout)
    {
        NativeDiagramLayoutPlanner.Validate(layout);
        var captured = JsonSerializer.Deserialize<NativeDiagramLayoutRequest>(JsonSerializer.Serialize(layout))!;
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Native diagram source revision conflict.");
        return operations.Start("native_diagram_layout", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            var before = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = source }, RunDirectory(id, "source-reader"), progress, token);
            async Task<NativeMutation[]> ResolveAnchors(NativeAnchorResizeRequest resize)
            {
                string previewDirectory = RunDirectory(id, "anchor-preview-" + resize.HostId);
                var preview = await Execute(new EngineRequest { OperationId = id, Action = "anchor_preview", InputPath = source,
                    AnchorResize = resize }, previewDirectory, progress, token);
                if (BpmnDocument.Revision(await File.ReadAllBytesAsync(source, token)) != expectedRevision)
                    throw new InvalidDataException("Planning preview changed the immutable native input.");
                var policy = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(previewDirectory, "native-layout-policy.json")));
                if (!policy.GetProperty("installed").GetBoolean() || !policy.GetProperty("acknowledged").GetBoolean())
                    throw new InvalidDataException("Native anchor preview lacks actual command acknowledgment.");
                if (!policy.TryGetProperty("suppressedPreviewReflow", out var isolated) || isolated.GetProperty("hostId").GetString() != resize.HostId ||
                    isolated.GetProperty("priority").GetInt32() != 450)
                    throw new InvalidDataException("Native anchor preview did not isolate the pinned container reflow phase.");
                var callbacks = File.ReadLines(Path.Combine(previewDirectory, "actual-cef-callbacks.jsonl"))
                    .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).Where(e => e.GetProperty("method").GetString() == "UpdateElementShape").ToArray();
                if (callbacks.Length != 1) throw new InvalidDataException("Native anchor preview must produce one actual callback.");
                var anchors = NativeAnchorResolutionPolicy.Resolve(before.Elements, resize, preview, callbacks[0].GetProperty("payload").GetString()!);
                File.WriteAllText(Path.Combine(previewDirectory, "anchor-resolution.json"), JsonSerializer.Serialize(new { resize, preview, anchors,
                    interpretation = "Transient native preview; only checked anchor positions enter the final plan. No native preview file was persisted." }));
                return anchors;
            }
            var plan = await NativeDiagramLayoutPlanner.CalculateAsync(before.Elements, captured, progress, token, ResolveAnchors);
            // Intent is retained before dispatch. It is not reverse-engineered from the output.
            File.WriteAllText(Path.Combine(directory, "diagram-layout-plan.json"), JsonSerializer.Serialize(plan));
            token.ThrowIfCancellationRequested();
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "mutate_save", InputPath = source, OutputPath = output, Mutations = plan.Changes }, RunDirectory(id, "editor"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            File.WriteAllText(Path.Combine(directory, "diagram-layout-readback.json"), JsonSerializer.Serialize(new { before, edited, reopened }));
            NativeEditPlan.VerifyRestartContainment(edited.Elements, reopened.Elements);
            NativeImagePolicy.VerifyRestart(File.ReadAllBytes(output), edited, reopened);
            var fidelity = NativeMutationFidelity.Compare(input.Bytes, File.ReadAllBytes(output), plan.Changes, reopened.Elements, edited.ImageImports, reopened.ImageFiles);
            File.WriteAllText(Path.Combine(directory, "diagram-layout-fidelity.json"), JsonSerializer.Serialize(fidelity));
            if (!fidelity.Preserved) throw new InvalidDataException("Diagram layout changed unrequested native content; original retained.");
            NativeDiagramLayoutGeometry.Verify(reopened.Elements, captured.DiagramId);
            NativeDiagramGroupLayout.Verify(before.Elements, reopened.Elements, captured.DiagramId);
            return new { before, edited, reopened, fidelity, plan, nativeSourceUnmodified = true,
                outputArtifact = "artifact:" + id + ":edited.bpm", outputRevision = BpmnDocument.Revision(File.ReadAllBytes(output)),
                interpretationWarning = "Experimental complete selected-diagram planning for represented pool/partition/expanded/anchor/midpoint-port surfaces. Unsupported content fails rather than receiving partial layout. Rectangular geometry checks are not glyph, rounded-curve or desktop GUI accreditation." };
        });
    }
}
