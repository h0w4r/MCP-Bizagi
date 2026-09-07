namespace McpBizagi.Contracts;

/// <summary>Explicit native metadata transactions; BPSim remains a separate native format.</summary>
public sealed class NativeMetadataPatch
{
    public NativeResourceChange[] Resources { get; set; } = System.Array.Empty<NativeResourceChange>();
    public NativeSimulationConfiguration[] Simulations { get; set; } = System.Array.Empty<NativeSimulationConfiguration>();
    public NativeResourceAssignments[] Assignments { get; set; } = System.Array.Empty<NativeResourceAssignments>();
    public bool DiscardSimulationResults { get; set; }
}

public sealed class NativeResourceChange
{
    public string Operation { get; set; } = "upsert";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "Role";
}

public sealed class NativeResource
{
    public string Id { get; set; } = "";
    public string BpmnId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
}

/// <summary>Complete replacement of one diagram's simulation configuration, not a BPMN conversion.</summary>
public sealed class NativeSimulationConfiguration
{
    public string DiagramId { get; set; } = "";
    public string Xml { get; set; } = "";
}

public sealed class NativeMetadataSnapshot
{
    public NativeResource[] Resources { get; set; } = System.Array.Empty<NativeResource>();
    public NativeSimulationConfiguration[] Simulations { get; set; } = System.Array.Empty<NativeSimulationConfiguration>();
    public NativeResourceAssignments[] Assignments { get; set; } = System.Array.Empty<NativeResourceAssignments>();
}

/// <summary>Complete RACI assignment sets for a native activity. All references are native resource GUIDs.</summary>
public sealed class NativeResourceAssignments
{
    public string ElementId { get; set; } = "";
    public string[] Responsible { get; set; } = System.Array.Empty<string>();
    public string[] Accountable { get; set; } = System.Array.Empty<string>();
    public string[] Consulted { get; set; } = System.Array.Empty<string>();
    public string[] Informed { get; set; } = System.Array.Empty<string>();
}
