namespace McpBizagi.Contracts;

/// <summary>Inline one local call's body, preserving the shared source process and all other callers.</summary>
public sealed class NativeSubProcessInlining
{
    public string ElementId { get; set; } = "";
    public string ExpectedProcessId { get; set; } = "";
    /// <summary>Explicit origin for the copied body in its new embedded coordinate canvas.</summary>
    public NativePoint? Position { get; set; }
}
