using System.Collections;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static IEnumerable<object> Items(object? value) => value is IEnumerable values ? values.Cast<object>() : Enumerable.Empty<object>();
    private Type DocumentationType(string name) => Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Documentation." + name);
    private Type AttributeDefinitionType() => DocumentationType("ExtendedAttribute`1").MakeGenericType(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementType"));
    private XmlSerializer DocumentationSerializer(Type type) => new(type, new[] { DocumentationType("AttachmentAttributeValue") });

    // XML is bound only to these explicit native documentation types. Unknown fields cannot disappear silently.
    private object ReadDocumentationXml(string xml, Type type)
    {
        var serializer = DocumentationSerializer(type);
        serializer.UnknownAttribute += (_, e) =>
        {
            // The Framework generated reader reports this consumed polymorphic discriminator again.
            // Accept only the exact native attachment subtype already selected by the serializer.
            if (e.Attr.NamespaceURI == "http://www.w3.org/2001/XMLSchema-instance" && e.Attr.LocalName == "type" &&
                e.Attr.Value == "AttachmentAttributeValue" && e.ObjectBeingDeserialized?.GetType() == DocumentationType("AttachmentAttributeValue")) return;
            throw new InvalidDataException("Unknown documentation attribute: " + e.Attr.Name);
        };
        serializer.UnknownElement += (_, e) => throw new InvalidDataException("Unknown documentation element: " + e.Element.Name);
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
        return serializer.Deserialize(reader)!;
    }

    private string WriteDocumentationXml(object value)
    {
        using var text = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        DocumentationSerializer(value.GetType()).Serialize(text, value);
        return text.ToString();
    }

    private string AttachmentFolder(object model, string diagramId, string elementId)
    {
        var special = Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Common.ModelerSpecialFolder"), "Attachments");
        object facade = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.Persistence.IPersistenceUtilFacade");
        string folder = Path.GetFullPath(Path.Combine((string)Call(facade, "GetFolderPath", model, Guid.Parse(diagramId), special)!, elementId));
        if (!folder.StartsWith(Path.Combine(workRoot, "models") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Attachment folder escaped the isolated native model scratch directory.");
        return folder;
    }

    private static IEnumerable<object> AttributeValues(object elementValues)
    {
        IEnumerable<object> Walk(object value)
        {
            yield return value;
            foreach (object row in Items(Get(value, "TableValues")))
                foreach (object cell in Items(row)) foreach (object nested in Walk(cell)) yield return nested;
        }
        return Items(Get(elementValues, "Values")).SelectMany(Walk);
    }

    private void ValidateAttributeValue(object value, object definition)
    {
        if (!Get(definition, "AttributeType").Equals(Get(value, "AttributeType"))) throw new InvalidDataException("Attribute value type does not match its definition.");
        string type = Get(value, "AttributeType").ToString()!;
        var rows = Items(Get(value, "TableValues")).ToArray();
        if (type != "Table" && rows.Length != 0) throw new InvalidDataException("Only table attributes can contain rows.");
        if (type is "FileEmbedded" or "Image" && value.GetType() != DocumentationType("AttachmentAttributeValue"))
            throw new InvalidDataException("Embedded values require the native AttachmentAttributeValue subtype.");
        var columns = Items(Get(definition, "TableColumns")).ToArray();
        foreach (object row in rows)
            foreach (object cell in Items(row))
            {
                object column = columns.SingleOrDefault(c => Get(c, "Id").Equals(Get(cell, "ExtendedAttributeId"))) ?? throw new InvalidDataException("Table cell references an unknown column definition.");
                ValidateAttributeValue(cell, column);
            }
    }

    private NativeDocumentationSnapshot Documentation(object model)
    {
        object holder = Get(model, "ExtendedAttributes");
        var values = new List<NativeAttributeValues>();
        var attachments = new List<NativeAttachmentInfo>();
        foreach (DictionaryEntry pair in (IDictionary)Get(holder, "Values"))
        {
            string diagramId = pair.Key.ToString()!;
            foreach (object element in Items(pair.Value))
            {
                string elementId = Get(element, "ElementId").ToString()!;
                string folder = AttachmentFolder(model, diagramId, elementId);
                var doc = XDocument.Parse(WriteDocumentationXml(element));
                foreach (var node in doc.Descendants().Where(e => (string?)e.Attribute("Type") is "FileEmbedded" or "Image"))
                {
                    var content = node.Element("Content");
                    if (content == null || content.Value.Length == 0) continue;
                    string path = Path.GetFullPath(content.Value);
                    if (!string.Equals(Path.GetDirectoryName(path), folder, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                        throw new InvalidDataException("Native embedded attachment is absent or outside its owning element.");
                    content.Value = "attachment:" + Path.GetFileName(path);
                }
                values.Add(new NativeAttributeValues { DiagramId = diagramId, ElementId = elementId, Xml = doc.ToString() });
                if (!Directory.Exists(folder)) continue;
                foreach (string path in Directory.GetFiles(folder))
                {
                    using var sha = SHA256.Create();
                    using var stream = File.OpenRead(path);
                    attachments.Add(new NativeAttachmentInfo
                    {
                        DiagramId = diagramId,
                        ElementId = elementId,
                        FileName = Path.GetFileName(path),
                        Length = stream.Length,
                        Sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant()
                    });
                }
            }
        }
        return new NativeDocumentationSnapshot
        {
            Definitions = Items(Get(holder, "Definitions")).Select(d => new NativeAttributeDefinition { Id = Get(d, "Id").ToString()!, Xml = WriteDocumentationXml(d) }).ToArray(),
            Values = values.ToArray(),
            Attachments = attachments.ToArray()
        };
    }

    private void EditDocumentation(object model, object persistence, NativeDocumentationPatch patch, Action<string> progress)
    {
        object holder = Get(model, "ExtendedAttributes"), definitions = Get(holder, "Definitions");
        var valueMap = (IDictionary)Get(holder, "Values");
        var affectedOrders = new HashSet<object>();
        var graph = Graph(model).ToDictionary(e => Get(e.Value, "Id").ToString()!, StringComparer.Ordinal);
        // Stage attachment bytes only under the native facade's own scratch folders. Never follow linked references.
        foreach (var attachment in patch.Attachments)
        {
            if (!graph.TryGetValue(attachment.ElementId, out var entry) || entry.DiagramId != attachment.DiagramId) throw new InvalidDataException("Attachment owner is not in the requested diagram.");
            RequireExportLabel(attachment.FileName);
            string folder = AttachmentFolder(model, attachment.DiagramId, attachment.ElementId), path = Path.Combine(folder, attachment.FileName);
            if (attachment.Operation == "delete")
            {
                if (!File.Exists(path)) throw new FileNotFoundException("Attachment deletion requires an existing file.");
                File.Delete(path);
            }
            else { Directory.CreateDirectory(folder); File.WriteAllBytes(path, Convert.FromBase64String(attachment.DataBase64)); }
        }
        progress("native_documentation_definitions");
        foreach (var change in patch.Definitions)
        {
            object? previous = Items(definitions).SingleOrDefault(d => Get(d, "Id").ToString() == change.Id);
            if (previous != null && (bool)Get(previous, "IsGlobal")) throw new NotSupportedException("Global attribute definitions are not local model edits.");
            if (change.Operation == "delete")
            {
                if (previous == null) throw new InvalidDataException("Attribute deletion requires an existing definition.");
                // Values must be explicitly cleared in a preceding durable transaction.
                if (valueMap.Values.Cast<object>().SelectMany(Items).SelectMany(AttributeValues).Any(v => Get(v, "ExtendedAttributeId").ToString() == change.Id))
                    throw new InvalidDataException("Cannot delete a referenced attribute definition; clear values first.");
                Call(persistence, "RemoveExtendedAttribute", model, previous, null);
            }
            else
            {
                object definition = ReadDocumentationXml(change.Xml, AttributeDefinitionType());
                if (Get(definition, "Id").ToString() != change.Id) throw new InvalidDataException("Attribute identity mismatch.");
                if (previous != null) Call(definitions, "Remove", previous);
                Call(definitions, "Add", definition);
                // Preserve prior order positions while rebinding the actual native definition object.
                foreach (object item in Items(Get(holder, "ElementTypes")))
                {
                    var order = (IList)Get(Get(item, "AttributesOrder"), "ExtendedAttributes");
                    int index = previous == null ? -1 : order.IndexOf(previous);
                    bool applies = Items(Get(definition, "ElementTypes")).Any(t => Get(t, "ElementType").Equals(Get(item, "ElementType")) && Equals(Get(t, "CustomArtifactTypeId"), Get(item, "CustomArtifactTypeId")));
                    if (index >= 0) { if (applies) order[index] = definition; else order.RemoveAt(index); }
                    else if (applies) order.Add(definition);
                    if (index >= 0 || applies) affectedOrders.Add(Get(item, "AttributesOrder"));
                }
            }
            if (change.Operation == "delete")
                foreach (object item in Items(Get(holder, "ElementTypes")))
                {
                    object order = Get(item, "AttributesOrder"), attributes = Get(order, "ExtendedAttributes");
                    if (Items(attributes).Any(d => Get(d, "Id").ToString() == change.Id))
                    { Call(attributes, "Remove", Guid.Parse(change.Id)); affectedOrders.Add(order); }
                }
        }
        // Empty order lists also need durable native persistence when applicability is removed.
        foreach (object order in affectedOrders) Call(persistence, "PersistExtendedAttributeOrder", model, order);
        progress("native_documentation_values");
        foreach (var replacement in patch.Values)
        {
            if (!graph.TryGetValue(replacement.ElementId, out var entry) || entry.DiagramId != replacement.DiagramId) throw new InvalidDataException("Extended attribute owner is not in the requested diagram.");
            var doc = XDocument.Parse(replacement.Xml);
            foreach (var node in doc.Descendants().Where(e => (string?)e.Attribute("Type") is "FileEmbedded" or "Image"))
            {
                var content = node.Element("Content");
                if (content == null || content.Value.Length == 0) continue;
                if (!content.Value.StartsWith("attachment:", StringComparison.Ordinal)) throw new InvalidDataException("Embedded values require attachment:file-name references.");
                string name = content.Value.Substring(11); RequireExportLabel(name);
                content.Value = Path.Combine(AttachmentFolder(model, replacement.DiagramId, replacement.ElementId), name);
                if (!File.Exists(content.Value)) throw new FileNotFoundException("Embedded attachment was not supplied or preserved.");
            }
            object element = ReadDocumentationXml(doc.ToString(), DocumentationType("ElementAttributeValues"));
            foreach (object value in Items(Get(element, "Values")))
            {
                object definition = Items(definitions).SingleOrDefault(d => Get(d, "Id").Equals(Get(value, "ExtendedAttributeId"))) ?? throw new InvalidDataException("Unknown extended attribute definition.");
                ValidateAttributeValue(value, definition);
                if (!Items(Get(definition, "ElementTypes")).Any(t => Get(t, "ElementType").Equals(Get(entry.Value, "ElementType")))) throw new InvalidDataException("Attribute definition does not apply to this element type.");
            }
            Guid id = Guid.Parse(replacement.DiagramId);
            if (!valueMap.Contains(id)) valueMap.Add(id, New(DocumentationType("DiagramAttributeValues")));
            var list = (IList)valueMap[id]!;
            object? old = Items(list).SingleOrDefault(e => Get(e, "ElementId").ToString() == replacement.ElementId);
            if (old == null) list.Add(element); else list[list.IndexOf(old)] = element;
        }
        // A definition change must not invalidate values which were not part of the same patch.
        foreach (DictionaryEntry pair in valueMap)
            foreach (object element in Items(pair.Value))
                foreach (object value in Items(Get(element, "Values")))
                {
                    object definition = Items(definitions).SingleOrDefault(d => Get(d, "Id").Equals(Get(value, "ExtendedAttributeId"))) ?? throw new InvalidDataException("A remaining value references a missing definition.");
                    ValidateAttributeValue(value, definition);
                    string owner = Get(element, "ElementId").ToString()!;
                    if (!graph.TryGetValue(owner, out var entry) || !Items(Get(definition, "ElementTypes")).Any(t => Get(t, "ElementType").Equals(Get(entry.Value, "ElementType"))))
                        throw new InvalidDataException("A remaining value no longer has applicable element scope.");
                }
    }
}
