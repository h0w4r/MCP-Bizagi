namespace McpBizagi.Contracts;

/// <summary>Sparse artifact content patch; this is not the unrelated DisplayName or documentation field.</summary>
public sealed class NativeArtifactProperties
{
    /// <summary>Plain annotation text or the native formatted-text markup. Empty clears; null preserves.</summary>
    public string? Text { get; set; }
    public NativeImageImport? Image { get; set; }
    public string? CustomArtifactTypeId { get; set; }
}

/// <summary>Actual installed-model artifact properties, without inferring unsupported writable fields.</summary>
public sealed class NativeArtifactInfo
{
    public string Type { get; set; } = "";
    public string? Text { get; set; }
    /// <summary>Observed only; the native XPDL route does not persist annotation TextFormat edits.</summary>
    public string? TextFormat { get; set; }
    public string? HeaderDiagramId { get; set; }
    public NativeImageInfo? Image { get; set; }
    public string? CustomArtifactTypeId { get; set; }
}
