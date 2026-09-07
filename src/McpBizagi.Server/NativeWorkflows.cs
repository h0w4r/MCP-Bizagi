using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

/// <summary>Native use cases, separated from MCP schemas and the version-specific engine adapter.</summary>
public sealed class NativeWorkflows(WorkspaceFiles files, ServerOptions options, WorkerClient worker, Operations operations)
{
    public OperationView Probe() => operations.Start("native_probe", async (id, progress, token) =>
        await Execute(new EngineRequest { OperationId = id }, RunDirectory(id, "probe"), progress, token));

    public OperationView Roundtrip(string path, string modelName)
    {
        var input = files.ReadBpmn(path);
        if (BpmnDocument.Validate(input.Text).Any(f => f.Severity == "error")) throw new InvalidDataException("Input has structural errors.");
        return operations.Start("native_roundtrip", async (id, progress, token) =>
        {
            string run = CreateArtifactDirectory(id);
            string source = Path.Combine(run, "input.bpmn"), native = Path.Combine(run, "model.bpm");
            await File.WriteAllBytesAsync(source, BpmnDocument.Encode(input.Text), token);
            var saved = await Execute(new EngineRequest { OperationId = id, Action = "import_save", InputPath = source,
                OutputPath = native, ModelName = modelName }, RunDirectory(id, "writer"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "read_export", InputPath = native,
                OutputPath = Path.Combine(run, "exported"), ModelName = modelName }, RunDirectory(id, "reader"), progress, token);
            var summaries = reopened.Artifacts.Select(file => BpmnDocument.Inspect(File.ReadAllText(file), BpmnDocument.Revision(File.ReadAllBytes(file)))).ToArray();
            var fidelity = reopened.Artifacts.Select(file => new { file, findings = BpmnFidelity.Compare(input.Text, File.ReadAllText(file)) }).ToArray();
            return new { saved, reopened, summaries, fidelity, evidence = run, sourceRevision = input.Revision,
                accreditation = "diagnostic_completed_not_full_fidelity_or_visual_accreditation" };
        });
    }

    public OperationView Inspect(string path)
    {
        var input = ReadNative(path);
        return operations.Start("native_inspect", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id);
            string source = Path.Combine(directory, "input.bpm"); await File.WriteAllBytesAsync(source, input.Bytes, token);
            var result = await Execute(new EngineRequest { OperationId = id, Action = "read_export", InputPath = source,
                OutputPath = Path.Combine(directory, "exported") }, RunDirectory(id, "reader"), progress, token);
            return new { sourceRevision = input.Revision, result, nativeSourceUnmodified = true,
                warning = "Export is a projection, not a lossless representation of all native model data." };
        });
    }

    public OperationView ApplyNames(string path, string expectedRevision, NativeNameChange[] changes)
    {
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Revision conflict: native file changed since inspection.");
        if (changes.Length == 0 || changes.Length > 1000 || changes.Any(c => string.IsNullOrWhiteSpace(c.ElementId) || c.Name == null))
            throw new InvalidDataException("Supply between 1 and 1000 native element name changes with nonempty IDs.");
        if (changes.Select(c => c.ElementId).Distinct().Count() != changes.Length)
            throw new InvalidDataException("A native batch must not edit the same element twice.");
        return operations.Start("native_apply_changes", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id);
            string source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "edit_save", InputPath = source,
                OutputPath = output, Changes = changes }, RunDirectory(id, "editor"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "read_export", InputPath = output,
                OutputPath = Path.Combine(directory, "exported") }, RunDirectory(id, "reader"), progress, token);
            // Every requested identity and value must survive an independently started reader process.
            foreach (var change in changes)
                if (reopened.Elements.Count(e => e.Id == change.ElementId && e.Name == change.Name) != 1)
                    throw new InvalidDataException("Native edit did not survive fresh-worker readback: " + change.ElementId);
            return new { edited, reopened, sourceRevision = input.Revision, outputRevision = BpmnDocument.Revision(File.ReadAllBytes(output)),
                nativeSourceUnmodified = true, requestedChangesVerified = changes.Length, evidence = directory,
                warning = "Experimental copy-only edit. Native rich-content preservation and visual compatibility are not accredited; inspect the output before adopting it." };
        });
    }

    private (byte[] Bytes, string Revision) ReadNative(string path)
    {
        if (!Path.GetExtension(path).Equals(".bpm", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("Expected a native .bpm file.");
        byte[] bytes = files.Read(path); NativeArchive.Validate(bytes);
        return (bytes, BpmnDocument.Revision(bytes));
    }
    private async Task<EngineReply> Execute(EngineRequest request, string directory, Action<string> progress, CancellationToken token)
    {
        var reply = await worker.Execute(request, directory, progress, token);
        if (!reply.Success) throw new InvalidOperationException(reply.Code + ": " + reply.Message);
        return reply;
    }
    private string RunDirectory(string id, string component) => Path.Combine(Path.GetFullPath(options.State), "runs", id, component);
    private string CreateArtifactDirectory(string id)
    {
        string directory = RunDirectory(id, "artifacts"); Directory.CreateDirectory(directory);
        if (options.Installation != null)
        {
            var fingerprint = new[] { "BizagiModeler.exe", "Bizagi.ProcessModeler.Persistence.dll", "Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessLogic.dll" }
                .Select(name => { string path = Path.Combine(options.Installation, name); return new { name, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), version = FileVersionInfo.GetVersionInfo(path).FileVersion }; }).ToArray();
            File.WriteAllText(Path.Combine(directory, "engine-fingerprint.json"), JsonSerializer.Serialize(fingerprint, new JsonSerializerOptions { WriteIndented = true }));
        }
        return directory;
    }
}
