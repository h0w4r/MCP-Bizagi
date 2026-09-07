namespace McpBizagi.Contracts;

/// <summary>Native activity fields. Null fields preserve existing values; inspection fills every field.</summary>
public sealed class NativeActivityProperties
{
    public int? StartQuantity { get; set; }
    public int? CompletionQuantity { get; set; }
    public bool? IsForCompensation { get; set; }
    public string? State { get; set; }
}

/// <summary>Complete sequence-flow condition; expression text is documentation, not executable server code.</summary>
public sealed class NativeFlowCondition
{
    public string Kind { get; set; } = "None";
    public string Text { get; set; } = "";
}
