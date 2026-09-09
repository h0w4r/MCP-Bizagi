namespace McpBizagi.Contracts;

/// <summary>Internal read-only planning request; never authorizes persistence or arbitrary editor scripts.</summary>
public sealed class NativeAnchorResizeRequest
{
    public string DiagramId { get; set; } = "";
    public string HostId { get; set; } = "";
    public NativeSize Size { get; set; } = new();
}
