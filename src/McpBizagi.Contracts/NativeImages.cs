namespace McpBizagi.Contracts;

/// <summary>Revision-checked image input. Native persistence stores a selected raster frame as PNG.</summary>
public sealed class NativeImageImport
{
    public string SourcePath { get; set; } = "";
    public string ExpectedRevision { get; set; } = "";
    /// <summary>Accepts selected-frame 8-bit RGBA decoding/PNG encoding, not preservation of source metadata, profiles, precision or other frames.</summary>
    public bool AllowPngReencoding { get; set; }
    public string? FrameDimension { get; set; }
    public int? FrameIndex { get; set; }
}

/// <summary>Decoded native bitmap evidence. BGRA pixels are hashed row-major without stride padding.</summary>
public sealed class NativeImageInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
    public string PixelSha256 { get; set; } = "";
    public bool HasTransparency { get; set; }
}

/// <summary>One actual native image file, distinct from the decoded picture and generic attachments.</summary>
public sealed class NativeImageFile
{
    public string DiagramId { get; set; } = "";
    public string ElementId { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Length { get; set; }
}

public sealed class NativeImageImportReceipt
{
    public string ElementId { get; set; } = "";
    public string SourceSha256 { get; set; } = "";
    public string SourceFormat { get; set; } = "";
    public string DecodedPixelFormat { get; set; } = "";
    public string FrameDimension { get; set; } = "";
    public int FrameCount { get; set; }
    public int FrameIndex { get; set; }
    public int[] SourceMetadataIds { get; set; } = System.Array.Empty<int>();
    public bool PngReencoded { get; set; }
    public string PayloadEncoder { get; set; } = "";
    public NativeImageInfo Image { get; set; } = new();
    public NativeImageFile File { get; set; } = new();
}

/// <summary>Actual SVG image payload evidence, distinct from mere shape-identity presence.</summary>
public sealed class NativeRenderedImage
{
    public string SurfaceId { get; set; } = "";
    public string ElementId { get; set; } = "";
    public string DeclaredMimeType { get; set; } = "";
    public string EmbeddedSha256 { get; set; } = "";
    public NativeImageInfo Image { get; set; } = new();
}
