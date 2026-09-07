using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Verifies native activity/event I/O derived from explicit graphical association edits.</summary>
public static class NativeDataFlowPolicy
{
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private sealed record Link(string OwnerId, string ItemId, bool Input)
    {
        public string Key => OwnerId + ":" + ItemId + ":" + Input;
    }
    private sealed record Binding(Link Link, XElement Port, XElement Association, XElement SetReference);
    public static IEnumerable<NativeElement> OwnedNodes(NativeElement owner) => owner.DataFlow is { } data
        ? data.Inputs.Concat(data.Outputs).Concat(data.InputAssociations).Concat(data.OutputAssociations) : [];
    public static void VerifyClonedData(XDocument doc, NativeElement[] graph)
    {
        var nodes = Nodes(doc);
        foreach (var node in nodes.Values)
        {
            string[] references = node.Name.LocalName switch
            {
                "DataStoreReference" => ["DataStoreRef"], "Association" => ["Source", "Target"], "DataAssociation" => ["From", "To"], _ => []
            };
            foreach (string field in references)
                if (!nodes.TryGetValue((string?)node.Attribute(field) ?? "", out var target) || field == "DataStoreRef" && target.Name != Ns + "DataStore")
                    throw new InvalidDataException("Cloned data still references an original or unresolved identity.");
        }
        foreach (var binding in Bindings(doc, nodes).Values) VerifyReadback(binding, graph);
    }
    public static bool IsSetReference(XElement node)
    {
        if (node.Name.Namespace != Ns || node.Name.LocalName is not "Input" and not "Output") return false;
        string direction = node.Name.LocalName;
        return node.Parent?.Name == Ns + (direction + "Set") && node.Parent.Parent?.Name == Ns + (direction + "Sets") &&
            node.Parent.Parent.Parent?.Name == Ns + "Activity" && NativeFidelity.IsNativeNameOwner(node.Parent.Parent.Parent);
    }
    private static string Id(XElement node) => (string?)node.Attribute("Id") ?? "";
    private static Dictionary<string, XElement> Nodes(XDocument doc)
    {
        var nodes = new Dictionary<string, XElement>();
        // An embedded ActivitySet deliberately repeats its enclosing subprocess's identity.
        // It is a container companion, never a data endpoint or a port identity.
        foreach (var node in doc.Descendants().Where(e => NativeFidelity.IsNativeNameOwner(e) && e.Name != Ns + "ActivitySet"))
            if (!nodes.TryAdd(Id(node), node)) throw new InvalidDataException("Ambiguous native data graph identity.");
        return nodes;
    }
    private static Dictionary<string, Link> Requirements(Dictionary<string, XElement> nodes)
    {
        var result = new Dictionary<string, Link>();
        void Add(string owner, string item, bool input)
        {
            if (!nodes.TryGetValue(owner, out var activity) || !SupportsDirection(activity, input)) return;
            var link = new Link(owner, item, input); result[link.Key] = link;
        }
        void Connect(string item, string owner, bool input)
        {
            if (!nodes.TryGetValue(item, out var data) || data.Name != Ns + "DataObject" && data.Name != Ns + "DataStoreReference") return;
            if (nodes.TryGetValue(owner, out var flow) && flow.Name == Ns + "Transition")
            { Add((string?)flow.Attribute("From") ?? "", item, false); Add((string?)flow.Attribute("To") ?? "", item, true); }
            else Add(owner, item, input);
        }
        foreach (var association in nodes.Values.Where(e => e.Name == Ns + "Association"))
        {
            string source = (string?)association.Attribute("Source") ?? "", target = (string?)association.Attribute("Target") ?? "";
            Connect(source, target, true); Connect(target, source, false);
        }
        return result;
    }
    public static bool SupportsDirection(XElement activity, bool input)
    {
        if (activity.Name != Ns + "Activity" || !NativeFidelity.IsNativeNameOwner(activity)) return false;
        if (activity.Element(Ns + "Implementation") != null || activity.Element(Ns + "BlockActivity") != null) return true;
        var wrappers = activity.Elements(Ns + "Event").ToArray();
        if (wrappers.Length == 0) return false;
        if (wrappers.Length != 1) throw new InvalidDataException("Ambiguous native event I/O owner.");
        var modes = wrappers[0].Elements().ToArray();
        if (modes.Length != 1) throw new InvalidDataException("Ambiguous native event I/O mode.");
        var mode = modes[0];
        if (mode.Name == Ns + "StartEvent") return !input;
        if (mode.Name == Ns + "EndEvent") return input;
        if (mode.Name != Ns + "IntermediateEvent") throw new InvalidDataException("Unknown native event I/O mode.");
        if ((string?)mode.Attribute("Target") is { Length: > 0 } || (string?)mode.Attribute("IsAttached") == "true") return !input;
        string trigger = (string?)mode.Attribute("Trigger") ?? "None";
        if (trigger is "None" or "Escalation" or "Compensation") return input;
        if (trigger is "Timer" or "Conditional" or "ParallelMultiple" or "Error" or "Cancel") return !input;
        // Mode markers belong to the exact direct native trigger, never a nested
        // lookalike or an individual definition inside a multiple event.
        string marker = trigger switch { "Message" => "TriggerResultMessage", "Signal" => "TriggerResultSignal", "Link" => "TriggerResultLink", "Multiple" => "TriggerIntermediateMultiple", _ => "" };
        if (marker == "") throw new InvalidDataException("Unknown native event I/O trigger.");
        var payloads = mode.Elements(Ns + marker).ToArray();
        if (payloads.Length != 1) throw new InvalidDataException("Native event I/O direction marker is absent or ambiguous.");
        string value = (string?)payloads[0].Attribute(trigger == "Multiple" ? "IsThrow" : "CatchThrow") ?? (trigger == "Multiple" ? "false" : "CATCH");
        bool throws = value is "true" or "THROW";
        if (trigger == "Multiple" ? value is not "true" and not "false" : value is not "CATCH" and not "THROW") throw new InvalidDataException("Unknown native event I/O direction marker.");
        return input == throws;
    }
    private static Dictionary<string, Binding> Bindings(XDocument doc, Dictionary<string, XElement> nodes)
    {
        var result = new Dictionary<string, Binding>();
        foreach (var reference in doc.Descendants().Where(IsSetReference))
        {
            string portId = (string?)reference.Attribute("ArtifactId") ?? "";
            bool input = reference.Name == Ns + "Input";
            if (!nodes.TryGetValue(portId, out var port) || port.Name != Ns + (input ? "DataInput" : "DataOutput")) throw new InvalidDataException("Native I/O set references an absent or wrong-kind port.");
            var associations = nodes.Values.Where(e => e.Name == Ns + "DataAssociation" && (string?)e.Attribute(input ? "To" : "From") == portId).ToArray();
            // Unbound imported ports remain compared verbatim; they are not auto-managed bindings.
            if (associations.Length == 0) continue;
            if (associations.Length != 1) throw new InvalidDataException("Native I/O port has ambiguous associations.");
            string item = (string?)associations[0].Attribute(input ? "From" : "To") ?? "";
            var link = new Link(Id(reference.Parent!.Parent!.Parent!), item, input);
            if (!result.TryAdd(link.Key, new(link, port, associations[0], reference))) throw new InvalidDataException("Native I/O binding has ambiguous set membership.");
        }
        return result;
    }
    private static void VerifyReadback(Binding binding, NativeElement[] graph)
    {
        var link = binding.Link;
        var owner = graph.Single(e => e.Id == link.OwnerId);
        var data = owner.DataFlow ?? throw new InvalidDataException("Native I/O owner readback is absent.");
        var ports = link.Input ? data.Inputs : data.Outputs; var associations = link.Input ? data.InputAssociations : data.OutputAssociations;
        string portId = Id(binding.Port), associationId = Id(binding.Association);
        var port = ports.SingleOrDefault(e => e.Id == portId); var association = associations.SingleOrDefault(e => e.Id == associationId);
        if (port == null || association == null || port.ParentId != owner.Id || association.ParentId != owner.Id ||
            association.SourceId != (link.Input ? link.ItemId : portId) || association.TargetId != (link.Input ? portId : link.ItemId) ||
            (link.Input ? data.InputSets : data.OutputSets).SelectMany(s => s).Count(id => id == portId) != 1)
            throw new InvalidDataException("Native I/O binding differs after independent worker readback: owner=" + owner.Id + "; item=" + link.ItemId +
                "; port=" + portId + "; association=" + associationId + "; observed=" + association?.SourceId + "->" + association?.TargetId + ".");
        if ((string?)binding.Port.Attribute("Name") is string name && name != port.Name ||
            ((string?)binding.Port.Attribute("IsCollection") ?? "false") != (port.Data?.IsCollection == true ? "true" : "false") ||
            ((string?)binding.Port.Attribute("State") ?? "") != port.Data?.State)
            throw new InvalidDataException("Native I/O port values differ after restart.");
    }
    private static void RequireDefaultRemoval(Binding binding)
    {
        // Automatic unlinking must not discard user-authored payload on a rich input/output.
        // Such ports need an explicit future owned-I/O editing transaction, not implicit cleanup.
        if (((string?)binding.Port.Attribute("Name") ?? "") != "" || ((string?)binding.Port.Attribute("State") ?? "") != "" ||
            ((string?)binding.Port.Attribute("IsCollection") ?? "false") != "false" || (binding.Port.Element(Ns + "Documentation")?.Value ?? "") != "" ||
            ((string?)binding.Association.Attribute("Name") ?? "") != "" || (binding.Association.Element(Ns + "Description")?.Value ?? "") != "" ||
            binding.Association.Element(Ns + "ExtendedAttributes")?.HasElements == true)
            throw new InvalidDataException("Automatic association cleanup cannot discard rich native I/O payload.");
        RequireKnownPayload(binding.Port, false); RequireKnownPayload(binding.Association, true);
    }
    private static void RequireKnownPayload(XElement node, bool association)
    {
        string[] attributes = association ? ["Id", "Name", "From", "To"] : ["Id", "Name", "State", "IsCollection"];
        string[] children = association ? ["Description", "ConnectorGraphicsInfos", "ExtendedAttributes"] : ["Documentation", "NodeGraphicsInfos"];
        if (node.Attributes().Any(a => !attributes.Any(n => a.Name == n)) || node.Nodes().Any(n => n is not XElement && n.GetType() != typeof(XText) || n is XText text && !string.IsNullOrWhiteSpace(text.Value)) ||
            node.Elements().Any(e => !children.Any(n => e.Name == Ns + n)) || node.Elements().GroupBy(e => e.Name).Any(g => g.Count() > 1))
            throw new InvalidDataException("Unknown native I/O payload cannot be removed implicitly.");
        foreach (var child in node.Elements().Where(e => e.Name.LocalName is "Description" or "Documentation" or "ExtendedAttributes"))
            if (child.HasAttributes || child.Nodes().Any(n => n.GetType() != typeof(XText) || !string.IsNullOrEmpty(((XText)n).Value))) throw new InvalidDataException("Native I/O metadata is not empty.");
        // Rendering metadata is part of the explicitly deleted port/association, but any
        // unrecognized schema content remains guarded rather than presumed disposable.
        var graphic = node.Elements().SingleOrDefault(e => e.Name.LocalName is "NodeGraphicsInfos" or "ConnectorGraphicsInfos");
        if (graphic == null) return;
        var names = new HashSet<string> { "NodeGraphicsInfos", "NodeGraphicsInfo", "ConnectorGraphicsInfos", "ConnectorGraphicsInfo", "Coordinates", "Formatting", "Alignment", "FontName", "SizeFont", "Bold", "Italic", "Strikeout", "Underline", "ColorFont", "TextDirection", "TextBackgroundColor" };
        var attrs = new HashSet<string> { "ToolId", "Width", "Height", "BorderColor", "BorderVisible", "FillColor", "XCoordinate", "YCoordinate" };
        if (graphic.DescendantsAndSelf().Any(e => e.Name.Namespace != Ns || !names.Contains(e.Name.LocalName) ||
            e.Attributes().Any(a => a.Name != XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance") && (a.Name.Namespace != XNamespace.None || !attrs.Contains(a.Name.LocalName))) ||
            e.Nodes().Any(n => n is not XElement && n.GetType() != typeof(XText)))) throw new InvalidDataException("Unknown native I/O rendering payload cannot be removed implicitly.");
    }
    private static void RemoveForComparison(Binding binding)
    {
        if (binding.SetReference.Attributes().Any(a => a.Name != "ArtifactId") || binding.SetReference.Nodes().Any())
            throw new InvalidDataException("Unknown I/O membership content cannot be projected away.");
        var set = binding.SetReference.Parent!;
        binding.SetReference.Remove(); binding.Port.Remove(); binding.Association.Remove();
        if (Empty(set)) set.RemoveNodes();
    }
    private static bool Empty(XElement node) => !node.HasAttributes && node.Nodes().All(n => n.GetType() == typeof(XText) && string.IsNullOrWhiteSpace(((XText)n).Value)) &&
        node.AncestorsAndSelf().Select(e => (string?)e.Attribute(XNamespace.Xml + "space")).FirstOrDefault(v => v != null) != "preserve";
    private static void ProjectTouchedContainers(IEnumerable<XElement> containers, XDocument peer)
    {
        foreach (var container in containers.Distinct())
        {
            if (container.Parent is not { } owner || !NativeFidelity.IsNativeNameOwner(owner) ||
                container.Name != Ns + "DataInputOutputs" && container.Name != Ns + "DataAssociations" || !Empty(container)) continue;
            // ActivitySet and its subprocess Activity deliberately share an ID; the
            // exact owner kind is part of this container lookup, not only that ID.
            var owners = peer.Descendants(owner.Name).Where(e => NativeFidelity.IsNativeNameOwner(e) && Id(e) == Id(owner)).ToArray();
            if (owners.Length != 1) continue;
            var otherOwner = owners[0];
            var other = otherOwner.Elements(container.Name).ToArray();
            if (other.Length == 0) container.Remove();
            else if (other.Length == 1 && Empty(other[0])) container.ReplaceNodes(other[0].Nodes().Select(n => new XText(((XText)n).Value)));
        }
    }
    private static void ProjectNewEmptySets(XDocument before, XDocument after, IEnumerable<string> changedOwners)
    {
        var a = Nodes(before); var b = Nodes(after);
        foreach (string id in changedOwners.Distinct())
        {
            if (!a.TryGetValue(id, out var left) || !b.TryGetValue(id, out var right)) continue;
            foreach (string direction in new[] { "Input", "Output" })
            {
                var old = left.Elements(Ns + (direction + "Sets")).ToArray(); var next = right.Elements(Ns + (direction + "Sets")).ToArray();
                if (old.Length > 1 || next.Length > 1) throw new InvalidDataException("Ambiguous native I/O set container.");
                if (next.Length != 1) continue;
                var oldSets = old.SingleOrDefault()?.Elements(Ns + (direction + "Set")).ToArray() ?? [];
                var sets = next[0].Elements(Ns + (direction + "Set")).ToArray();
                // Native AddDataInput/Output creates a first set. Remove only an empty new
                // set after its verified new memberships have been projected; never unknown content.
                if (oldSets.Length == 0 && sets.Length == 1 && Empty(sets[0])) sets[0].Remove();
                if (old.Length == 0 && Empty(next[0])) next[0].Remove();
            }
        }
    }
    public static void ProjectDerived(XDocument before, XDocument after, NativeMutation[] changes, NativeElement[] graph)
    {
        var oldNodes = Nodes(before); var newNodes = Nodes(after);
        var oldRequirements = Requirements(oldNodes); var newRequirements = Requirements(newNodes);
        var oldBindings = Bindings(before, oldNodes); var newBindings = Bindings(after, newNodes);
        foreach (var required in newRequirements)
        {
            if (!newBindings.TryGetValue(required.Key, out var binding)) throw new InvalidDataException("A graphical data association has no durable native activity/event I/O binding.");
            VerifyReadback(binding, graph);
        }
        var changedOwners = new HashSet<string>();
        var oldContainers = new List<XElement>(); var newContainers = new List<XElement>();
        foreach (var pair in newBindings.Where(p => !oldBindings.ContainsKey(p.Key)))
        {
            if (!newRequirements.ContainsKey(pair.Key) || oldRequirements.ContainsKey(pair.Key) || oldNodes.ContainsKey(Id(pair.Value.Port)) || oldNodes.ContainsKey(Id(pair.Value.Association)))
                throw new InvalidDataException("Native I/O creation is not derived from a new graphical data relationship.");
            NativeMetadataPolicy.RequireId(Id(pair.Value.Port)); NativeMetadataPolicy.RequireId(Id(pair.Value.Association));
            // The installed helper creates default owned nodes, not authored metadata.
            // Do not hide unexpected rich/unknown payload merely because the IDs are new.
            RequireDefaultRemoval(pair.Value);
            changedOwners.Add(pair.Value.Link.OwnerId);
            newContainers.Add(pair.Value.Port.Parent!); newContainers.Add(pair.Value.Association.Parent!); RemoveForComparison(pair.Value);
        }
        foreach (var pair in oldBindings.Where(p => !newBindings.ContainsKey(p.Key)))
        {
            if (!oldRequirements.ContainsKey(pair.Key) || newRequirements.ContainsKey(pair.Key)) throw new InvalidDataException("Native I/O removal is not derived from a removed graphical data relationship.");
            RequireDefaultRemoval(pair.Value);
            oldContainers.Add(pair.Value.Port.Parent!); oldContainers.Add(pair.Value.Association.Parent!); RemoveForComparison(pair.Value);
        }
        foreach (var key in oldBindings.Keys.Intersect(newBindings.Keys))
            if (Id(oldBindings[key].Port) != Id(newBindings[key].Port) || Id(oldBindings[key].Association) != Id(newBindings[key].Association))
                throw new InvalidDataException("An unchanged native I/O binding replaced its durable identities.");
        ProjectNewEmptySets(before, after, changedOwners);
        ProjectTouchedContainers(oldContainers, after); ProjectTouchedContainers(newContainers, before);
        // Process-level DataInputOutputs/DataAssociations are emitted empty by native defaults;
        // unknown wrappers and all other fields remain visible to the whole-archive comparator.
        _ = changes;
    }
}
