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
            nativeEventPayloadKinds = NativeEventPayloadPolicy.Kinds,
            nativeArtifactTextKinds = new[] { "TextAnnotation", "FormattedTextArtifact" },
            nativeXpdl = new { version = "2.2", tools = new[] { "native_xpdl_import", "native_xpdl_export" }, maximumDocuments = 100,
                scope = "explicit_interchange_projection_with_differences_not_native_backup_or_visual_equivalence" },
            nativeVisio = new { format = "vdx", tools = new[] { "native_visio_import", "native_visio_export" }, maximumPages = 100,
                scope = "lossy_installed_mapper_with_separate_subprocess_body_pages_and_source_page_receipts_not_hierarchy_behavior_or_visual_equivalence" },
            nativeRefactoring = new { tool = "native_subprocess_extract", operation = "embedded_to_reusable", sourceKind = "ordinary_SubProcess",
                scope = "native_command_descendant_relocation_current_user_tab_remap_and_whole_container_gate_not_behavioral_equivalence_or_arbitrary_selection_refactoring" },
            nativeReparenting = new { tool = "native_elements_reparent", maximumBatchSize = 1000,
                crossDiagram = new { requires = new[] { "ExpectedDiagramId", "TargetDiagramId" }, migrates = new[] { "native_subtree", "extended_values", "embedded_files", "images", "current_user_subprocess_tabs" },
                    configuredSimulationMigration = true, presentationActionMigration = false },
                scope = "explicit_process_or_embedded_subprocess_ownership_complete_reference_closure_and_optional_node_position_with_archive_and_fresh_reader_gates_not_automatic_layout_or_live_documents" },
            nativePresentation = new { tools = new[] { "native_presentation_get", "native_presentation_apply" },
                types = NativePresentationPolicy.Types, valueTypes = NativePresentationPolicy.ValueTypes, maximumBatchSize = 1000,
                scope = "experimental_native_action_definitions_and_owned_payloads_with_fresh_readback_not_playback_cross_diagram_migration_or_live_documents" },
            nativeAlignment = new { tool = "native_elements_align", modes = NativeAlignmentPolicy.Modes, maximumSelection = 1000,
                scope = "experimental_native_editor_selected_shape_alignment_with_revision_callback_and_archive_gates_not_global_layout_or_visual_accreditation" },
            nativeSelectionCopy = new { tool = "native_elements_copy", maximumSelection = 1000,
                scope = "experimental_closed_native_selection_explicit_parent_and_position_complete_identity_payload_archive_and_restart_gates_not_OS_clipboard_or_live_unsaved_documents" },
            nativeConversions = new { tool = "native_elements_convert", taskTypes = NativeConversionPolicy.TaskTypes, gatewayTypes = NativeConversionPolicy.GatewayTypes,
                taskToUnboundCall = new { targetType = "CallActivity", createsDiagram = false, selectsTarget = false, bindingTool = "native_mutate", bindingField = "CallTarget.ProcessId", reverseSupported = true, reverseRequiresUnbound = true, reverseInlinesProcess = false },
                events = new { requiredField = "ExpectedEventMode", preservesRole = true, neutralDefinitionsOnly = true,
                    typesByMode = NativeEventConversionPolicy.Modes.ToDictionary(mode => mode, NativeEventConversionPolicy.TypesFor), changesContext = false },
                maximumBatchSize = 1000, scope = "same_category_or_task_unbound_call_bidirectional_explicit_expected_type_and_revision_no_silent_content_retirement" },
            nativeStyles = new { patch = "NativeMutation.Style", fontInventoryTool = "native_fonts_get", fontSize = "whole_native_units_1_to_512_not_css_pixels",
                alignments = NativeStylePolicy.Alignments, directions = NativeStylePolicy.Directions,
                labels = "whole_nonnegative_native_bounds_or_four_zeroes_to_clear_not_universal_rendered_geometry",
                pools = "font_and_fill_border_colors_only", connectors = "no_background_fill_or_border_visibility" },
            nativeCustomArtifacts = new { ownership = "model_not_global_palette", operations = new[] { "create", "update", "delete", "native_bca_import", "native_bca_export" },
                references = "ArtifactProperties.CustomArtifactTypeId", pixelConversion = "explicit_native_rasterization_acknowledgement_with_stable_serialization_and_restart_verification" },
            nativeImageInput = new { source = "confined_path_or_completed_native_image_export", revision = "sha256", frames = "explicit_when_multiple", sourceByteBound = NativeImagePolicy.MaxSourceBytes, decodedPixelBound = 64L * 1024 * 1024,
                encoding = "explicitly_acknowledged_selected_frame_8bit_rgba_png_without_source_container_metadata_profiles_or_other_frames" },
            nativeDataKinds = new[] { "DataObject", "DataStore", "DataStoreReference" },
            nativeDataBindingOwners = new[] {
                new { owner = "Activity", directions = new[] { "Input", "Output" } },
                new { owner = "Start/Catch/Boundary", directions = new[] { "Output" } },
                new { owner = "End/Throw", directions = new[] { "Input" } } },
            intermediateCreationModes = new[] { "Catch", "Throw", "Boundary" },
            capabilities = new[] {
                new { name = "bpmn_xml_inspect_create_rename_validate", status = "implemented", backend = "standards_xml" },
                new { name = "bpm_native_import_save_reopen_export", status = "experimental_diagnostic", backend = "bizagi_worker" },
                new { name = "native_xpdl_22_exchange", status = "experimental_installed_unicode_file_route_with_explicit_losses_and_fresh_native_readback_not_native_backup", backend = "bizagi_worker" },
                new { name = "native_visio_vdx_exchange", status = "experimental_lossy_installed_mapper_with_strict_native_noop_readback_not_complete_visio_or_nested_content_support", backend = "bizagi_worker" },
                new { name = "bpm_native_graph_inspect_and_copy_only_name_edits", status = "experimental_diagnostic", backend = "bizagi_worker" },
                new { name = "native_diagram_lifecycle_and_persisted_tabs", status = "experimental_copy_only_native_cloning_and_ordered_preferences_with_fresh_readback_not_live_session_control", backend = "bizagi_worker" },
                new { name = "native_blank_model_creation", status = "experimental_native_constructor_fresh_readback_and_noop_stability_gate_not_live_desktop", backend = "bizagi_worker" },
                new { name = "native_workspace_commit_and_reconciliation", status = "experimental_byte_exact_adoption_durable_intent_backup_and_fresh_native_readback_no_blind_replay", backend = "bizagi_worker" },
                new { name = "native_structural_geometry_documentation_batches", status = "experimental_palette_connections_and_explicit_pool_lane_milestone_subprocess_lifecycle_not_full_editor", backend = "bizagi_worker" },
                new { name = "native_task_gateway_type_conversion", status = "experimental_installed_command_three_process_readback_and_whole_archive_gate_including_guarded_same_role_events_not_live_sessions", backend = "bizagi_worker" },
                new { name = "native_embedded_subprocess_extraction", status = "experimental_installed_command_root_and_nested_extraction_with_rich_content_and_persisted_tab_readback_not_general_reparenting_or_scenario_migration", backend = "bizagi_worker" },
                new { name = "native_explicit_same_diagram_reparenting", status = "experimental_copy_only_native_collections_three_process_readback_and_whole_archive_gate_not_cross_diagram_migration_layout_or_live_sessions", backend = "bizagi_worker" },
                new { name = "native_explicit_cross_diagram_reparenting", status = "experimental_explicit_subtree_content_tabs_and_mapped_scenario_parameters_three_process_readback_and_whole_archive_gate_not_presentation_actions_layout_or_live_sessions", backend = "bizagi_worker" },
                new { name = "native_event_boundary_lifecycle", status = "experimental_explicit_modes_interruption_activity_references_and_clone_remapping_not_simulation_accreditation", backend = "bizagi_worker" },
                new { name = "native_event_definition_payloads", status = "experimental_unique_existing_kind_text_timer_and_compensation_patches_not_definition_collection_editing_or_execution_validation", backend = "bizagi_worker" },
                new { name = "native_data_and_activity_io", status = "experimental_native_data_properties_store_references_and_derived_activity_and_event_bindings_with_fresh_readback_not_full_io_editor_or_visual_accreditation", backend = "bizagi_worker" },
                new { name = "native_content_artifacts", status = "experimental_native_annotation_formatted_text_group_and_header_lifecycle_with_fresh_readback_not_all_artifacts_or_visual_accreditation", backend = "bizagi_worker" },
                new { name = "native_image_artifacts", status = "experimental_revision_checked_raster_import_exact_pixel_and_file_readback_clone_export_and_deletion_not_vector_or_color_managed_editor", backend = "bizagi_worker" },
                new { name = "native_custom_artifacts", status = "experimental_model_owned_definitions_instances_native_bca_exchange_explicit_pixel_conversion_and_restart_fidelity_not_global_palette_or_desktop_gui_accreditation", backend = "bizagi_worker" },
                new { name = "native_container_fidelity_and_noop_save", status = "experimental_verified_on_tested_inputs", backend = "bizagi_worker" },
                new { name = "native_model_validation", status = "experimental_diagnostic", backend = "bizagi_worker" },
                new { name = "native_metadata_resources_activity_raci", status = "experimental_copy_only_verified_on_tested_inputs", backend = "bizagi_worker" },
                new { name = "native_extended_attributes_and_embedded_files", status = "experimental_native_xml_and_byte_transactions_with_fresh_readback_not_full_editor_or_visual_accreditation", backend = "bizagi_worker" },
                new { name = "native_simulation_configuration", status = "experimental_full_diagram_bpsim_replacement_with_fidelity_gate", backend = "bizagi_worker" },
                new { name = "native_simulation_and_what_if", status = "experimental_levels_one_to_four_and_replications_verified_on_tested_inputs", backend = "bizagi_worker" },
                new { name = "native_saved_simulation_results", status = "experimental_explicit_single_scenario_native_copy_persistence_and_historical_readback_not_what_if_result_history_or_gui_equivalence", backend = "bizagi_worker" },
                new { name = "native_offscreen_svg_png", status = "experimental_basic_diagram_locally_verified_rich_visual_fidelity_pending", backend = "bizagi_worker" },
                new { name = "native_documentation", status = "experimental_excel_word_pdf_verified_on_tested_inputs_not_all_publication_formats", backend = "bizagi_worker" },
                new { name = "native_web_publication", status = "experimental_selected_nested_pages_search_attachment_native_readback_verified_browser_quality_partial", backend = "bizagi_worker" }
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

    [McpServerTool(Name = "native_elements_align"), Description("Experimental native selected-shape alignment or distribution using the installed editor's programmatic engine, not simulated input. Supply native diagram, optional embedded subprocess, 2-1000 distinct element IDs and one of Top, Bottom, Left, Right, Horizontal, Vertical, HorizontalEvenly, VerticalEvenly. Distribution needs at least three nodes. Revision checked; original retained. Three workers and whole-archive fidelity gate the output. Cross-owner/semantic changes and unrepresentable results are rejected. Poll operation_get; availability is not universal operational accreditation.")]
    public CallToolResult AlignNative(string path, string expectedRevision, NativeAlignmentRequest alignment) => Guard(() => native.Align(path, expectedRevision, alignment));

    [McpServerTool(Name = "native_elements_copy"), Description("Experimental native selection copy without OS clipboard or simulated input. Supply source diagram, target process or embedded subprocess, 1-1000 distinct native root IDs with a closed reference graph, and explicit destination position. Source revision checked and original retained. Three workers verify native persistence, fresh readback and complete archive fidelity. Unsupported or lossy selections fail; poll operation_get. Availability is not universal operational accreditation.")]
    public CallToolResult CopyNativeSelection(string path, string expectedRevision, NativeSelectionCopyRequest selection) => Guard(() => native.CopySelection(path, expectedRevision, selection));

    [McpServerTool(Name = "native_subprocess_extract"), Description("Experimental native embedded-to-reusable subprocess extraction on a private copy. Explicit source ID, target diagram name and revision; original retained. Fresh-reader and whole-container gates quarantine unaccredited changes. Poll operation_get; tool availability is not operational accreditation.")]
    public CallToolResult ExtractSubProcess(string path, string expectedRevision, NativeSubProcessExtraction extraction) =>
        Guard(() => native.ExtractSubProcess(path, expectedRevision, extraction));

    [McpServerTool(Name = "native_elements_reparent"), Description("Experimental explicit native reparenting between processes and embedded subprocesses. Cross-diagram moves require ExpectedDiagramId and TargetDiagramId on every crossing root; migrate native values, embedded/image files and current-user tabs. Configured parameters require simulationMigration with complete source/target scenario mappings and matching units/global context/inheritance. CopyMissingDependencies explicitly permits absent resource/calendar copies; conflicts reject. Saved-result retirement requires separate explicit consent; unknown container content rejects. Presentation actions reject. Preserve identities, original file and connector/boundary closure. Optional Position changes only the selected node. Three workers verify persistence and archive content. Poll operation_get; not automatic layout or live editing.")]
    public CallToolResult ReparentNative(string path, string expectedRevision, NativeReparenting[] moves, NativeSimulationMigration? simulationMigration = null) =>
        Guard(() => native.Reparent(path, expectedRevision, moves, simulationMigration));

    [McpServerTool(Name = "native_visio_import"), Description("Experimental installed Visio VDX import to a new native model. Requires a revision-checked .vdx input and acknowledgeFormatLimits=true. Return page inventory, import normalizations, strict native no-op/restart evidence and a .bpm artifact. Unmapped-page conflicts fail explicitly; no lossless or desktop visual claim. Poll operation_get.")]
    public CallToolResult ImportVisio(NativeExchangeInput input, bool acknowledgeFormatLimits, string modelName = "Imported Visio") =>
        Guard(() => native.ImportVisio(input, modelName, acknowledgeFormatLimits));

    [McpServerTool(Name = "native_visio_export"), Description("Experimental selected native diagrams to Visio .vdx through the installed manager, with import/save/fresh-readback verification. Requires expectedRevision and acknowledgeFormatLimits=true. Returns reusable VDX, source-to-page receipts, separate populated subprocess body pages, explicit graph/metadata/archive losses and native verification model. At most 100 root plus body pages; negative subprocess coordinates fail. Imported pages are separate diagrams, not reconstructed hierarchy. Original untouched; not VSDX or a native backup. Poll operation_get.")]
    public CallToolResult ExportVisio(string path, string expectedRevision, string[] diagramIds, bool acknowledgeFormatLimits) =>
        Guard(() => native.ExportVisio(path, expectedRevision, diagramIds, acknowledgeFormatLimits));

    [McpServerTool(Name = "native_xpdl_import"), Description("Import 1-100 revision-checked XPDL 2.2 files or completed export artifacts through the installed importer, save a new .bpm, reopen and re-export with explicit XML differences. Requires acknowledgeFormatLimits=true; never overwrites sources or promises lossless interchange. Poll operation_get.")]
    public CallToolResult ImportXpdl(NativeExchangeInput[] inputs, bool acknowledgeFormatLimits, string modelName = "Imported XPDL") =>
        Guard(() => native.ImportXpdl(inputs, modelName, acknowledgeFormatLimits));

    [McpServerTool(Name = "native_xpdl_export"), Description("Export selected native diagrams as Unicode XPDL 2.2 using installed serializers, then import/save/reopen in fresh workers. Requires source revision and acknowledgeFormatLimits=true. Returns explicit graph/archive differences and reusable XPDL artifacts; original .bpm is untouched. Not a native backup. Poll operation_get.")]
    public CallToolResult ExportXpdl(string path, string expectedRevision, string[] diagramIds, bool acknowledgeFormatLimits) =>
        Guard(() => native.ExportXpdl(path, expectedRevision, diagramIds, acknowledgeFormatLimits));

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

    [McpServerTool(Name = "native_presentation_get"), Description("Read native presentation action definitions and payload hashes without activating links or files. Poll operation_get.")]
    public CallToolResult GetNativePresentation(string path) => Guard(() => native.ReadPresentation(path));

    [McpServerTool(Name = "native_presentation_apply"), Description("Upsert/delete native presentation actions in a revision-checked copy. Explicit owner IDs, native description/attribute references, or normal content; File/Image require action-file:name and DataBase64. Verifies full native archive and fresh-process readback. Does not activate content or edit live documents.")]
    public CallToolResult ApplyNativePresentation(string path, string expectedRevision, NativePresentationActionChange[] changes) => Guard(() => native.ApplyPresentation(path, expectedRevision, changes));
    [McpServerTool(Name = "native_metadata_get"), Description("Read native resources, activity RACI assignments and complete diagram BPSim 1.0 configurations through the installed engine. Returns native IDs, BPMN references and source revision. Poll operation_get.")]
    public CallToolResult GetNativeMetadata(string path) => Guard(() => native.ReadMetadata(path));

    [McpServerTool(Name = "native_attributes_get"), Description("Read native extended attribute definitions, element values and embedded file hashes. Embedded references use attachment:file-name; linked files are never opened. Poll operation_get.")]
    public CallToolResult GetNativeAttributes(string path) => Guard(() => native.ReadDocumentation(path));

    [McpServerTool(Name = "native_attributes_apply"), Description("Explicit native extended-attribute definition XML, complete per-element value XML and embedded byte transactions. Uses a copy, expected revision, fresh-worker readback and whole-archive fidelity gate. Unknown fields and unrequested losses fail. Poll operation_get.")]
    public CallToolResult ApplyNativeAttributes(string path, string expectedRevision, NativeDocumentationPatch patch) => Guard(() => native.ApplyDocumentation(path, expectedRevision, patch));

    [McpServerTool(Name = "native_attachment_export"), Description("Export an embedded file or image from a native model to a private operation artifact. Verify native-loaded bytes against the original archive and durable output hash. Does not fetch linked files or open the exported file.")]
    public CallToolResult ExportNativeAttachment(string path, string diagramId, string elementId, string fileName) => Guard(() => native.ExportAttachment(path, diagramId, elementId, fileName));

    [McpServerTool(Name = "native_image_export"), Description("Export one actual ImageArtifact payload without re-encoding. Verify exact bytes against the native archive. Returns original file name, decoded pixel fingerprint, SHA-256 and a typed reusable image artifact reference; this is not an extended-attribute attachment.")]
    public CallToolResult ExportNativeImage(string path, string diagramId, string elementId) => Guard(() => native.ExportImage(path, diagramId, elementId));

    [McpServerTool(Name = "native_fonts_get"), Description("Observe installed Windows/GDI+ font families and native face availability through the isolated worker. Does not install fonts, modify settings or establish glyph-level rendering compatibility. Use the returned family names in native_mutate Style.FontName. Poll operation_get.")]
    public CallToolResult GetNativeFonts() => Guard(() => native.Fonts());

    [McpServerTool(Name = "native_elements_convert"), Description("Convert task/gateway types within one category, task/unbound-CallActivity types, or event kinds within the same explicit role, using installed native commands. Each change requires ElementId, ExpectedType and TargetType plus the source revision. Events additionally require ExpectedEventMode (Start, End, Catch, Throw or Boundary); role, interruption, attachment and common data remain unchanged. Only neutral event definitions may be replaced; configured/unknown payloads and referenced message identities are protected. Special event contexts remain required. Task-to-call creates no diagram and selects no target: bind explicitly through native_mutate CallTarget.ProcessId. Call-to-task requires explicit unlinking first and rejects nondefault expanded layout; it never inlines or deletes a called process. Preserves identities, common properties, relations, styling and unknown archive content; rejects nondefault type-specific content that would be lost and attributes not applicable to the target type. Writes a new artifact and verifies it in an independent reader. Not event-role conversion, embedded subprocess extraction or live-session editing. Poll operation_get.")]
    public CallToolResult ConvertNativeElements(string path, string expectedRevision, NativeTypeConversion[] changes) => Guard(() => native.ConvertElements(path, expectedRevision, changes));

    [McpServerTool(Name = "native_custom_artifacts_apply"), Description("Create, update or delete model-owned custom artifact definitions in a revision-checked native copy. Inspect CustomArtifacts first. Changes require explicit Operation and Id; creation requires Name and Image. Image uses the same revision-checked raster/frame input as native images. AllowNativeRasterization explicitly permits the installed custom-type serializer's pixel conversion; unstable repeated serialization fails. Referenced definitions cannot be deleted. Never modifies the global user palette. Poll operation_get.")]
    public CallToolResult ApplyNativeCustomArtifacts(string path, string expectedRevision, NativeCustomArtifactPatch patch) => Guard(() => native.ApplyCustomArtifacts(path, expectedRevision, patch));

    [McpServerTool(Name = "native_custom_artifacts_export"), Description("Export selected model-owned custom definitions through the installed .bca exporter, re-import in another native worker, persist and independently reopen. Returns a verified reusable .bca artifact, not a hand-built archive.")]
    public CallToolResult ExportNativeCustomArtifacts(string path, string[] definitionIds) => Guard(() => native.ExportCustomArtifacts(path, definitionIds));

    [McpServerTool(Name = "native_custom_artifacts_import"), Description("Import a revision-checked confined .bca file or completed native_custom_artifacts_export reference into a native model copy. Uses strict archive preflight and the installed importer. Existing identities require replaceExisting; pixel conversion requires allowNativeRasterization. Unknown fields, conflicting image representations and unexplained archive changes fail. Never modifies the global palette.")]
    public CallToolResult ImportNativeCustomArtifacts(string path, string expectedRevision, string archivePath, string archiveRevision, bool replaceExisting = false, bool allowNativeRasterization = false) => Guard(() => native.ImportCustomArtifacts(path, expectedRevision, archivePath, archiveRevision, replaceExisting, allowNativeRasterization));

    [McpServerTool(Name = "native_metadata_apply"), Description("Edit local resources, replace complete activity RACI sets and explicitly replace complete per-diagram BPSim configurations in a native copy. Requires revision, validates native readback and all non-targeted archive content. Never silently discards saved simulation results.")]
    public CallToolResult ApplyNativeMetadata(string path, string expectedRevision, NativeMetadataPatch patch) => Guard(() => native.ApplyMetadata(path, expectedRevision, patch));

    [McpServerTool(Name = "native_simulate_what_if"), Description("Run installed native what-if analysis for explicit scenario IDs at level 1-4. Preserve every real replication result and identity as separate artifacts. Reusable subprocesses are native black boxes; inspect SimulationLimitations, not assumed linked-task execution. No changes to the source file. Poll operation_get.")]
    public CallToolResult SimulateNativeWhatIf(string path, string diagramId, string[] scenarioIds, int simulationLevel = 1) => Guard(() => native.RunWhatIf(path, diagramId, scenarioIds, simulationLevel));

    [McpServerTool(Name = "native_mutate"), Description("Apply explicit create/update/delete/reconnect mutations to a native copy. ArtifactProperties.Image accepts a confined SourcePath (or completed native_image_export reference), exact ExpectedRevision, explicit AllowPngReencoding and optional native FrameDimension/FrameIndex. ImageArtifact creation requires that input; multi-frame sources require an explicit frame. Conversion is selected-frame 8-bit RGBA PNG without source metadata, profiles or other frames; receipts verify decoded pixels and native payload hashes. ArtifactProperties.Text patches native TextAnnotation or FormattedTextArtifact content; omission preserves and empty clears. These artifacts and HeaderArtifact do not persist Name. Groups require a diagram parent and Geometry.Expanded=true without ExpandedSize; group Documentation is not supported by the native loader. Headers require a root process; text content is derived from diagram metadata. DataProperties patches object State/IsCollection, catalog State/Capacity/IsUnlimited or reference StoreId. Store creation uses a diagram parent without geometry; reference creation requires an existing same-diagram StoreId. Associations require explicit points and same-container endpoints; activity/event I/O is reconciled through native utilities, including sequence-flow endpoint changes. Catch/start/boundary events produce outputs; throw/end events consume inputs. Geometry must retain the native pool/container. Clear shared store state before removing its final reference. SubProcessKind selects SubProcess/Transaction/AdHoc at creation; SubProcessProperties patches TriggeredByEvent and ad hoc ordering/completion text. EventMode explicitly selects Catch/Throw/Boundary on intermediate creation; EventProperties patches interruption and same-container AttachedToActivityId. EventPayloads patches a unique existing definition by Kind: names, conditional text, canonical Cycle/Date/None timers, error/escalation codes and same-container compensation ActivityId/WaitForCompletion. Empty text explicitly clears; omission preserves. This does not edit definition collections or validate event execution. Delete, clear or redirect boundary/compensation references before deleting their activity. ActivityLoop explicitly replaces None/Standard/MultiInstance configuration. ActivityProperties edits token quantities, compensation and native model state; GatewayDirection and complete FlowCondition (None/Expression/Default) require compatible kinds. Participant creation requires a new ProcessId; lanes/milestones use that process as ParentId with complete partition geometry. Embedded expanded subprocesses require separate ExpandedSize. CallActivity accepts CallTarget.ProcessId (existing local process, not diagram ID); empty unlinks, omission preserves. Requires revision, fresh-worker readback and whole-archive fidelity. Handle children, incident flows and incoming calls explicitly before deletion; never overwrites the input.")]
    public CallToolResult MutateNative(string path, string expectedRevision, NativeMutation[] mutations) => Guard(() => native.Mutate(path, expectedRevision, mutations));

    [McpServerTool(Name = "native_apply_changes"), Description("Native name-change batch using native IDs and expected source revision. Writes ONLY a new artifact, verifies edits in a fresh worker, and rejects unexplained whole-container differences. Broad rich-model coverage remains experimental. Poll operation_get.")]
    public CallToolResult ApplyNative(string path, string expectedRevision, NativeNameChange[] changes) => Guard(() => native.ApplyNames(path, expectedRevision, changes));

    [McpServerTool(Name = "native_save_copy"), Description("Load and save a native model without requested changes, reopen in a fresh worker and compare the entire archive. Reject unexplained differences. The source is never overwritten.")]
    public CallToolResult SaveNativeCopy(string path, string expectedRevision) => Guard(() => native.ApplyNames(path, expectedRevision, [], saveCopy: true));

    [McpServerTool(Name = "native_validate"), Description("Run the installed native BPMN validator on a private model copy. Poll operation_get for vendor severities and native element IDs. Does not fix or overwrite the source.")]
    public CallToolResult ValidateNative(string path) => Guard(() => native.Analyze(path, "validate"));

    [McpServerTool(Name = "native_simulate"), Description("Experimental installed-engine simulation of one native diagram. Supply native diagram/scenario IDs from native_inspect. Empty scenario ID uses native defaults. Reusable subprocesses are black boxes; nondefault activity token execution is unaccredited. Inspect SimulationLimitations and SimulationInputs separately from actual result metrics. Level 1-4; no source edits. Optional saveResultsAsNativeCopy requires an explicit existing scenario and persists only this actual result into a new .bpm, preserving other results and all configuration, with fresh-worker and archive verification. Poll operation_get.")]
    public CallToolResult SimulateNative(string path, string diagramId, string scenarioId = "", int simulationLevel = 1, bool saveResultsAsNativeCopy = false) =>
        Guard(() => native.Analyze(path, "simulate", diagramId, scenarioId, simulationLevel, saveResultsAsNativeCopy: saveResultsAsNativeCopy));

    [McpServerTool(Name = "native_simulation_results_get"), Description("Read one explicit existing scenario's saved results through the installed native loader, without running a simulation or altering the source. Returns native property digests, structured metrics and a UTF-8 XML export. Missing saved results fail explicitly. Poll operation_get. Historical results are not proof that current settings would reproduce them.")]
    public CallToolResult GetNativeSimulationResults(string path, string diagramId, string scenarioId) =>
        Guard(() => native.Analyze(path, "saved_results_read", diagramId, scenarioId));

    [McpServerTool(Name = "native_render_svg"), Description("Experimental native offscreen SVG rendering of one diagram, using the installed renderer without clicks or foreground control. Produces a private operation artifact; failures remain explicit.")]
    public CallToolResult RenderNative(string path, string diagramId, string subProcessId = "") => Guard(() => native.Analyze(path, "render_svg", diagramId, subProcessId: subProcessId));

    [McpServerTool(Name = "native_publish"), Description("Publish local native model documentation to excel, word, pdf or an experimental web directory using installed generators, without opening a desktop application. Select native diagram IDs or omit for all. Fresh-worker text/image readback; source unchanged. Poll operation_get.")]
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
