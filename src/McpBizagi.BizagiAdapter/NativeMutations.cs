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
            var dataLinks = RequiredDataLinks(graph.Values);
            object Require(string id) => graph.TryGetValue(id, out var entry) ? entry.Value : throw new InvalidDataException("Unknown native identity: " + id);
            object element;
            if (change.Operation == "create")
            {
                if (graph.ContainsKey(change.ElementId) || graph.Values.SelectMany(DataFlowNodes).Any(e => Text(e.Value, "Id") == change.ElementId))
                    throw new InvalidDataException("Native identity already exists: " + change.ElementId);
                object parent = Require(change.ParentId);
                // Use the installed factory so event definitions and element-specific defaults remain vendor-owned.
                // DataStore is a native catalog class whose ElementType is Other, not a palette enum.
                object kind = Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementType"), change.ElementType == "DataStore" ? "Other" : change.ElementType, false);
                object descriptor = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementDescriptor"), kind);
                // The native factory requires an explicit intermediate-event mode. Current names use
                // NoneIntermediate = throw, MessageIntermediate/TimerIntermediate = catch.
                if (change.ElementType.EndsWith("Intermediate", StringComparison.Ordinal))
                    Set(descriptor, "Options", Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementTypeOptions"),
                        "IntermediateEvent" + (change.EventMode ?? (change.ElementType == "NoneIntermediate" ? "Throw" : "Catch"))));
                if (change.ElementType == "SubProcess" && change.SubProcessKind is "Transaction" or "AdHoc")
                    Set(descriptor, "Options", Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementTypeOptions"),
                        change.SubProcessKind == "Transaction" ? "TransactionSubProcess" : "AdHocSubProcess"));
                element = (change.ElementType == "DataStore" ? New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.DataStore")) : Call(Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.IElementFactory"), "Create", descriptor))
                    ?? throw new NotSupportedException("Native factory does not create this element type.");
                Set(element, "Id", Guid.Parse(change.ElementId));
                Set(element, "DisplayName", change.Name ?? "");
                if (change.ElementType == "Participant")
                {
                    if (graph.ContainsKey(change.ProcessId)) throw new InvalidDataException("Native process identity already exists.");
                    Set(Get(element, "Process"), "Id", Guid.Parse(change.ProcessId));
                }
                if (change.ElementType is "Lane" or "Milestone")
                {
                    if (parent.GetType().Name != "Process") throw new InvalidDataException("Modeler lanes require a participant process, not an embedded subprocess or runtime lane set.");
                    object participant = Require(graph[change.ParentId].ParentId);
                    if (participant.GetType().Name != "Participant" || (bool)Get(participant, "IsMainParticipant"))
                        throw new InvalidDataException("Lanes require a visible native participant.");
                    Set(element, "ParentParticipant", participant);
                    if (change.ElementType == "Milestone")
                    {
                        object runtime = Get(element, "Runtime");
                        Set(runtime, "MilestoneType", Enum.Parse(runtime.GetType().GetProperty("MilestoneType")!.PropertyType, "Process"));
                    }
                }
                if (change.ElementType != "DataStore") Call(Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.IBpmnUtilFacade"), "SetDefaultBizAgiName", parent, element);
                PrepareArtifact(element, parent, Require(graph[change.ParentId].DiagramId));
                Call(MutationCollection(parent, element), "Add", element);
                graph.Add(change.ElementId, new GraphEntry(element, change.ParentId, graph[change.ParentId].DiagramId));
            }
            else element = Require(change.ElementId);
            ValidateArtifactMutation(element, change);
            object? previousSource = element.GetType().Name == "SequenceFlow" ? Optional(element, "Source") : null;

            switch (change.Operation)
            {
                case "create":
                case "update":
                    if (change.SubProcessProperties != null) ApplySubProcessProperties(element, change.SubProcessProperties);
                    if (change.EventProperties != null) ApplyEventProperties(element, change.EventProperties, graph);
                    if (change.EventPayloads != null) ApplyEventPayloads(element, change.EventPayloads, graph);
                    if (change.DataProperties != null) ApplyDataProperties(element, change.DataProperties, graph);
                    if (change.ArtifactProperties != null) ApplyArtifactProperties(model, graph[change.ElementId], change.ArtifactProperties);
                    if (change.CallTarget != null) ApplyCallTarget(element, change.CallTarget, graph);
                    if (change.ActivityProperties != null) ApplyActivityProperties(element, change.ActivityProperties);
                    if (change.ActivityLoop != null) ApplyLoop(element, change.ActivityLoop);
                    if (change.GatewayDirection != null)
                    {
                        var property = element.GetType().GetProperty("GatewayDirection") ?? throw new InvalidDataException("GatewayDirection requires a native gateway.");
                        Set(element, "GatewayDirection", Enum.Parse(property.PropertyType, change.GatewayDirection));
                    }
                    if (change.Name != null) Set(element, "DisplayName", change.Name);
                    if (change.Documentation != null)
                        // A cleared pool description must use the native absent value. Persisting "" leaves
                        // a transient process-runtime JSON key that the next native load/save removes.
                        Set(element, "Documentation", change.Documentation == "" && element.GetType().Name == "Participant" ? null! : change.Documentation);
                    if (change.Operation == "create") InitializeNewStyle(element);
                    if (change.Geometry != null) ApplyGeometry(element, change.Geometry);
                    if (change.Style != null) ApplyStyle(element, change.Style);
                    if (change.ExpandedSize is { } size)
                    {
                        if (Text(element, "ElementType") != "SubProcess") throw new InvalidDataException("ExpandedSize currently applies to embedded subprocesses only.");
                        Set(Get(element, "GraphicalProperties"), "ExpandedSize", new SizeF((float)size.Width, (float)size.Height));
                    }
                    if (!string.IsNullOrEmpty(change.SourceId))
                    {
                        Connect(element, Require(change.SourceId), Require(change.TargetId), change.Points, graph);
                        ApplyConnectorPorts(element, change);
                    }
                    if (change.FlowCondition != null) ApplyFlowCondition(element, change.FlowCondition);
                    break;
                case "reconnect":
                    Connect(element, Require(change.SourceId), Require(change.TargetId), change.Points, graph);
                    ApplyConnectorPorts(element, change);
                    break;
                case "delete":
                    string processId = element.GetType().Name == "Participant" ? Text(Get(element, "Process"), "Id") : "";
                    RequireNoAttachedBoundaries(graph.Values, change.ElementId);
                    RequireNoCompensationTargets(graph.Values, change.ElementId);
                    RequireNoStoreReferences(graph.Values, change.ElementId);
                    RequireStoreStateCarrier(graph.Values, element);
                    RequireNoIncomingCalls(graph.Values, new HashSet<string>(new[] { change.ElementId, processId }.Where(v => v != ""), StringComparer.Ordinal));
                    if (processId != "" && ((bool)Get(element, "IsMainParticipant") || graph.Values.Count(e => e.DiagramId == graph[change.ElementId].DiagramId && e.Value.GetType().Name == "Participant") <= 1))
                        throw new InvalidDataException("Deleting the main or last participant would invoke native implicit-model reconstruction.");
                    if (graph.Values.Any(e => e.ParentId == change.ElementId && Text(e.Value, "Id") != processId || processId != "" && e.ParentId == processId))
                        throw new InvalidDataException("Delete child elements explicitly before deleting their container.");
                    if (graph.Values.Any(e => Text(Optional(e.Value, "Source") ?? e.Value, "Id") == change.ElementId && e.Value != element ||
                        Text(Optional(e.Value, "Target") ?? e.Value, "Id") == change.ElementId && e.Value != element))
                        throw new InvalidDataException("Delete or reconnect incident connections before deleting this element.");
                    object owner = Require(graph[change.ElementId].ParentId);
                    if (element.GetType().Name == "SequenceFlow") UnlinkSequence(element);
                    if (!(bool)Call(MutationCollection(owner, element), "Remove", element)!)
                        throw new InvalidDataException("Native collection did not remove the requested element.");
                    DeleteImageFile(model, graph[change.ElementId]);
                    break;
                default: throw new NotSupportedException("Unknown native mutation: " + change.Operation);
            }
            if (element.GetType().Name == "SequenceFlow" && (change.FlowCondition != null || change.Operation is "create" or "reconnect" or "delete"))
            {
                SynchronizeDefaultFlow(previousSource);
                if (change.Operation != "delete") SynchronizeDefaultFlow(Optional(element, "Source"));
            }
            SynchronizeDataLinks(model, dataLinks);
            progress("native_mutation:" + change.Operation + ":" + change.ElementId);
        }
        ValidateSubProcessContexts(model, changes);
        ValidateDataStateOwnership(model, changes);
        ValidateLanePartitions(model);
        ValidateArtifactContainment(model);
    }

    private static void ApplyConnectorPorts(object element, NativeMutation change)
    {
        // Omission never resets imported port metadata. Clearing uses the native absent value.
        if (change.SourcePort != null) Set(element, "SourcePort", change.SourcePort == "" ? null! : change.SourcePort);
        if (change.TargetPort != null) Set(element, "TargetPort", change.TargetPort == "" ? null! : change.TargetPort);
    }

    private static void ValidateArtifactContainment(object model)
    {
        foreach (object diagram in Items(model, "Diagrams"))
        {
            var participants = Items(diagram, "Participants").ToArray();
            foreach (object participant in participants)
                foreach (object artifact in Items(Get(participant, "Process"), "Artifacts"))
                {
                    // Native XPDL stores root-process artifacts in a shared diagram list.
                    // Its loader assigns them to the first visible pool containing their
                    // native center, otherwise the main pool. Check the same installed
                    // geometry predicate before saving rather than silently moving them.
                    object center = Get(Get(artifact, "GraphicalProperties"), "Center");
                    object? selected = participants.FirstOrDefault(p => !(bool)Get(p, "IsMainParticipant") &&
                        (bool)Call(Get(p, "GraphicalProperties"), "IsPointInRectangle", center)!);
                    selected ??= participants.SingleOrDefault(p => (bool)Get(p, "IsMainParticipant"));
                    if (selected == null || Text(selected, "Id") != Text(participant, "Id"))
                        throw new InvalidDataException("Artifact geometry would change native containment after restart: " + Text(artifact, "Id") + ". An implicit containment move is not permitted.");
                }
        }
    }

    private object MutationCollection(object parent, object element)
    {
        if (element.GetType().Name != "Lane") return Get(parent, CollectionFor(parent, element));
        if (parent.GetType().Name != "Process") throw new InvalidDataException("Lane ownership must be a stable native process identity.");
        object sets = Get(parent, "LaneSets");
        var groups = ((IEnumerable)sets).Cast<object>().ToArray();
        if (groups.Length > 1) throw new InvalidDataException("Multiple runtime lane sets cannot be silently flattened during editing.");
        if (groups.Length == 0)
        {
            object group = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.LaneSet"));
            Call(sets, "Add", group); groups = new[] { group };
        }
        return Get(groups[0], "Lanes");
    }

    private static void ValidateLanePartitions(object model)
    {
        // Native loading partitions lanes at X=50 and consecutive Y offsets and derives the pool height.
        // Require the complete intended layout instead of quietly accepting a lossy geometry request.
        foreach (var entry in Graph(model).Where(e => e.Value.GetType().Name == "Participant"))
        {
            object pool = entry.Value, graphics = Get(pool, "GraphicalProperties");
            var lanes = Items(Get(pool, "Process"), "LaneSets").SelectMany(s => Items(s, "Lanes")).OrderBy(l => Convert.ToDouble(Get(Get(l, "GraphicalProperties"), "Y"))).ToArray();
            double y = 0, width = Convert.ToDouble(Get(graphics, "Width"));
            foreach (object lane in lanes)
            {
                object g = Get(lane, "GraphicalProperties");
                bool Same(string key, double expected) => Math.Abs(Convert.ToDouble(Get(g, key)) - expected) <= 0.001;
                if (!Same("X", 50) || !Same("Y", y) || !Same("Width", width - 50))
                    throw new InvalidDataException("Lane partitions require X=50, width=pool width-50 and consecutive Y offsets; include all affected bounds explicitly in the batch.");
                y += Convert.ToDouble(Get(g, "Height"));
            }
            if (lanes.Length > 0 && Math.Abs(Convert.ToDouble(Get(graphics, "Height")) - y) > 0.001)
                throw new InvalidDataException("The requested participant height must equal the sum of its lane heights.");
            double x = 50, height = Convert.ToDouble(Get(graphics, "Height"));
            var milestones = Items(Get(pool, "Process"), "Milestones").ToArray();
            foreach (object milestone in milestones)
            {
                object g = Get(milestone, "GraphicalProperties");
                if (Math.Abs(Convert.ToDouble(Get(g, "X")) - x) > 0.001 || Math.Abs(Convert.ToDouble(Get(g, "Height")) - height) > 0.001)
                    throw new InvalidDataException("Milestones require consecutive X offsets starting at 50 and the complete pool height.");
                x += Convert.ToDouble(Get(g, "Width"));
            }
            if (milestones.Length > 0 && Math.Abs(width - x) > 0.001)
                throw new InvalidDataException("The requested pool width must equal 50 plus all milestone widths.");
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
            "DataStore" when parent.GetType().Name == "Collaboration" => "DataStore",
            _ when Derives("FlowElement") => "FlowElements",
            _ when Derives("Artifact") => "Artifacts",
            _ => throw new NotSupportedException("Unsupported native containment for " + element.GetType().Name)
        };
        if (Optional(parent, property) is not IEnumerable) throw new InvalidDataException("Invalid native parent for " + element.GetType().Name);
        return property;
    }

    private static void ApplyGeometry(object element, NativeGeometry geometry)
    {
        if (geometry.Expanded && Text(element, "ElementType") is not "SubProcess" and not "Group") throw new InvalidDataException("Expanded geometry applies to embedded subprocesses or the native intrinsic group view.");
        if ((bool?)Optional(element, "IsConnector") == true) throw new InvalidDataException("Connections require explicit points, not node bounds.");
        object graphics = Get(element, "GraphicalProperties");
        Set(graphics, "X", (float)geometry.X); Set(graphics, "Y", (float)geometry.Y);
        Set(graphics, "Width", (float)geometry.Width); Set(graphics, "Height", (float)geometry.Height);
        Set(graphics, "Expanded", geometry.Expanded);
        // Group load materializes these derived dimensions from its visible bounds.
        if (Text(element, "ElementType") == "Group") Set(graphics, "ExpandedSize", new SizeF((float)geometry.Width, (float)geometry.Height));
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
        if (kind is not "SequenceFlow" and not "MessageFlow" and not "Association") throw new NotSupportedException("Unsupported native connector kind.");
        if (kind == "Association")
        {
            var owner = graph[Text(connection, "Id")];
            if (!graph.TryGetValue(Text(source, "Id"), out var a) || !graph.TryGetValue(Text(target, "Id"), out var b) ||
                a.DiagramId != owner.DiagramId || b.DiagramId != owner.DiagramId || a.ParentId != owner.ParentId || b.ParentId != owner.ParentId ||
                Optional(source, "GraphicalProperties") == null || Optional(target, "GraphicalProperties") == null || source == connection || target == connection)
                throw new InvalidDataException("Association endpoints must be graphical elements in the same native container.");
        }
        if (kind == "SequenceFlow")
        {
            // Event subprocesses exist outside their parent's normal sequence flow.
            if (Optional(source, "TriggeredByEvent") is true || Optional(target, "TriggeredByEvent") is true)
                throw new InvalidDataException("Event-triggered subprocesses cannot have incoming or outgoing sequence flows.");
            // Sequence flows cannot enter a boundary/start/instantiating gateway or leave an end event.
            if (target.GetType().Name is "BoundaryEvent" or "StartEvent" || source.GetType().Name == "EndEvent" ||
                target.GetType().Name == "EventBasedGateway" && (bool)Get(target, "Instantiate"))
                throw new InvalidDataException("Sequence flow direction violates the native event or instantiating gateway boundary.");
            if (!graph.TryGetValue(Text(source, "Id"), out var a) || !graph.TryGetValue(Text(target, "Id"), out var b) || a.ParentId != b.ParentId ||
                graph[Text(connection, "Id")].ParentId != a.ParentId)
                throw new InvalidDataException("Sequence flow endpoints must share the same native flow container.");
            if (Optional(source, "Outgoing") == null || Optional(target, "Incoming") == null) throw new InvalidDataException("Sequence flow endpoints must be flow nodes.");
            UnlinkSequence(connection);
        }
        Set(connection, "Source", source); Set(connection, "Target", target);
        Set(connection, "SourceRef", kind != "SequenceFlow" ? new System.Xml.XmlQualifiedName(Text(source, "BpmnId")) : (object)Text(source, "BpmnId"));
        Set(connection, "TargetRef", kind != "SequenceFlow" ? new System.Xml.XmlQualifiedName(Text(target, "BpmnId")) : (object)Text(target, "BpmnId"));
        object vertices = Get(connection, "Points"); Call(vertices, "Clear");
        foreach (var point in points) Call(vertices, "Add", new PointF((float)point.X, (float)point.Y));
        // The installed connector loader anchors GraphicalProperties at the first
        // persisted point with zero shape size. Keep that derived native state current
        // before containment checks; editing PointCollection alone does not update it.
        object graphics = Get(connection, "GraphicalProperties");
        Set(graphics, "X", (float)points[0].X); Set(graphics, "Y", (float)points[0].Y);
        Set(graphics, "Size", SizeF.Empty);
        if (kind == "SequenceFlow")
        {
            // Event subprocesses exist outside their parent's normal sequence flow.
            if (Optional(source, "TriggeredByEvent") is true || Optional(target, "TriggeredByEvent") is true)
                throw new InvalidDataException("Event-triggered subprocesses cannot have incoming or outgoing sequence flows.");
            // Sequence flows cannot enter a boundary/start/instantiating gateway or leave an end event.
            if (target.GetType().Name is "BoundaryEvent" or "StartEvent" || source.GetType().Name == "EndEvent" ||
                target.GetType().Name == "EventBasedGateway" && (bool)Get(target, "Instantiate"))
                throw new InvalidDataException("Sequence flow direction violates the native event or instantiating gateway boundary.");
            Call(Get(source, "Outgoing"), "Add", Text(connection, "BpmnId"));
            Call(Get(target, "Incoming"), "Add", Text(connection, "BpmnId"));
            Call(Get(source, "OutgoingSequenceFlows"), "Add", connection);
            Call(Get(target, "IncomingSequenceFlows"), "Add", connection);
        }
    }
}
