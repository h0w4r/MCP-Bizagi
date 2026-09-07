using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Projects only explicitly verified edits out of a comparison; never rebuilds or writes a native model.</summary>
public static class NativeMutationFidelity
{
    private static readonly XNamespace Xpdl = "http://www.wfmc.org/2009/XPDL2.2";
    public static NativeFidelityReport Compare(byte[] before, byte[] after, NativeMutation[] changes, NativeElement[] reopened)
    {
        NativeEditPlan.Validate(changes); NativeEditPlan.Verify(changes, reopened);
        var left = NativeArchive.ReadEntries(before).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var right = NativeArchive.ReadEntries(after).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var coverage = changes.ToDictionary(c => c.ElementId, _ => 0, StringComparer.Ordinal);
        foreach (string entry in left.Keys.Intersect(right.Keys, StringComparer.OrdinalIgnoreCase).Where(p => p.EndsWith(".diag!/Diagram.xml", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            var a = Read(left[entry]); var b = Read(right[entry]);
            if (a.Root?.Name != Xpdl + "Package" || b.Root?.Name != Xpdl + "Package") continue;
            foreach (var c in changes)
            {
                var x = Identified(a, c.ElementId); var y = Identified(b, c.ElementId);
                coverage[c.ElementId] += c.Operation == "create" ? y.Length : x.Length;
                if (c.Operation == "create")
                {
                    if (x.Length != 0) throw new InvalidDataException("Created identity already existed in the native container.");
                    foreach (var element in y) element.Remove();
                    continue;
                }
                if (c.Operation == "delete") { foreach (var element in x) element.Remove(); continue; }
                if (x.Length == 0 && y.Length == 0) continue;
                if (x.Length != 1 || y.Length != 1) throw new InvalidDataException("Ambiguous native XML mutation identity.");
                if (c.Documentation != null)
                    foreach (string name in new[] { "Description", "Documentation" })
                    {
                        var oldText = x[0].Element(Xpdl + name); var newText = y[0].Element(Xpdl + name);
                        if (oldText != null && newText != null && !oldText.HasElements && !newText.HasElements && newText.Value == c.Documentation &&
                            oldText.Nodes().All(n => n is XText) && newText.Nodes().All(n => n is XText))
                        { newText.ReplaceNodes(oldText.Nodes().Select(n => new XText(((XText)n).Value))); }
                    }
                if (c.Geometry is { } g)
                {
                    var oldGraphics = x[0].Element(Xpdl + "NodeGraphicsInfos")?.Element(Xpdl + "NodeGraphicsInfo");
                    var newGraphics = y[0].Element(Xpdl + "NodeGraphicsInfos")?.Element(Xpdl + "NodeGraphicsInfo");
                    if (oldGraphics == null || newGraphics == null) throw new InvalidDataException("Missing native node graphics for geometry mutation.");
                    RestoreNumber(oldGraphics, newGraphics, "Width", g.Width); RestoreNumber(oldGraphics, newGraphics, "Height", g.Height);
                    RestoreNumber(oldGraphics.Element(Xpdl + "Coordinates")!, newGraphics.Element(Xpdl + "Coordinates")!, "XCoordinate", g.X);
                    RestoreNumber(oldGraphics.Element(Xpdl + "Coordinates")!, newGraphics.Element(Xpdl + "Coordinates")!, "YCoordinate", g.Y);
                    if (g.BackgroundArgb.HasValue) RestoreNumber(oldGraphics, newGraphics, "FillColor", g.BackgroundArgb.Value, integer: true);
                    if (g.BorderArgb.HasValue) RestoreNumber(oldGraphics, newGraphics, "BorderColor", g.BorderArgb.Value, integer: true);
                }
                if (c.Operation == "reconnect")
                {
                    RestoreAttribute(x[0], y[0], "From", c.SourceId); RestoreAttribute(x[0], y[0], "To", c.TargetId);
                    var oldGraphics = x[0].Element(Xpdl + "ConnectorGraphicsInfos")?.Element(Xpdl + "ConnectorGraphicsInfo");
                    var newGraphics = y[0].Element(Xpdl + "ConnectorGraphicsInfos")?.Element(Xpdl + "ConnectorGraphicsInfo");
                    if (oldGraphics == null || newGraphics == null) throw new InvalidDataException("Missing native connector graphics.");
                    var oldPoints = oldGraphics.Elements(Xpdl + "Coordinates").ToArray(); var newPoints = newGraphics.Elements(Xpdl + "Coordinates").ToArray();
                    if (oldPoints.Concat(newPoints).Any(p => p.HasElements || p.Nodes().Any() || p.Attributes().Any(v => v.Name != "XCoordinate" && v.Name != "YCoordinate")))
                        throw new InvalidDataException("Unknown content in connector coordinates cannot be normalized away.");
                    if (newPoints.Length != c.Points.Length) throw new InvalidDataException("Native coordinate count differs from the requested path.");
                    for (int i = 0; i < newPoints.Length; i++)
                        if (!NumberMatches((string?)newPoints[i].Attribute("XCoordinate"), c.Points[i].X) || !NumberMatches((string?)newPoints[i].Attribute("YCoordinate"), c.Points[i].Y))
                            throw new InvalidDataException("Native XML connector coordinates differ from the requested path.");
                    newPoints[0].AddBeforeSelf(oldPoints.Select(p => new XElement(p))); foreach (var point in newPoints) point.Remove();
                }
            }
            left[entry] = Encoding.UTF8.GetBytes(a.ToString(SaveOptions.DisableFormatting));
            right[entry] = Encoding.UTF8.GetBytes(b.ToString(SaveOptions.DisableFormatting));
        }
        if (coverage.Values.Any(count => count != 1)) throw new InvalidDataException("Every mutation must address exactly one supported native XML identity.");
        var names = changes.Where(c => c.Name != null).Select(c => new ExpectedNativeName(c.ElementId, c.Name!)).ToArray();
        var report = NativeFidelity.CompareEntries(left, right, names);
        // Keep a visible record that authorized intent was projected; callers also retain the full request and readback.
        return report with
        {
            Differences = report.Differences.Concat(changes.Select(c => new NativeDifference("request", c.Operation,
            "verified_requested_mutation", c.ElementId, null, "fresh-worker postconditions verified"))).ToArray()
        };
    }
    private static XElement[] Identified(XDocument doc, string id) => doc.Descendants().Where(e => e.Name.Namespace == Xpdl &&
        (string?)e.Attribute("Id") == id && new[] { "Activity", "Transition", "Pool", "Lane", "Artifact", "MessageFlow" }.Contains(e.Name.LocalName)).ToArray();
    private static XDocument Read(byte[] data)
    {
        using var stream = new MemoryStream(data); using var reader = XmlReader.Create(stream,
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = BpmnDocument.MaxXmlCharacters });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }
    private static bool NumberMatches(string? actual, double expected) => double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) && Math.Abs(n - (double)(float)expected) <= 0.001;
    private static void RestoreNumber(XElement before, XElement after, string name, double expected, bool integer = false)
    {
        string? value = (string?)after.Attribute(name);
        if (integer ? value != expected.ToString(CultureInfo.InvariantCulture) : !NumberMatches(value, expected))
            throw new InvalidDataException("Unexpected native XML geometry/style value: " + name);
        after.SetAttributeValue(name, (string?)before.Attribute(name));
    }
    private static void RestoreAttribute(XElement before, XElement after, string name, string expected)
    {
        if ((string?)after.Attribute(name) != expected) throw new InvalidDataException("Unexpected native connector endpoint: " + name);
        after.SetAttributeValue(name, (string?)before.Attribute(name));
    }
}
