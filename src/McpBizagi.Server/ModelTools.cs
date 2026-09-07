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
        IsError = error,
        StructuredContent = JsonSerializer.SerializeToElement(data),
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
        string? version = options.Installation is { } p && File.Exists(Path.Combine(p, "BizagiModeler.exe"))
            ? FileVersionInfo.GetVersionInfo(Path.Combine(p, "BizagiModeler.exe")).FileVersion : null;
        return Result(new
        {
            ok = true,
            protocol = 1,
            nativeVersion = version,
            experimentalNativeEnabled = options.ExperimentalNative,
            nativePrerequisitesAvailable = version == "4.3.0.008" && options.ExperimentalNative && File.Exists(options.Worker),
            // Publish the host's actual allowlist so a client need not guess enum spellings.
            // Schema availability remains separate from installed-engine accreditation.
            nativeMutationTypes = NativeEditPlan.CreatableTypes,
            nativeSubProcessKinds = NativeSubProcessPolicy.Kinds,
            intermediateCreationModes = new[] { "Catch", "Throw", "Boundary" },
            capabilities = new[] {
                new { name = "bpmn_xml_inspect_create_rename_validate", status = "implemented", backend = "standards_xml" },
                new { name = "bpm_native_import_save_reopen_export", status = "experimental_diagnostic", backend = "bizagi_worker" },
                new { name = "bpm_native_graph_inspect_and_copy_only_name_edits", status = "experimental_diagnostic", backend = "bizagi_worker" },
                new { name = "native_diagram_lifecycle_and_persisted_tabs", status = "experimental_copy_only_native_cloning_and_ordered_preferences_with_fresh_readback_not_live_session_control", backend = "bizagi_worker" },
                new { name = "native_blank_model_creation", status = "experimental_native_constructor_fresh_readback_and_noop_stability_gate_not_live_desktop", backend = "bizagi_worker" },
                new { name = "native_workspace_commit_and_reconciliation", status = "experimental_byte_exact_adoption_durable_intent_backup_and_fresh_native_readback_no_blind_replay", backend = "bizagi_worker" },
                new { name = "native_structural_geometry_documentation_batches", status = "experimental_palette_connections_and_explicit_pool_lane_milestone_subprocess_lifecycle_not_full_editor", backend = "bizagi_worker" },
                new { name = "native_event_boundary_lifecycle", status = "experimental_explicit_modes_interruption_activity_references_and_clone_remapping_not_payload_editor_or_simulation_accreditation", backend = "bizagi_worker" },
                new { name = "native_container_fidelity_and_noop_save", status = "experimental_verified_on_tested_inputs", backend = "bizagi_worker" },
                new { name = "native_model_validation", status = "experimental_diagnostic", backend = "bizagi_worker" },
                new { name = "native_metadata_resources_activity_raci", status = "experimental_copy_only_verified_on_tested_inputs", backend = "bizagi_worker" },
                new { name = "native_extended_attributes_and_embedded_files", status = "experimental_native_xml_and_byte_transactions_with_fresh_readback_not_full_editor_or_visual_accreditation", backend = "bizagi_worker" },
                new { name = "native_simulation_configuration", status = "experimental_full_diagram_bpsim_replacement_with_fidelity_gate", backend = "bizagi_worker" },
                new { name = "native_simulation_and_what_if", status = "experimental_levels_one_to_four_and_replications_verified_on_tested_inputs", backend = "bizagi_worker" },
                new { name = "native_offscreen_svg_png", status = "experimental_basic_diagram_locally_verified_rich_visual_fidelity_pending", backend = "bizagi_worker" },
                new { name = "native_documentation", status = "experimental_excel_word_pdf_verified_on_tested_inputs_not_all_publication_formats", backend = "bizagi_worker" }
            },
            operationalEvidence = "Run acceptance in this environment; a declaration never overrides an actual failure.",
            foregroundAutomation = false
        });
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

    [McpServerTool(Name = "native_model_create"), Description("Create a blank native .bpm with 1-100 explicitly named diagrams using installed model constructors and domain defaults, not BPMN import. IDs are generated once and retained in the receipt. Verify fresh-worker readback and no-op persistence before returning an artifact. No existing file is overwritten; initial tab order follows diagramNames.")]
    public CallToolResult CreateNativeModel(string[] diagramNames) => Guard(() => native.CreateModel(diagramNames));

    [McpServerTool(Name = "native_commit", Destructive = true, OpenWorld = false), Description("Adopt exact native file/artifact bytes into a workspace .bpm after installed-engine validation. Requires source SHA-256. Omit expectedDestinationRevision only to create a new file; replacement requires the previous destination SHA-256 and retains a backup. Durable intent precedes publication; a fresh worker reads the committed bytes. Poll operation_get; after interruption use native_commit_reconcile, never blind replay.")]
    public CallToolResult CommitNative(string path, string expectedRevision, string destinationPath, string? expectedDestinationRevision = null) =>
        Guard(() => native.CommitNative(path, expectedRevision, destinationPath, expectedDestinationRevision));

    [McpServerTool(Name = "native_commit_reconcile", Destructive = false, OpenWorld = false), Description("Observe a terminal native_commit intent, destination, stage and backup; never replay, roll back or delete model files. Distinguishes applied, not_applied, ambiguous, conflict, unreadable and missing_intent. Applied content must pass another real native reader. Original interrupted/cancelled status remains unchanged. Poll operation_get for the separate reconciliation result.")]
    public CallToolResult ReconcileNativeCommit(string operationId) => Guard(() => native.ReconcileNativeCommit(operationId));

    [McpServerTool(Name = "native_diagrams_get"), Description("Inspect native diagram identities and names plus the persisted ordered OpenedItems preferences. Filesystem/ZIP enumeration order is not desktop tab order. Poll operation_get.")]
    public CallToolResult GetNativeDiagrams(string path) => Guard(() => native.ReadDiagrams(path));

    [McpServerTool(Name = "native_diagrams_apply"), Description("Create, rename, clone or delete native diagrams in a copy; clone IDs are generated by the installed native cloners. OpenedItems replaces the complete ordered tab-preference list, including selected diagram/subprocess. Requires revision, independent reader and whole-archive fidelity. Never controls an open desktop window.")]
    public CallToolResult ApplyNativeDiagrams(string path, string expectedRevision, NativeDiagramPatch patch) => Guard(() => native.ApplyDiagrams(path, expectedRevision, patch));

    [McpServerTool(Name = "native_metadata_get"), Description("Read native resources, activity RACI assignments and complete diagram BPSim 1.0 configurations through the installed engine. Returns native IDs, BPMN references and source revision. Poll operation_get.")]
    public CallToolResult GetNativeMetadata(string path) => Guard(() => native.ReadMetadata(path));

    [McpServerTool(Name = "native_attributes_get"), Description("Read native extended attribute definitions, element values and embedded file hashes. Embedded references use attachment:file-name; linked files are never opened. Poll operation_get.")]
    public CallToolResult GetNativeAttributes(string path) => Guard(() => native.ReadDocumentation(path));

    [McpServerTool(Name = "native_attributes_apply"), Description("Explicit native extended-attribute definition XML, complete per-element value XML and embedded byte transactions. Uses a copy, expected revision, fresh-worker readback and whole-archive fidelity gate. Unknown fields and unrequested losses fail. Poll operation_get.")]
    public CallToolResult ApplyNativeAttributes(string path, string expectedRevision, NativeDocumentationPatch patch) => Guard(() => native.ApplyDocumentation(path, expectedRevision, patch));

    [McpServerTool(Name = "native_attachment_export"), Description("Export an embedded file or image from a native model to a private operation artifact. Verify native-loaded bytes against the original archive and durable output hash. Does not fetch linked files or open the exported file.")]
    public CallToolResult ExportNativeAttachment(string path, string diagramId, string elementId, string fileName) => Guard(() => native.ExportAttachment(path, diagramId, elementId, fileName));

    [McpServerTool(Name = "native_metadata_apply"), Description("Edit local resources, replace complete activity RACI sets and explicitly replace complete per-diagram BPSim configurations in a native copy. Requires revision, validates native readback and all non-targeted archive content. Never silently discards saved simulation results.")]
    public CallToolResult ApplyNativeMetadata(string path, string expectedRevision, NativeMetadataPatch patch) => Guard(() => native.ApplyMetadata(path, expectedRevision, patch));

    [McpServerTool(Name = "native_simulate_what_if"), Description("Run installed native what-if analysis for explicit scenario IDs at level 1-4. Preserve every real replication result and identity as separate artifacts. Reusable subprocesses are native black boxes; inspect SimulationLimitations, not assumed linked-task execution. No changes to the source file. Poll operation_get.")]
    public CallToolResult SimulateNativeWhatIf(string path, string diagramId, string[] scenarioIds, int simulationLevel = 1) => Guard(() => native.RunWhatIf(path, diagramId, scenarioIds, simulationLevel));

    [McpServerTool(Name = "native_mutate"), Description("Apply explicit create/update/delete/reconnect mutations to a native copy. SubProcessKind selects SubProcess/Transaction/AdHoc at creation; SubProcessProperties patches TriggeredByEvent and ad hoc ordering/completion text. EventMode explicitly selects Catch/Throw/Boundary on intermediate creation; EventProperties patches interruption and same-container AttachedToActivityId. Delete or reattach boundaries before deleting their activity. ActivityLoop explicitly replaces None/Standard/MultiInstance configuration. ActivityProperties edits token quantities, compensation and native model state; GatewayDirection and complete FlowCondition (None/Expression/Default) require compatible kinds. Participant creation requires a new ProcessId; lanes/milestones use that process as ParentId with complete partition geometry. Embedded expanded subprocesses require separate ExpandedSize. CallActivity accepts CallTarget.ProcessId (existing local process, not diagram ID); empty unlinks, omission preserves. Requires revision, fresh-worker readback and whole-archive fidelity. Handle children, incident flows and incoming calls explicitly before deletion; never overwrites the input.")]
    public CallToolResult MutateNative(string path, string expectedRevision, NativeMutation[] mutations) => Guard(() => native.Mutate(path, expectedRevision, mutations));

    [McpServerTool(Name = "native_apply_changes"), Description("Native name-change batch using native IDs and expected source revision. Writes ONLY a new artifact, verifies edits in a fresh worker, and rejects unexplained whole-container differences. Broad rich-model coverage remains experimental. Poll operation_get.")]
    public CallToolResult ApplyNative(string path, string expectedRevision, NativeNameChange[] changes) => Guard(() => native.ApplyNames(path, expectedRevision, changes));

    [McpServerTool(Name = "native_save_copy"), Description("Load and save a native model without requested changes, reopen in a fresh worker and compare the entire archive. Reject unexplained differences. The source is never overwritten.")]
    public CallToolResult SaveNativeCopy(string path, string expectedRevision) => Guard(() => native.ApplyNames(path, expectedRevision, [], saveCopy: true));

    [McpServerTool(Name = "native_validate"), Description("Run the installed native BPMN validator on a private model copy. Poll operation_get for vendor severities and native element IDs. Does not fix or overwrite the source.")]
    public CallToolResult ValidateNative(string path) => Guard(() => native.Analyze(path, "validate"));

    [McpServerTool(Name = "native_simulate"), Description("Experimental installed-engine simulation of one native diagram. Supply native diagram/scenario IDs from native_inspect. Empty scenario ID uses native defaults. Reusable subprocesses are black boxes; nondefault activity token execution is unaccredited. Inspect SimulationLimitations and SimulationInputs separately from actual result metrics. Level 1-4; no source edits. Poll operation_get.")]
    public CallToolResult SimulateNative(string path, string diagramId, string scenarioId = "", int simulationLevel = 1) =>
        Guard(() => native.Analyze(path, "simulate", diagramId, scenarioId, simulationLevel));

    [McpServerTool(Name = "native_render_svg"), Description("Experimental native offscreen SVG rendering of one diagram, using the installed renderer without clicks or foreground control. Produces a private operation artifact; failures remain explicit.")]
    public CallToolResult RenderNative(string path, string diagramId, string subProcessId = "") => Guard(() => native.Analyze(path, "render_svg", diagramId, subProcessId: subProcessId));

    [McpServerTool(Name = "native_publish"), Description("Publish local native model documentation to excel, word or pdf using installed generators, without opening a desktop application. Select native diagram IDs or omit for all. Fresh-worker text/image readback; source unchanged. Poll operation_get.")]
    public CallToolResult PublishNative(string path, string format, string[]? diagramIds = null, string title = "Process documentation", bool allowImageResampling = false) =>
        Guard(() => native.Publish(path, format, diagramIds, title, allowImageResampling));

    [McpServerTool(Name = "native_compare", ReadOnly = true, Destructive = false, OpenWorld = false), Description("Compare entire unencrypted native containers without converting them to BPMN. Includes nested diagram XML and binary attachments; reports unknown changes instead of silently discarding them.")]
    public CallToolResult CompareNative(string path, string otherPath, NativeNameChange[]? expectedNames = null) => Guard(() =>
        native.Compare(path, otherPath, expectedNames));

    [McpServerTool(Name = "operation_get"), Description("Read durable operation status, phases, artifacts and real failures.")]
    public CallToolResult GetOperation(string operationId) => Guard(() => operations.Get(operationId));

    [McpServerTool(Name = "operation_cancel"), Description("Cancel an owned operation and isolated worker. Never closes the operator's Modeler session.")]
    public CallToolResult CancelOperation(string operationId) => Guard(() => operations.Cancel(operationId));
}
