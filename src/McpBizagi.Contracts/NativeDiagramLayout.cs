namespace McpBizagi.Contracts;

/// <summary>Server-planned diagram geometry; clients do not provide coordinates or a partial selection.</summary>
public sealed class NativeDiagramLayoutRequest
{
    public string DiagramId { get; set; } = "";
    public string Direction { get; set; } = "Right";
}
