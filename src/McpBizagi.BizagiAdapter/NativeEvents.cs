using System.Xml;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static NativeEventInfo? DescribeEvent(object element)
    {
        string kind = element.GetType().Name;
        string mode = kind switch { "StartEvent" => "Start", "EndEvent" => "End", "IntermediateCatchEvent" => "Catch", "IntermediateThrowEvent" => "Throw", "BoundaryEvent" => "Boundary", _ => "" };
        if (mode == "") return null;
        var qname = Optional(element, "AttachedToRef") as XmlQualifiedName;
        object? activity = Optional(element, "AttachedToRefActivity"), catalog = Optional(element, "AttachedToCatalogReference");
        string catalogId = catalog == null ? "" : Text(Get(catalog, "BaRef"), "Ref");
        return new NativeEventInfo
        {
            Mode = mode, IsInterrupting = Optional(element, "IsInterrupting") as bool?, IsParallelMultiple = Optional(element, "IsParallelMultiple") as bool?,
            AttachedToActivityId = activity == null ? "" : Text(activity, "Id"), AttachedToBpmnName = qname?.Name ?? "", AttachedToBpmnNamespace = qname?.Namespace ?? "",
            AttachedToCatalogActivityId = catalogId == Guid.Empty.ToString() ? "" : catalogId,
            DefinitionKinds = Items(element, "EventDefinitions").Select(d => Text(d, "EventDefinitionType")).ToArray(),
            Definitions = Items(element, "EventDefinitions").Select(DescribeEventDefinition).ToArray()
        };
    }

    private void ApplyEventProperties(object element, NativeEventProperties patch, Dictionary<string, GraphEntry> graph)
    {
        var info = DescribeEvent(element);
        if (info == null || info.Mode is not "Start" and not "Boundary") throw new InvalidDataException("EventProperties requires a native start or boundary event.");
        if (patch.IsInterrupting is bool interrupting)
        {
            if (!interrupting && info.Mode == "Start" && (!graph.TryGetValue(graph[Text(element, "Id")].ParentId, out var owner) ||
                Optional(owner.Value, "TriggeredByEvent") is not true))
                throw new InvalidDataException("Noninterrupting start events require an event-triggered subprocess.");
            if (!interrupting && Text(element, "ElementType") is "ErrorIntermediate" or "CancelIntermediate" or "CompensationIntermediate" or "ErrorStart" or "CompensationStart")
                throw new InvalidDataException("This native event kind does not support a noninterrupting flag.");
            Set(element, "IsInterrupting", interrupting);
        }
        if (patch.AttachedToActivityId is { } id)
        {
            string elementId = Text(element, "Id");
            if (info.Mode != "Boundary" || !graph.TryGetValue(id, out var target) || DescribeActivity(target.Value) == null ||
                target.ParentId != graph[elementId].ParentId || target.DiagramId != graph[elementId].DiagramId)
                throw new InvalidDataException("Boundary attachment requires a native activity in the same flow container.");
            if (Text(element, "ElementType") == "CancelIntermediate" && target.Value.GetType().Name != "Transaction")
                throw new InvalidDataException("Cancel boundaries require a native transaction subprocess.");
            // Persist uses the native Activity object, not only its BPMN or catalog alias.
            // Keep all in-memory representations consistent for other installed services.
            Set(element, "AttachedToRefActivity", target.Value);
            Set(element, "AttachedToRef", new XmlQualifiedName(Text(target.Value, "BpmnId")));
            Set(element, "AttachedToCatalogReference", New(Type("Bizagi.ProcessModeler.BusinessEntities.dll",
                "Bizagi.ProcessModeler.BusinessEntities.Common.CatalogReference"), Guid.Parse(id)));
        }
    }

    private static void RequireNoAttachedBoundaries(IEnumerable<GraphEntry> graph, string removedId)
    {
        foreach (var entry in graph)
        {
            var boundary = DescribeEvent(entry.Value);
            if (boundary?.Mode != "Boundary") continue;
            // Protect actual object links and unresolved legacy/native aliases. Never silently
            // remove another shape's event attachment when deleting the referenced activity.
            string name = boundary.AttachedToBpmnName;
            if (boundary.AttachedToActivityId == removedId || boundary.AttachedToCatalogActivityId == removedId || name == removedId || name == "Id_" + removedId)
                throw new InvalidDataException("Delete or reattach boundary events before deleting their activity: " + Text(entry.Value, "Id"));
        }
    }
}
