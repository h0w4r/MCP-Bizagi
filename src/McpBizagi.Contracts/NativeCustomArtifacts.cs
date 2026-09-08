namespace McpBizagi.Contracts;

/// <summary>Model-owned custom artifact definitions, separate from the user's global palette.</summary>
public sealed class NativeCustomArtifactPatch
{
    public NativeCustomArtifactChange[] Changes { get; set; } = System.Array.Empty<NativeCustomArtifactChange>();
}

public sealed class NativeCustomArtifactChange
{
    public string Operation { get; set; } = "";
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public NativeImageImport? Image { get; set; }
    /// <summary>Explicitly permits one native premultiplied-alpha/DPI rasterization; unstable results fail.</summary>
    public bool AllowNativeRasterization { get; set; }
}

public sealed class NativeCustomArtifactDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public NativeImageInfo Image { get; set; } = new();
    /// <summary>Actual native SerializableImage bytes, not an inferred codec or source-file hash.</summary>
    public string SerializedImageSha256 { get; set; } = "";
}

public sealed class NativeCustomArtifactReceipt
{
    public string Id { get; set; } = "";
    public NativeImageImportReceipt Source { get; set; } = new();
    public NativeCustomArtifactDefinition Result { get; set; } = new();
    public bool PixelsChanged { get; set; }
    public bool NativeRasterizationAcknowledged { get; set; }
    public bool RepeatedSerializationStable { get; set; }
}
