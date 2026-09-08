using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    public OperationView ConvertElements(string path, string expectedRevision, NativeTypeConversion[] changes)
    {
        NativeConversionPolicy.Validate(changes);
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Revision conflict: native file changed since inspection.");
        NativeConversionPolicy.Preflight(input.Bytes, changes);
        var captured = JsonSerializer.Deserialize<NativeTypeConversion[]>(JsonSerializer.Serialize(changes))!;
        return operations.Start("native_elements_convert", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            File.WriteAllText(Path.Combine(directory, "conversion-request.json"), JsonSerializer.Serialize(captured));
            var before = await Execute(new EngineRequest { OperationId = id, Action = "inspect", InputPath = source }, RunDirectory(id, "source-reader"), progress, token);
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "convert_save", InputPath = source, OutputPath = output, Conversions = captured }, RunDirectory(id, "converter"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "inspect", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            // Keep the real three-process observations even when fidelity rejects the candidate.
            File.WriteAllText(Path.Combine(directory, "conversion-readback.json"), JsonSerializer.Serialize(new { before, edited, reopened }));
            NativeEditPlan.VerifyRestartContainment(edited.Elements, reopened.Elements);
            NativeConversionPolicy.Verify(before.Elements, reopened.Elements, captured);
            NativeImagePolicy.VerifyRestart(File.ReadAllBytes(output), edited, reopened);
            progress("native_conversion_fidelity");
            var fidelity = NativeConversionPolicy.Compare(input.Bytes, File.ReadAllBytes(output), captured);
            File.WriteAllText(Path.Combine(directory, "native-fidelity.json"), JsonSerializer.Serialize(fidelity, new JsonSerializerOptions { WriteIndented = true }));
            if (!fidelity.Preserved) throw new InvalidDataException("Native conversion fidelity rejected unexplained changes; original unchanged. Inspect " + directory);
            return new { before, edited, reopened, fidelity, nativeSourceUnmodified = true, requestedChangesVerified = captured.Length,
                conversionInterpretationWarning = "Requested type selectors and observed neutral type-owned factory defaults may change. Event conversion preserves its explicit role, interruption and attachment while replacing only neutral definitions, including unreferenced generated message identities. It does not migrate configured payloads or change special event contexts. Task-to-call conversion produces an unbound CallActivity, not a new diagram or a chosen target; bind it explicitly with native_mutate CallTarget. Call-to-task requires an unbound call with no nondefault expanded layout; unlink explicitly first. It never deletes or inlines a called process. All other archive content remains protected. Successful persistence does not establish model validity, equivalent simulation behavior or desktop visual compatibility.",
                outputArtifact = "artifact:" + id + ":edited.bpm", outputRevision = BpmnDocument.Revision(File.ReadAllBytes(output)) };
        });
    }
}
