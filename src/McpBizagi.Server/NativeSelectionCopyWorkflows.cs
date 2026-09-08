using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    public OperationView CopySelection(string path, string expectedRevision, NativeSelectionCopyRequest selection)
    {
        NativeSelectionCopyPolicy.Validate(selection);
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Native selection copy source revision conflict.");
        // Capture intent before returning an asynchronous operation; callers cannot mutate it in flight.
        var captured = JsonSerializer.Deserialize<NativeSelectionCopyRequest>(JsonSerializer.Serialize(selection))!;
        return operations.Start("native_elements_copy", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            File.WriteAllText(Path.Combine(directory, "copy-request.json"), JsonSerializer.Serialize(captured));
            var before = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = source }, RunDirectory(id, "source-reader"), progress, token);
            NativeSelectionCopyPolicy.Closure(before.Elements, captured);
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "copy_save", InputPath = source, OutputPath = output, SelectionCopy = captured },
                RunDirectory(id, "editor"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            var graphDifferences = XpdlDocument.CompareGraph(edited.Elements, reopened.Elements);
            var metadataDifferences = XpdlDocument.CompareMetadata(edited, reopened);
            File.WriteAllText(Path.Combine(directory, "copy-readback.json"), JsonSerializer.Serialize(new { before, edited, reopened, graphDifferences, metadataDifferences }));
            byte[] outputBytes = File.ReadAllBytes(output);
            NativeImagePolicy.VerifyRestart(outputBytes, edited, reopened);
            if (graphDifferences.Length != 0 || metadataDifferences.Length != 0) throw new InvalidDataException("Native selection copy changed after restart. Original retained; inspect copy-readback.json.");
            var receipt = edited.SelectionCopy ?? throw new InvalidDataException("Native editor returned no selection copy receipt.");
            var fidelity = NativeSelectionCopyPolicy.Compare(input.Bytes, outputBytes, before.Elements, reopened.Elements, captured, receipt);
            File.WriteAllText(Path.Combine(directory, "copy-fidelity.json"), JsonSerializer.Serialize(fidelity, new JsonSerializerOptions { WriteIndented = true }));
            if (!fidelity.Preserved) throw new InvalidDataException("Native selection copy fidelity failed. Output is quarantined and the original is retained.");
            return new { before, edited, reopened, fidelity, receipt, nativeSourceUnmodified = true,
                interpretationWarning = "Closed native selection copying is experimental; it does not accredit arbitrary selections, live unsaved documents or desktop visual compatibility.",
                outputArtifact = "artifact:" + id + ":edited.bpm", outputRevision = BpmnDocument.Revision(outputBytes) };
        });
    }
}
