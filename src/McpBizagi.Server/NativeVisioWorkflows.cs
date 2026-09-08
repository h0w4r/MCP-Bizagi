using System.Text.Json;
using System.Text.RegularExpressions;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

public sealed partial class NativeWorkflows
{
    private const string VisioWarning = "Visio VDX is a lossy interchange projection, not a native backup. The installed mapper can reserve empty subprocess pages and omit nested contents, change identities, kinds, labels, styles, geometry and connections, and omit unsupported shapes, metadata, attachments and simulation. Inspect the complete page inventory and native graph/archive differences. Import initialization normalizations are recorded separately from strict native restart verification. Original inputs are never overwritten. No desktop visual or behavioral equivalence is claimed. Review exported content for private information before sharing.";

    private static void RequireVisioAcknowledgement(bool acknowledged)
    {
        if (!acknowledged) throw new InvalidDataException("Visio requires acknowledgeFormatLimits=true. " + VisioWarning);
    }

    private byte[] ReadVisio(NativeExchangeInput input)
    {
        if (input == null || string.IsNullOrWhiteSpace(input.Path)) throw new InvalidDataException("A revision-checked VDX input is required.");
        byte[] bytes;
        if (input.Path.StartsWith("artifact:", StringComparison.Ordinal))
        {
            var match = Regex.Match(input.Path, @"\Aartifact:([0-9a-f]{32}):export\.vdx\z");
            if (!match.Success) throw new InvalidDataException("Invalid Visio artifact reference.");
            var operation = operations.Get(match.Groups[1].Value);
            if (operation.State != "completed" || operation.Kind != "native_visio_export") throw new InvalidDataException("Only completed Visio exports can be reused.");
            bytes = new WorkspaceFiles(RunDirectory(match.Groups[1].Value, "artifacts")).Read("export.vdx");
        }
        else
        {
            if (!Path.GetExtension(input.Path).Equals(".vdx", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("This Visio adapter currently accepts .vdx, not VSD/VSDX.");
            bytes = files.Read(input.Path);
        }
        if (BpmnDocument.Revision(bytes) != input.ExpectedRevision) throw new IOException("Visio source revision conflict.");
        VisioDocument.Inspect(bytes);
        return bytes;
    }

    private sealed record VisioImportVerification(EngineReply Imported, EngineReply Reopened, EngineReply Verified,
        ExchangeDifference[] ImportNormalizations, ExchangeDifference[] MetadataNormalizations, NativeFidelityReport Fidelity);

    private async Task<VisioImportVerification> VerifyVisioImport(string id, string input, string directory, string modelName,
        Action<string> progress, CancellationToken token)
    {
        string initial = Path.Combine(directory, "imported.bpm"), output = Path.Combine(directory, "model.bpm");
        var imported = await Execute(new EngineRequest { OperationId = id, Action = "visio_import_save", InputPath = input, OutputPath = initial, ModelName = modelName },
            RunDirectory(id, "importer"), progress, token);
        var reopened = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = initial }, RunDirectory(id, "reader"), progress, token);
        var normalizations = XpdlDocument.CompareGraph(imported.Elements, reopened.Elements);
        File.WriteAllText(Path.Combine(directory, "visio-import-normalizations.json"), JsonSerializer.Serialize(new { imported, reopened, normalizations }));
        VisioDocument.ImportNormalization(imported.Elements, reopened.Elements);
        var metadataNormalizations = VisioDocument.ImportMetadataNormalization(imported, reopened);
        if (reopened.Diagrams.Length != VisioDocument.Inspect(File.ReadAllBytes(input)).Length)
            throw new InvalidDataException("Native Visio import skipped one or more document pages.");
        // Initial foreign-format normalization is NOT permission to lose native data on subsequent saves.
        await Execute(new EngineRequest { OperationId = id, Action = "edit_save", InputPath = initial, OutputPath = output }, RunDirectory(id, "noop-writer"), progress, token);
        var verified = await Execute(new EngineRequest { OperationId = id, Action = "exchange_read", InputPath = output }, RunDirectory(id, "final-reader"), progress, token);
        var fidelity = NativeFidelity.Compare(File.ReadAllBytes(initial), File.ReadAllBytes(output));
        var differences = XpdlDocument.CompareGraph(reopened.Elements, verified.Elements);
        var metadataDifferences = XpdlDocument.CompareMetadata(reopened, verified);
        File.WriteAllText(Path.Combine(directory, "visio-native-restart.json"), JsonSerializer.Serialize(new { fidelity, differences, metadataDifferences, verified }));
        if (!fidelity.Preserved || differences.Length != 0 || metadataDifferences.Length != 0)
            throw new InvalidDataException("Imported Visio model failed strict native no-op/restart fidelity. Originals retained; inspect visio-native-restart.json.");
        return new(imported, reopened, verified, normalizations, metadataNormalizations, fidelity);
    }

    public OperationView ImportVisio(NativeExchangeInput input, string modelName, bool acknowledgeFormatLimits)
    {
        RequireVisioAcknowledgement(acknowledgeFormatLimits);
        byte[] captured = ReadVisio(input);
        return operations.Start("native_visio_import", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.vdx");
            await File.WriteAllBytesAsync(source, captured, token);
            var verification = await VerifyVisioImport(id, source, directory, modelName, progress, token);
            var result = new { verification, sourcePages = VisioDocument.Inspect(captured), sourceUnmodified = true,
                sourceRevision = BpmnDocument.Revision(captured), outputArtifact = "artifact:" + id + ":model.bpm",
                outputRevision = BpmnDocument.Revision(File.ReadAllBytes(Path.Combine(directory, "model.bpm"))), warning = VisioWarning };
            File.WriteAllText(Path.Combine(directory, "visio-import-readback.json"), JsonSerializer.Serialize(result));
            return result;
        });
    }

    public OperationView ExportVisio(string path, string expectedRevision, string[] diagramIds, bool acknowledgeFormatLimits)
    {
        RequireVisioAcknowledgement(acknowledgeFormatLimits);
        if (diagramIds == null || diagramIds.Length is < 1 or > 100 || diagramIds.Distinct(StringComparer.Ordinal).Count() != diagramIds.Length)
            throw new InvalidDataException("Select 1-100 distinct native diagram IDs.");
        foreach (string diagramId in diagramIds) NativeMetadataPolicy.RequireId(diagramId);
        string[] selected = diagramIds.ToArray(); var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Native source revision conflict.");
        return operations.Start("native_visio_export", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "export.vdx");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            var exported = await Execute(new EngineRequest { OperationId = id, Action = "visio_export", InputPath = source, OutputPath = output, SelectedDiagramIds = selected },
                RunDirectory(id, "exporter"), progress, token);
            byte[] vdx = File.ReadAllBytes(output); var pages = VisioDocument.Inspect(vdx);
            var verification = await VerifyVisioImport(id, output, directory, "Visio readback", progress, token);
            if (BpmnDocument.Revision(File.ReadAllBytes(source)) != input.Revision) throw new InvalidDataException("Native Visio export modified its staged source.");
            var result = new { exported, verification, selectedDiagramIds = selected, pages,
                emptyPages = pages.Where(p => p.Shapes.Length == 0).Select(p => p.Id).ToArray(),
                projectionDifferences = VisioDocument.CompareProjection(exported.Elements.Where(e => selected.Contains(e.DiagramId)).ToArray(), verification.Verified.Elements),
                graphDifferences = XpdlDocument.CompareGraph(exported.Elements, verification.Verified.Elements),
                metadataDifferences = XpdlDocument.CompareMetadata(exported, verification.Verified),
                nativeRoundtripComparison = NativeFidelity.Compare(input.Bytes, File.ReadAllBytes(Path.Combine(directory, "model.bpm"))),
                nativeSourceUnmodified = true, sourceRevision = input.Revision,
                outputArtifact = "artifact:" + id + ":export.vdx", outputRevision = BpmnDocument.Revision(vdx),
                verificationModel = "artifact:" + id + ":model.bpm", verificationRevision = BpmnDocument.Revision(File.ReadAllBytes(Path.Combine(directory, "model.bpm"))), warning = VisioWarning };
            File.WriteAllText(Path.Combine(directory, "visio-export-readback.json"), JsonSerializer.Serialize(result));
            return result;
        });
    }
}
