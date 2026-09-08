using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

public sealed record NativePresentationPlan(NativePresentationAction[] Actions, NativeAttachmentInfo[] Files,
    IReadOnlyDictionary<string, byte[]> ExpectedEntries);

/// <summary>Explicit native action intent; never launch content or silently rewrite unrepresented records.</summary>
public static class NativePresentationPolicy
{
    public static readonly string[] Types = ["None", "Link", "File", "Image", "Text"];
    public static readonly string[] ValueTypes = ["Normal", "Description", "ExtendedAttribute"];
    private static string Key(NativePresentationAction a) => a.DiagramId + ":" + a.ElementId;
    private static bool Binary(NativePresentationAction a) => a.TypeValue == "Normal" && a.Type is "File" or "Image";
    private static string FileKey(NativePresentationAction a) => a.DiagramId + ".diag!/Actions/" + a.Content[12..];
    public static void Validate(NativePresentationActionChange[] changes)
    {
        if (changes == null || changes.Length is < 1 or > 1000 || changes.Any(c => c?.Action == null)) throw new InvalidDataException("Supply 1-1000 explicit presentation changes.");
        if (changes.Select(c => Key(c.Action)).Distinct().Count() != changes.Length) throw new InvalidDataException("A presentation owner cannot be changed twice in one batch.");
        long total = 0;
        foreach (var c in changes)
        {
            var a = c.Action; NativeMetadataPolicy.RequireId(a.DiagramId); NativeMetadataPolicy.RequireId(a.ElementId);
            if (c.Operation is not "upsert" and not "delete" || !Types.Contains(a.Type) || !ValueTypes.Contains(a.TypeValue)) throw new InvalidDataException("Unknown native presentation operation or type.");
            if (a.Content == null || a.DisplayName == null || a.ExtendedAttributeId == null || c.DataBase64 == null || a.Content.Length > 1024 * 1024 || a.DisplayName.Length > 10000)
                throw new InvalidDataException("Missing or oversized presentation content.");
            if (c.Operation == "delete")
            {
                if (a.Type != "None" || a.TypeValue != "Normal" || a.Content != "" || a.DisplayName != "" || a.ExtendedAttributeId != "" || c.DataBase64 != "")
                    throw new InvalidDataException("Deletion accepts owner identities only.");
                continue;
            }
            if (a.TypeValue == "ExtendedAttribute") NativeMetadataPolicy.RequireId(a.ExtendedAttributeId);
            else if (a.ExtendedAttributeId != "") throw new InvalidDataException("Only an extended-attribute action can contain its reference ID.");
            if (a.TypeValue != "Normal" && a.Content != "") throw new InvalidDataException("Referenced action content is derived, not user-supplied.");
            if (a.TypeValue == "Description" && a.Type != "Text" || a.Type == "None" && (a.TypeValue != "Normal" || a.Content != ""))
                throw new InvalidDataException("Description actions require Text; None actions cannot activate content.");
            if (Binary(a))
            {
                if (!a.Content.StartsWith("action-file:", StringComparison.Ordinal)) throw new InvalidDataException("Normal file/image actions require action-file:name and explicit bytes.");
                NativeDocumentationPolicy.RequireFileName(a.Content[12..]);
                // The installed persistence manager reserves GUID stems for owner-based cleanup.
                if (Guid.TryParse(Path.GetFileNameWithoutExtension(a.Content[12..]), out var stem) && stem.ToString() != a.ElementId)
                    throw new InvalidDataException("A GUID-named action payload must use its own element identity.");
                if (c.DataBase64.Length > 12 * 1024 * 1024) throw new InvalidDataException("Presentation payload exceeds its limit.");
                byte[] bytes = Convert.FromBase64String(c.DataBase64); total += bytes.Length;
                if (bytes.Length > 8 * 1024 * 1024 || total > 32 * 1024 * 1024) throw new InvalidDataException("Presentation payload exceeds file or transaction limit.");
            }
            else if (c.DataBase64 != "") throw new InvalidDataException("Only normal file/image actions accept binary data.");
        }
    }

    public static string Normalize(string xml, string diagram, IReadOnlyDictionary<string, byte[]> entries)
    {
        var doc = NativeMetadataPolicy.Read(xml);
        if (doc.Root?.Name != "DiagramActions") throw new InvalidDataException("Unsupported native action container.");
        foreach (var action in doc.Root.Elements("PresentationAction"))
        {
            var content = action.Element("Content"); if (content == null || content.Value == "") continue;
            bool normal = (string?)action.Attribute("TypeValue") == "Normal" && (string?)action.Attribute("Type") is "File" or "Image";
            bool embedded = false;
            if ((string?)action.Attribute("TypeValue") == "ExtendedAttribute" &&
                entries.TryGetValue("Documentation/" + (string?)action.Attribute("ExtendedAttributeId") + ".xml", out var definition))
            {
                var root = NativeMetadataPolicy.Read(Encoding.UTF8.GetString(definition)).Root;
                embedded = root?.Name == "ExtendedAttribute" && (string?)root.Attribute("Id") == (string?)action.Attribute("ExtendedAttributeId") &&
                    (string?)root.Attribute("Type") is "FileEmbedded" or "Image";
            }
            if (!normal && !embedded) continue;
            if (content.Nodes().Any(n => n is not XText)) throw new InvalidDataException("Presentation file reference contains unrepresented structured content.");
            string owner = (string?)action.Attribute("ElementId") ?? "";
            NativeMetadataPolicy.RequireId(diagram); NativeMetadataPolicy.RequireId(owner);
            string[] parts = content.Value.Replace('\\', '/').Split('/');
            string prefix = normal ? "action-file:" : "attachment:";
            string name;
            if (content.Value.StartsWith(prefix, StringComparison.Ordinal)) name = content.Value[prefix.Length..];
            else
            {
                if (parts.Length < 3 || parts[^2] != (normal ? "Actions" : owner) || parts[^3] != (normal ? diagram : "Files"))
                    throw new InvalidDataException("Presentation payload reference does not identify the expected native owner.");
                name = parts[^1];
            }
            NativeDocumentationPolicy.RequireFileName(name);
            if (!entries.ContainsKey(diagram + ".diag!/" + (normal ? "Actions/" : "Files/" + owner + "/") + name))
                throw new InvalidDataException("Presentation payload is absent from the native archive.");
            content.Value = prefix + name;
        }
        return doc.ToString();
    }

    private static NativePresentationAction ReadAction(XElement node, string diagram)
    {
        if (node.Attributes().Any(a => a.Name.Namespace != XNamespace.None || !new[] { "ElementId", "Type", "TypeValue", "ExtendedAttributeId" }.Contains(a.Name.LocalName)) ||
            node.Nodes().Any(n => n is not XElement && (n is not XText t || !string.IsNullOrWhiteSpace(t.Value))) ||
            node.Elements().Any(e => e.Name != "DisplayName" && e.Name != "Content" || e.HasAttributes || e.Nodes().Any(n => n is not XText)) ||
            node.Elements().GroupBy(e => e.Name).Any(g => g.Count() > 1)) throw new InvalidDataException("Unrepresented native action fields cannot be replaced safely.");
        return new() { DiagramId = diagram, ElementId = (string?)node.Attribute("ElementId") ?? "", Type = (string?)node.Attribute("Type") ?? "None",
            TypeValue = (string?)node.Attribute("TypeValue") ?? "Normal", ExtendedAttributeId = (string?)node.Attribute("ExtendedAttributeId") ?? "",
            DisplayName = node.Element("DisplayName")?.Value ?? "", Content = node.Element("Content")?.Value ?? "" };
    }
    private static XElement Xml(NativePresentationAction a) => new("PresentationAction", new XAttribute("ElementId", a.ElementId), new XAttribute("Type", a.Type),
        new XAttribute("TypeValue", a.TypeValue), a.ExtendedAttributeId == "" ? null : new XAttribute("ExtendedAttributeId", a.ExtendedAttributeId),
        new XElement("DisplayName", a.DisplayName), new XElement("Content", a.Content));

    public static NativePresentationPlan Prepare(byte[] bytes, EngineReply source, NativePresentationActionChange[] changes)
    {
        Validate(changes);
        var entries = NativeArchive.ReadEntries(bytes).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var docs = source.Elements.Where(e => e.Kind == "Collaboration").ToDictionary(e => e.Id, e =>
            entries.TryGetValue(e.Id + ".diag!/Actions.xml", out var xml) ? NativeMetadataPolicy.Read(Normalize(Encoding.UTF8.GetString(xml), e.Id, entries)) : new XDocument(new XElement("DiagramActions")));
        var actions = docs.SelectMany(p => p.Value.Root!.Elements("PresentationAction").Select(e => ReadAction(e, p.Key))).ToDictionary(Key);
        if (actions.Values.Any(a => Binary(a) && !a.Content.StartsWith("action-file:", StringComparison.Ordinal)))
            throw new InvalidDataException("An existing binary action lacks a represented archive-owned payload.");
        Verify(actions.Values.ToArray(), null, source.Presentation ?? throw new InvalidDataException("Missing native action source observation."));
        var previousFiles = actions.Values.Where(Binary).Select(FileKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (string diagram in changes.Select(c => c.Action.DiagramId).Distinct())
        {
            if (!docs.TryGetValue(diagram, out var document)) throw new InvalidDataException("Presentation diagram is absent.");
            var root = document.Root!;
            // Formatting between records must not become new literal content when deleting the last record.
            // Explicit xml:space preservation remains exact and can still reject an incompatible native save.
            if (root.HasElements && (string?)root.Attribute(XNamespace.Xml + "space") != "preserve")
                root.Nodes().OfType<XText>().Where(t => string.IsNullOrWhiteSpace(t.Value)).Remove();
        }
        foreach (var change in changes)
        {
            var a = JsonSerializer.Deserialize<NativePresentationAction>(JsonSerializer.Serialize(change.Action))!;
            if (!source.Elements.Any(e => e.Id == a.ElementId && e.DiagramId == a.DiagramId)) throw new InvalidDataException("Presentation owner is absent from its explicit diagram.");
            var container = docs[a.DiagramId].Root!; var old = container.Elements("PresentationAction").SingleOrDefault(e => (string?)e.Attribute("ElementId") == a.ElementId);
            if (change.Operation == "delete")
            { if (old == null) throw new InvalidDataException("Presentation action to delete is absent."); old.Remove(); actions.Remove(Key(a)); continue; }
            if (a.TypeValue == "Description") a.Content = source.Elements.Single(e => e.Id == a.ElementId).Documentation;
            if (a.TypeValue == "ExtendedAttribute")
            {
                var metadata = source.Documentation ?? throw new InvalidDataException("Missing native documentation for action reference.");
                var definition = metadata.Definitions.SingleOrDefault(d => d.Id == a.ExtendedAttributeId) ?? throw new InvalidDataException("Referenced native attribute definition is absent.");
                string kind = (string?)NativeMetadataPolicy.Read(definition.Xml).Root?.Attribute("Type") ?? "";
                bool valid = a.Type switch { "Text" => kind is "Text" or "LongText" or "Number" or "Date", "Link" => kind == "Link", "File" => kind is "FileEmbedded" or "FileLinked", "Image" => kind == "Image", _ => false };
                if (!valid) throw new InvalidDataException("Presentation type does not match the referenced attribute type.");
                var values = metadata.Values.SingleOrDefault(v => v.DiagramId == a.DiagramId && v.ElementId == a.ElementId) ?? throw new InvalidDataException("Referenced attribute owner has no native values.");
                var value = NativeMetadataPolicy.Read(values.Xml).Root!.Element("Values")!.Elements().SingleOrDefault(e => (string?)e.Attribute("Id") == a.ExtendedAttributeId)
                    ?? throw new InvalidDataException("Referenced native attribute value is absent.");
                a.Content = value.Element("Content")?.Value ?? "";
            }
            if (Binary(a))
            {
                string path = FileKey(a); byte[] data = Convert.FromBase64String(change.DataBase64);
                if (entries.TryGetValue(path, out var existing) && !existing.AsSpan().SequenceEqual(data) &&
                    (!actions.Values.Any(b => Binary(b) && FileKey(b).Equals(path, StringComparison.OrdinalIgnoreCase) && b.ElementId == a.ElementId) ||
                    actions.Values.Any(b => Binary(b) && FileKey(b).Equals(path, StringComparison.OrdinalIgnoreCase) && b.ElementId != a.ElementId)))
                    throw new InvalidDataException("Presentation payload would overwrite unrelated or shared native content.");
                entries[path] = data;
            }
            if (old == null) container.Add(Xml(a)); else old.ReplaceWith(Xml(a)); actions[Key(a)] = a;
        }
        foreach (string path in previousFiles)
            if (!actions.Values.Any(a => Binary(a) && FileKey(a).Equals(path, StringComparison.OrdinalIgnoreCase))) entries.Remove(path);
        foreach (var diagram in changes.Select(c => c.Action.DiagramId).Distinct()) entries[diagram + ".diag!/Actions.xml"] = Encoding.UTF8.GetBytes(docs[diagram].ToString());
        var files = actions.Values.Where(Binary).Select(a => new NativeAttachmentInfo { DiagramId = a.DiagramId, ElementId = a.ElementId,
            FileName = a.Content[12..], Length = entries[FileKey(a)].Length, Sha256 = BpmnDocument.Revision(entries[FileKey(a)]) }).ToArray();
        return new(actions.Values.ToArray(), files, entries);
    }

    private static void Verify(NativePresentationAction[] expected, NativeAttachmentInfo[]? files, NativePresentationSnapshot actual)
    {
        string Stable<T>(IEnumerable<T> values) => JsonSerializer.Serialize(values);
        if (Stable(expected.OrderBy(Key)) != Stable(actual.Actions.OrderBy(Key))) throw new InvalidDataException("Native presentation action fields differ from expected intent.");
        if (files != null && Stable(files.OrderBy(f => f.DiagramId + f.ElementId)) != Stable(actual.Files.OrderBy(f => f.DiagramId + f.ElementId)))
            throw new InvalidDataException("Native presentation payload readback differs from expected bytes.");
    }

    public static NativeFidelityReport Compare(byte[] after, NativePresentationPlan plan, EngineReply edited, EngineReply reopened)
    {
        Verify(plan.Actions, plan.Files, edited.Presentation ?? throw new InvalidDataException("Missing native presentation editor evidence."));
        Verify(plan.Actions, plan.Files, reopened.Presentation ?? throw new InvalidDataException("Missing native presentation restart evidence."));
        return NativeFidelity.CompareEntries(plan.ExpectedEntries, NativeArchive.ReadEntries(after));
    }
}
