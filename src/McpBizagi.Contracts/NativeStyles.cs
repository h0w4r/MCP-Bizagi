namespace McpBizagi.Contracts;

/// <summary>Sparse native graphical intent. Null preserves; no arbitrary property or reflection paths.</summary>
public sealed class NativeStylePatch
{
    public string? FontName { get; set; }
    /// <summary>Native font units. The installed XPDL serializer requires whole values.</summary>
    public int? FontSize { get; set; }
    public string? Alignment { get; set; }
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public bool? Underline { get; set; }
    public bool? Strikeout { get; set; }
    public int? FontArgb { get; set; }
    public int? BackgroundArgb { get; set; }
    public int? BorderArgb { get; set; }
    public bool? BorderVisible { get; set; }
    public int? TextBackgroundArgb { get; set; }
    public string? TextDirection { get; set; }
    /// <summary>Whole nonnegative native coordinates; four zeroes explicitly clear manual label bounds.</summary>
    public NativeLabelBoundsPatch? LabelBounds { get; set; }
}

/// <summary>Complete rectangle intent. Nullable members distinguish omission from an explicit zero reset.</summary>
public sealed class NativeLabelBoundsPatch
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
}

public sealed class NativeLabelBounds
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

/// <summary>Actual native graphical properties, distinct from measured renderer output.</summary>
public sealed class NativeStyleInfo
{
    public string FontName { get; set; } = "";
    public double FontSize { get; set; }
    public string Alignment { get; set; } = "";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool Strikeout { get; set; }
    public int FontArgb { get; set; }
    public int BackgroundArgb { get; set; }
    public int BorderArgb { get; set; }
    public bool BorderVisible { get; set; }
    public int? TextBackgroundArgb { get; set; }
    public string? TextDirection { get; set; }
    public NativeLabelBounds LabelBounds { get; set; } = new();
}

public sealed class NativeFontFamily
{
    public string Name { get; set; } = "";
    public bool Regular { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool BoldItalic { get; set; }
}
