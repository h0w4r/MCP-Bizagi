using System.Drawing;
using System.Drawing.Text;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static NativeFontFamily[] InstalledFonts()
    {
        // Enumerate existing Windows/GDI+ families only. Never install fonts or change user settings.
        using var collection = new InstalledFontCollection();
        var families = collection.Families;
        try { return families.Select(f => new NativeFontFamily { Name = f.Name, Regular = f.IsStyleAvailable(FontStyle.Regular),
            Bold = f.IsStyleAvailable(FontStyle.Bold), Italic = f.IsStyleAvailable(FontStyle.Italic), BoldItalic = f.IsStyleAvailable(FontStyle.Bold | FontStyle.Italic) }).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray(); }
        finally { foreach (var family in families) family.Dispose(); }
    }
    private static NativeStyleInfo? DescribeStyle(object element)
    {
        if (Optional(element, "GraphicalProperties") is not object graphics) return null;
        object format = Get(graphics, "Formatting"); var location = (PointF)Get(graphics, "TextLocation"); var size = (SizeF)Get(graphics, "TextSize");
        return new NativeStyleInfo { FontName = Text(format, "FontName"), FontSize = (float)Get(format, "SizeFont"), Alignment = Text(format, "Alignment"),
            Bold = (bool)Get(format, "Bold"), Italic = (bool)Get(format, "Italic"), Underline = (bool)Get(format, "Underline"), Strikeout = (bool)Get(format, "Strikeout"),
            FontArgb = ((Color)Get(format, "ColorFont")).ToArgb(), BackgroundArgb = ((Color)Get(graphics, "BackgroundColor")).ToArgb(),
            BorderArgb = ((Color)Get(graphics, "BorderColor")).ToArgb(), BorderVisible = (bool)Get(graphics, "BorderVisible"),
            TextBackgroundArgb = Optional(graphics, "TextBackgroundColor") is Color color ? color.ToArgb() : null,
            TextDirection = Optional(graphics, "TextDirection")?.ToString(),
            LabelBounds = new NativeLabelBounds { X = location.X, Y = location.Y, Width = size.Width, Height = size.Height } };
    }
    private static void InitializeNewStyle(object element)
    {
        if (Optional(element, "GraphicalProperties") is not object graphics) return;
        // Materialize native reader defaults only for newly constructed objects.
        // Existing models retain their observed values; readback is never normalized to conceal a loss.
        if (Optional(graphics, "TextBackgroundColor") == null) Set(graphics, "TextBackgroundColor", Color.Transparent);
        if (element.GetType().Name == "Participant") Set(graphics, "BorderVisible", !(bool)Get(element, "IsMainParticipant"));
    }
    private void ApplyStyle(object element, NativeStylePatch patch)
    {
        if (element.GetType().Name is "Collaboration" or "Process" or "DataStore" or "Resource" or "LaneSet")
            throw new InvalidDataException("This native definition/container is not an independently styled graphical shape.");
        object graphics = Optional(element, "GraphicalProperties") ?? throw new InvalidDataException("Styling requires a native graphical element.");
        if (element.GetType().Name == "Participant" && (patch.BorderVisible != null || patch.TextDirection != null || patch.TextBackgroundArgb != null || patch.LabelBounds != null))
            throw new InvalidDataException("The installed pool adapter persists font and fill/border colors, but not label bounds, text background/direction or independent boundary visibility.");
        if ((bool?)Optional(element, "IsConnector") == true && (patch.BorderVisible != null || patch.BackgroundArgb != null))
            throw new InvalidDataException("The installed connector serializer does not persist BorderVisible or BackgroundArgb.");
        if (patch.FontName != null && !InstalledFonts().Any(f => f.Name.Equals(patch.FontName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Requested font family is not installed; implicit font substitution is not permitted.");
        object format = Get(graphics, "Formatting");
        // Only these discovered native fields are writable. Node geometry and connector paths remain separate.
        if (patch.FontName != null) Set(format, "FontName", patch.FontName);
        if (patch.FontSize.HasValue) Set(format, "SizeFont", (float)patch.FontSize.Value);
        if (patch.Alignment != null) Set(format, "Alignment", Enum.Parse(typeof(StringAlignment), patch.Alignment));
        if (patch.Bold.HasValue) Set(format, "Bold", patch.Bold.Value);
        if (patch.Italic.HasValue) Set(format, "Italic", patch.Italic.Value);
        if (patch.Underline.HasValue) Set(format, "Underline", patch.Underline.Value);
        if (patch.Strikeout.HasValue) Set(format, "Strikeout", patch.Strikeout.Value);
        if (patch.FontArgb.HasValue) Set(format, "ColorFont", Color.FromArgb(patch.FontArgb.Value));
        if (patch.BackgroundArgb.HasValue) Set(graphics, "BackgroundColor", Color.FromArgb(patch.BackgroundArgb.Value));
        if (patch.BorderArgb.HasValue) Set(graphics, "BorderColor", Color.FromArgb(patch.BorderArgb.Value));
        if (patch.BorderVisible.HasValue) Set(graphics, "BorderVisible", patch.BorderVisible.Value);
        if (patch.TextBackgroundArgb.HasValue) Set(graphics, "TextBackgroundColor", Color.FromArgb(patch.TextBackgroundArgb.Value));
        if (patch.TextDirection != null) Set(graphics, "TextDirection", Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.TextDirection"), patch.TextDirection));
        if (patch.LabelBounds is { } label)
        {
            if (label.X == null || label.Y == null || label.Width == null || label.Height == null) throw new InvalidDataException("Native label rectangle requires four explicit coordinates.");
            Set(graphics, "TextLocation", new PointF((float)label.X.Value, (float)label.Y.Value)); Set(graphics, "TextSize", new SizeF((float)label.Width.Value, (float)label.Height.Value));
        }
    }
}
