namespace McpBizagi.Contracts;

/// <summary>Explicit native type conversion for tasks, gateways, unbound calls and same-role events. Nondefault content is never silently discarded.</summary>
public sealed class NativeTypeConversion
{
    public string ElementId { get; set; } = "";
    public string ExpectedType { get; set; } = "";
    public string TargetType { get; set; } = "";
    /// <summary>Required for events: Start, End, Catch, Throw or Boundary. Conversion preserves this role.</summary>
    public string? ExpectedEventMode { get; set; }
}
