using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    public OperationView ExtractSubProcess(string path, string expectedRevision, NativeSubProcessExtraction extraction)
    {
        if (extraction == null) throw new InvalidDataException("Missing explicit extraction request.");
        // Reuse the existing native identity/export-label rules; this call performs no diagram mutation.
        NativeDiagramPolicy.Validate(new NativeDiagramPatch { Changes = [new() {
            Operation = "create", DiagramId = extraction.ElementId, Name = extraction.NewDiagramName }] });
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Native extraction source revision conflict.");
        var captured = JsonSerializer.Deserialize<NativeSubProcessExtraction>(JsonSerializer.Serialize(extraction))!;
        return operations.Start("native_subprocess_extract", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            File.WriteAllText(Path.Combine(directory, "extraction-request.json"), JsonSerializer.Serialize(captured));
            var before = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = source }, RunDirectory(id, "source-reader"), progress, token);
            NativeExtractionPolicy.Preflight(input.Bytes, captured, before);
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "extract_save", InputPath = source, OutputPath = output, Extraction = captured },
                RunDirectory(id, "extractor"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            var graphDifferences = XpdlDocument.CompareGraph(edited.Elements, reopened.Elements);
            var metadataDifferences = XpdlDocument.CompareMetadata(edited, reopened);
            File.WriteAllText(Path.Combine(directory, "extraction-readback.json"), JsonSerializer.Serialize(new { before, edited, reopened, graphDifferences, metadataDifferences }));
            NativeEditPlan.VerifyRestartContainment(edited.Elements, reopened.Elements);
            NativeImagePolicy.VerifyRestart(File.ReadAllBytes(output), edited, reopened);
            if (graphDifferences.Length != 0 || metadataDifferences.Length != 0) throw new InvalidDataException("Extracted native content changed during fresh-process restart; inspect extraction-readback.json.");
            progress("native_extraction_fidelity");
            // Reverse only the independently verified graph/XML/file relocation in comparison copies.
            var fidelity = NativeExtractionPolicy.Compare(input.Bytes, File.ReadAllBytes(output), captured, before, edited, reopened);
            File.WriteAllText(Path.Combine(directory, "extraction-fidelity.json"), JsonSerializer.Serialize(fidelity, new JsonSerializerOptions { WriteIndented = true }));
            if (!fidelity.Preserved) throw new InvalidDataException("Extraction contains unaccredited native archive changes; original retained. Inspect extraction-fidelity.json.");
            return new { before, edited, reopened, fidelity, nativeSourceUnmodified = true,
                extractionInterpretationWarning = "Extraction changes embedded execution into a reusable call. The installed simulator treats reusable calls as black boxes; this is not behavioral or visual equivalence. Only the current native user's persisted tab references are remapped. Other users' preferences remain unchanged. Configured child simulation inputs and presentation actions require separate migration contracts.",
                outputArtifact = "artifact:" + id + ":edited.bpm", outputRevision = BpmnDocument.Revision(File.ReadAllBytes(output)) };
        });
    }
}
