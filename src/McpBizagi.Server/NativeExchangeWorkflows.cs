using System.Text.Json;
using System.Text.RegularExpressions;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    private const string ExchangeWarning = "XPDL is an interchange projection, not a native backup. Review XML, graph and archive differences, including omitted diagrams, metadata, attachments, custom artifacts and simulation content. The native importer can normalize values. Exported attribute strings may contain local paths and private metadata: review before sharing. Original source bytes are not overwritten. No behavioral or desktop visual equivalence is claimed.";

    private static void RequireExchangeAcknowledgement(bool acknowledged)
    {
        if (!acknowledged) throw new InvalidDataException("XPDL requires acknowledgeFormatLimits=true. " + ExchangeWarning);
    }

    private byte[] ReadXpdl(NativeExchangeInput input)
    {
        byte[] bytes;
        if (input.Path.StartsWith("artifact:", StringComparison.Ordinal))
        {
            var match = Regex.Match(input.Path, @"\Aartifact:([0-9a-f]{32}):([0-9a-f-]{36})\.xpdl\z");
            if (!match.Success || !Guid.TryParseExact(match.Groups[2].Value, "D", out _)) throw new InvalidDataException("Invalid XPDL artifact reference.");
            var operation = operations.Get(match.Groups[1].Value);
            if (operation.State != "completed" || operation.Kind != "native_xpdl_export") throw new InvalidDataException("Only completed native XPDL exports may be reused.");
            bytes = new WorkspaceFiles(RunDirectory(match.Groups[1].Value, "artifacts")).Read("exported/" + match.Groups[2].Value + ".xpdl");
        }
        else
        {
            if (!Path.GetExtension(input.Path).Equals(".xpdl", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Expected an .xpdl file.");
            bytes = files.Read(input.Path);
        }
        if (BpmnDocument.Revision(bytes) != input.ExpectedRevision) throw new IOException("XPDL source revision conflict.");
        XpdlDocument.Parse(bytes);
        return bytes;
    }

    public OperationView ImportXpdl(NativeExchangeInput[] inputs, string modelName, bool acknowledgeFormatLimits)
    {
        RequireExchangeAcknowledgement(acknowledgeFormatLimits);
        if (inputs == null || inputs.Length is < 1 or > 100 || inputs.Any(i => i == null || string.IsNullOrWhiteSpace(i.Path)) ||
            inputs.Select(i => i.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != inputs.Length)
            throw new InvalidDataException("Supply 1-100 distinct revision-checked XPDL inputs.");
        // Capture bytes and revisions before queueing. Later changes to external files cannot race the worker.
        var capturedList = new List<byte[]>(); long capturedBytes = 0;
        foreach (var input in inputs)
        {
            byte[] bytes = ReadXpdl(input); capturedBytes += bytes.LongLength;
            if (capturedBytes > 128L * 1024 * 1024) throw new InvalidDataException("XPDL batch exceeds the aggregate input bound.");
            capturedList.Add(bytes);
        }
        byte[][] captured = capturedList.ToArray();
        return operations.Start("native_xpdl_import", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), output = Path.Combine(directory, "model.bpm");
            var paths = new List<string>();
            for (int i = 0; i < captured.Length; i++)
            {
                string path = Path.Combine(directory, "input-" + i + ".xpdl");
                await File.WriteAllBytesAsync(path, captured[i], token); paths.Add(path);
            }
            var saved = await Execute(new EngineRequest { OperationId = id, Action = "xpdl_import_save", InputPaths = paths.ToArray(), OutputPath = output, ModelName = modelName },
                RunDirectory(id, "importer"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            VerifyExchangeRestart(directory, saved, reopened);
            // Match receipts by the actual native import result, not labels or an assumed graph enumeration order.
            if (!saved.ExchangeFiles.Select(f => f.Path).SequenceEqual(paths) || saved.Diagrams.Length != captured.Length)
                throw new InvalidDataException("Native XPDL import receipt count/order mismatch.");
            var exported = await Execute(new EngineRequest { OperationId = id, Action = "xpdl_export", InputPath = output,
                OutputPath = Path.Combine(directory, "reexported"), SelectedDiagramIds = saved.ExchangeFiles.Select(f => f.DiagramId).ToArray() },
                RunDirectory(id, "exporter"), progress, token);
            if (!exported.ExchangeFiles.Select(f => f.DiagramId).SequenceEqual(saved.ExchangeFiles.Select(f => f.DiagramId)))
                throw new InvalidDataException("Native XPDL re-export receipt mismatch.");
            var differences = captured.Select((bytes, index) => new { inputIndex = index, diagramId = saved.ExchangeFiles[index].DiagramId,
                sourceRevision = BpmnDocument.Revision(bytes), roundtripRevision = BpmnDocument.Revision(File.ReadAllBytes(exported.ExchangeFiles[index].Path)),
                differences = XpdlDocument.CompareXml(bytes, File.ReadAllBytes(exported.ExchangeFiles[index].Path)) }).ToArray();
            var result = new { saved, reopened, exported, differences, sourceFilesUnmodified = true,
                outputArtifact = "artifact:" + id + ":model.bpm", outputRevision = BpmnDocument.Revision(File.ReadAllBytes(output)), warning = ExchangeWarning };
            File.WriteAllText(Path.Combine(directory, "xpdl-import-readback.json"), JsonSerializer.Serialize(result));
            return result;
        });
    }

    public OperationView ExportXpdl(string path, string expectedRevision, string[] diagramIds, bool acknowledgeFormatLimits)
    {
        RequireExchangeAcknowledgement(acknowledgeFormatLimits);
        if (diagramIds == null || diagramIds.Length is < 1 or > 100 || diagramIds.Distinct(StringComparer.Ordinal).Count() != diagramIds.Length)
            throw new InvalidDataException("Select 1-100 distinct native diagram IDs.");
        foreach (string diagramId in diagramIds) NativeMetadataPolicy.RequireId(diagramId);
        string[] selected = diagramIds.ToArray(); var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Native source revision conflict.");
        return operations.Start("native_xpdl_export", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), verification = Path.Combine(directory, "model.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            var exported = await Execute(new EngineRequest { OperationId = id, Action = "xpdl_export", InputPath = source,
                OutputPath = Path.Combine(directory, "exported"), SelectedDiagramIds = selected }, RunDirectory(id, "exporter"), progress, token);
            if (!exported.ExchangeFiles.Select(f => f.DiagramId).SequenceEqual(selected)) throw new InvalidDataException("Native XPDL export receipt mismatch.");
            foreach (var file in exported.ExchangeFiles) XpdlDocument.Parse(File.ReadAllBytes(file.Path));
            var imported = await Execute(new EngineRequest { OperationId = id, Action = "xpdl_import_save", InputPaths = exported.ExchangeFiles.Select(f => f.Path).ToArray(),
                OutputPath = verification }, RunDirectory(id, "importer"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = verification }, RunDirectory(id, "reader"), progress, token);
            VerifyExchangeRestart(directory, imported, reopened);
            if (reopened.Diagrams.Length != selected.Length) throw new InvalidDataException("XPDL verification diagram count mismatch.");
            var exchangeFiles = exported.ExchangeFiles.Select(f => new { f.DiagramId, artifact = "artifact:" + id + ":" + Path.GetFileName(f.Path),
                revision = BpmnDocument.Revision(File.ReadAllBytes(f.Path)), bytes = new FileInfo(f.Path).Length }).ToArray();
            var result = new { exported, imported, reopened, files = exchangeFiles, sourceRevision = input.Revision, nativeSourceUnmodified = true,
                nativeRoundtripComparison = NativeFidelity.Compare(input.Bytes, File.ReadAllBytes(verification)),
                graphDifferences = XpdlDocument.CompareGraph(exported.Elements, reopened.Elements),
                metadataDifferences = XpdlDocument.CompareMetadata(exported, reopened),
                verificationModel = "artifact:" + id + ":model.bpm", verificationRevision = BpmnDocument.Revision(File.ReadAllBytes(verification)), warning = ExchangeWarning };
            File.WriteAllText(Path.Combine(directory, "xpdl-export-readback.json"), JsonSerializer.Serialize(result));
            return result;
        });
    }

    private static void VerifyExchangeRestart(string directory, EngineReply written, EngineReply reopened)
    {
        var differences = XpdlDocument.CompareGraph(written.Elements, reopened.Elements);
        var metadataDifferences = XpdlDocument.CompareMetadata(written, reopened);
        // Format-loss acknowledgement applies to exchange, never unexplained changes during native persistence.
        File.WriteAllText(Path.Combine(directory, "xpdl-restart-readback.json"), JsonSerializer.Serialize(new { written, reopened, differences, metadataDifferences }));
        NativeEditPlan.VerifyRestartContainment(written.Elements, reopened.Elements);
        if (differences.Length != 0 || metadataDifferences.Length != 0) throw new InvalidDataException("Native XPDL graph or metadata changed during fresh-process restart. Inspect xpdl-restart-readback.json; original retained.");
    }
}
