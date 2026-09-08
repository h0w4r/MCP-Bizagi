namespace McpBizagi.Contracts;

/// <summary>Versioned, explicitly allowlisted operations; never arbitrary reflection.</summary>
public sealed class EngineRequest
{
    public int ProtocolVersion { get; set; } = 1;
    public string OperationId { get; set; } = "";
    public string Action { get; set; } = "probe";
    public string InputPath { get; set; } = "";
    public string[] InputPaths { get; set; } = System.Array.Empty<string>();
    public string OutputPath { get; set; } = "";
    public string ModelName { get; set; } = "Model";
    public NativeNameChange[] Changes { get; set; } = System.Array.Empty<NativeNameChange>();
    public NativeMutation[] Mutations { get; set; } = System.Array.Empty<NativeMutation>();
    public NativeTypeConversion[] Conversions { get; set; } = System.Array.Empty<NativeTypeConversion>();
    public NativeSubProcessExtraction? Extraction { get; set; }
    public NativeReparenting[] Reparentings { get; set; } = System.Array.Empty<NativeReparenting>();
    public string DiagramId { get; set; } = "";
    public string SubProcessId { get; set; } = "";
    public string ImageElementId { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public int SimulationLevel { get; set; } = 1;
    public string PublicationFormat { get; set; } = "";
    public string PublicationTitle { get; set; } = "Process documentation";
    public string[] SelectedDiagramIds { get; set; } = System.Array.Empty<string>();
    public int AtomicStepSeconds { get; set; } = 30;
    public int InactivitySeconds { get; set; } = 120;
    public NativeMetadataPatch? MetadataPatch { get; set; }
    public NativeDocumentationPatch? DocumentationPatch { get; set; }
    public NativeAttachmentInfo? Attachment { get; set; }
    public NativeDiagramPatch? DiagramPatch { get; set; }
    public NativeCustomArtifactPatch? CustomArtifactPatch { get; set; }
    public string CustomArtifactArchivePath { get; set; } = "";
    public bool ReplaceCustomArtifacts { get; set; }
    public bool AllowCustomArtifactRasterization { get; set; }
    public string[] CustomArtifactIds { get; set; } = System.Array.Empty<string>();
    public string[] ScenarioIds { get; set; } = System.Array.Empty<string>();
}

/// <summary>Explicit semantic mutations. Unused fields are rejected rather than treated as reflection paths.</summary>
public sealed class NativeMutation
{
    public string Operation { get; set; } = "";
    public string ElementId { get; set; } = "";
    public string ParentId { get; set; } = "";
    public string ElementType { get; set; } = "";
    /// <summary>Required only when creating a participant; identifies its durable native process.</summary>
    public string ProcessId { get; set; } = "";
    public string? Name { get; set; }
    public string? Documentation { get; set; }
    public NativeGeometry? Geometry { get; set; }
    /// <summary>Explicit latent/visible expanded size for an embedded subprocess; collapsed bounds remain separate.</summary>
    public NativeSize? ExpandedSize { get; set; }
    /// <summary>Null preserves the call target; an empty ProcessId explicitly unlinks a call activity.</summary>
    public NativeCallTarget? CallTarget { get; set; }
    public NativeActivityProperties? ActivityProperties { get; set; }
    public NativeActivityLoop? ActivityLoop { get; set; }
    public NativeFlowCondition? FlowCondition { get; set; }
    public string? GatewayDirection { get; set; }
    /// <summary>Intermediate creation mode; cannot convert an existing native event implicitly.</summary>
    public string? EventMode { get; set; }
    public NativeEventProperties? EventProperties { get; set; }
    public NativeEventPayloadPatch[]? EventPayloads { get; set; }
    public NativeDataProperties? DataProperties { get; set; }
    public NativeArtifactProperties? ArtifactProperties { get; set; }
    public NativeStylePatch? Style { get; set; }
    /// <summary>Creation-only native embedded subclass: SubProcess, Transaction or AdHoc.</summary>
    public string? SubProcessKind { get; set; }
    public NativeSubProcessProperties? SubProcessProperties { get; set; }
    public string SourceId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public NativePoint[] Points { get; set; } = System.Array.Empty<NativePoint>();
}

public sealed class NativeCallTarget
{
    /// <summary>A native participant Process.Id, not a diagram ID. Empty explicitly clears the link.</summary>
    public string ProcessId { get; set; } = "";
    /// <summary>Replacing an existing external-model reference requires this explicit acknowledgement.</summary>
    public bool ReplaceExternalReference { get; set; }
}

public sealed class NativeCallReference
{
    public string CatalogProcessId { get; set; } = "";
    public string BpmnName { get; set; } = "";
    public string BpmnNamespace { get; set; } = "";
    public NativeExternalCallReference? External { get; set; }
}

public sealed class NativeExternalCallReference
{
    public string WorkspaceId { get; set; } = "";
    public string DiagramId { get; set; } = "";
    public string ProcessId { get; set; } = "";
}

public sealed class NativePoint
{
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class NativeSize
{
    public double Width { get; set; }
    public double Height { get; set; }
}

public sealed class NativeNameChange
{
    public string ElementId { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class NativeElement
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    /// <summary>Native hidden main-participant boundary flag; null for non-participant elements. Its child elements remain visible.</summary>
    public bool? IsMainParticipant { get; set; }
    public string ElementType { get; set; } = "";
    public string Name { get; set; } = "";
    public string BpmnId { get; set; } = "";
    /// <summary>Raw native call-reference representations; null for non-call elements. No external model is fetched.</summary>
    public NativeCallReference? CallReference { get; set; }
    public NativeActivityProperties? ActivityProperties { get; set; }
    public NativeActivityLoop? ActivityLoop { get; set; }
    public NativeFlowCondition? FlowCondition { get; set; }
    public string? GatewayDirection { get; set; }
    public NativeSubProcessInfo? SubProcess { get; set; }
    public NativeEventInfo? Event { get; set; }
    public NativeEventGatewayInfo? EventGateway { get; set; }
    /// <summary>Derived from actual outgoing native sequence-flow conditions, not a guessed catalog alias.</summary>
    public string[] DefaultSequenceFlowIds { get; set; } = System.Array.Empty<string>();
    public string ParentId { get; set; } = "";
    public string DiagramId { get; set; } = "";
    public string Documentation { get; set; } = "";
    public NativeDataInfo? Data { get; set; }
    public NativeArtifactInfo? Artifact { get; set; }
    public NativeStyleInfo? Style { get; set; }
    public NativeDataFlowInfo? DataFlow { get; set; }
    public NativeGeometry? Geometry { get; set; }
    /// <summary>Native expanded bounds are distinct from the collapsed shape's size.</summary>
    public NativeGeometry? ExpandedGeometry { get; set; }
    public string SourceRef { get; set; } = "";
    public string TargetRef { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public NativePoint[] Points { get; set; } = System.Array.Empty<NativePoint>();
}

public sealed class NativeGeometry
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Expanded { get; set; }
    public int? BackgroundArgb { get; set; }
    public int? BorderArgb { get; set; }
}

/// <summary>A native-engine result is distinct from an independently verified capability.</summary>
public sealed class EngineReply
{
    public bool Success { get; set; }
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public string EngineVersion { get; set; } = "";
    public string OperationId { get; set; } = "";
    public string[] Artifacts { get; set; } = System.Array.Empty<string>();
    public NativeExchangeArtifact[] ExchangeFiles { get; set; } = System.Array.Empty<NativeExchangeArtifact>();
    public NativeExtractionReceipt? Extraction { get; set; }
    public string[] Diagrams { get; set; } = System.Array.Empty<string>();
    public NativeElement[] Elements { get; set; } = System.Array.Empty<NativeElement>();
    public NativeValidationMessage[] Validation { get; set; } = System.Array.Empty<NativeValidationMessage>();
    public NativeScenario[] Scenarios { get; set; } = System.Array.Empty<NativeScenario>();
    public NativePublicationReadback? Publication { get; set; }
    public string[] IntegrationAdjustments { get; set; } = System.Array.Empty<string>();
    public NativeMetadataSnapshot? Metadata { get; set; }
    public NativeDocumentationSnapshot? Documentation { get; set; }
    public NativeDiagramSnapshot? DiagramState { get; set; }
    public NativeDiagramClone[] DiagramClones { get; set; } = System.Array.Empty<NativeDiagramClone>();
    public NativeImageFile[] ImageFiles { get; set; } = System.Array.Empty<NativeImageFile>();
    public NativeImageImportReceipt[] ImageImports { get; set; } = System.Array.Empty<NativeImageImportReceipt>();
    public NativeRenderedImage[] RenderedImages { get; set; } = System.Array.Empty<NativeRenderedImage>();
    public NativeCustomArtifactDefinition[] CustomArtifacts { get; set; } = System.Array.Empty<NativeCustomArtifactDefinition>();
    public NativeCustomArtifactReceipt[] CustomArtifactImports { get; set; } = System.Array.Empty<NativeCustomArtifactReceipt>();
    public NativeFontFamily[] Fonts { get; set; } = System.Array.Empty<NativeFontFamily>();
    public NativeSimulationReport[] SimulationReports { get; set; } = System.Array.Empty<NativeSimulationReport>();
    public NativeSimulationLimitation[] SimulationLimitations { get; set; } = System.Array.Empty<NativeSimulationLimitation>();
    public NativeSimulationInputReadback[] SimulationInputs { get; set; } = System.Array.Empty<NativeSimulationInputReadback>();
}

/// <summary>Input-specific native simulation semantics, not a claim that all limitations were enumerated.</summary>
public sealed class NativeSimulationLimitation
{
    public string Code { get; set; } = "";
    public string DiagramId { get; set; } = "";
    public string ElementId { get; set; } = "";
    public string BpmnId { get; set; } = "";
    public string Message { get; set; } = "";
    public string DocumentationUrl { get; set; } = "";
    public NativeCallReference? CallReference { get; set; }
    public NativeActivityProperties? ActivityProperties { get; set; }
    public NativeActivityLoop? ActivityLoop { get; set; }
    public NativeSubProcessInfo? SubProcess { get; set; }
}

/// <summary>Native activity quantities checked against the actual XML consumed by the installed simulator.</summary>
public sealed class NativeSimulationInputReadback
{
    public string Artifact { get; set; } = "";
    public NativeSimulationActivityInput[] Activities { get; set; } = System.Array.Empty<NativeSimulationActivityInput>();
}

public sealed class NativeSimulationActivityInput
{
    public string ElementId { get; set; } = "";
    public string BpmnId { get; set; } = "";
    public int StartQuantity { get; set; }
    public int CompletionQuantity { get; set; }
}

/// <summary>Text and image counts read from a durable publication by an independent worker.</summary>
public sealed class NativePublicationReadback
{
    public string Format { get; set; } = "";
    public string Text { get; set; } = "";
    public int PagesOrSheets { get; set; }
    public int Images { get; set; }
    public NativeImageSize[] ImageSizes { get; set; } = System.Array.Empty<NativeImageSize>();
}

public sealed class NativeImageSize
{
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>Vendor validation findings retain their own severity and element identities.</summary>
public sealed class NativeValidationMessage
{
    public string Severity { get; set; } = "";
    public string Description { get; set; } = "";
    public string DiagramId { get; set; } = "";
    public string[] ElementIds { get; set; } = System.Array.Empty<string>();
}

public sealed class NativeScenario
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string DiagramId { get; set; } = "";
    public int ConfiguredElements { get; set; }
}
