using System.Text;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Complete native documentation intent and whole-archive comparison; projections never rewrite a model.</summary>
public static class NativeDocumentationPolicy
{
    private static readonly string[] Types = ["Text", "LongText", "Number", "Date", "Image", "Combo", "Radio", "Check", "FileEmbedded", "FileLinked", "Link", "Table"];
    public static void Validate(NativeDocumentationPatch patch)
    {
        if (patch?.Definitions == null || patch.Values == null || patch.Attachments == null ||
            patch.Definitions.Length + patch.Values.Length + patch.Attachments.Length is < 1 or > 1000)
            throw new InvalidDataException("Documentation patch must contain 1-1000 explicit changes.");
        var ids = new HashSet<string>();
        foreach (var d in patch.Definitions)
        {
            if (d == null) throw new InvalidDataException("Null definition.");
            NativeMetadataPolicy.RequireId(d.Id);
            if (!ids.Add(d.Id) || d.Operation is not ("upsert" or "delete")) throw new InvalidDataException("Duplicate or unsupported definition operation.");
            if (d.Operation == "delete") { if (d.Xml != "") throw new InvalidDataException("Deletion cannot ignore supplied XML."); continue; }
            var root = NativeMetadataPolicy.Read(d.Xml).Root!;
            if (root.Name != "ExtendedAttribute" || (string?)root.Attribute("Id") != d.Id) throw new InvalidDataException("Definition XML identity mismatch.");
            foreach (var attribute in root.DescendantsAndSelf().Where(e => e.Name == "ExtendedAttribute" || e.Name == "ColumnAttribute"))
            {
                NativeMetadataPolicy.RequireId((string?)attribute.Attribute("Id") ?? "");
                if (!Types.Contains((string?)attribute.Attribute("Type")) || string.IsNullOrWhiteSpace(attribute.Element("Name")?.Value)) throw new InvalidDataException("Attribute requires a native type and name.");
            }
            var definitionIds = root.DescendantsAndSelf().Where(e => e.Name == "ExtendedAttribute" || e.Name == "ColumnAttribute").Select(e => (string?)e.Attribute("Id")).ToArray();
            if (definitionIds.Distinct().Count() != definitionIds.Length) throw new InvalidDataException("Definition and column identities must be distinct.");
            var scopes = root.Element("ElementTypes")?.Elements("AttributeElementType").ToArray() ?? [];
            if (scopes.Length == 0 || scopes.Any(e => string.IsNullOrWhiteSpace((string?)e.Attribute("Type"))) ||
                scopes.Select(e => ((string?)e.Attribute("Type"), (string?)e.Attribute("CustomArtifactTypeId"))).Distinct().Count() != scopes.Length) throw new InvalidDataException("Definition requires distinct explicit element applicability.");
        }
        ids.Clear();
        foreach (var v in patch.Values)
        {
            if (v == null) throw new InvalidDataException("Null value replacement.");
            NativeMetadataPolicy.RequireId(v.DiagramId); NativeMetadataPolicy.RequireId(v.ElementId);
            if (!ids.Add(v.ElementId)) throw new InvalidDataException("Duplicate element value replacement.");
            var root = NativeMetadataPolicy.Read(v.Xml).Root!;
            if (root.Name != "ElementAttributeValues" || (string?)root.Attribute("ElementId") != v.ElementId || root.Element("Values") == null) throw new InvalidDataException("Value XML identity mismatch or missing explicit Values.");
            foreach (var values in root.Descendants().Where(e => e.Name == "Values" || e.Name == "RowValues"))
            {
                var children = values.Elements().ToArray();
                if (children.Select(e => (string?)e.Attribute("Id")).Distinct().Count() != children.Length) throw new InvalidDataException("Duplicate attribute values within a row or element.");
                foreach (var child in children)
                {
                    NativeMetadataPolicy.RequireId((string?)child.Attribute("Id") ?? "");
                    if (!Types.Contains((string?)child.Attribute("Type"))) throw new InvalidDataException("Unknown value type.");
                    if ((string?)child.Attribute("Type") is "FileEmbedded" or "Image" && child.Element("Content") is { Value.Length: > 0 } content)
                    {
                        if (!content.Value.StartsWith("attachment:", StringComparison.Ordinal)) throw new InvalidDataException("Embedded values require attachment:file-name.");
                        RequireFileName(content.Value[11..]);
                    }
                }
            }
        }
        ids.Clear(); long total = 0;
        foreach (var a in patch.Attachments)
        {
            if (a == null) throw new InvalidDataException("Null attachment.");
            NativeMetadataPolicy.RequireId(a.DiagramId); NativeMetadataPolicy.RequireId(a.ElementId); RequireFileName(a.FileName);
            if (!ids.Add(a.DiagramId + "/" + a.ElementId + "/" + a.FileName.ToUpperInvariant()) || a.Operation is not ("upsert" or "delete")) throw new InvalidDataException("Duplicate or unsupported attachment operation.");
            if (a.DataBase64 == null || a.DataBase64.Length > 12 * 1024 * 1024) throw new InvalidDataException("Attachment payload exceeds limit.");
            if (a.Operation == "delete") { if (a.DataBase64 != "") throw new InvalidDataException("Attachment deletion cannot ignore supplied bytes."); continue; }
            byte[] bytes = Convert.FromBase64String(a.DataBase64); total += bytes.Length;
            if (bytes.Length > 8 * 1024 * 1024 || total > 32 * 1024 * 1024) throw new InvalidDataException("Attachment payload exceeds per-file or transaction limit.");
        }
    }

    public static void RequireFileName(string name)
    {
        // Cross-platform validation must still enforce Windows file-name semantics in public CI.
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || name.IndexOfAny("<>:\"/\\|?*".ToCharArray()) >= 0 || name.Any(char.IsControl) || name.EndsWith('.') || name.EndsWith(' '))
            throw new InvalidDataException("Attachment names must be plain Windows file names, not paths.");
        string stem = name.Split('.')[0].ToUpperInvariant();
        if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem)) throw new InvalidDataException("Reserved attachment name.");
    }

    public static string DefinitionContent(string xml)
    {
        var doc = NativeMetadataPolicy.Read(xml);
        doc.Root!.Attribute("ModifiedBy")?.Remove(); doc.Root.Attribute("ModificationDate")?.Remove();
        return doc.ToString();
    }

    // Native persistence relocates embedded attachment paths on every load. Normalize only verified archive-owned bytes.
    public static string ValuesContent(string xml, string diagramId, IReadOnlyDictionary<string, byte[]>? archive = null)
    {
        var doc = NativeMetadataPolicy.Read(xml);
        foreach (var node in doc.Descendants().Where(e => (string?)e.Attribute("Type") is "FileEmbedded" or "Image"))
        {
            if (node.Element("Content") is not { Value.Length: > 0 } content || content.Value.StartsWith("attachment:", StringComparison.Ordinal)) continue;
            if (content.Nodes().Any(n => n is not XText)) throw new InvalidDataException("Embedded reference contains unknown structured content.");
            string owner = (string?)node.Ancestors("ElementAttributeValues").FirstOrDefault()?.Attribute("ElementId") ?? "";
            string[] path = content.Value.Replace('\\', '/').Split('/');
            if (path.Length < 2 || path[^2] != owner || archive == null || !archive.ContainsKey(diagramId + ".diag!/Files/" + owner + "/" + path[^1]))
                throw new InvalidDataException("Embedded reference does not resolve to an archive-owned attachment.");
            content.Value = "attachment:" + path[^1];
        }
        return doc.ToString();
    }

    public static NativeFidelityReport Compare(byte[] before, byte[] after, NativeDocumentationPatch patch, NativeDocumentationSnapshot reopened)
    {
        Validate(patch);
        var left = NativeArchive.ReadEntries(before).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var right = NativeArchive.ReadEntries(after).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);
        void Project(string path) { if (left.TryGetValue(path, out var original)) right[path] = original; else right.Remove(path); }
        foreach (var d in patch.Definitions)
        {
            string path = "Documentation/" + d.Id + ".xml";
            var actual = reopened.Definitions.SingleOrDefault(e => e.Id == d.Id);
            if (d.Operation == "delete")
            {
                if (!left.ContainsKey(path) || right.ContainsKey(path) || actual != null) throw new InvalidDataException("Definition deletion was not verified.");
                right[path] = left[path];
            }
            else
            {
                if (actual == null || !right.TryGetValue(path, out var bytes) || !NativeMetadataPolicy.XmlEquivalent(DefinitionContent(d.Xml), DefinitionContent(actual.Xml)) ||
                    !NativeMetadataPolicy.XmlEquivalent(DefinitionContent(d.Xml), DefinitionContent(Text(bytes)))) throw new InvalidDataException("Definition did not survive native persistence and readback exactly.");
                Project(path);
            }
        }
        foreach (var group in patch.Values.GroupBy(v => v.DiagramId))
        {
            string path = group.Key + ".diag!/ExtendedAttributeValues.xml";
            var a = left.TryGetValue(path, out var oldBytes) ? NativeMetadataPolicy.Read(ValuesContent(Text(oldBytes), group.Key, left)) : new XDocument(new XElement("DiagramAttributeValues"));
            if (!right.TryGetValue(path, out var newBytes)) throw new InvalidDataException("Native values file was not persisted.");
            var b = NativeMetadataPolicy.Read(ValuesContent(Text(newBytes), group.Key, right));
            foreach (var v in group)
            {
                var actual = reopened.Values.Single(e => e.DiagramId == v.DiagramId && e.ElementId == v.ElementId);
                var node = b.Root!.Elements("ElementAttributeValues").Single(e => (string?)e.Attribute("ElementId") == v.ElementId);
                if (!NativeMetadataPolicy.XmlEquivalent(v.Xml, actual.Xml) || !NativeMetadataPolicy.XmlEquivalent(v.Xml, node.ToString())) throw new InvalidDataException("Extended attribute values did not survive native persistence and readback exactly.");
                var previous = a.Root!.Elements("ElementAttributeValues").SingleOrDefault(e => (string?)e.Attribute("ElementId") == v.ElementId);
                if (previous == null) NativeComparisonProjection.RemoveVerifiedNode(node); else node.ReplaceWith(new XElement(previous));
            }
            if (!NativeMetadataPolicy.XmlEquivalent(a.ToString(), b.ToString())) throw new InvalidDataException("Unrequested native attribute values changed.");
            Project(path);
        }
        // Verify all definition-driven order changes, including preserving non-targeted order positions.
        if (patch.Definitions.Length > 0)
            foreach (string path in left.Keys.Union(right.Keys).Where(p => p.StartsWith("Documentation/", StringComparison.Ordinal) && p.EndsWith(".order", StringComparison.Ordinal)).ToArray())
            {
                string type = Path.GetFileNameWithoutExtension(path);
                var original = left.TryGetValue(path, out var bytes) ? NativeMetadataPolicy.Read(Text(bytes)) : null;
                if (!right.TryGetValue(path, out var resulting)) throw new InvalidDataException("Attribute order disappeared.");
                var actual = NativeMetadataPolicy.Read(Text(resulting));
                var expected = original == null ? new XDocument(new XElement("ExtendedAttributeOrder", new XElement("ExtendedAttributes"), new XElement("ElementType", new XAttribute("Type", type)))) : new XDocument(original);
                expected.Root!.Attribute("ModifiedBy")?.Remove(); actual.Root!.Attribute("ModifiedBy")?.Remove();
                foreach (var d in patch.Definitions)
                {
                    var ids = expected.Root.Element("ExtendedAttributes")!;
                    var current = ids.Elements("Id").SingleOrDefault(e => e.Value == d.Id);
                    bool applies = d.Operation != "delete" && NativeMetadataPolicy.Read(d.Xml).Root!.Element("ElementTypes")!.Elements("AttributeElementType").Any(e => (string?)e.Attribute("Type") == type);
                    if (!applies) current?.Remove(); else if (current == null) ids.Add(new XElement("Id", d.Id));
                }
                if (!NativeMetadataPolicy.XmlEquivalent(expected.ToString(), actual.ToString())) throw new InvalidDataException("Unexpected native attribute ordering change.");
                Project(path);
            }
        foreach (var attachment in patch.Attachments)
        {
            string path = attachment.DiagramId + ".diag!/Files/" + attachment.ElementId + "/" + attachment.FileName;
            var actual = reopened.Attachments.SingleOrDefault(a => a.DiagramId == attachment.DiagramId && a.ElementId == attachment.ElementId && a.FileName == attachment.FileName);
            if (attachment.Operation == "delete")
            {
                if (!left.ContainsKey(path) || right.ContainsKey(path) || actual != null) throw new InvalidDataException("Attachment deletion was not verified.");
                right[path] = left[path];
            }
            else
            {
                byte[] wanted = Convert.FromBase64String(attachment.DataBase64);
                if (!right.TryGetValue(path, out var bytes) || !wanted.SequenceEqual(bytes) || actual == null || actual.Length != wanted.Length || actual.Sha256 != BpmnDocument.Revision(wanted)) throw new InvalidDataException("Attachment bytes failed durable native readback verification.");
                Project(path);
            }
        }
        return NativeFidelity.CompareEntries(left, right);
    }
}
