using System.Xml;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static NativeSimulationLimitation[] CallSimulationLimitations(object model, string diagramId) => Graph(model)
        .Where(e => e.DiagramId == diagramId && e.Value.GetType().Name == "CallActivity")
        .Select(e => new NativeSimulationLimitation
        {
            Code = "reusable_subprocess_black_box", DiagramId = e.DiagramId,
            ElementId = Text(e.Value, "Id"), BpmnId = Text(e.Value, "BpmnId"), CallReference = DescribeCall(e.Value),
            Message = "The installed Modeler simulator treats this reusable subprocess as a black box. Configure its overall processing time on the call shape; linked-process elements are not simulated by this call. Embedded subprocesses are required to simulate internal logic.",
            DocumentationUrl = "https://help.bizagi.com/platform/en/simulation_in_bizagi.htm"
        }).ToArray();

    private static NativeCallReference? DescribeCall(object element)
    {
        if (element.GetType().Name != "CallActivity") return null;
        var reference = Optional(element, "CalledElement") as XmlQualifiedName;
        object? catalog = Optional(element, "CalledElementCatalogRef"), external = Optional(element, "ExternalModelReference");
        string catalogId = catalog == null ? "" : Text(Get(catalog, "BaRef"), "Ref");
        return new NativeCallReference
        {
            CatalogProcessId = catalogId == Guid.Empty.ToString() ? "" : catalogId,
            BpmnName = reference?.Name ?? "", BpmnNamespace = reference?.Namespace ?? "",
            External = external == null ? null : new NativeExternalCallReference
            { WorkspaceId = Text(external, "WorkSpaceId"), DiagramId = Text(external, "DiagramId"), ProcessId = Text(external, "ProcessId") }
        };
    }

    private void ApplyCallTarget(object element, NativeCallTarget change, Dictionary<string, GraphEntry> graph)
    {
        var previous = DescribeCall(element) ?? throw new InvalidDataException("CallTarget applies only to a native call activity.");
        if (previous.External != null && !change.ReplaceExternalReference)
            throw new InvalidDataException("Replacing an external-model call reference requires ReplaceExternalReference=true.");
        if (change.ProcessId != "" && (!graph.TryGetValue(change.ProcessId, out var target) || target.Value.GetType().Name != "Process" ||
            !graph.TryGetValue(target.ParentId, out var owner) || owner.Value.GetType().Name != "Participant"))
            throw new InvalidDataException("CallTarget requires an existing local participant Process.Id, not a diagram or activity ID.");
        // Native Modeler uses the catalog reference for local calls. Do not retain a contradictory
        // legacy BPMN QName or external workspace link after an explicit complete target change.
        Set(element, "CalledElementCatalogRef", change.ProcessId == "" ? null! : New(Type("Bizagi.ProcessModeler.BusinessEntities.dll",
            "Bizagi.ProcessModeler.BusinessEntities.Common.CatalogReference"), Guid.Parse(change.ProcessId)));
        Set(element, "CalledElement", null!); Set(element, "ExternalModelReference", null!);
    }

    private static void RequireNoIncomingCalls(IEnumerable<GraphEntry> graph, HashSet<string> removedIds)
    {
        foreach (var caller in graph.Where(e => !removedIds.Contains(Text(e.Value, "Id"))))
        {
            var reference = DescribeCall(caller.Value); if (reference == null) continue;
            // Preserve unresolved QName evidence, but protect known local native/BPMN process
            // identities too. Catalog links are the primary representation, not just CalledElement.
            string qname = reference.BpmnName.StartsWith("Id_", StringComparison.Ordinal) ? reference.BpmnName.Substring(3) : reference.BpmnName;
            if (removedIds.Contains(reference.CatalogProcessId) || removedIds.Contains(qname))
                throw new InvalidDataException("Another call activity references this process; unlink or redirect it explicitly before deletion: " + Text(caller.Value, "Id"));
        }
    }
}
