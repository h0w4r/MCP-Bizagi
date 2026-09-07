namespace McpBizagi.Contracts;

/// <summary>Metrics read from actual engine-generated result XML; raw artifacts retain additional nested statistics.</summary>
public sealed class NativeSimulationReport
{
    public string ScenarioId { get; set; } = "";
    public int? Replication { get; set; }
    public string Artifact { get; set; } = "";
    public NativeSimulationElement[] Elements { get; set; } = System.Array.Empty<NativeSimulationElement>();
}

public sealed class NativeSimulationElement
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public System.Collections.Generic.Dictionary<string, string> Metrics { get; set; } = new();
}
