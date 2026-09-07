using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

/// <summary>Native use cases, separated from MCP schemas and the version-specific engine adapter.</summary>
public sealed class NativeWorkflows(WorkspaceFiles files, ServerOptions options, WorkerClient worker, Operations operations)
{
    public OperationView Probe() => operations.Start("native_probe", async (id, progress, token) =>
        await Execute(new EngineRequest { OperationId = id }, RunDirectory(id, "probe"), progress, token));

    public OperationView Roundtrip(string path, string modelName, string[]? additionalPaths = null)
    {
        var input = files.ReadBpmn(path);
        var inputs = new[] { input }.Concat((additionalPaths ?? []).Select(files.ReadBpmn)).ToArray();
        if (inputs.Any(i => BpmnDocument.Validate(i.Text).Any(f => f.Severity == "error"))) throw new InvalidDataException("Input has structural errors.");
        return operations.Start("native_roundtrip", async (id, progress, token) =>
        {
            string run = CreateArtifactDirectory(id);
            string native = Path.Combine(run, "model.bpm");
            var sources = new List<string>();
            for (int i = 0; i < inputs.Length; i++)
            {
                string source = Path.Combine(run, "input-" + i + ".bpmn");
                await File.WriteAllBytesAsync(source, BpmnDocument.Encode(inputs[i].Text), token); sources.Add(source);
            }
            var saved = await Execute(new EngineRequest { OperationId = id, Action = "import_save", InputPaths = sources.ToArray(),
                OutputPath = native, ModelName = modelName }, RunDirectory(id, "writer"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "read_export", InputPath = native,
                OutputPath = Path.Combine(run, "exported"), ModelName = modelName }, RunDirectory(id, "reader"), progress, token);
            var summaries = reopened.Artifacts.Select(file => BpmnDocument.Inspect(File.ReadAllText(file), BpmnDocument.Revision(File.ReadAllBytes(file)))).ToArray();
            if (saved.Diagrams.Length != inputs.Length || reopened.Diagrams.Length != saved.Diagrams.Length || reopened.Artifacts.Length != inputs.Length)
                throw new InvalidDataException("Native multi-diagram import/reload/export count mismatch.");
            // Native Export names each artifact after its collaboration. Do not silently skip multi-diagram fidelity checks.
            var fidelity = saved.Diagrams.Select((diagram, index) =>
            {
                string file = reopened.Artifacts.Single(f => Path.GetFileNameWithoutExtension(f) == diagram);
                return new { inputIndex = index, file, findings = BpmnFidelity.Compare(inputs[index].Text, File.ReadAllText(file)) };
            }).ToArray();
            return new { saved, reopened, summaries, fidelity, evidence = run, nativeArtifact = "artifact:" + id + ":model.bpm", sourceRevision = input.Revision,
                sourceRevisions = inputs.Select(i => i.Revision).ToArray(),
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
            var result = await Execute(new EngineRequest { OperationId = id, Action = "inspect", InputPath = source }, RunDirectory(id, "reader"), progress, token);
            return new { sourceRevision = input.Revision, result, nativeSourceUnmodified = true,
                warning = "Native graph inspection is not visual validation and does not serialize every native property." };
        });
    }

    public OperationView ApplyNames(string path, string expectedRevision, NativeNameChange[] changes, bool saveCopy = false)
    {
        var input = ReadNative(path);
        if (input.Revision != expectedRevision) throw new IOException("Revision conflict: native file changed since inspection.");
        if ((!saveCopy && changes.Length == 0) || changes.Length > 1000 || changes.Any(c => string.IsNullOrWhiteSpace(c.ElementId) || c.Name == null))
            throw new InvalidDataException("Supply between 1 and 1000 native element name changes with nonempty IDs.");
        if (changes.Select(c => c.ElementId).Distinct().Count() != changes.Length)
            throw new InvalidDataException("A native batch must not edit the same element twice.");
        return operations.Start(saveCopy ? "native_save_copy" : "native_apply_changes", async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id);
            string source = Path.Combine(directory, "input.bpm"), output = Path.Combine(directory, "edited.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            var edited = await Execute(new EngineRequest { OperationId = id, Action = "edit_save", InputPath = source,
                OutputPath = output, Changes = changes }, RunDirectory(id, "editor"), progress, token);
            var reopened = await Execute(new EngineRequest { OperationId = id, Action = "inspect", InputPath = output }, RunDirectory(id, "reader"), progress, token);
            // Every requested identity and value must survive an independently started reader process.
            foreach (var change in changes)
                if (reopened.Elements.Count(e => e.Id == change.ElementId && e.Name == change.Name) != 1)
                    throw new InvalidDataException("Native edit did not survive fresh-worker readback: " + change.ElementId);
            progress("native_fidelity_verification");
            var fidelity = NativeFidelity.Compare(input.Bytes, File.ReadAllBytes(output), changes.Select(c => new ExpectedNativeName(c.ElementId, c.Name)).ToArray());
            string fidelityPath = Path.Combine(directory, "native-fidelity.json");
            File.WriteAllText(fidelityPath, JsonSerializer.Serialize(fidelity, new JsonSerializerOptions { WriteIndented = true }));
            if (!fidelity.Preserved) throw new InvalidDataException("Native fidelity gate rejected unexplained changes; source is unchanged. Inspect " + fidelityPath);
            return new { edited, reopened, sourceRevision = input.Revision, outputArtifact = "artifact:" + id + ":edited.bpm", outputRevision = BpmnDocument.Revision(File.ReadAllBytes(output)),
                nativeSourceUnmodified = true, requestedChangesVerified = changes.Length, evidence = directory, fidelity,
                warning = "Experimental copy-only edit. Native rich-content preservation and visual compatibility are not accredited; inspect the output before adopting it." };
        });
    }

    public OperationView Analyze(string path, string action, string diagramId = "", string scenarioId = "", int simulationLevel = 1)
    {
        if (action is not "validate" and not "simulate" and not "render_svg") throw new NotSupportedException("Unknown analysis action.");
        var input = ReadNative(path);
        return operations.Start("native_" + action, async (id, progress, token) =>
        {
            string directory = CreateArtifactDirectory(id), source = Path.Combine(directory, "input.bpm");
            await File.WriteAllBytesAsync(source, input.Bytes, token);
            var result = await Execute(new EngineRequest { OperationId = id, Action = action, InputPath = source,
                OutputPath = Path.Combine(directory, "results"), DiagramId = diagramId, ScenarioId = scenarioId, SimulationLevel = simulationLevel },
                RunDirectory(id, "analyzer"), progress, token);
            return new { sourceRevision = input.Revision, result, nativeSourceUnmodified = true,
                warning = action == "simulate" ? "Scenario settings are used in memory only; an empty scenario ID uses native defaults. No result is saved into the source model."
                    : action == "render_svg" ? "Native offscreen rendering is not independent visual compatibility accreditation."
                    : "Validation findings are reported by the installed engine; a successful execution can contain validation errors." };
        });
    }

    private (byte[] Bytes, string Revision) ReadNative(string path)
    {
        byte[] bytes;
        if (path.StartsWith("artifact:", StringComparison.Ordinal))
        {
            // Opaque references permit MCP-only handoff without granting arbitrary access to private state/log files.
            var match = Regex.Match(path, @"\Aartifact:([0-9a-f]{32}):(model\.bpm|edited\.bpm)\z");
            if (!match.Success) throw new InvalidDataException("Invalid native artifact reference.");
            string id = match.Groups[1].Value;
            var operation = operations.Get(id);
            if (operation.State != "completed") throw new InvalidOperationException("Only completed operation artifacts can become new inputs.");
            string directory = RunDirectory(id, "artifacts");
            bytes = new WorkspaceFiles(directory).Read(match.Groups[2].Value);
        }
        else
        {
            if (!Path.GetExtension(path).Equals(".bpm", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("Expected a native .bpm file.");
            bytes = files.Read(path);
        }
        NativeArchive.Validate(bytes);
        return (bytes, BpmnDocument.Revision(bytes));
    }
    public NativeFidelityReport Compare(string path, string otherPath, NativeNameChange[]? names) =>
        NativeFidelity.Compare(ReadNative(path).Bytes, ReadNative(otherPath).Bytes, names?.Select(n => new ExpectedNativeName(n.ElementId, n.Name)).ToArray());
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
