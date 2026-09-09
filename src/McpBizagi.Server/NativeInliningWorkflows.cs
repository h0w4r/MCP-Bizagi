using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    public OperationView InlineSubProcess(string path, string expectedRevision, NativeSubProcessInlining inlining)
    {
        NativeInliningPolicy.Validate(inlining);
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Native inlining source revision conflict.");
        var captured = JsonSerializer.Deserialize<NativeSubProcessInlining>(JsonSerializer.Serialize(inlining))!;
        return operations.Start("native_subprocess_inline", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"),
                convertedPath = Path.Combine(directory, "converted.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            File.WriteAllText(Path.Combine(directory, "inlining-request.json"), JsonSerializer.Serialize(captured));
            var before = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = source }, RunDirectory(id, "source-reader"), progress, token);
            var selection = NativeInliningPolicy.Preflight(input.Bytes, before, captured);
            // Qualify the empty native container before cloning into it. Neither intermediate is
            // published; a failure leaves the original input unchanged and output quarantined.
            var converter = await Execute(new EngineRequest { OperationId = id, Action = "inline_prepare_save", InputPath = source, OutputPath = convertedPath, Inlining = captured }, RunDirectory(id, "converter"), progress, token);
            var converted = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = convertedPath }, RunDirectory(id, "conversion-reader"), progress, token);
            byte[] convertedBytes = File.ReadAllBytes(convertedPath);
            VerifyRestart("conversion", convertedBytes, converter, converted);
            var conversionFidelity = NativeInliningPolicy.CompareConversion(input.Bytes, convertedBytes, before.Elements, converted.Elements, captured);
            File.WriteAllText(Path.Combine(directory, "inlining-conversion-fidelity.json"), JsonSerializer.Serialize(conversionFidelity));
            if (!conversionFidelity.Preserved) throw new InvalidDataException("Native inlining container fidelity failed; original retained.");
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "copy_save", InputPath = convertedPath, OutputPath = output, SelectionCopy = selection }, RunDirectory(id, "body-copier"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            byte[] outputBytes = File.ReadAllBytes(output);
            VerifyRestart("copy", outputBytes, edited, reopened);
            var receipt = edited.SelectionCopy ?? throw new InvalidDataException("Inlining body copy has no native provenance receipt.");
            var copyFidelity = NativeSelectionCopyPolicy.Compare(convertedBytes, outputBytes, converted.Elements, reopened.Elements, selection, receipt);
            File.WriteAllText(Path.Combine(directory, "inlining-copy-fidelity.json"), JsonSerializer.Serialize(copyFidelity));
            if (!copyFidelity.Preserved) throw new InvalidDataException("Native inlining body fidelity failed; original retained.");
            return new { before, converted, edited, reopened, conversionFidelity, copyFidelity, receipt,
                nativeSourceUnmodified = true, sharedProcessPreserved = true,
                interpretationWarning = "Inlining copies the local process body, not its pool/diagram identity, process-level metadata, simulation configuration or presentation actions. It changes reusable-call execution into embedded execution; it is not behavioral or desktop visual equivalence. The shared process and other callers are retained.",
                outputArtifact = "artifact:" + id + ":edited.bpm", outputRevision = BpmnDocument.Revision(outputBytes) };

            void VerifyRestart(string stage, byte[] bytes, EngineReply editor, EngineReply reader)
            {
                var graphDifferences = XpdlDocument.CompareGraph(editor.Elements, reader.Elements);
                var metadataDifferences = XpdlDocument.CompareMetadata(editor, reader);
                File.WriteAllText(Path.Combine(directory, "inlining-" + stage + "-readback.json"), JsonSerializer.Serialize(new { editor, reader, graphDifferences, metadataDifferences }));
                NativeEditPlan.VerifyRestartContainment(editor.Elements, reader.Elements);
                NativeImagePolicy.VerifyRestart(bytes, editor, reader);
                if (graphDifferences.Length != 0 || metadataDifferences.Length != 0) throw new InvalidDataException("Native inlining changed after restart at " + stage + "; inspect retained readback evidence.");
            }
        });
    }
}
