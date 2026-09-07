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
                new { name = "bpm_native_graph_inspect_and_copy_only_name_edits", status = "experimental_diagnostic", backend = "bizagi_worker" },
                new { name = "native_container_fidelity_and_noop_save", status = "experimental_verified_on_tested_inputs", backend = "bizagi_worker" },
                new { name = "native_model_validation", status = "experimental_diagnostic", backend = "bizagi_worker" },
                new { name = "native_simulation", status = "experimental_level_one_locally_verified_other_scenarios_pending", backend = "bizagi_worker" },
                new { name = "native_offscreen_svg_png", status = "experimental_basic_diagram_locally_verified_rich_visual_fidelity_pending", backend = "bizagi_worker" },
                new { name = "native_documentation", status = "investigated_not_implemented", backend = "bizagi_worker" }
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
    public CallToolResult Roundtrip(string path, string modelName = "Model", string[]? additionalPaths = null) => Guard(() => native.Roundtrip(path, modelName, additionalPaths));

    [McpServerTool(Name = "native_inspect"), Description("Read an existing unencrypted .bpm through a private native copy, returning native IDs, containment, geometry, documentation, scenarios and source revision. Poll operation_get.")]
    public CallToolResult InspectNative(string path) => Guard(() => native.Inspect(path));

    [McpServerTool(Name = "native_apply_changes"), Description("Native name-change batch using native IDs and expected source revision. Writes ONLY a new artifact, verifies edits in a fresh worker, and rejects unexplained whole-container differences. Broad rich-model coverage remains experimental. Poll operation_get.")]
    public CallToolResult ApplyNative(string path, string expectedRevision, NativeNameChange[] changes) => Guard(() => native.ApplyNames(path, expectedRevision, changes));

    [McpServerTool(Name = "native_save_copy"), Description("Load and save a native model without requested changes, reopen in a fresh worker and compare the entire archive. Reject unexplained differences. The source is never overwritten.")]
    public CallToolResult SaveNativeCopy(string path, string expectedRevision) => Guard(() => native.ApplyNames(path, expectedRevision, [], saveCopy: true));

    [McpServerTool(Name = "native_validate"), Description("Run the installed native BPMN validator on a private model copy. Poll operation_get for vendor severities and native element IDs. Does not fix or overwrite the source.")]
    public CallToolResult ValidateNative(string path) => Guard(() => native.Analyze(path, "validate"));

    [McpServerTool(Name = "native_simulate"), Description("Experimental installed-engine simulation of one native diagram. Supply native diagram/scenario IDs from native_inspect. An empty scenario ID explicitly uses native defaults. Level 1-4; no source edits. Poll operation_get for real results or errors.")]
    public CallToolResult SimulateNative(string path, string diagramId, string scenarioId = "", int simulationLevel = 1) =>
        Guard(() => native.Analyze(path, "simulate", diagramId, scenarioId, simulationLevel));

    [McpServerTool(Name = "native_render_svg"), Description("Experimental native offscreen SVG rendering of one diagram, using the installed renderer without clicks or foreground control. Produces a private operation artifact; failures remain explicit.")]
    public CallToolResult RenderNative(string path, string diagramId) => Guard(() => native.Analyze(path, "render_svg", diagramId));

    [McpServerTool(Name = "native_compare", ReadOnly = true, Destructive = false, OpenWorld = false), Description("Compare entire unencrypted native containers without converting them to BPMN. Includes nested diagram XML and binary attachments; reports unknown changes instead of silently discarding them.")]
    public CallToolResult CompareNative(string path, string otherPath, NativeNameChange[]? expectedNames = null) => Guard(() =>
        native.Compare(path, otherPath, expectedNames));

    [McpServerTool(Name = "operation_get"), Description("Read durable operation status, phases, artifacts and real failures.")]
    public CallToolResult GetOperation(string operationId) => Guard(() => operations.Get(operationId));

    [McpServerTool(Name = "operation_cancel"), Description("Cancel an owned operation and isolated worker. Never closes the operator's Modeler session.")]
    public CallToolResult CancelOperation(string operationId) => Guard(() => operations.Cancel(operationId));
}
