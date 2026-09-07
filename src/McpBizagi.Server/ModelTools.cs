using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpBizagi.Server;

/// <summary>MCP schemas and result envelopes; native orchestration belongs to NativeWorkflows.</summary>
[McpServerToolType]
public sealed class ModelTools(WorkspaceFiles files, ServerOptions options, NativeWorkflows native, Operations operations)
{
    private static CallToolResult Result(object data, bool error = false) => new()
    {
        IsError = error, StructuredContent = JsonSerializer.SerializeToElement(data),
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(data) }]
    };
    private static CallToolResult Guard(Func<object> action, bool xml = false)
    {
        try { return xml ? Result(new { ok = true, backend = "standards_xml", result = action() }) : Result(action()); }
        catch (Exception error) { return Result(new { ok = false, code = error.GetType().Name, error = error.Message }, true); }
    }

    [McpServerTool(Name = "capabilities_get"), Description("Inspect installation and implemented capability boundaries. Availability is not operational accreditation.")]
    public CallToolResult Capabilities()
    {
        string? version = options.Installation is {} p && File.Exists(Path.Combine(p, "BizagiModeler.exe"))
            ? FileVersionInfo.GetVersionInfo(Path.Combine(p, "BizagiModeler.exe")).FileVersion : null;
        return Result(new { ok = true, protocol = 1, nativeVersion = version, experimentalNativeEnabled = options.ExperimentalNative,
            nativePrerequisitesAvailable = version == "4.3.0.008" && options.ExperimentalNative && File.Exists(options.Worker),
            capabilities = new[] {
                new { name = "bpmn_xml_inspect_create_rename_validate", status = "implemented", backend = "standards_xml" },
                new { name = "bpm_native_import_save_reopen_export", status = "experimental_diagnostic", backend = "bizagi_worker" },
                new { name = "bpm_native_inspect_and_copy_only_name_edits", status = "experimental_diagnostic", backend = "bizagi_worker" },
                new { name = "native_documentation_simulation", status = "investigated_not_implemented", backend = "bizagi_worker" }
            }, operationalEvidence = "Run acceptance in this environment; a declaration never overrides an actual failure.", foregroundAutomation = false });
    }

    [McpServerTool(Name = "bpmn_inspect"), Description("Read BPMN XML element identities, nesting and SHA-256 revision. Does not invoke Bizagi.")]
    public CallToolResult Inspect(string path) => Guard(() => { var input = files.ReadBpmn(path); return BpmnDocument.Inspect(input.Text, input.Revision); }, xml: true);

    [McpServerTool(Name = "bpmn_validate"), Description("Run bounded structural checks. Not complete OMG, native-engine, behavioral or visual validation.")]
    public CallToolResult Validate(string path) => Guard(() => BpmnDocument.Validate(files.ReadBpmn(path).Text), xml: true);

    [McpServerTool(Name = "bpmn_create"), Description("Save supplied BPMN XML preserving extensions and nesting. Refuses existing destinations; invents no layout.")]
    public CallToolResult Create(string path, string xml) => Guard(() => files.SaveBpmn(path, xml), xml: true);

    [McpServerTool(Name = "bpmn_apply_changes"), Description("Batch element-name edits with an expected SHA-256 revision, preserved XML and backup. Other change types fail.")]
    public CallToolResult Apply(string path, string expectedRevision, ModelChange[] changes) => Guard(() =>
    {
        var input = files.ReadBpmn(path);
        if (input.Revision != expectedRevision) throw new IOException("Revision conflict: file changed since inspection.");
        return files.SaveBpmn(path, BpmnDocument.Apply(input.Text, changes), expectedRevision);
    }, xml: true);

    [McpServerTool(Name = "native_probe"), Description("Experimental native service bootstrap. Returns an operation ID, not a claim of full engine support.")]
    public CallToolResult Probe() => Guard(() => native.Probe());

    [McpServerTool(Name = "native_roundtrip"), Description("Experimental BPMN -> .bpm -> fresh-worker reload -> BPMN diagnostic. Originals are untouched; outputs are operation artifacts. Poll operation_get.")]
    public CallToolResult Roundtrip(string path, string modelName = "Model") => Guard(() => native.Roundtrip(path, modelName));

    [McpServerTool(Name = "native_inspect"), Description("Read an existing unencrypted .bpm through a private native copy, returning native IDs, source revision and projected BPMN artifacts. Poll operation_get.")]
    public CallToolResult InspectNative(string path) => Guard(() => native.Inspect(path));

    [McpServerTool(Name = "native_apply_changes"), Description("Experimental native name-change batch using native IDs and expected source revision. Writes ONLY a new operation artifact, verifies every edit in a fresh worker, and never overwrites the source. Rich-content preservation remains unaccredited. Poll operation_get.")]
    public CallToolResult ApplyNative(string path, string expectedRevision, NativeNameChange[] changes) => Guard(() => native.ApplyNames(path, expectedRevision, changes));

    [McpServerTool(Name = "operation_get"), Description("Read durable operation status, phases, artifacts and real failures.")]
    public CallToolResult GetOperation(string operationId) => Guard(() => operations.Get(operationId));

    [McpServerTool(Name = "operation_cancel"), Description("Cancel an owned operation and isolated worker. Never closes the operator's Modeler session.")]
    public CallToolResult CancelOperation(string operationId) => Guard(() => operations.Cancel(operationId));
}
