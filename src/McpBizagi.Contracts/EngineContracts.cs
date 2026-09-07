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
    public string DiagramId { get; set; } = "";
    public string SubProcessId { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public int SimulationLevel { get; set; } = 1;
    public string PublicationFormat { get; set; } = "";
    public string PublicationTitle { get; set; } = "Process documentation";
    public string[] SelectedDiagramIds { get; set; } = System.Array.Empty<string>();
    public int AtomicStepSeconds { get; set; } = 30;
    public int InactivitySeconds { get; set; } = 120;
}

/// <summary>Explicit semantic mutations. Unused fields are rejected rather than treated as reflection paths.</summary>
public sealed class NativeMutation
{
    public string Operation { get; set; } = "";
    public string ElementId { get; set; } = "";
    public string ParentId { get; set; } = "";
    public string ElementType { get; set; } = "";
    public string? Name { get; set; }
    public string? Documentation { get; set; }
    public NativeGeometry? Geometry { get; set; }
    public string SourceId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public NativePoint[] Points { get; set; } = System.Array.Empty<NativePoint>();
}

public sealed class NativePoint
{
    public double X { get; set; }
    public double Y { get; set; }
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
    public string ElementType { get; set; } = "";
    public string Name { get; set; } = "";
    public string ParentId { get; set; } = "";
    public string DiagramId { get; set; } = "";
    public string Documentation { get; set; } = "";
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
    public string[] Diagrams { get; set; } = System.Array.Empty<string>();
    public NativeElement[] Elements { get; set; } = System.Array.Empty<NativeElement>();
    public NativeValidationMessage[] Validation { get; set; } = System.Array.Empty<NativeValidationMessage>();
    public NativeScenario[] Scenarios { get; set; } = System.Array.Empty<NativeScenario>();
    public NativePublicationReadback? Publication { get; set; }
    public string[] IntegrationAdjustments { get; set; } = System.Array.Empty<string>();
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
