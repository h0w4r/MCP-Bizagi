using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private string[] RestoreNestedDataFlows(object model, Action<string> progress)
    {
        var graph = Graph(model).ToDictionary(e => Text(e.Value, "Id"));
        var nested = graph.Values.Where(e => e.Value.GetType().GetProperty("IoSpecification") != null &&
            graph.TryGetValue(e.ParentId, out var parent) && IsNativeSubProcess(parent.Value)).ToArray();
        if (nested.Length == 0) return Array.Empty<string>();
        XNamespace ns = "http://www.wfmc.org/2009/XPDL2.2";
        object Adapter(string name, params object[] args) => New(Type("Bizagi.ProcessModeler.Persistence.dll", "Bizagi.ProcessModeler.Persistence.Interop.XPDL.BPMNXPDL22Adapters." + name), args);
        object Deserialize(XElement node, string typeName)
        {
            var type = Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.XPDL22." + typeName);
            var serializer = new XmlSerializer(type, new XmlRootAttribute(node.Name.LocalName) { Namespace = ns.NamespaceName });
            using var reader = node.CreateReader(); return serializer.Deserialize(reader)!;
        }
        var adjustments = new List<string>();
        foreach (var group in nested.GroupBy(e => e.DiagramId))
        {
            // The native subprocess loader does not forward its enclosing process's port
            // records to LoadInputAndOutputSets. Reuse that installed adapter with the
            // actual durable XPDL records; do not invent ports from graphical associations.
            string path = Path.Combine((string)Call(model, "GetTempPath")!, group.Key, "Diagram.xml");
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 64 * 1024 * 1024 });
            var doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            if (doc.Root?.Name != ns + "Package" || (string?)doc.Root.Attribute("Id") != group.Key) throw new InvalidDataException("Nested I/O source is not its native diagram package.");
            var processes = doc.Root.Element(ns + "WorkflowProcesses")?.Elements(ns + "WorkflowProcess").ToArray() ?? Array.Empty<XElement>();
            var sets = processes.SelectMany(p => p.Element(ns + "ActivitySets")?.Elements(ns + "ActivitySet") ?? Enumerable.Empty<XElement>()).ToArray();
            var activities = sets.SelectMany(s => s.Element(ns + "Activities")?.Elements(ns + "Activity") ?? Enumerable.Empty<XElement>()).ToArray();
            object flag = Adapter("Util.XpdlCompliantFlag", false), formatting = Adapter("FormattingAdapter", flag), ids = Adapter("ElementIdAdapter");
            object graphics = Adapter("ElementGraphicsInfoAdapter", flag, formatting), connections = Adapter("ConnectingObjectGraphicInfoAdapter", flag, formatting);
            object loader = Adapter("InputAndOutputSetsAdapter", Adapter("DataInputOutputsAdapter"), Adapter("DataAssociationAdapter"));
            object all = Adapter("FlowElements.FlowNodes.Interfaces.XpdlPersistedActivityCollection");
            foreach (var process in processes)
                foreach (string direction in new[] { "Input", "Output" })
                    foreach (var port in process.Element(ns + "DataInputOutputs")?.Elements(ns + ("Data" + direction)) ?? Enumerable.Empty<XElement>())
                        Call(Get(all, "XpdlData" + direction + "s"), "Add", Deserialize(port, "DataInputOutput"));
            foreach (var container in processes.Concat(sets))
                foreach (var association in container.Element(ns + "DataAssociations")?.Elements(ns + "DataAssociation") ?? Enumerable.Empty<XElement>())
                    Call(Get(all, "XpdlDataAssociations"), "Add", Deserialize(association, "DataAssociation"));
            foreach (var owner in group)
            {
                var nodes = activities.Where(a => (string?)a.Attribute("Id") == Text(owner.Value, "Id")).ToArray();
                if (nodes.Length != 1) throw new InvalidDataException("Nested I/O activity identity is missing or ambiguous.");
                var expected = new[] { "Input", "Output" }.SelectMany(d => nodes[0].Elements(ns + (d + "Sets")).Elements(ns + (d + "Set")).Elements(ns + d))
                    .Select(e => (string?)e.Attribute("ArtifactId") ?? "").ToArray();
                var current = DescribeDataFlow(owner)!;
                if (expected.Length == 0 || expected.OrderBy(v => v).SequenceEqual(current.Inputs.Concat(current.Outputs).Select(e => e.Id).OrderBy(v => v))) continue;
                if (current.Inputs.Length != 0 || current.Outputs.Length != 0 || current.InputAssociations.Length != 0 || current.OutputAssociations.Length != 0)
                    throw new InvalidDataException("Partially loaded nested I/O cannot be replaced implicitly.");
                Set(owner.Value, "IoSpecification", null!);
                Call(loader, "LoadInputAndOutputSets", graphics, ids, connections, Deserialize(nodes[0], "Activity"), all, owner.Value);
                foreach (string direction in new[] { "Input", "Output" })
                    foreach (object association in OptionalItems(owner.Value, "Data" + direction + "Associations"))
                    {
                        string side = direction == "Input" ? "Source" : "Target";
                        string id = direction == "Input" ? ((string[])Get(association, "SourceRef")).Single() : Text(association, "TargetRef");
                        if (!graph.TryGetValue(id, out var endpoint) || endpoint.ParentId != owner.ParentId || endpoint.DiagramId != owner.DiagramId)
                            throw new InvalidDataException("Nested I/O endpoint is absent or outside its native flow container.");
                        Set(association, side, endpoint.Value);
                    }
                var restored = DescribeDataFlow(owner)!;
                if (!expected.OrderBy(v => v).SequenceEqual(restored.Inputs.Concat(restored.Outputs).Select(e => e.Id).OrderBy(v => v)))
                    throw new InvalidDataException("The installed nested I/O loader did not restore the durable port identities.");
                string adjustment = "nested_native_io_rehydrated:" + Text(owner.Value, "Id"); adjustments.Add(adjustment); progress(adjustment);
            }
        }
        return adjustments.ToArray();
    }
}
