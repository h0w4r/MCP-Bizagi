namespace McpBizagi.Contracts;

/// <summary>Patch the unique existing definition of a kind; do not replace its identity or collection.</summary>
public sealed class NativeEventPayloadPatch
{
    public string Kind { get; set; } = "";
    public string? Name { get; set; }
    public string? Condition { get; set; }
    public NativeEventTimer? Timer { get; set; }
    public string? ErrorCode { get; set; }
    public string? EscalationCode { get; set; }
    public NativeCompensationPatch? Compensation { get; set; }
}

/// <summary>Complete timer replacement. None clears; Cycle/Date use canonical, durable native text.</summary>
public sealed class NativeEventTimer
{
    public string Kind { get; set; } = "None";
    public string Text { get; set; } = "";
}

/// <summary>Nullable fields preserve existing values; an empty ActivityId explicitly clears the target.</summary>
public sealed class NativeCompensationPatch
{
    public bool? WaitForCompletion { get; set; }
    public string? ActivityId { get; set; }
}

/// <summary>Representable native payload fields, not a serialization of every extension or runtime property.</summary>
public sealed class NativeEventDefinitionInfo
{
    public string Kind { get; set; } = "";
    public string? Name { get; set; }
    public string? Condition { get; set; }
    public NativeEventTimer? Timer { get; set; }
    public string? ErrorCode { get; set; }
    public string? EscalationCode { get; set; }
    public NativeCompensationInfo? Compensation { get; set; }
}

public sealed class NativeCompensationInfo
{
    public bool WaitForCompletion { get; set; }
    public string ActivityId { get; set; } = "";
    public string BpmnName { get; set; } = "";
    public string BpmnNamespace { get; set; } = "";
    public string CatalogActivityId { get; set; } = "";
}
