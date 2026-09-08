namespace McpBizagi.Contracts;

/// <summary>Copy explicit native selection roots and their owned subtrees, never the OS clipboard.</summary>
public sealed class NativeSelectionCopyRequest
{
    public string SourceDiagramId { get; set; } = "";
    public string TargetParentId { get; set; } = "";
    public string[] ElementIds { get; set; } = System.Array.Empty<string>();
    /// <summary>Requested selection bounding-box origin in the destination canvas.</summary>
    public NativePoint? Position { get; set; }
}

/// <summary>Actual native cloner provenance, including owned I/O identities; independently verified by the host.</summary>
public sealed class NativeSelectionCopyReceipt
{
    public string SourceDiagramId { get; set; } = "";
    public string TargetDiagramId { get; set; } = "";
    public string TargetParentId { get; set; } = "";
    public NativeCloneIdentity[] Identities { get; set; } = System.Array.Empty<NativeCloneIdentity>();
}
