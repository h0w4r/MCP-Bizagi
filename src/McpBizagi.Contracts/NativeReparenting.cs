namespace McpBizagi.Contracts;

/// <summary>Move an existing native element, preserving its identity and complete subtree.</summary>
public sealed class NativeReparenting
{
    public string ElementId { get; set; } = "";
    public string ExpectedParentId { get; set; } = "";
    public string TargetParentId { get; set; } = "";
    /// <summary>Optional explicit node position. Omission retains coordinates without guessed translation or resizing.</summary>
    public NativePoint? Position { get; set; }
}
