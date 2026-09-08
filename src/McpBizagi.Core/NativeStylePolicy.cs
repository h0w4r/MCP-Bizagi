using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Explicit persisted styling intent and leaf-level projection without hiding unknown content.</summary>
public static class NativeStylePolicy
{
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    public static readonly string[] Alignments = ["Near", "Center", "Far"];
    public static readonly string[] Directions = ["Horizontal", "TopToBottom", "BottomToTop"];
    public static void Validate(NativeMutation change)
    {
        if (change.Style is not { } p) return;
        if (change.Operation is not "create" and not "update") throw new InvalidDataException("Styling requires a create or update intent.");
        if (change.Operation == "create" && change.ElementType == "DataStore") throw new InvalidDataException("Style the graphical DataStoreReference, not its shared DataStore definition.");
        if (p.FontName == null && p.FontSize == null && p.Alignment == null && p.Bold == null && p.Italic == null && p.Underline == null && p.Strikeout == null &&
            p.FontArgb == null && p.BackgroundArgb == null && p.BorderArgb == null && p.BorderVisible == null && p.TextBackgroundArgb == null && p.TextDirection == null && p.LabelBounds == null)
            throw new InvalidDataException("An empty style patch is not an operation.");
        if (p.FontName != null)
        {
            if (string.IsNullOrWhiteSpace(p.FontName) || p.FontName.Length > 255 || p.FontName != p.FontName.Trim()) throw new InvalidDataException("Use an explicit installed font family name.");
            XmlConvert.VerifyXmlChars(p.FontName);
        }
        if (p.FontSize is < 1 or > 512 || p.Alignment != null && !Alignments.Contains(p.Alignment) || p.TextDirection != null && !Directions.Contains(p.TextDirection))
            throw new InvalidDataException("Invalid native font size, alignment or text direction.");
        if (change.Geometry?.BackgroundArgb != null && p.BackgroundArgb != null || change.Geometry?.BorderArgb != null && p.BorderArgb != null)
            throw new InvalidDataException("Do not duplicate color intent between Geometry and Style.");
        if (change.Operation == "create" && change.ElementType is "SequenceFlow" or "MessageFlow" or "Association" && (p.BackgroundArgb != null || p.BorderVisible != null))
            throw new InvalidDataException("Native connectors do not persist BackgroundArgb or BorderVisible.");
        if (change.Operation == "create" && change.ElementType == "Participant" && (p.BorderVisible != null || p.TextDirection != null || p.TextBackgroundArgb != null || p.LabelBounds != null))
            throw new InvalidDataException("Native pools do not persist label bounds, text direction/background or independent boundary visibility.");
        if (p.LabelBounds is { } b) ValidateBounds(b);
    }
    private static void ValidateBounds(NativeLabelBoundsPatch b)
    {
        var values = new[] { b.X, b.Y, b.Width, b.Height };
        if (values.Any(v => !v.HasValue || !double.IsFinite(v.Value) || v < 0 || v > 1000000 || v != Math.Truncate(v.Value)) ||
            values.Any(v => v != 0) && (b.Width <= 0 || b.Height <= 0))
            throw new InvalidDataException("Label bounds require all four nonnegative whole coordinates and positive size, or four explicit zeroes to clear.");
    }
    public static void Verify(NativeStylePatch patch, NativeElement element)
    {
        var actual = element.Style ?? throw new InvalidDataException("Native styling was not loaded for this element.");
        bool bad = patch.FontName != null && patch.FontName != actual.FontName || patch.FontSize != null && patch.FontSize != actual.FontSize ||
            patch.Alignment != null && patch.Alignment != actual.Alignment || patch.Bold != null && patch.Bold != actual.Bold ||
            patch.Italic != null && patch.Italic != actual.Italic || patch.Underline != null && patch.Underline != actual.Underline ||
            patch.Strikeout != null && patch.Strikeout != actual.Strikeout || patch.FontArgb != null && patch.FontArgb != actual.FontArgb ||
            patch.BackgroundArgb != null && patch.BackgroundArgb != actual.BackgroundArgb || patch.BorderArgb != null && patch.BorderArgb != actual.BorderArgb ||
            patch.BorderVisible != null && patch.BorderVisible != actual.BorderVisible || patch.TextBackgroundArgb != null && patch.TextBackgroundArgb != actual.TextBackgroundArgb ||
            patch.TextDirection != null && patch.TextDirection != actual.TextDirection || patch.LabelBounds != null && !SameBounds(patch.LabelBounds, actual.LabelBounds);
        if (bad) throw new InvalidDataException("Requested native styling did not survive independent readback: " + element.Id);
    }
    public static bool SameBounds(NativeLabelBounds a, NativeLabelBounds b) => a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
    public static bool SameBounds(NativeLabelBoundsPatch a, NativeLabelBounds b) => a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
    public static bool Same(NativeStyleInfo? a, NativeStyleInfo? b) => a == null && b == null || a != null && b != null &&
        a.FontName == b.FontName && a.FontSize == b.FontSize && a.Alignment == b.Alignment && a.Bold == b.Bold && a.Italic == b.Italic &&
        a.Underline == b.Underline && a.Strikeout == b.Strikeout && a.FontArgb == b.FontArgb && a.BackgroundArgb == b.BackgroundArgb &&
        a.BorderArgb == b.BorderArgb && a.BorderVisible == b.BorderVisible && a.TextBackgroundArgb == b.TextBackgroundArgb && a.TextDirection == b.TextDirection && SameBounds(a.LabelBounds, b.LabelBounds);

    public static void Project(XElement before, XElement after, NativeStylePatch p)
    {
        if (before.Name != after.Name || !NativeFidelity.IsNativeNameOwner(before) || !NativeFidelity.IsNativeNameOwner(after)) throw new InvalidDataException("Styling projection requires exact native owners.");
        var a = Graphics(before); var b = Graphics(after);
        void Attr(string name, object? value)
        {
            if (value == null) return; string wanted = value is bool flag ? XmlConvert.ToString(flag) : Convert.ToString(value, CultureInfo.InvariantCulture)!;
            if ((string?)b.Attribute(name) != wanted) throw new InvalidDataException("Native graphical attribute differs from explicit style intent: " + name);
            b.SetAttributeValue(name, (string?)a.Attribute(name));
        }
        void Child(XElement left, XElement right, string name, object? value)
        {
            if (value == null) return;
            var old = left.Elements(Ns + name).ToArray(); var current = right.Elements(Ns + name).ToArray();
            string wanted = value is bool flag ? XmlConvert.ToString(flag) : Convert.ToString(value, CultureInfo.InvariantCulture)!;
            if (old.Length > 1 || old.Any(e => e.HasElements || e.Nodes().Any(n => n is not XText)) || current.Length != 1 || current[0].HasElements || current[0].Nodes().Any(n => n is not XText) || current[0].Value != wanted)
                throw new InvalidDataException("Native graphical value differs from explicit style intent: " + name);
            if (old.Length == 0)
            {
                if (current[0].HasAttributes) throw new InvalidDataException("Unknown added style attributes cannot be discarded.");
                current[0].Remove();
            }
            else
            {
                // Nullable native enum serialization uses xsi:nil. Restore only that known null marker;
                // unknown attributes are retained so the complete container comparison still sees changes.
                XName nil = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "nil";
                if (name == "TextDirection" && old[0].Attribute(nil)?.Value == "true" && old[0].Value == "" && current[0].Attribute(nil) == null)
                    current[0].SetAttributeValue(nil, "true");
                current[0].ReplaceNodes(old[0].Nodes().Select(n => new XText(((XText)n).Value)));
            }
        }
        Attr("BorderVisible", p.BorderVisible); Attr("BorderColor", p.BorderArgb); Attr("FillColor", p.BackgroundArgb);
        Child(a, b, "TextDirection", p.TextDirection); Child(a, b, "TextBackgroundColor", p.TextBackgroundArgb);
        if (p.LabelBounds is { } bounds)
        {
            ValidateBounds(bounds);
            foreach (var pair in new[] { ("TextX", bounds.X), ("TextY", bounds.Y), ("TextWidth", bounds.Width), ("TextHeight", bounds.Height) })
            {
                // The installed serializer omits a zero location pair and a zero size pair.
                bool absent = pair.Item1 is "TextX" or "TextY" ? bounds.X == 0 && bounds.Y == 0 : bounds.Width == 0 && bounds.Height == 0;
                if (absent)
                { if (b.Attribute(pair.Item1) != null) throw new InvalidDataException("Cleared label bounds remain persisted."); b.SetAttributeValue(pair.Item1, (string?)a.Attribute(pair.Item1)); }
                else Attr(pair.Item1, pair.Item2);
            }
        }
        if (p.FontName != null || p.FontSize != null || p.Alignment != null || p.Bold != null || p.Italic != null || p.Underline != null || p.Strikeout != null || p.FontArgb != null)
        {
            var old = a.Elements(Ns + "Formatting").ToArray(); var current = b.Elements(Ns + "Formatting").ToArray();
            if (old.Length != 1 || current.Length != 1) throw new InvalidDataException("Missing or ambiguous native font formatting block.");
            Child(old[0], current[0], "FontName", p.FontName); Child(old[0], current[0], "SizeFont", p.FontSize);
            Child(old[0], current[0], "Alignment", p.Alignment); Child(old[0], current[0], "Bold", p.Bold); Child(old[0], current[0], "Italic", p.Italic);
            Child(old[0], current[0], "Underline", p.Underline); Child(old[0], current[0], "Strikeout", p.Strikeout); Child(old[0], current[0], "ColorFont", p.FontArgb);
        }
    }
    private static XElement Graphics(XElement owner)
    {
        var nodes = owner.Elements(Ns + "NodeGraphicsInfos").Elements(Ns + "NodeGraphicsInfo")
            .Concat(owner.Elements(Ns + "ConnectorGraphicsInfos").Elements(Ns + "ConnectorGraphicsInfo")).ToArray();
        if (nodes.Length != 1) throw new InvalidDataException("Native styling requires one exact graphical record.");
        return nodes[0];
    }
}
