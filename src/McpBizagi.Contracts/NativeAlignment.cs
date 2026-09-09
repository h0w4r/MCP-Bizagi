namespace McpBizagi.Contracts;

/// <summary>Explicit native selection operation, not whole-diagram automatic layout.</summary>
public sealed class NativeAlignmentRequest
{
    public string DiagramId { get; set; } = "";
    public string SubProcessId { get; set; } = "";
    public string Mode { get; set; } = "";
    public string[] ElementIds { get; set; } = System.Array.Empty<string>();
    /// <summary>Internal calculated placement intent. The public alignment tool does not accept this mode.</summary>
    public NativeLayoutPlacement[] Placements { get; set; } = System.Array.Empty<NativeLayoutPlacement>();
}

/// <summary>Server-calculated position; dimensions and semantic ownership remain unchanged.</summary>
public sealed class NativeLayoutPlacement
{
    public string ElementId { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>Automatic layout of every direct node on one explicitly identified native surface.</summary>
public sealed class NativeSurfaceLayoutRequest
{
    public string DiagramId { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string Direction { get; set; } = "Right";
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
