namespace McpBizagi.Contracts;

/// <summary>Patch a native embedded subprocess; omitted properties remain unchanged.</summary>
public sealed class NativeSubProcessProperties
{
    public bool? TriggeredByEvent { get; set; }
    public string? AdHocOrdering { get; set; }
    /// <summary>Null preserves the expression; an empty string explicitly clears it.</summary>
    public string? AdHocCompletionCondition { get; set; }
}

/// <summary>Actual native subclass and properties; read-only fields are not writable capabilities.</summary>
public sealed class NativeSubProcessInfo
{
    public string Kind { get; set; } = "";
    public bool TriggeredByEvent { get; set; }
    public string? AdHocOrdering { get; set; }
    public string? AdHocCompletionCondition { get; set; }
    public bool? CancelRemainingInstances { get; set; }
    public string? TransactionMethod { get; set; }
}
