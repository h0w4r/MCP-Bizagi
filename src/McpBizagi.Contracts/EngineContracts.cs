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
    public string DiagramId { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public int SimulationLevel { get; set; } = 1;
    public int AtomicStepSeconds { get; set; } = 30;
    public int InactivitySeconds { get; set; } = 120;
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
    public string Name { get; set; } = "";
    public string ParentId { get; set; } = "";
    public string DiagramId { get; set; } = "";
    public string Documentation { get; set; } = "";
    public NativeGeometry? Geometry { get; set; }
    public string SourceRef { get; set; } = "";
    public string TargetRef { get; set; } = "";
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
