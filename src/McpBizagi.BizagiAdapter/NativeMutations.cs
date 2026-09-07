using System.Collections;
using System.Drawing;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private void Mutate(object model, NativeMutation[] changes, Action<string> progress)
    {
        foreach (var change in changes)
        {
            var graph = Graph(model).ToDictionary(e => Text(e.Value, "Id"), StringComparer.Ordinal);
            object Require(string id) => graph.TryGetValue(id, out var entry) ? entry.Value : throw new InvalidDataException("Unknown native identity: " + id);
            object element;
            if (change.Operation == "create")
            {
                if (graph.ContainsKey(change.ElementId)) throw new InvalidDataException("Native identity already exists: " + change.ElementId);
                object parent = Require(change.ParentId);
                // Use the installed factory so event definitions and element-specific defaults remain vendor-owned.
                object kind = Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementType"), change.ElementType, false);
                object descriptor = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementDescriptor"), kind);
                // The native factory requires an explicit intermediate-event mode. Current names use
                // NoneIntermediate = throw, MessageIntermediate/TimerIntermediate = catch; boundary events are separate.
                if (change.ElementType.EndsWith("Intermediate", StringComparison.Ordinal))
                    Set(descriptor, "Options", Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementTypeOptions"),
                        change.ElementType == "NoneIntermediate" ? "IntermediateEventThrow" : "IntermediateEventCatch"));
                element = Call(Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.IElementFactory"), "Create", descriptor)
                    ?? throw new NotSupportedException("Native factory does not create this element type.");
                Set(element, "Id", Guid.Parse(change.ElementId));
                Set(element, "DisplayName", change.Name ?? "");
                Call(Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.IBpmnUtilFacade"), "SetDefaultBizAgiName", parent, element);
                string collection = CollectionFor(parent, element);
                Call(Get(parent, collection), "Add", element);
                graph.Add(change.ElementId, new GraphEntry(element, change.ParentId, graph[change.ParentId].DiagramId));
            }
            else element = Require(change.ElementId);

            switch (change.Operation)
            {
                case "create":
                case "update":
                    if (change.Name != null) Set(element, "DisplayName", change.Name);
                    if (change.Documentation != null) Set(element, "Documentation", change.Documentation);
                    if (change.Geometry != null) ApplyGeometry(element, change.Geometry);
                    if (!string.IsNullOrEmpty(change.SourceId)) Connect(element, Require(change.SourceId), Require(change.TargetId), change.Points, graph);
                    break;
                case "reconnect":
                    Connect(element, Require(change.SourceId), Require(change.TargetId), change.Points, graph);
                    break;
                case "delete":
                    if (graph.Values.Any(e => e.ParentId == change.ElementId))
                        throw new InvalidDataException("Delete child elements explicitly before deleting their container.");
                    if (graph.Values.Any(e => Text(Optional(e.Value, "Source") ?? e.Value, "Id") == change.ElementId && e.Value != element ||
                        Text(Optional(e.Value, "Target") ?? e.Value, "Id") == change.ElementId && e.Value != element))
                        throw new InvalidDataException("Delete or reconnect incident connections before deleting this element.");
                    object owner = Require(graph[change.ElementId].ParentId);
                    if (element.GetType().Name == "SequenceFlow") UnlinkSequence(element);
                    if (!(bool)Call(Get(owner, CollectionFor(owner, element)), "Remove", element)!)
                        throw new InvalidDataException("Native collection did not remove the requested element.");
                    break;
                default: throw new NotSupportedException("Unknown native mutation: " + change.Operation);
            }
            progress("native_mutation:" + change.Operation + ":" + change.ElementId);
        }
    }

    private static string CollectionFor(object parent, object element)
    {
        bool Derives(string name) { for (var t = element.GetType(); t != null; t = t.BaseType) if (t.Name == name) return true; return false; }
        string property = element.GetType().Name switch
        {
            "Participant" => "Participants",
            "Lane" => "Lanes",
            "Milestone" => "Milestones",
            "MessageFlow" => "MessageFlows",
            _ when Derives("FlowElement") => "FlowElements",
            _ when Derives("Artifact") => "Artifacts",
            _ => throw new NotSupportedException("Unsupported native containment for " + element.GetType().Name)
        };
        if (Optional(parent, property) is not IEnumerable) throw new InvalidDataException("Invalid native parent for " + element.GetType().Name);
        return property;
    }

    private static void ApplyGeometry(object element, NativeGeometry geometry)
    {
        if ((bool?)Optional(element, "IsConnector") == true) throw new InvalidDataException("Connections require explicit points, not node bounds.");
        object graphics = Get(element, "GraphicalProperties");
        Set(graphics, "X", (float)geometry.X); Set(graphics, "Y", (float)geometry.Y);
        Set(graphics, "Width", (float)geometry.Width); Set(graphics, "Height", (float)geometry.Height);
        Set(graphics, "Expanded", geometry.Expanded);
        if (geometry.BackgroundArgb.HasValue) Set(graphics, "BackgroundColor", Color.FromArgb(geometry.BackgroundArgb.Value));
        if (geometry.BorderArgb.HasValue) Set(graphics, "BorderColor", Color.FromArgb(geometry.BorderArgb.Value));
    }

    private static void UnlinkSequence(object flow)
    {
        string reference = Text(flow, "BpmnId");
        foreach (var side in new[] { ("Source", "Outgoing"), ("Target", "Incoming") })
            if (Optional(flow, side.Item1) is object node && Optional(node, side.Item2) is object refs)
            {
                Call(refs, "Remove", reference);
                Call(Get(node, side.Item2 + "SequenceFlows"), "Remove", Guid.Parse(Text(flow, "Id")));
            }
    }

    private static void Connect(object connection, object source, object target, NativePoint[] points, Dictionary<string, GraphEntry> graph)
    {
        string kind = connection.GetType().Name;
        if (kind is not "SequenceFlow" and not "MessageFlow") throw new NotSupportedException("Only native sequence/message flows can be connected.");
        if (kind == "SequenceFlow")
        {
            if (!graph.TryGetValue(Text(source, "Id"), out var a) || !graph.TryGetValue(Text(target, "Id"), out var b) || a.ParentId != b.ParentId ||
                graph[Text(connection, "Id")].ParentId != a.ParentId)
                throw new InvalidDataException("Sequence flow endpoints must share the same native flow container.");
            if (Optional(source, "Outgoing") == null || Optional(target, "Incoming") == null) throw new InvalidDataException("Sequence flow endpoints must be flow nodes.");
            UnlinkSequence(connection);
        }
        Set(connection, "Source", source); Set(connection, "Target", target);
        Set(connection, "SourceRef", kind == "MessageFlow" ? new System.Xml.XmlQualifiedName(Text(source, "BpmnId")) : (object)Text(source, "BpmnId"));
        Set(connection, "TargetRef", kind == "MessageFlow" ? new System.Xml.XmlQualifiedName(Text(target, "BpmnId")) : (object)Text(target, "BpmnId"));
        object vertices = Get(connection, "Points"); Call(vertices, "Clear");
        foreach (var point in points) Call(vertices, "Add", new PointF((float)point.X, (float)point.Y));
        if (kind == "SequenceFlow")
        {
            Call(Get(source, "Outgoing"), "Add", Text(connection, "BpmnId"));
            Call(Get(target, "Incoming"), "Add", Text(connection, "BpmnId"));
            Call(Get(source, "OutgoingSequenceFlows"), "Add", connection);
            Call(Get(target, "IncomingSequenceFlows"), "Add", connection);
        }
    }
}
