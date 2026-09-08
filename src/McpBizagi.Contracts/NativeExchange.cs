namespace McpBizagi.Contracts;

/// <summary>A revision-checked local interchange document, separate from a native model.</summary>
public sealed class NativeExchangeInput
{
    public string Path { get; set; } = "";
    public string ExpectedRevision { get; set; } = "";
}

/// <summary>Actual installed exporter output associated with its source diagram.</summary>
public sealed class NativeExchangeArtifact
{
    public string DiagramId { get; set; } = "";
    public string Path { get; set; } = "";
}
