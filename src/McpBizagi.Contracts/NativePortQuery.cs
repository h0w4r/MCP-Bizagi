namespace McpBizagi.Contracts;

/// <summary>Internal bounded, read-only geometry queries against actual installed connections.</summary>
public sealed class NativePortQueryRequest
{
    public string DiagramId { get; set; } = "";
    public string SubProcessId { get; set; } = "";
    public NativePortQuery[] Queries { get; set; } = System.Array.Empty<NativePortQuery>();
}

/// <summary>Explicit endpoint coordinates, never an inverse mapping from a native port number.</summary>
public sealed class NativePortQuery
{
    public string ConnectionId { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public NativeGeometry SourceBounds { get; set; } = new();
    public NativeGeometry TargetBounds { get; set; } = new();
    public NativePoint SourcePoint { get; set; } = new();
    public NativePoint TargetPoint { get; set; } = new();
}

/// <summary>Actual installed layouter output, including failures that its internal fallback may swallow.</summary>
public sealed class NativePortObservation
{
    public NativePortQuery Query { get; set; } = new();
    public string SourcePort { get; set; } = "";
    public string TargetPort { get; set; } = "";
    public NativePoint[] Route { get; set; } = System.Array.Empty<NativePoint>();
    public string[] Errors { get; set; } = System.Array.Empty<string>();
}

/// <summary>Read-only service evidence; not a native write or desktop visual accreditation.</summary>
public sealed class NativePortQueryReceipt
{
    public string EditorAssetSha256 { get; set; } = "";
    public bool RegistryUnchanged { get; set; }
    public NativePortObservation[] Observations { get; set; } = System.Array.Empty<NativePortObservation>();
}
