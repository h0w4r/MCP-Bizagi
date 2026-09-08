using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

public sealed record ExchangeDifference(string Location, string? Before, string? After);

/// <summary>Bounded XPDL preflight and explicit observable differences, never a native model replacement.</summary>
public static class XpdlDocument
{
    public static readonly XNamespace Namespace = "http://www.wfmc.org/2009/XPDL2.2";

    public static XDocument Parse(byte[] bytes)
    {
        if (bytes.LongLength > BpmnDocument.MaxXmlCharacters * 4) throw new InvalidDataException("XPDL exceeds the input byte bound.");
        using var stream = new MemoryStream(bytes, writable: false);
        var settings = new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = BpmnDocument.MaxXmlCharacters, MaxCharactersFromEntities = 1024 };
        // Bound nesting before either native deserialization or recursive difference projection.
        using (var boundedReader = XmlReader.Create(stream, settings))
            while (boundedReader.Read())
                if (boundedReader.Depth > 128) throw new InvalidDataException("XPDL exceeds the XML nesting bound.");
        stream.Position = 0;
        using var reader = XmlReader.Create(stream, settings);
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        if (document.Root?.Name != Namespace + "Package" ||
            document.Root.Element(Namespace + "PackageHeader")?.Element(Namespace + "XPDLVersion")?.Value.Trim() != "2.2")
            throw new InvalidDataException("Expected an XPDL 2.2 Package with a matching PackageHeader/XPDLVersion.");
        // No secondary package, XInclude or processing-instruction resolver is part of this local-file contract.
        if (document.DescendantNodes().OfType<XProcessingInstruction>().Any() ||
            document.Descendants().Any(e => e.Name.NamespaceName == "http://www.w3.org/2001/XInclude" || e.Name == Namespace + "ExternalPackage"))
            throw new InvalidDataException("External packages, XInclude and processing instructions are not supported.");
        return document;
    }

    public static ExchangeDifference[] CompareXml(byte[] before, byte[] after) => Differences(Atoms(Parse(before)), Atoms(Parse(after)));

    public static ExchangeDifference[] CompareGraph(NativeElement[] before, NativeElement[] after) => Differences(
        before.ToDictionary(e => e.Id, e => JsonSerializer.Serialize(e), StringComparer.Ordinal),
        after.ToDictionary(e => e.Id, e => JsonSerializer.Serialize(e), StringComparer.Ordinal));

    public static ExchangeDifference[] CompareMetadata(EngineReply before, EngineReply after) => Differences(MetadataAtoms(before), MetadataAtoms(after));

    private static Dictionary<string, string> MetadataAtoms(EngineReply reply)
    {
        if (reply.Metadata == null || reply.Documentation == null) throw new InvalidDataException("Missing real native exchange metadata/documentation snapshots.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        // Catalog enumeration order is not an identity. Compare every keyed record, including its complete native XML.
        foreach (var resource in reply.Metadata.Resources) result.Add("resource/" + resource.Id, JsonSerializer.Serialize(resource));
        foreach (var assignment in reply.Metadata.Assignments) result.Add("raci/" + assignment.ElementId, JsonSerializer.Serialize(assignment));
        foreach (var simulation in reply.Metadata.Simulations) result.Add("simulation/" + simulation.DiagramId, simulation.Xml);
        foreach (var definition in reply.Documentation.Definitions) result.Add("definition/" + definition.Id, JsonSerializer.Serialize(definition));
        foreach (var values in reply.Documentation.Values) result.Add("values/" + values.DiagramId + "/" + values.ElementId, values.Xml);
        foreach (var file in reply.Documentation.Attachments) result.Add("attachment/" + file.DiagramId + "/" + file.ElementId + "/" + file.FileName, JsonSerializer.Serialize(file));
        return result;
    }

    private static ExchangeDifference[] Differences(Dictionary<string, string> before, Dictionary<string, string> after) =>
        before.Keys.Union(after.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Where(key => before.GetValueOrDefault(key) != after.GetValueOrDefault(key))
            .Select(key => new ExchangeDifference(key, before.GetValueOrDefault(key), after.GetValueOrDefault(key))).ToArray();

    private static Dictionary<string, string> Atoms(XDocument document)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        void Visit(XElement element, string path)
        {
            result.Add(path, element.Name.ToString());
            foreach (var attribute in element.Attributes()) result.Add(path + "/@" + attribute.Name, attribute.Value);
            int index = 0;
            foreach (var node in element.Nodes())
            {
                // Ignore serializer indentation only, never xml:space-preserved or mixed-content text.
                if (node is XText text && string.IsNullOrWhiteSpace(text.Value) && element.HasElements &&
                    !element.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value)) &&
                    !element.AncestorsAndSelf().Any(e => (string?)e.Attribute(XNamespace.Xml + "space") == "preserve")) continue;
                string child = path + "/node[" + index++ + "]";
                if (node is XElement nested) Visit(nested, child);
                else result.Add(child, node is XText value ? value.Value : node.ToString(SaveOptions.DisableFormatting));
            }
        }
        Visit(document.Root!, "/Package");
        int topIndex = 0;
        foreach (var node in document.Nodes().Where(n => n is not XElement && !(n is XText t && string.IsNullOrWhiteSpace(t.Value))))
            result.Add("/document-node[" + topIndex++ + "]", node.ToString(SaveOptions.DisableFormatting));
        return result;
    }
}
