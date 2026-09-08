using System.Text;
using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Validate intent and compare all non-targeted native content, including unknown archive leaves.</summary>
public static class NativeMetadataPolicy
{
    public const string BpsimNamespace = "http://www.bpsim.org/schemas/1.0";
    private static readonly XNamespace Bp = BpsimNamespace;
    private static readonly XNamespace Xpdl = "http://www.wfmc.org/2009/XPDL2.2";
    public static void Validate(NativeMetadataPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (patch.Resources == null || patch.Simulations == null || patch.Assignments == null || patch.Resources.Length + patch.Simulations.Length + patch.Assignments.Length is < 1 or > 1000)
            throw new InvalidDataException("Supply 1-1000 explicit metadata changes.");
        var ids = new HashSet<string>();
        foreach (var resource in patch.Resources)
        {
            if (resource == null) throw new InvalidDataException("Resource change cannot be null.");
            RequireId(resource.Id);
            if (!ids.Add(resource.Id) || resource.Operation is not "upsert" and not "delete") throw new InvalidDataException("Duplicate resource ID or unknown operation.");
            if (resource.Name == null || resource.Description == null || resource.Name.Length > 10000 || resource.Description.Length > 1024 * 1024)
                throw new InvalidDataException("Resource text is missing or too large.");
            if (resource.Operation == "upsert" && (string.IsNullOrWhiteSpace(resource.Name) || resource.Type is not "Role" and not "Entity"))
                throw new InvalidDataException("Resources require a name and native type Role or Entity.");
            if (resource.Operation == "delete" && (resource.Name.Length > 0 || resource.Description.Length > 0 || resource.Type != "Role"))
                throw new InvalidDataException("Deletion must not contain ignored resource values.");
        }
        ids.Clear();
        foreach (var simulation in patch.Simulations)
        {
            if (simulation == null) throw new InvalidDataException("Simulation replacement cannot be null.");
            RequireId(simulation.DiagramId);
            if (!ids.Add(simulation.DiagramId)) throw new InvalidDataException("Duplicate simulation diagram.");
            ValidateSimulation(simulation.Xml);
        }
        if (patch.DiscardSimulationResults && patch.Simulations.Length == 0) throw new InvalidDataException("Discarding results requires an explicit simulation replacement.");
        ids.Clear();
        foreach (var assignment in patch.Assignments)
        {
            if (assignment == null) throw new InvalidDataException("Assignment replacement cannot be null.");
            RequireId(assignment.ElementId);
            if (!ids.Add(assignment.ElementId)) throw new InvalidDataException("Duplicate assignment target.");
            foreach (var set in new[] { assignment.Responsible, assignment.Accountable, assignment.Consulted, assignment.Informed })
            {
                if (set == null || set.Length > 1000 || set.Distinct().Count() != set.Length) throw new InvalidDataException("Assignment sets require distinct IDs and at most 1000 resources.");
                foreach (string resourceId in set) RequireId(resourceId);
            }
        }
    }
    public static void RequireId(string id)
    {
        if (!Guid.TryParseExact(id, "D", out var value) || value == Guid.Empty || value.ToString() != id)
            throw new InvalidDataException("Expected a canonical nonempty native GUID.");
    }
    public static XDocument Read(string xml)
    {
        if (xml == null || xml.Length > BpmnDocument.MaxXmlCharacters) throw new InvalidDataException("Metadata XML exceeds the document limit.");
        // Decoding a UTF-8 archive leaf into a string retains its optional byte-order mark.
        using var input = new StringReader(xml.StartsWith('\uFEFF') ? xml[1..] : xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = BpmnDocument.MaxXmlCharacters
        });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }
    public static void ValidateSimulation(string xml)
    {
        var doc = Read(xml);
        if (doc.Root?.Name != Bp + "BPSimData") throw new InvalidDataException("Expected native BPSim 1.0 configuration, not BPMN.");
        if ((string?)doc.Root.Attribute("simulationLevel") is not ("LevelOne" or "LevelTwo" or "LevelThree" or "LevelFour"))
            throw new InvalidDataException("An explicit simulationLevel is required.");
        var scenarios = doc.Root.Elements(Bp + "Scenario").ToArray();
        if (scenarios.Length > 1000) throw new InvalidDataException("Too many scenarios.");
        var ids = new HashSet<string>();
        foreach (var scenario in scenarios)
        {
            string id = (string?)scenario.Attribute("id") ?? "";
            if (id.Length == 0 || !ids.Add(id)) throw new InvalidDataException("Scenarios require unique IDs.");
            XmlConvert.VerifyNCName(id);
            var parameters = scenario.Element(Bp + "ScenarioParameters") ?? throw new InvalidDataException("ScenarioParameters are required.");
            if ((string?)parameters.Attribute("baseTimeUnit") == "year") throw new NotSupportedException("This engine normalizes years; supply an explicit supported time unit instead.");
            if (parameters.Attribute("replication") is { } r && (!int.TryParse(r.Value, out int n) || n is < 1 or > 10000))
                throw new InvalidDataException("Replication count must be between 1 and 10000.");
            var refs = scenario.Elements(Bp + "ElementParameters").Select(e => (string?)e.Attribute("elementRef")).ToArray();
            if (refs.Any(string.IsNullOrWhiteSpace) || refs.Distinct().Count() != refs.Length) throw new InvalidDataException("ElementParameters require distinct nonempty references within a scenario.");
            var calendars = scenario.Elements(Bp + "Calendar").Select(c => (string?)c.Attribute("id")).ToArray();
            if (calendars.Any(string.IsNullOrWhiteSpace) || calendars.Distinct().Count() != calendars.Length) throw new InvalidDataException("Calendars require distinct IDs.");
            foreach (var validity in scenario.Descendants().Attributes("validFor"))
                if (!calendars.Contains(validity.Value)) throw new InvalidDataException("Calendar validity references an unknown calendar.");
        }
    }
    public static bool XmlEquivalent(string left, string right) => Normalize(Read(left)) == Normalize(Read(right));
    private static string Normalize(XDocument doc)
    {
        // Expand element/attribute names. Retain namespace bindings: unknown scalar values may be QNames.
        // Only the serializer's unused standard declarations are insignificant.
        var atoms = new List<string>();
        void Walk(XElement element)
        {
            atoms.Add("element:" + element.Name);
            foreach (var a in element.Attributes().OrderBy(a => a.Name.ToString()))
            {
                if (a.IsNamespaceDeclaration && (a.Value == BpsimNamespace || a.Value == "http://www.w3.org/2001/XMLSchema" || a.Value == "http://www.w3.org/2001/XMLSchema-instance"))
                {
                    string prefix = a.Name.LocalName == "xmlns" ? "" : a.Name.LocalName;
                    bool usedInValue = prefix.Length > 0 && element.DescendantsAndSelf().Any(e =>
                        e.Attributes().Any(v => !v.IsNamespaceDeclaration && v.Value.Contains(prefix + ":", StringComparison.Ordinal)) ||
                        e.Nodes().OfType<XText>().Any(v => v.Value.Contains(prefix + ":", StringComparison.Ordinal)));
                    if (!usedInValue) continue;
                }
                atoms.Add("attribute:" + a.Name + "=" + a.Value);
            }
            bool preserve = element.AncestorsAndSelf().Select(e => (string?)e.Attribute(XNamespace.Xml + "space")).FirstOrDefault(v => v != null) == "preserve";
            foreach (var n in element.Nodes())
            {
                if (n is XElement e) Walk(e);
                else if (preserve || !element.HasElements || n is not XText t || !string.IsNullOrWhiteSpace(t.Value)) atoms.Add(n.NodeType + ":" + n);
            }
            atoms.Add("end");
        }
        foreach (var node in doc.Nodes()) { if (node is XElement e) Walk(e); else if (node is not XText) atoms.Add(node.NodeType + ":" + node); }
        return System.Text.Json.JsonSerializer.Serialize(atoms);
    }
    public static NativeFidelityReport Compare(byte[] before, byte[] after, NativeMetadataPatch patch, NativeMetadataSnapshot reopened)
    {
        Validate(patch);
        var left = NativeArchive.ReadEntries(before).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var right = NativeArchive.ReadEntries(after).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        if (patch.DiscardSimulationResults)
            foreach (var simulation in patch.Simulations) NativeSavedSimulationPolicy.ValidateResultRetirement(left, simulation.DiagramId);
        foreach (var simulation in patch.Simulations)
        {
            var readback = reopened.Simulations.Single(s => s.DiagramId == simulation.DiagramId);
            if (!XmlEquivalent(simulation.Xml, readback.Xml)) throw new InvalidDataException("Simulation settings changed during native persistence/readback.");
            string path = simulation.DiagramId + ".diag!/BPSimData.xml";
            if (!left.ContainsKey(path) || !right.ContainsKey(path) || !XmlEquivalent(simulation.Xml, Encoding.UTF8.GetString(right[path])))
                throw new InvalidDataException("Durable BPSim configuration does not match the requested replacement.");
            right[path] = left[path]; // Comparison-only projection; native archives are never rewritten here.
            if (patch.DiscardSimulationResults)
            {
                string results = simulation.DiagramId + ".diag!/BPSimDataResult.xml";
                if (!right.TryGetValue(results, out var bytes) || !XmlEquivalent(Encoding.UTF8.GetString(bytes), "<ScenarioResults/>"))
                    throw new InvalidDataException("Explicit simulation-result discard did not produce an empty native result set.");
                if (left.ContainsKey(results)) right[results] = left[results]; else right.Remove(results);
            }
        }
        if (patch.Resources.Length > 0)
        {
            // Native V5 persists the catalog both at model level and inside every diagram package.
            foreach (string path in left.Keys.Where(p => p == "Participants.xml" || p.EndsWith(".diag!/Diagram.xml", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                var a = Read(Encoding.UTF8.GetString(left[path])); var b = Read(Encoding.UTF8.GetString(right[path]));
                bool topLevel = path == "Participants.xml";
                if (topLevel ? a.Root?.Name != Xpdl + "Participants" || b.Root?.Name != Xpdl + "Participants"
                    : a.Root?.Name != Xpdl + "Package" || b.Root?.Name != Xpdl + "Package") throw new InvalidDataException("Unsupported native resource container.");
                XElement? oldContainer = topLevel ? a.Root : a.Root!.Element(Xpdl + "Participants");
                XElement? newContainer = topLevel ? b.Root : b.Root!.Element(Xpdl + "Participants");
                var originalContainer = oldContainer;
                oldContainer ??= new XElement(Xpdl + "Participants");
                newContainer ??= new XElement(Xpdl + "Participants");
                foreach (var change in patch.Resources)
                {
                    var oldNode = oldContainer.Elements(Xpdl + "Participant").Where(e => (string?)e.Attribute("Id") == change.Id).ToArray();
                    var newNode = newContainer.Elements(Xpdl + "Participant").Where(e => (string?)e.Attribute("Id") == change.Id).ToArray();
                    if (oldNode.Length > 1 || newNode.Length > 1) throw new InvalidDataException("Duplicate persisted resource identity.");
                    if (change.Operation == "delete")
                    {
                        if (oldNode.Length != 1 || newNode.Length != 0 || reopened.Resources.Any(r => r.Id == change.Id)) throw new InvalidDataException("Resource deletion was not verified.");
                        oldNode[0].Remove(); continue;
                    }
                    var actual = reopened.Resources.Single(r => r.Id == change.Id);
                    if (actual.Name != change.Name || actual.Description != change.Description || actual.Type != change.Type || newNode.Length != 1)
                        throw new InvalidDataException("Resource values did not survive native readback.");
                    var node = newNode[0];
                    if ((string?)node.Attribute("Name") != change.Name || node.Element(Xpdl + "Description")?.Value != change.Description ||
                        (string?)node.Element(Xpdl + "ParticipantType")?.Attribute("Type") != (change.Type == "Role" ? "ROLE" : "RESOURCE"))
                        throw new InvalidDataException("Unexpected persisted resource values.");
                    if (string.IsNullOrWhiteSpace(actual.BpmnId) || (string?)node.Element(Xpdl + "ExtendedAttributes")?.Elements(Xpdl + "ExtendedAttribute").FirstOrDefault()?.Attribute("Name") != actual.BpmnId)
                        throw new InvalidDataException("Resource BPMN identity changed during native readback.");
                    if (oldNode.Length == 0) node.Remove();
                    else
                    {
                        node.SetAttributeValue("Name", oldNode[0].Attribute("Name")?.Value);
                        // Restore known scalar values only; unfamiliar siblings/attributes must still compare.
                        var description = node.Element(Xpdl + "Description")!;
                        if (description.Nodes().Any(n => n is not XText) || oldNode[0].Element(Xpdl + "Description") is not { } oldDescription || oldDescription.Nodes().Any(n => n is not XText))
                            throw new InvalidDataException("Unsupported structured resource description.");
                        description.ReplaceNodes(oldDescription.Nodes());
                        node.Element(Xpdl + "ParticipantType")!.SetAttributeValue("Type", oldNode[0].Element(Xpdl + "ParticipantType")?.Attribute("Type")?.Value);
                    }
                }
                // Requested membership changes leave formatting whitespace behind. Strip only that container's indentation.
                foreach (var container in new[] { oldContainer, newContainer })
                    if (container.AncestorsAndSelf().Select(e => (string?)e.Attribute(XNamespace.Xml + "space")).FirstOrDefault(v => v != null) != "preserve")
                        container.Nodes().OfType<XText>().Where(t => string.IsNullOrWhiteSpace(t.Value)).Remove();
                if (!topLevel && originalContainer == null && !newContainer.HasAttributes && !newContainer.Nodes().Any()) newContainer.Remove();
                if (!topLevel && originalContainer != null && !oldContainer.HasAttributes && !oldContainer.Nodes().Any() && b.Root!.Element(Xpdl + "Participants") == null) oldContainer.Remove();
                left[path] = Encoding.UTF8.GetBytes(a.ToString()); right[path] = Encoding.UTF8.GetBytes(b.ToString());
            }
        }
        ProjectAssignments(left, right, patch.Assignments, reopened);
        return NativeFidelity.CompareEntries(left, right);
    }

    private static void ProjectAssignments(Dictionary<string, byte[]> left, Dictionary<string, byte[]> right, NativeResourceAssignments[] assignments, NativeMetadataSnapshot reopened)
    {
        foreach (var assignment in assignments)
        {
            var actual = reopened.Assignments.Single(a => a.ElementId == assignment.ElementId);
            foreach (string resourceId in assignment.Responsible.Concat(assignment.Accountable).Concat(assignment.Consulted).Concat(assignment.Informed))
                if (reopened.Resources.Count(r => r.Id == resourceId) != 1) throw new InvalidDataException("RACI readback references a missing or ambiguous native resource.");
            if (!actual.Responsible.SequenceEqual(assignment.Responsible) || !actual.Accountable.SequenceEqual(assignment.Accountable) ||
                !actual.Consulted.SequenceEqual(assignment.Consulted) || !actual.Informed.SequenceEqual(assignment.Informed))
                throw new InvalidDataException("RACI assignments did not survive fresh-worker readback.");
            var matches = left.Keys.Where(p => p.EndsWith(".diag!/Diagram.xml", StringComparison.OrdinalIgnoreCase)).Select(p =>
                (Path: p, Xml: Read(Encoding.UTF8.GetString(left[p])))).Where(p => p.Xml.Descendants(Xpdl + "Activity").Any(e => (string?)e.Attribute("Id") == assignment.ElementId)).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Expected a unique persisted native activity.");
            var a = matches[0].Xml; var b = Read(Encoding.UTF8.GetString(right[matches[0].Path]));
            var oldNode = a.Descendants(Xpdl + "Activity").Single(e => (string?)e.Attribute("Id") == assignment.ElementId);
            var newNode = b.Descendants(Xpdl + "Activity").Single(e => (string?)e.Attribute("Id") == assignment.ElementId);
            var oldPerformers = oldNode.Element(Xpdl + "Performers"); var newPerformers = newNode.Element(Xpdl + "Performers");
            foreach (var container in new[] { oldPerformers, newPerformers }.OfType<XElement>())
                if (container.HasAttributes || container.Elements().Any(e => e.Name != Xpdl + "Performer" || e.HasAttributes || e.Nodes().Any(n => n is not XText)) ||
                    container.Nodes().Any(n => n is not XElement && (n is not XText t || !string.IsNullOrWhiteSpace(t.Value))) ||
                    container.AncestorsAndSelf().Select(e => (string?)e.Attribute(XNamespace.Xml + "space")).FirstOrDefault(v => v != null) == "preserve")
                    throw new InvalidDataException("Unknown content in native performer assignments.");
            foreach (var performer in new[] { oldPerformers, newPerformers }.OfType<XElement>().SelectMany(c => c.Elements())) RequireId(performer.Value);
            if (!(newPerformers?.Elements(Xpdl + "Performer").Select(e => e.Value) ?? []).SequenceEqual(assignment.Responsible))
                throw new InvalidDataException("Durable responsible assignments differ from the requested resources.");
            // Remove only acknowledged scalar assignment containers on both comparison copies.
            oldPerformers?.Remove(); newPerformers?.Remove();
            foreach (var role in new[] { (Name: "BizagiAccountables", Ids: assignment.Accountable), (Name: "BizagiConsulted", Ids: assignment.Consulted), (Name: "BizagiInformed", Ids: assignment.Informed) })
            {
                var before = oldNode.Element(Xpdl + "ExtendedAttributes")?.Elements(Xpdl + "ExtendedAttribute").Where(e => (string?)e.Attribute("Name") == role.Name).ToArray() ?? [];
                var after = newNode.Element(Xpdl + "ExtendedAttributes")?.Elements(Xpdl + "ExtendedAttribute").Where(e => (string?)e.Attribute("Name") == role.Name).ToArray() ?? [];
                if (before.Length > 1 || after.Length > 1 || before.Concat(after).Any(e => e.Nodes().Any() || e.Attributes().Any(v => v.Name != "Name" && v.Name != "Value")))
                    throw new InvalidDataException("Unknown content in native ACI assignments.");
                string expected = string.Join(",", role.Ids.Select(id => reopened.Resources.Single(r => r.Id == id).Name));
                if ((after.SingleOrDefault()?.Attribute("Value")?.Value ?? "") != expected) throw new InvalidDataException("Durable ACI values do not match the requested resources.");
                foreach (var node in before.Concat(after)) node.Remove();
            }
            // Native ExtendedAttributes is an existing container; retain any unknown siblings and attributes.
            foreach (var node in new[] { oldNode, newNode })
                foreach (var c in node.Elements(Xpdl + "ExtendedAttributes"))
                    if (c.AncestorsAndSelf().Select(e => (string?)e.Attribute(XNamespace.Xml + "space")).FirstOrDefault(v => v != null) != "preserve")
                        c.Nodes().OfType<XText>().Where(t => string.IsNullOrWhiteSpace(t.Value)).Remove();
            left[matches[0].Path] = Encoding.UTF8.GetBytes(a.ToString()); right[matches[0].Path] = Encoding.UTF8.GetBytes(b.ToString());
        }
    }
}
