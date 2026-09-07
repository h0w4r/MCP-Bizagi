namespace McpBizagi.Contracts;

/// <summary>Complete native loop replacement. Omit the enclosing mutation property to preserve a loop.</summary>
public sealed class NativeActivityLoop
{
    public string Kind { get; set; } = "None";
    public NativeStandardLoop? Standard { get; set; }
    public NativeMultiInstanceLoop? MultiInstance { get; set; }
}

/// <summary>Fields durably represented by the installed native standard-loop persistence adapter.</summary>
public sealed class NativeStandardLoop
{
    public int Maximum { get; set; }
    public int Counter { get; set; }
    public bool TestBefore { get; set; }
    public string? Condition { get; set; }
}

/// <summary>Native multi-instance metadata, not a promise of native simulation support.</summary>
public sealed class NativeMultiInstanceLoop
{
    public bool IsSequential { get; set; }
    public int Counter { get; set; }
    public string Behavior { get; set; } = "All";
    public string? CompletionCondition { get; set; }
    public string? ComplexCondition { get; set; }
}
