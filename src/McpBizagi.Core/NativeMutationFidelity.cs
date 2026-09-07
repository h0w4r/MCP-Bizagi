using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        var collections = new List<NativeDifference>();
        foreach (string entry in left.Keys.Intersect(right.Keys, StringComparer.OrdinalIgnoreCase).Where(p => p.EndsWith(".diag!/Diagram.xml", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            var a = Read(left[entry]); var b = Read(right[entry]);
            if (a.Root?.Name != Xpdl + "Package" || b.Root?.Name != Xpdl + "Package") continue;
            NativeSemanticPolicy.ProjectDerivedQuantities(a, b, changes, reopened);
            var removeLeft = new HashSet<XElement>(); var removeRight = new HashSet<XElement>();
            foreach (var c in changes)
            {
                var x = Identified(a, c.ElementId); var y = Identified(b, c.ElementId);
                coverage[c.ElementId] += c.Operation == "create" ? y.Length : x.Length;
                if (c.Operation == "create")
                {
                    if (x.Length != 0) throw new InvalidDataException("Created identity already existed in the native container.");
                    foreach (var element in y)
                    {
                        removeRight.Add(element);
                        var companion = Companion(b, element);
                        if (companion != null)
                        {
                            string id = (string)companion.Attribute("Id")!;
                            if (a.Descendants(companion.Name).Any(e => (string?)e.Attribute("Id") == id))
                                throw new InvalidDataException("Created container companion identity already existed.");
                            if (element.Name == Xpdl + "Pool" && id != c.ProcessId)
                                throw new InvalidDataException("Persisted pool process differs from the explicit process identity.");
                            removeRight.Add(companion);
                        }
                    }
                    continue;
                }
                if (c.Operation == "delete")
                {
                    foreach (var element in x)
                    {
                        removeLeft.Add(element);
                        var companion = Companion(a, element);
                        if (companion != null) removeLeft.Add(companion);
                    }
                    continue;
                }
                if (x.Length == 0 && y.Length == 0) continue;
                if (x.Length != 1 || y.Length != 1) throw new InvalidDataException("Ambiguous native XML mutation identity.");
                if (c.CallTarget != null) NativeCallFidelity.ProjectTarget(x[0], y[0], c.CallTarget);
                if (c.ActivityProperties != null || c.FlowCondition != null || c.GatewayDirection != null) NativeSemanticPolicy.Project(x[0], y[0], c);
                var oldCompanion = Companion(a, x[0]); var newCompanion = Companion(b, y[0]);
                if ((string?)oldCompanion?.Attribute("Id") != (string?)newCompanion?.Attribute("Id"))
                    throw new InvalidDataException("A property update cannot replace its native container companion.");
                if (c.Documentation != null && x[0].Name == Xpdl + "Pool")
                {
                    RestoreText(oldCompanion?.Element(Xpdl + "ProcessHeader")?.Element(Xpdl + "Description"),
                        newCompanion?.Element(Xpdl + "ProcessHeader")?.Element(Xpdl + "Description"), c.Documentation);
                    RestoreRuntimeDescription(oldCompanion!, newCompanion!, c.Documentation);
                }
                if (c.Name != null && x[0].Name == Xpdl + "Pool" && (string?)newCompanion?.Attribute("Name") == c.Name)
                    newCompanion!.SetAttributeValue("Name", (string?)oldCompanion?.Attribute("Name"));
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
                    if (oldGraphics.Attribute("Expanded") != null || newGraphics.Attribute("Expanded") != null)
                        RestoreAttribute(oldGraphics, newGraphics, "Expanded", g.Expanded ? "true" : "false");
                    if (x[0].Element(Xpdl + "BlockActivity") is { } oldBlock && y[0].Element(Xpdl + "BlockActivity") is { } newBlock)
                    {
                        // The native XPDL serializer omits the default COLLAPSED enum value.
                        if (((string?)newBlock.Attribute("View") ?? "COLLAPSED") != (g.Expanded ? "EXPANDED" : "COLLAPSED"))
                            throw new InvalidDataException("Native subprocess view differs from the explicit geometry request.");
                        newBlock.SetAttributeValue("View", (string?)oldBlock.Attribute("View"));
                    }
                    if (c.ExpandedSize is { } size)
                    {
                        RestoreNumber(oldGraphics, newGraphics, "ExpandedWidth", size.Width);
                        RestoreNumber(oldGraphics, newGraphics, "ExpandedHeight", size.Height);
                    }
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
            // Defer removals until every identity has been checked. Parent-first creation and child-first
            // deletion batches must not hide subsequent members from the fidelity comparison.
            var leftParents = removeLeft.Select(e => e.Parent).OfType<XElement>().Distinct().ToArray();
            var rightParents = removeRight.Select(e => e.Parent).OfType<XElement>().Distinct().ToArray();
            RemoveContainers(removeLeft, changes, "delete"); RemoveContainers(removeRight, changes, "create");
            // The serializer omits some empty structural lists, then materializes them when their first
            // requested child is inserted. Only lists touched by verified child projection are eligible.
            ProjectEmptyCollections(leftParents, b, entry, collections);
            ProjectEmptyCollections(rightParents, a, entry, collections);
            left[entry] = Encoding.UTF8.GetBytes(a.ToString(SaveOptions.DisableFormatting));
            right[entry] = Encoding.UTF8.GetBytes(b.ToString(SaveOptions.DisableFormatting));
        }
        if (coverage.Values.Any(count => count != 1)) throw new InvalidDataException("Every mutation must address exactly one supported native XML identity.");
        var names = changes.Where(c => c.Name != null).Select(c => new ExpectedNativeName(c.ElementId, c.Name!)).ToArray();
        var report = NativeFidelity.CompareEntries(left, right, names);
        // Keep a visible record that authorized intent was projected; callers also retain the full request and readback.
        return report with
        {
            Differences = report.Differences.Concat(collections).Concat(changes.Select(c => new NativeDifference("request", c.Operation,
            "verified_requested_mutation", c.ElementId, null, "fresh-worker postconditions verified"))).ToArray()
        };
    }
    private static XElement[] Identified(XDocument doc, string id) => doc.Descendants().Where(e => NativeFidelity.IsNativeNameOwner(e) &&
        (string?)e.Attribute("Id") == id && new[] { "Activity", "Transition", "Pool", "Lane", "Milestone", "Artifact", "MessageFlow" }.Contains(e.Name.LocalName)).ToArray();

    private static void ProjectEmptyCollections(XElement[] candidates, XDocument peer, string entry, List<NativeDifference> evidence)
    {
        bool Empty(XElement element) => !element.HasAttributes && element.Nodes().All(n => n is XText t && string.IsNullOrWhiteSpace(t.Value)) &&
            element.AncestorsAndSelf().Select(e => (string?)e.Attribute(XNamespace.Xml + "space")).FirstOrDefault(v => v != null) != "preserve";
        foreach (var collection in candidates)
        {
            var owner = collection.Parent;
            if (collection.Document == null || owner == null || !NativeFidelity.IsNativeNameOwner(owner) || collection.Name.Namespace != Xpdl || !Empty(collection)) continue;
            string[] allowed = owner.Name.LocalName switch
            {
                "Package" => ["Pools", "WorkflowProcesses", "MessageFlows", "Artifacts"],
                "WorkflowProcess" => ["Activities", "Transitions", "ActivitySets", "Artifacts"],
                "ActivitySet" => ["Activities", "Transitions", "Artifacts"],
                "Pool" => ["Lanes", "Milestones"],
                _ => []
            };
            if (!allowed.Contains(collection.Name.LocalName) || owner.Elements(collection.Name).Count() != 1) continue;
            var owners = peer.Descendants().Where(e => e.Name == owner.Name && (string?)e.Attribute("Id") == (string?)owner.Attribute("Id") && NativeFidelity.IsNativeNameOwner(e)).ToArray();
            if (owners.Length != 1) continue;
            var opposite = owners[0].Elements(collection.Name).ToArray();
            if (opposite.Length > 1 || opposite.Length == 1 && !Empty(opposite[0])) continue;
            // Attributes, namespace declarations, comments, unknown children, meaningful text and
            // xml:space=preserve remain outside this narrow structural-list projection.
            evidence.Add(new(entry, collection.Name.LocalName, "verified_empty_collection_projection", (string?)owner.Attribute("Id"), "verified child lifecycle", "empty structural wrapper only"));
            collection.Remove(); if (opposite.Length == 1) opposite[0].Remove();
        }
    }
    private static XElement? Companion(XDocument doc, XElement owner)
    {
        // Bizagi persists pools/processes and embedded subprocesses/activity sets as linked structures.
        // Follow the actual reference, never a same-name heuristic or arbitrary XML subtree.
        bool pool = owner.Name == Xpdl + "Pool";
        var block = owner.Name == Xpdl + "Activity" ? owner.Element(Xpdl + "BlockActivity") : null;
        if (!pool && block == null) return null;
        string? id = pool ? (string?)owner.Attribute("Process") : (string?)block!.Attribute("ActivitySetId");
        string kind = pool ? "WorkflowProcess" : "ActivitySet";
        if (id == null || !pool && id != (string?)owner.Attribute("Id")) throw new InvalidDataException("Invalid native container reference.");
        var matches = doc.Descendants(Xpdl + kind).Where(e => (string?)e.Attribute("Id") == id).ToArray();
        if (matches.Length != 1 || (pool ? doc.Descendants(Xpdl + "Pool").Count(e => (string?)e.Attribute("Process") == id)
            : doc.Descendants(Xpdl + "BlockActivity").Count(e => (string?)e.Attribute("ActivitySetId") == id)) != 1)
            throw new InvalidDataException("Native container companion must have one identity and one owner.");
        if (!pool && owner.Ancestors(Xpdl + "WorkflowProcess").FirstOrDefault() != matches[0].Ancestors(Xpdl + "WorkflowProcess").FirstOrDefault())
            throw new InvalidDataException("Embedded subprocess companion escaped its workflow process.");
        return matches[0];
    }
    private static void RemoveContainers(HashSet<XElement> removals, NativeMutation[] changes, string operation)
    {
        var intended = changes.Where(c => c.Operation == operation).Select(c => c.ElementId).ToHashSet(StringComparer.Ordinal);
        foreach (var root in removals)
            foreach (var child in root.Descendants().Where(e => e.Name.Namespace == Xpdl &&
                new[] { "Activity", "Transition", "Pool", "Lane", "Milestone", "Artifact", "MessageFlow", "ActivitySet" }.Contains(e.Name.LocalName)))
                if (!intended.Contains((string?)child.Attribute("Id") ?? ""))
                    throw new InvalidDataException("Container projection would conceal an unrequested child identity.");
        foreach (var element in removals.OrderByDescending(e => e.Ancestors().Count())) element.Remove();
    }
    private static void RestoreText(XElement? before, XElement? after, string expected)
    {
        if (before != null && after != null && !before.HasElements && !after.HasElements && after.Value == expected &&
            before.Nodes().All(n => n is XText) && after.Nodes().All(n => n is XText))
            after.ReplaceNodes(before.Nodes().Select(n => new XText(((XText)n).Value)));
    }
    private static void RestoreRuntimeDescription(XElement before, XElement after, string expected)
    {
        XElement[] Runtime(XElement process) => process.Element(Xpdl + "ExtendedAttributes")?.Elements(Xpdl + "ExtendedAttribute")
            .Where(e => (string?)e.Attribute("Name") == "RuntimeProperties").ToArray() ?? [];
        var a = Runtime(before); var b = Runtime(after);
        if (a.Length == 0 && b.Length == 0) return;
        if (a.Length != 1 || b.Length != 1) throw new InvalidDataException("Ambiguous process runtime metadata.");
        string left = (string?)a[0].Attribute("Value") ?? "", right = (string?)b[0].Attribute("Value") ?? "";
        // Preserve every unrequested JSON leaf, including exact numeric encodings and array order.
        // Duplicate keys, unknown fields and malformed JSON cannot disappear behind this projection.
        var options = new JsonDocumentOptions { AllowDuplicateProperties = false };
        using var oldDoc = JsonDocument.Parse(left, options); using var newDoc = JsonDocument.Parse(right, options);
        var oldNode = JsonNode.Parse(oldDoc.RootElement.GetRawText())!;
        var newNode = JsonNode.Parse(newDoc.RootElement.GetRawText())!;
        if (oldNode["processClassProperties"] is not JsonObject oldProperties || newNode["processClassProperties"] is not JsonObject newProperties ||
            (newProperties["description"]?.GetValue<string>() ?? "") != expected)
            throw new InvalidDataException("Native process runtime description differs from the request.");
        if (oldProperties.TryGetPropertyValue("description", out var previous)) newProperties["description"] = previous?.DeepClone();
        else newProperties.Remove("description");
        using var restored = JsonDocument.Parse(newNode.ToJsonString(), options);
        if (!SameJson(oldDoc.RootElement, restored.RootElement)) throw new InvalidDataException("Unrequested native process runtime change.");
        b[0].SetAttributeValue("Value", left);
    }
    private static bool SameJson(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) return false;
        if (a.ValueKind == JsonValueKind.Object)
        {
            var left = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
            var right = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
            return left.Count == right.Count && left.All(p => right.TryGetValue(p.Key, out var value) && SameJson(p.Value, value));
        }
        if (a.ValueKind == JsonValueKind.Array) return a.GetArrayLength() == b.GetArrayLength() && a.EnumerateArray().Zip(b.EnumerateArray()).All(p => SameJson(p.First, p.Second));
        return a.ValueKind == JsonValueKind.String ? a.GetString() == b.GetString() : a.GetRawText() == b.GetRawText();
    }
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
