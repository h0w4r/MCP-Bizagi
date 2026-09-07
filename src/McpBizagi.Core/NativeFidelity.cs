using System.Xml;
using System.Xml.Linq;

namespace McpBizagi.Core;

public sealed record ExpectedNativeName(string ElementId, string Name);
public sealed record NativeDifference(string Entry, string Location, string Classification, string? ElementId, string? Before, string? After);
public sealed record NativeFidelityReport(bool Preserved, int OriginalEntries, int ResultingEntries, int CheckedAtoms, NativeDifference[] Differences)
{
    public string Policy => "native-v5-content-with-explicit-engine-metadata-v2";
}

/// <summary>Checks the entire native container, including unknown XML, nested diagram archives, and binary attachments.</summary>
public static class NativeFidelity
{
    private sealed record Atom(string Value, string? ElementId, string? Property, string? Metadata = null, bool Audit = false, bool ImplicitDefault = false, string? RuntimePath = null);
    private sealed record RuntimePathMarker(string Value);
    private sealed class DefaultMarker;
    private const string Xpdl = "http://www.wfmc.org/2009/XPDL2.2";

    public static NativeFidelityReport Compare(byte[] before, byte[] after, IReadOnlyList<ExpectedNativeName>? names = null)
    {
        var left = NativeArchive.ReadEntries(before); var right = NativeArchive.ReadEntries(after);
        return CompareEntries(left, right, names);
    }

    internal static NativeFidelityReport CompareEntries(IReadOnlyDictionary<string, byte[]> left, IReadOnlyDictionary<string, byte[]> right,
        IReadOnlyList<ExpectedNativeName>? names = null)
    {
        var differences = new List<NativeDifference>();
        int checkedAtoms = 0;
        foreach (string entry in left.Keys.Union(right.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            bool had = left.TryGetValue(entry, out var original), has = right.TryGetValue(entry, out var resulting);
            if (!had || !has)
            {
                differences.Add(new(entry, "/", had ? "entry_removed" : "entry_added", null,
                    had ? BpmnDocument.Revision(original!) : null, has ? BpmnDocument.Revision(resulting!) : null));
                continue;
            }
            if (!entry.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || entry.Contains(".diag!/Files/", StringComparison.OrdinalIgnoreCase))
            {
                checkedAtoms++;
                if (!original!.AsSpan().SequenceEqual(resulting))
                    differences.Add(new(entry, "/", "binary_changed", null, BpmnDocument.Revision(original!), BpmnDocument.Revision(resulting!)));
                continue;
            }
            var a = Atoms(original!, entry, left); var b = Atoms(resulting!, entry, right);
            foreach (string location in a.Keys.Union(b.Keys).Order(StringComparer.Ordinal))
            {
                checkedAtoms++;
                a.TryGetValue(location, out var x); b.TryGetValue(location, out var y);
                if (x?.Value == y?.Value)
                {
                    if (x?.ImplicitDefault == true && y?.ImplicitDefault == false)
                        differences.Add(new(entry, location, "transparent_text_background_materialized", y.ElementId, "implicit transparent", y.Value));
                    if (x?.RuntimePath != y?.RuntimePath && x?.RuntimePath != null && y?.RuntimePath != null)
                        differences.Add(new(entry, location, "attachment_runtime_path_relocated", y.ElementId, x.RuntimePath, y.RuntimePath));
                    continue;
                }
                string classification = "unexpected_xml_change";
                if (x != null && y != null && x.ElementId == y.ElementId && x.Property == "Name" && y.Property == "Name" &&
                    names?.Any(n => n.ElementId == y.ElementId && n.Name == y.Value) == true) classification = "requested_name_change";
                else if (x?.Metadata != null && y?.Metadata == x.Metadata && DateTimeOffset.TryParse(x.Value, out _) && DateTimeOffset.TryParse(y.Value, out _)) classification = y.Metadata;
                else if (x == null && y?.Audit == true) classification = "modification_audit_appended";
                differences.Add(new(entry, location, classification, y?.ElementId ?? x?.ElementId, x?.Value, y?.Value));
            }
        }
        // Unknown additions/removals are never silently excused by a simplified object model.
        bool preserved = differences.All(d => d.Classification is "requested_name_change" or "modification_timestamp" or "engine_process_timestamp" or "modification_audit_appended" or "transparent_text_background_materialized" or "attachment_runtime_path_relocated");
        return new(preserved, left.Count, right.Count, checkedAtoms, differences.ToArray());
    }

    private static Dictionary<string, Atom> Atoms(byte[] bytes, string entry, IReadOnlyDictionary<string, byte[]> archive)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = BpmnDocument.MaxXmlCharacters
        });
        var doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        var result = new Dictionary<string, Atom>(StringComparer.Ordinal);
        if (doc.Root == null) throw new InvalidDataException("Native XML has no document element.");
        if (entry.EndsWith(".diag!/ExtendedAttributeValues.xml", StringComparison.OrdinalIgnoreCase) && doc.Root.Name == "DiagramAttributeValues")
        {
            // The native loader relocates only embedded files. Linked files and arbitrary paths remain exact.
            string diagramId = entry[..entry.IndexOf(".diag!/", StringComparison.OrdinalIgnoreCase)];
            var normalized = NativeMetadataPolicy.Read(NativeDocumentationPolicy.ValuesContent(doc.ToString(), diagramId, archive));
            var sources = doc.Descendants().ToArray(); var targets = normalized.Descendants().ToArray();
            for (int i = 0; i < sources.Length; i++)
                if (sources[i].Name == "Content" && sources[i].Value != targets[i].Value)
                { string original = sources[i].Value; sources[i].Value = targets[i].Value; sources[i].AddAnnotation(new RuntimePathMarker(original)); }
        }
        bool nativeDiagram = entry.EndsWith(".diag!/Diagram.xml", StringComparison.OrdinalIgnoreCase) && doc.Root.Name == XName.Get("Package", Xpdl);
        if (nativeDiagram)
        {
            // The installed XPDL loaders map an absent text background to Color.Transparent (ARGB 16777215).
            // Normalize only this proven default in memory; never rewrite the native archive here.
            foreach (var graphics in doc.Descendants().Where(e => e.Name == XName.Get("NodeGraphicsInfo", Xpdl) || e.Name == XName.Get("ConnectorGraphicsInfo", Xpdl)))
            {
                if (graphics.Element(XName.Get("TextBackgroundColor", Xpdl)) != null) continue;
                var color = new XElement(XName.Get("TextBackgroundColor", Xpdl), "16777215"); color.AddAnnotation(new DefaultMarker());
                var coordinates = graphics.Element(XName.Get("Coordinates", Xpdl));
                if (graphics.Name.LocalName == "ConnectorGraphicsInfo" && coordinates != null) coordinates.AddBeforeSelf(color);
                else graphics.Add(color);
            }
        }
        Walk(doc.Root, "", null, result, nativeDiagram, entry.Equals("ModelInfo.xml", StringComparison.OrdinalIgnoreCase));
        if (entry.StartsWith("Documentation/", StringComparison.Ordinal) && doc.Root.Name == "ExtendedAttribute" &&
            Guid.TryParse(Path.GetFileNameWithoutExtension(entry), out var definitionId) && (string?)doc.Root.Attribute("Id") == definitionId.ToString() &&
            result.TryGetValue("/ExtendedAttribute/@ModificationDate", out var stamp))
            result["/ExtendedAttribute/@ModificationDate"] = stamp with { Metadata = "modification_timestamp" };
        int outsideIndex = 0;
        foreach (var node in doc.Nodes().Where(n => n != doc.Root && n is not XText))
            result.Add("/#document[" + outsideIndex++ + "]", new(node.ToString(), null, null));
        return result;
    }

    private static void Walk(XElement element, string path, string? parentId, Dictionary<string, Atom> atoms, bool nativeDiagram, bool modelInfo)
    {
        string? id = (string?)element.Attribute("Id") ?? (string?)element.Attribute("id") ?? parentId;
        string location = path + "/" + element.Name;
        bool defaulted = element.Annotation<DefaultMarker>() != null;
        bool audit = nativeDiagram && element.Name == XName.Get("Modification", Xpdl) && element.Parent?.Name == XName.Get("Modifications", Xpdl)
            && element.Parent.Parent?.Name == XName.Get("PackageHeader", Xpdl) && !element.HasElements
            && element.Attributes().All(a => a.Name == "Date" || a.Name == "UserName")
            && DateTimeOffset.TryParse((string?)element.Attribute("Date"), out _) && !string.IsNullOrEmpty((string?)element.Attribute("UserName"));
        atoms.Add(location + "/#element", new(element.Name.ToString(), id, null, Audit: audit, ImplicitDefault: defaulted));
        // Unknown XML may contain QName-valued attributes. Conservatively retain namespace bindings rather than guessing which text is a QName.
        foreach (var declaration in element.Attributes().Where(a => a.IsNamespaceDeclaration))
            atoms.Add(location + "/#namespace/" + declaration.Name.LocalName, new(declaration.Value, id, null));
        foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
            atoms.Add(location + "/@" + attribute.Name, new(attribute.Value, id,
                attribute.Name.Namespace == XNamespace.None && (attribute.Name != "Name" || nativeDiagram && IsNativeNameOwner(element)) ? attribute.Name.LocalName : null,
                (modelInfo && element.Name == "BizAgiModelInfo" && attribute.Name == "ModifiedDate") || (audit && attribute.Name == "Date") ? "modification_timestamp" : null, audit));
        string? metadata = nativeDiagram && element.Name == XName.Get("ModificationDate", Xpdl) && element.Parent?.Name == XName.Get("PackageHeader", Xpdl) ? "modification_timestamp"
            : nativeDiagram && element.Name == XName.Get("Created", Xpdl) && element.Parent?.Name == XName.Get("ProcessHeader", Xpdl)
                && element.Parent.Parent?.Name == XName.Get("WorkflowProcess", Xpdl) ? "engine_process_timestamp" : null;
        bool preserveSpace = element.AncestorsAndSelf().Select(e => (string?)e.Attribute(XNamespace.Xml + "space")).FirstOrDefault(v => v != null) == "preserve";
        var children = element.Nodes().Where(n => preserveSpace || !element.HasElements || n is not XText text || !string.IsNullOrWhiteSpace(text.Value)).ToArray();
        for (int i = 0; i < children.Length; i++)
        {
            // Retain child ordering and non-whitespace content. Attribute ordering is not a semantic change.
            if (children[i] is XElement child) Walk(child, location + "/[" + i + "]", id, atoms, nativeDiagram, modelInfo);
            else atoms.Add(location + "/#node[" + i + "]", new(children[i] is XText text ? text.Value : children[i].ToString(), id, null, metadata, ImplicitDefault: defaulted, RuntimePath: element.Annotation<RuntimePathMarker>()?.Value));
        }
    }
    private static bool IsNativeNameOwner(XElement element)
    {
        if (element.Name.NamespaceName != Xpdl || element.Attribute("Id") == null) return false;
        if (element.Name.LocalName == "Package") return element.Parent == null;
        string? collection = element.Name.LocalName switch
        {
            "Activity" => "Activities", "Transition" => "Transitions", "Pool" => "Pools", "Lane" => "Lanes",
            "Artifact" => "Artifacts", "MessageFlow" => "MessageFlows", "WorkflowProcess" => "WorkflowProcesses",
            "ActivitySet" => "ActivitySets", "Milestone" => "Milestones", "DataObject" => "DataObjects", "DataStoreReference" => "DataStoreReferences",
            _ => null
        };
        if (collection == null || element.Parent?.Name != XName.Get(collection, Xpdl)) return false;
        var container = element.Parent.Parent;
        return element.Name.LocalName switch
        {
            "Pool" or "WorkflowProcess" or "MessageFlow" => IsContainer(container, "Package"),
            "Lane" or "Milestone" => IsContainer(container, "Pool"),
            "ActivitySet" => IsContainer(container, "WorkflowProcess"),
            "Artifact" => IsContainer(container, "Package") || IsContainer(container, "ActivitySet") || IsContainer(container, "WorkflowProcess"),
            _ => IsContainer(container, "WorkflowProcess") || IsContainer(container, "ActivitySet")
        };
    }
    private static bool IsContainer(XElement? element, string kind)
    {
        if (element?.Name != XName.Get(kind, Xpdl)) return false;
        // Exact native containment prevents an unknown extension from impersonating an Activities list.
        if (kind == "Package") return element.Parent == null;
        string collection = kind switch { "WorkflowProcess" => "WorkflowProcesses", "ActivitySet" => "ActivitySets", "Pool" => "Pools", _ => "" };
        return collection != "" && element.Parent?.Name == XName.Get(collection, Xpdl) &&
            IsContainer(element.Parent.Parent, kind == "ActivitySet" ? "WorkflowProcess" : "Package");
    }
}
