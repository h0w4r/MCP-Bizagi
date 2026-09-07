using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace McpBizagi.Core;

public sealed record ModelElement(string Id, string Kind, string? Name, string? ParentId);
public sealed record ModelSummary(string Revision, int ProcessCount, int DiagramCount, ModelElement[] Elements);
public sealed record ValidationFinding(string Code, string Severity, string Message, string? ElementId = null);
public sealed record ModelChange(string ElementId, string Property, string Value);

/// <summary>Loss-aware XML operations. No simplified DTO is used to reconstruct a document.</summary>
public static class BpmnDocument
{
    public static readonly XNamespace Bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    public static readonly XNamespace Di = "http://www.omg.org/spec/BPMN/20100524/DI";
    public const long MaxXmlCharacters = 16 * 1024 * 1024;

    public static XDocument Parse(string xml)
    {
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = MaxXmlCharacters, MaxCharactersFromEntities = 1024
        });
        var doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        if (doc.Root?.Name != Bpmn + "definitions")
            throw new InvalidDataException("Expected BPMN 2.0 definitions in the OMG MODEL namespace.");
        return doc;
    }

    public static string Revision(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static ModelSummary Inspect(string xml, string revision)
    {
        var doc = Parse(xml);
        return new(revision, doc.Descendants(Bpmn + "process").Count(), doc.Descendants(Di + "BPMNDiagram").Count(),
            doc.Descendants().Where(e => e.Name.Namespace == Bpmn && e.Attribute("id") != null)
                .Select(e => new ModelElement((string)e.Attribute("id")!, e.Name.LocalName,
                    (string?)e.Attribute("name"), (string?)e.Parent?.Attribute("id"))).ToArray());
    }

    public static ValidationFinding[] Validate(string xml)
    {
        var doc = Parse(xml);
        var findings = new List<ValidationFinding>();
        var elements = doc.Descendants().Where(e => e.Attribute("id") != null).ToArray();
        var ids = elements.Select(e => (string)e.Attribute("id")!).ToHashSet(StringComparer.Ordinal);
        foreach (var group in elements.GroupBy(e => (string)e.Attribute("id")!).Where(g => g.Count() > 1))
            findings.Add(new("duplicate_id", "error", "Element ID is not unique.", group.Key));
        foreach (var flow in doc.Descendants().Where(e => e.Name == Bpmn + "sequenceFlow" || e.Name == Bpmn + "messageFlow"))
            foreach (var attribute in new[] { "sourceRef", "targetRef" })
                if (flow.Attribute(attribute) is not { } reference || !ids.Contains(reference.Value))
                    findings.Add(new("unresolved_reference", "error", attribute + " does not identify an element.", (string?)flow.Attribute("id")));
        if (!doc.Descendants(Di + "BPMNDiagram").Any())
            findings.Add(new("geometry_missing", "warning", "No BPMN Diagram Interchange geometry was supplied."));
        findings.Add(new("validation_scope", "info", "Structural checks only; not complete OMG XSD, behavioral, native-engine or visual validation."));
        return findings.ToArray();
    }

    public static string Apply(string xml, IReadOnlyList<ModelChange> changes)
    {
        var doc = Parse(xml);
        foreach (var change in changes)
        {
            // Resolve exactly one target. Preserve every unmodified XML node and extension.
            var matches = doc.Descendants().Where(e => e.Name.Namespace == Bpmn && (string?)e.Attribute("id") == change.ElementId).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Expected one BPMN element: " + change.ElementId);
            if (change.Property != "name")
                throw new NotSupportedException("This release supports name changes only; unsupported edits are never silently discarded.");
            matches[0].SetAttributeValue("name", change.Value);
        }
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    public static byte[] Encode(string xml)
    {
        // Serialize through an encoding-aware XML writer so a supplied UTF-16 declaration cannot corrupt UTF-8 output.
        var doc = Parse(xml);
        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false, true), Indent = false }))
            doc.Save(writer);
        return output.ToArray();
    }
}
