namespace McpBizagi.Contracts;

/// <summary>Explicit native selection operation, not whole-diagram automatic layout.</summary>
public sealed class NativeAlignmentRequest
{
    public string DiagramId { get; set; } = "";
    public string SubProcessId { get; set; } = "";
    public string Mode { get; set; } = "";
    public string[] ElementIds { get; set; } = System.Array.Empty<string>();
}

/// <summary>Observed native editor transaction; route intent originates in actual CEF callbacks.</summary>
public sealed class NativeAlignmentReceipt
{
    public string Mode { get; set; } = "";
    public string[] SelectedElementIds { get; set; } = System.Array.Empty<string>();
    public bool NoOp { get; set; }
    public string CallbackSha256 { get; set; } = "";
    public string EditorAssetSha256 { get; set; } = "";
    public NativeMutation[] Changes { get; set; } = System.Array.Empty<NativeMutation>();
    public string[] AutomaticLabelsPreserved { get; set; } = System.Array.Empty<string>();
}
