using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Validate the body-copy contract and independently verify the native call-to-container stage.</summary>
public static class NativeInliningPolicy
{
    private static readonly XNamespace Ns = XpdlDocument.Namespace;

    public static void Validate(NativeSubProcessInlining request)
    {
        if (request == null) throw new InvalidDataException("Missing explicit subprocess inlining request.");
        NativeMetadataPolicy.RequireId(request.ElementId); NativeMetadataPolicy.RequireId(request.ExpectedProcessId);
        if (request.Position == null || new[] { request.Position.X, request.Position.Y }.Any(n => !double.IsFinite(n) || n < 0 || n > 1000000))
            throw new InvalidDataException("Inlining requires bounded nonnegative body coordinates.");
    }

    public static NativeSelectionCopyRequest Preflight(byte[] bytes, EngineReply source, NativeSubProcessInlining request)
    {
        Validate(request);
        var call = source.Elements.SingleOrDefault(e => e.Id == request.ElementId);
        var process = source.Elements.SingleOrDefault(e => e.Id == request.ExpectedProcessId);
        if (call?.Kind != "CallActivity" || call.ElementType != "CallActivity" || process?.Kind != "Process" ||
            call.CallReference is not { External: null, BpmnNamespace: "" } reference || reference.CatalogProcessId != process.Id ||
            reference.BpmnName != "" && reference.BpmnName != process.Id)
            throw new InvalidDataException("Inlining requires the expected existing local process binding, not an external or unresolved call.");
        if (NativeExtractionPolicy.MovedIds(source.Elements, process.Id).Contains(call.Id))
            throw new NotSupportedException("Inlining a call inside its own referenced body requires a separate recursive-expansion contract.");
        var selection = new NativeSelectionCopyRequest { SourceDiagramId = process.DiagramId, TargetParentId = call.Id,
            ElementIds = source.Elements.Where(e => e.ParentId == process.Id).Select(e => e.Id).ToArray(),
            Position = JsonSerializer.Deserialize<NativePoint>(JsonSerializer.Serialize(request.Position)) };
        // Reuse the complete selection/reference closure, not a task-only subset.
        var closure = NativeSelectionCopyPolicy.Closure(ExpectedConvertedGraph(source.Elements, request), selection);
        if (source.Metadata == null || source.Documentation == null) throw new InvalidDataException("Inlining requires native metadata and documentation evidence.");
        var references = closure.Append(process).SelectMany(e => new[] { e.Id, e.BpmnId }).Where(id => id != "").ToHashSet(StringComparer.Ordinal);
        foreach (var simulation in source.Metadata.Simulations.Where(s => s.DiagramId == process.DiagramId))
            if (NativeMetadataPolicy.Read(simulation.Xml).Descendants().Attributes().Any(a => a.Name.LocalName == "elementRef" && references.Contains(a.Value)))
                throw new NotSupportedException("Configured source-body simulation inputs require explicit migration; inlining did not change them.");
        var entries = NativeArchive.ReadEntries(bytes);
        if (entries.TryGetValue(process.DiagramId + ".diag!/Actions.xml", out var actions) && Read(actions).Root?.Elements().Any() == true)
            throw new NotSupportedException("Source presentation actions require explicit migration before inlining.");
        // Attributes remain on the same caller identity, but their declared applicability must
        // permit the new native type; retaining bytes alone does not establish editor visibility.
        foreach (var values in source.Documentation.Values.Where(v => v.ElementId == call.Id))
            foreach (var value in NativeMetadataPolicy.Read(values.Xml).Root!.Element("Values")?.Elements() ?? [])
            {
                string id = (string?)value.Attribute("Id") ?? "";
                NativeMetadataPolicy.RequireId(id);
                if (!entries.TryGetValue("Documentation/" + id + ".xml", out var definition) ||
                    !Read(definition).Descendants("AttributeElementType").Any(e => (string?)e.Attribute("Type") == "SubProcess"))
                    throw new InvalidDataException("Caller-owned attributes must explicitly apply to SubProcess before inlining.");
            }
        // Validate exactly the reference that conversion is allowed to retire, including XML
        // unknown to the simplified graph. Arbitrary implementation extensions are not dropped.
        var xml = Read(entries[call.DiagramId + ".diag!/Diagram.xml"]);
        RequireCallImplementation(Owner(xml, "Activity", call.Id), request.ExpectedProcessId);
        return selection;
    }

    public static NativeElement[] ExpectedConvertedGraph(NativeElement[] source, NativeSubProcessInlining request)
    {
        Validate(request);
        var result = JsonSerializer.Deserialize<NativeElement[]>(JsonSerializer.Serialize(source))!;
        var call = result.Single(e => e.Id == request.ElementId);
        if (call.Kind != "CallActivity") throw new InvalidDataException("Inlining source is not a native call.");
        call.Kind = "SubProcess"; call.ElementType = "SubProcess"; call.CallReference = null;
        call.SubProcess = new NativeSubProcessInfo { Kind = "SubProcess" };
        return result;
    }

    public static NativeFidelityReport CompareConversion(byte[] before, byte[] converted, NativeElement[] source,
        NativeElement[] reopened, NativeSubProcessInlining request)
    {
        if (XpdlDocument.CompareGraph(ExpectedConvertedGraph(source, request), reopened).Length != 0)
            throw new InvalidDataException("Inlining conversion changed unrequested graph properties.");
        string diagram = source.Single(e => e.Id == request.ElementId).DiagramId, key = diagram + ".diag!/Diagram.xml";
        var left = NativeArchive.ReadEntries(before);
        var right = NativeArchive.ReadEntries(converted).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var originalXml = Read(left[key]); var actualXml = Read(right[key]);
        var original = Owner(originalXml, "Activity", request.ElementId); var current = Owner(actualXml, "Activity", request.ElementId);
        var implementation = RequireCallImplementation(original, request.ExpectedProcessId);
        if (current.Element(Ns + "Implementation") != null) throw new InvalidDataException("Embedded conversion retained an unexpected implementation selector.");
        var block = current.Elements(Ns + "BlockActivity").SingleOrDefault() ?? throw new InvalidDataException("Missing converted block reference.");
        if (block.Attributes().Count() != 1 || (string?)block.Attribute("ActivitySetId") != request.ElementId || block.Nodes().Any())
            throw new InvalidDataException("Unexpected converted block-reference content.");
        var set = Owner(actualXml, "ActivitySet", request.ElementId);
        if (originalXml.Descendants(Ns + "ActivitySet").Any(e => (string?)e.Attribute("Id") == request.ElementId) ||
            set.Attributes().Count() != 2 || (string?)set.Attribute("Name") != (string?)original.Attribute("Name") ||
            set.Parent?.Name != Ns + "ActivitySets" || set.Parent.Parent?.Name != Ns + "WorkflowProcess")
            throw new InvalidDataException("Converted body is not one new native activity set.");
        // XPDL flattens activity sets under their root process, including nested containers.
        var graph = source.ToDictionary(e => e.Id); string owner = graph[request.ElementId].ParentId;
        var visited = new HashSet<string>();
        while (graph[owner].Kind != "Process")
        { if (!visited.Add(owner)) throw new InvalidDataException("Cyclic inlining owner."); owner = graph[owner].ParentId; }
        if ((string?)set.Parent.Parent.Attribute("Id") != owner) throw new InvalidDataException("Converted body belongs to another process.");
        RequireFormattingOnly(set);
        string[] collections = ["Associations", "Artifacts", "Activities", "Transitions"];
        if (!set.Elements().Select(e => e.Name).SequenceEqual(collections.Select(n => Ns + n)))
            throw new InvalidDataException("Unexpected new body collections.");
        foreach (var collection in set.Elements())
            if (collection.HasAttributes || collection.Nodes().Any()) throw new InvalidDataException("The conversion stage must produce an empty body without unknown payloads.");
        var sets = set.Parent;
        NativeComparisonProjection.RemoveVerifiedNode(set);
        var oldProcess = Owner(originalXml, "WorkflowProcess", owner);
        if (oldProcess.Element(Ns + "ActivitySets") == null && !sets.HasAttributes && !sets.Nodes().Any())
            NativeComparisonProjection.RemoveVerifiedNode(sets);
        block.ReplaceWith(new XElement(implementation));
        // Only an in-memory comparison image is projected. Every other XML atom, binary file,
        // source process and caller remains subject to the existing whole-archive policy.
        right[key] = Encoding.UTF8.GetBytes(actualXml.ToString(SaveOptions.DisableFormatting));
        return NativeFidelity.CompareEntries(left, right);
    }

    private static XElement RequireCallImplementation(XElement call, string processId)
    {
        var implementation = call.Elements(Ns + "Implementation").SingleOrDefault() ?? throw new InvalidDataException("Missing native call implementation.");
        RequireFormattingOnly(implementation);
        var children = implementation.Elements().ToArray();
        if (implementation.HasAttributes || children.Length != 1 || children[0].Name != Ns + "SubFlow" ||
            children[0].Attributes().Count() != 1 || (string?)children[0].Attribute("Id") != processId || children[0].Nodes().Any())
            throw new InvalidDataException("Inlining cannot retire unresolved, external or unknown call implementation content.");
        return implementation;
    }

    private static void RequireFormattingOnly(XElement element)
    {
        if (element.AncestorsAndSelf().Any(e => (string?)e.Attribute(XNamespace.Xml + "space") == "preserve") ||
            element.Nodes().Any(n => n is not XElement && (n is not XText t || !string.IsNullOrWhiteSpace(t.Value))))
            throw new InvalidDataException("Inlining cannot project preserved whitespace, comments or unrepresented text.");
    }
    private static XDocument Read(byte[] bytes) => NativeMetadataPolicy.Read(Encoding.UTF8.GetString(bytes));
    private static XElement Owner(XDocument xml, string kind, string id) => xml.Descendants(Ns + kind)
        .Single(e => (string?)e.Attribute("Id") == id && NativeFidelity.IsNativeNameOwner(e));
}
