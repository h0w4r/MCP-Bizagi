using System.Collections;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private XmlSerializer SimulationSerializer() => new(Type("Bizagi.ProcessModeler.BusinessEntities.dll",
        "Bizagi.ProcessModeler.BusinessEntities.Simulation.BPSim.BPSimData"));

    private string SimulationXml(object data)
    {
        Set(data, "SimulationLevelSpecified", true);
        using var text = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        SimulationSerializer().Serialize(text, data);
        return text.ToString();
    }

    private NativeMetadataSnapshot Metadata(object model) => new()
    {
        Assignments = Graph(model).Where(e => IsActivity(e.Value)).Select(e => ReadAssignments(model, e.Value)).ToArray(),
        Resources = Items(model, "Resources").Select(r => new NativeResource
        {
            Id = Text(r, "Id"),
            BpmnId = Text(r, "BpmnId"),
            Name = Text(r, "DisplayName"),
            Description = Text(r, "Documentation"),
            Type = Text(r, "Type")
        }).ToArray(),
        Simulations = Items(model, "Diagrams").Select(d => new NativeSimulationConfiguration
        {
            DiagramId = Text(d, "Id"),
            Xml = SimulationXml(Get(d, "BPSimData"))
        }).ToArray()
    };

    private void EditMetadata(object model, NativeMetadataPatch patch, Action<string> progress)
    {
        var resources = (IList)Get(model, "Resources");
        foreach (var change in patch.Resources)
        {
            progress("native_resource:" + change.Operation + ":" + change.Id);
            object? resource = resources.Cast<object>().SingleOrDefault(r => Text(r, "Id") == change.Id);
            if (resource != null && (bool)Get(resource, "IsGlobal")) throw new NotSupportedException("Global catalog resources are not local editable resources.");
            if (change.Operation == "delete")
            {
                if (resource == null) throw new InvalidDataException("Resource deletion target does not exist.");
                string bpmnId = Text(resource, "BpmnId");
                // Refuse orphaned RACI assignments or simulation references, including expression selections.
                bool assigned = Graph(model).Any(e => new[] { "ResourceRoles", "Accountables", "Consulted", "Informed" }.Any(p =>
                    Items(e.Value, p).Any(role => (Optional(role, "ResourceRef") as XmlQualifiedName)?.Name == bpmnId)));
                if (assigned || Items(model, "Diagrams").Any(d => SimulationXml(Get(d, "BPSimData")).Contains(bpmnId)))
                    throw new InvalidDataException("Resource is referenced by assignments or simulation settings; remove references explicitly before deletion.");
                resources.Remove(resource);
                continue;
            }
            if (change.Operation != "upsert") throw new NotSupportedException("Unknown resource operation.");
            if (resources.Cast<object>().Any(r => Text(r, "Id") != change.Id && string.Equals(Text(r, "DisplayName"), change.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Native resource names must be unique, ignoring case.");
            if (resource == null)
            {
                if (Graph(model).Any(e => Text(e.Value, "Id") == change.Id)) throw new InvalidDataException("Resource identity collides with a model element.");
                resource = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.Resource"));
                Set(resource, "Id", Guid.Parse(change.Id));
                object generator = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.Util.Bpmn.IResourceIdGenerator");
                Set(resource, "BpmnId", Call(generator, "GenerateId", change.Name, resources)!);
                resources.Add(resource);
            }
            Set(resource, "DisplayName", change.Name);
            Set(resource, "Documentation", change.Description);
            Set(resource, "Type", Enum.Parse(resource.GetType().GetProperty("Type")!.PropertyType, change.Type));
        }
        foreach (var replacement in patch.Simulations)
        {
            progress("native_simulation_configuration:" + replacement.DiagramId);
            object diagram = Items(model, "Diagrams").Single(d => Text(d, "Id") == replacement.DiagramId);
            object previous = Get(diagram, "BPSimData");
            if (!patch.DiscardSimulationResults && Items(previous, "Scenarios").Any(s => !string.IsNullOrEmpty(Text(s, "SimulationResult"))))
                throw new InvalidDataException("Replacing configuration with saved results requires explicit DiscardSimulationResults, to avoid retaining stale results.");
            var serializer = SimulationSerializer();
            // Reject fields the installed version does not understand instead of silently discarding them.
            serializer.UnknownAttribute += (_, e) => throw new InvalidDataException("Unknown native simulation attribute: " + e.Attr.Name);
            serializer.UnknownElement += (_, e) => throw new InvalidDataException("Unknown native simulation element: " + e.Element.Name);
            using var text = new StringReader(replacement.Xml);
            using var reader = XmlReader.Create(text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
            object data = serializer.Deserialize(reader) ?? throw new InvalidDataException("Missing native BPSim configuration.");
            var ids = new HashSet<string>(Graph(model).Where(e => e.DiagramId == replacement.DiagramId || e.Value.GetType().Name == "Resource")
                .Select(e => Text(e.Value, "BpmnId")), StringComparer.Ordinal);
            foreach (var scenario in Items(data, "Scenarios"))
                foreach (var element in Items(scenario, "ElementParameters"))
                    if (!ids.Contains(Text(element, "ElementRef"))) throw new InvalidDataException("Simulation references an unknown element in this diagram: " + Text(element, "ElementRef"));
            Set(diagram, "BPSimData", data);
        }
        foreach (var assignment in patch.Assignments)
        {
            progress("native_resource_assignments:" + assignment.ElementId);
            object activity = Graph(model).Single(e => Text(e.Value, "Id") == assignment.ElementId).Value;
            if (!IsActivity(activity)) throw new NotSupportedException("RACI editing requires an activity, not a process or event.");
            foreach (var role in AssignmentRoles(assignment))
            {
                var collection = Get(activity, role.Property);
                Call(collection, "Clear");
                foreach (string id in role.Ids)
                {
                    object resource = resources.Cast<object>().Single(r => Text(r, "Id") == id);
                    if (role.Property != "ResourceRoles" && Text(resource, "DisplayName").Contains(","))
                        throw new NotSupportedException("This engine persists ACI names as comma-separated values; names containing commas cannot be assigned without ambiguity.");
                    object nativeRole = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ResourceRole"));
                    Set(nativeRole, "ResourceRef", new XmlQualifiedName(Text(resource, "BpmnId")));
                    Call(collection, "Add", nativeRole);
                }
            }
        }
    }

    private bool IsActivity(object element) => Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.Activity").IsInstanceOfType(element);
    private static IEnumerable<(string Property, string[] Ids)> AssignmentRoles(NativeResourceAssignments value)
    {
        yield return ("ResourceRoles", value.Responsible); yield return ("Accountables", value.Accountable);
        yield return ("Consulted", value.Consulted); yield return ("Informed", value.Informed);
    }
    private static NativeResourceAssignments ReadAssignments(object model, object activity)
    {
        string[] Read(string property) => Items(activity, property).Select(role =>
        {
            string reference = (Optional(role, "ResourceRef") as XmlQualifiedName)?.Name ?? "";
            return Text(Items(model, "Resources").Single(r => Text(r, "BpmnId") == reference), "Id");
        }).ToArray();
        return new NativeResourceAssignments { ElementId = Text(activity, "Id"), Responsible = Read("ResourceRoles"), Accountable = Read("Accountables"), Consulted = Read("Consulted"), Informed = Read("Informed") };
    }
}
