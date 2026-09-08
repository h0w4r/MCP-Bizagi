namespace McpBizagi.Contracts;

/// <summary>Explicit native category-preserving conversion. Unshared nondefault content is never silently discarded.</summary>
public sealed class NativeTypeConversion
{
    public string ElementId { get; set; } = "";
    public string ExpectedType { get; set; } = "";
    public string TargetType { get; set; } = "";
}
