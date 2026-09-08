namespace McpBizagi.Contracts;

/// <summary>Explicit scenario correspondence for a native cross-diagram containment transaction.</summary>
public sealed class NativeSimulationMigration
{
    public NativeScenarioMapping[] Mappings { get; set; } = System.Array.Empty<NativeScenarioMapping>();
    /// <summary>Copy missing source scenario resource parameters and calendars; never overwrite conflicting target content.</summary>
    public bool CopyMissingDependencies { get; set; }
    /// <summary>Explicitly retire saved results in affected diagrams, since containment changes invalidate those results.</summary>
    public bool DiscardSimulationResults { get; set; }
}

public sealed class NativeScenarioMapping
{
    public string SourceDiagramId { get; set; } = "";
    public string SourceScenarioId { get; set; } = "";
    public string TargetDiagramId { get; set; } = "";
    public string TargetScenarioId { get; set; } = "";
}

/// <summary>Host-derived allowlisted native collection edits, not user-supplied XML or reflection invocations.</summary>
public sealed class NativeScenarioTransfer
{
    public NativeScenarioMapping Mapping { get; set; } = new();
    public string[] ElementRefs { get; set; } = System.Array.Empty<string>();
    public string[] ResourceRefsToCopy { get; set; } = System.Array.Empty<string>();
    public string[] CalendarIdsToCopy { get; set; } = System.Array.Empty<string>();
}
