using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using McpBizagi.Contracts;
using McpBizagi.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpBizagi.Server;

[McpServerToolType]
public sealed class ModelTools(WorkspaceFiles files, ServerOptions options, WorkerClient worker, Operations operations)
{
    private static CallToolResult Result(object data, bool error = false)
    {
        var node = JsonSerializer.SerializeToNode(data)!;
        return new CallToolResult { IsError = error, StructuredContent = JsonSerializer.SerializeToElement(data),
            Content = [new TextContentBlock { Text = node.ToJsonString() }] };
    }
    private static CallToolResult Guard(Func<object> action)
    {
        try { return Result(new { ok = true, backend = "standards_xml", result = action() }); }
        catch (Exception e) { return Result(new { ok = false, code = e.GetType().Name, error = e.Message }, true); }
    }

    [McpServerTool(Name = "capabilities_get"), Description("Inspect installed engine and capability evidence. Presence and successful probes are not accreditation.")]
    public CallToolResult Capabilities()
    {
        string? version = options.Installation is {} p && File.Exists(Path.Combine(p, "BizagiModeler.exe"))
            ? FileVersionInfo.GetVersionInfo(Path.Combine(p, "BizagiModeler.exe")).FileVersion : null;
        return Result(new { ok = true, protocol = 1, nativeVersion = version, experimentalNativeEnabled = options.ExperimentalNative,
            capabilities = new[] {
                new { name = "bpmn_xml_inspect_create_rename_validate", status = "implemented", backend = "standards_xml" },
                new { name = "bpm_native_import_save_reopen_export", status = "unaccredited", backend = "bizagi_worker" },
                new { name = "native_documentation_simulation", status = "investigated_not_implemented", backend = "bizagi_worker" }
            }, foregroundAutomation = false });
    }

    [McpServerTool(Name = "bpmn_inspect"), Description("Read a BPMN XML file, including nested element identities and its SHA-256 revision. Does not invoke Bizagi.")]
    public CallToolResult Inspect(string path) => Guard(() => { var input = files.ReadBpmn(path); return BpmnDocument.Inspect(input.Text, input.Revision); });

    [McpServerTool(Name = "bpmn_validate"), Description("Run bounded structural BPMN checks. Not complete OMG, native-engine, behavior or visual validation.")]
    public CallToolResult Validate(string path) => Guard(() => BpmnDocument.Validate(files.ReadBpmn(path).Text));

    [McpServerTool(Name = "bpmn_create"), Description("Save supplied BPMN 2.0 XML without dropping extensions or nested subprocesses. Refuses existing destinations. No layout is invented.")]
    public CallToolResult Create(string path, string xml) => Guard(() => files.SaveBpmn(path, xml));

    [McpServerTool(Name = "bpmn_apply_changes"), Description("Apply a batch of element name changes using element IDs and an expected SHA-256 revision. Preserves other XML and creates a backup. Other change types are rejected.")]
    public CallToolResult Apply(string path, string expectedRevision, ModelChange[] changes) => Guard(() =>
    {
        var input = files.ReadBpmn(path);
        if (input.Revision != expectedRevision) throw new IOException("Revision conflict: file changed since inspection.");
        return files.SaveBpmn(path, BpmnDocument.Apply(input.Text, changes), expectedRevision);
    });

    [McpServerTool(Name = "native_probe"), Description("Start an experimental isolated native bootstrap diagnostic. Returns an operation ID; no capability is accredited merely by resolving services.")]
    public CallToolResult Probe() => Result(operations.Start("native_probe", async (id, progress, token) =>
    {
        var result = await worker.Execute(new EngineRequest { OperationId = id }, RunDirectory(id, "probe"), progress, token);
        if (!result.Success) throw new InvalidOperationException(result.Code + ": " + result.Message);
        return result;
    }));

    [McpServerTool(Name = "native_roundtrip"), Description("Experimental BPMN -> native .bpm -> fresh-worker reload -> BPMN diagnostic. Never overwrites source files. Outputs remain in isolated operation evidence.")]
    public CallToolResult Roundtrip(string path, string modelName = "Model")
    {
        try
        {
            var input = files.ReadBpmn(path);
            if (BpmnDocument.Validate(input.Text).Any(f => f.Severity == "error")) throw new InvalidDataException("Input has structural errors.");
            return Result(operations.Start("native_roundtrip", async (id, progress, token) =>
            {
                string run = RunDirectory(id, "artifacts"); Directory.CreateDirectory(run);
                string source = Path.Combine(run, "input.bpmn"), native = Path.Combine(run, "model.bpm"), exported = Path.Combine(run, "exported");
                await File.WriteAllTextAsync(source, input.Text, token);
                WriteEngineFingerprint(run);
                var saved = await worker.Execute(new EngineRequest { OperationId = id, Action = "import_save", InputPath = source,
                    OutputPath = native, ModelName = modelName }, RunDirectory(id, "writer"), progress, token);
                if (!saved.Success) throw new InvalidOperationException(saved.Code + ": " + saved.Message);
                var reopened = await worker.Execute(new EngineRequest { OperationId = id, Action = "read_export", InputPath = native,
                    OutputPath = exported, ModelName = modelName }, RunDirectory(id, "reader"), progress, token);
                if (!reopened.Success) throw new InvalidOperationException(reopened.Code + ": " + reopened.Message);
                var summaries = reopened.Artifacts.Select(file => BpmnDocument.Inspect(File.ReadAllText(file), BpmnDocument.Revision(File.ReadAllBytes(file)))).ToArray();
                return new { saved, reopened, summaries, evidence = run,
                    accreditation = "diagnostic_completed_not_full_fidelity_or_visual_accreditation" };
            }));
        }
        catch (Exception e) { return Result(new { ok = false, code = e.GetType().Name, error = e.Message }, true); }
    }

    [McpServerTool(Name = "operation_get"), Description("Read durable operation state, latest meaningful phase, errors and resulting artifacts.")]
    public CallToolResult GetOperation(string operationId)
    { try { return Result(operations.Get(operationId)); } catch (Exception e) { return Result(new { error = e.Message }, true); } }

    [McpServerTool(Name = "operation_cancel"), Description("Cancel an owned operation and its isolated worker; never closes the operator's Bizagi session.")]
    public CallToolResult CancelOperation(string operationId)
    { try { return Result(operations.Cancel(operationId)); } catch (Exception e) { return Result(new { error = e.Message }, true); } }

    private string RunDirectory(string id, string component) => Path.Combine(Path.GetFullPath(options.State), "runs", id, component);
    private void WriteEngineFingerprint(string directory)
    {
        if (options.Installation == null) return;
        var fingerprint = new[] { "BizagiModeler.exe", "Bizagi.ProcessModeler.Persistence.dll", "Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessLogic.dll" }
            .Select(name => { string p = Path.Combine(options.Installation, name); return new { name, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))), version = FileVersionInfo.GetVersionInfo(p).FileVersion }; }).ToArray();
        File.WriteAllText(Path.Combine(directory, "engine-fingerprint.json"), JsonSerializer.Serialize(fingerprint, new JsonSerializerOptions { WriteIndented = true }));
    }
}
