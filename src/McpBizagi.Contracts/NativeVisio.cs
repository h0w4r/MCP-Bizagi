namespace McpBizagi.Contracts;

/// <summary>Explicit native source surface associated with a generated Visio page, not an element identity map.</summary>
public sealed class NativeVisioPageReceipt
{
    public string SourceDiagramId { get; set; } = "";
    public string SourceSubProcessId { get; set; } = "";
    public string PageId { get; set; } = "";
    public string PageName { get; set; } = "";
}
