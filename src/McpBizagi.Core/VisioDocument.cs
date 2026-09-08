using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

public sealed record VisioShape(string Id, string ParentId, string MasterId, string Name, string Text);
public sealed record VisioPage(string Id, string Name, VisioShape[] Shapes);
public sealed record VisioProjectionDifference(string Kind, string Name, int SourceCount, int ImportedCount);

/// <summary>Bounded VDX preflight and observable page inventory, not a replacement Visio engine.</summary>
public static partial class VisioDocument
{
    public static readonly XNamespace Namespace = "http://schemas.microsoft.com/visio/2003/core";

    private static XDocument ReadXml(byte[] bytes)
    {
        if (bytes.LongLength > BpmnDocument.MaxXmlCharacters * 4) throw new InvalidDataException("VDX exceeds the byte bound.");
        using var stream = new MemoryStream(bytes, writable: false);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = BpmnDocument.MaxXmlCharacters, MaxCharactersFromEntities = 1024 };
        using (var bounded = XmlReader.Create(stream, settings))
            while (bounded.Read()) if (bounded.Depth > 128) throw new InvalidDataException("VDX nesting exceeds the bound.");
        stream.Position = 0;
        using var reader = XmlReader.Create(stream, settings);
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        if (document.Root?.Name != Namespace + "VisioDocument") throw new InvalidDataException("Expected a Visio 2003 VDX document.");
        if (document.DescendantNodes().OfType<XProcessingInstruction>().Any() || document.Descendants().Any(e =>
            e.Name.NamespaceName == "http://www.w3.org/2001/XInclude" || e.Attribute(XNamespace.Xml + "base") != null))
            throw new InvalidDataException("VDX processing instructions, XInclude and external base contexts are unsupported.");
        return document;
    }

    public static VisioPage[] Inspect(byte[] bytes)
    {
        var document = ReadXml(bytes);
        var pages = document.Root!.Elements(Namespace + "Pages").ToArray();
        if (pages.Length != 1) throw new InvalidDataException("VDX requires one Pages collection.");
        var result = pages[0].Elements(Namespace + "Page").Select(page =>
        {
            string id = RequiredId(page); var seen = new HashSet<string>(StringComparer.Ordinal);
            var shapes = page.Elements(Namespace + "Shapes").SelectMany(s => s.Descendants(Namespace + "Shape")).Select(shape =>
            {
                string shapeId = RequiredId(shape);
                if (!seen.Add(shapeId)) throw new InvalidDataException("VDX contains duplicate page-local shape identities.");
                return new VisioShape(shapeId, (string?)shape.Ancestors(Namespace + "Shape").FirstOrDefault()?.Attribute("ID") ?? "",
                    (string?)shape.Attribute("Master") ?? "", (string?)shape.Attribute("NameU") ?? "", shape.Element(Namespace + "Text")?.Value ?? "");
            }).ToArray();
            if (shapes.Length > 100000) throw new InvalidDataException("VDX page exceeds the shape bound.");
            return new VisioPage(id, (string?)page.Attribute("Name") ?? "", shapes);
        }).ToArray();
        if (result.Length is < 1 or > 100 || result.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != result.Length)
            throw new InvalidDataException("VDX requires 1-100 distinctly identified pages.");
        return result;
    }

    private static string RequiredId(XElement element)
    {
        string id = (string?)element.Attribute("ID") ?? "";
        if (!uint.TryParse(id, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) ||
            number.ToString(System.Globalization.CultureInfo.InvariantCulture) != id) throw new InvalidDataException("VDX requires canonical nonnegative integer IDs.");
        return id;
    }

    public static ExchangeDifference[] ImportNormalization(NativeElement[] imported, NativeElement[] reopened)
    {
        // The foreign importer creates transient graphics and unresolved serialized reference strings.
        // Permit only those observed fields at the FIRST import boundary, expose all differences,
        // and require a separate lossless native no-op/restart gate after this normalization.
        NativeEditPlan.VerifyRestartContainment(imported, reopened);
        var original = imported.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var durable = reopened.ToDictionary(e => e.Id, StringComparer.Ordinal);
        if (!original.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(durable.Keys)) throw new InvalidDataException("Visio import lost native identities during persistence.");
        foreach (var pair in original)
        {
            var before = pair.Value; var after = durable[pair.Key];
            var comparison = JsonSerializer.Deserialize<NativeElement>(JsonSerializer.Serialize(before))!;
            comparison.Geometry = after.Geometry; comparison.Style = after.Style;
            if (before.SourceRef == "" && after.SourceRef == after.SourceId) comparison.SourceRef = after.SourceRef;
            if (before.TargetRef == "" && after.TargetRef == after.TargetId) comparison.TargetRef = after.TargetRef;
            if (JsonSerializer.Serialize(comparison) != JsonSerializer.Serialize(after))
                throw new InvalidDataException("Unexpected semantic change at the Visio import persistence boundary: " + pair.Key);
        }
        return XpdlDocument.CompareGraph(imported, reopened);
    }

    public static ExchangeDifference[] ImportMetadataNormalization(EngineReply imported, EngineReply reopened)
    {
        var differences = XpdlDocument.CompareMetadata(imported, reopened);
        foreach (var difference in differences)
        {
            // Native load can materialize empty element value containers; no real attribute is waived.
            if (difference.Before != null || !difference.Location.StartsWith("values/", StringComparison.Ordinal) || difference.After == null)
                throw new InvalidDataException("Visio metadata changed during initial native persistence.");
            var value = reopened.Documentation!.Values.SingleOrDefault(v => "values/" + v.DiagramId + "/" + v.ElementId == difference.Location);
            if (value == null || !reopened.Elements.Any(e => e.Id == value.ElementId && e.DiagramId == value.DiagramId))
                throw new InvalidDataException("Visio metadata initialization refers to an absent native element.");
            var xml = XElement.Parse(value.Xml, LoadOptions.PreserveWhitespace);
            var children = xml.Elements().ToArray();
            if (xml.Name != "ElementAttributeValues" || (string?)xml.Attribute("ElementId") != value.ElementId ||
                xml.Attributes().Any(a => !a.IsNamespaceDeclaration && a.Name != "ElementId") || children.Length != 1 || children[0].Name != "Values" ||
                children[0].HasAttributes || children[0].HasElements ||
                xml.DescendantNodes().Any(n => n is not XElement && (n is not XText t || !string.IsNullOrWhiteSpace(t.Value))))
                throw new InvalidDataException("Visio metadata initialization contains nonempty or unknown content.");
        }
        return differences;
    }

    public static VisioProjectionDifference[] CompareProjection(NativeElement[] selected, NativeElement[] imported)
    {
        // Visio remaps identities. A label/kind multiset highlights observable omissions/conversions
        // without pretending labels identify objects or proving geometry/connection equivalence.
        var before = selected.GroupBy(e => (e.Kind, e.Name)).ToDictionary(g => g.Key, g => g.Count());
        var after = imported.GroupBy(e => (e.Kind, e.Name)).ToDictionary(g => g.Key, g => g.Count());
        return before.Keys.Union(after.Keys).OrderBy(k => k.Kind, StringComparer.Ordinal).ThenBy(k => k.Name, StringComparer.Ordinal)
            .Where(k => before.GetValueOrDefault(k) != after.GetValueOrDefault(k))
            .Select(k => new VisioProjectionDifference(k.Kind, k.Name, before.GetValueOrDefault(k), after.GetValueOrDefault(k))).ToArray();
    }
}
