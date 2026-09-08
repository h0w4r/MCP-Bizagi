using System.Collections;
using System.Globalization;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private sealed class GraphEntry(object value, string parentId, string diagramId)
    {
        public object Value { get; } = value;
        public string ParentId { get; } = parentId;
        public string DiagramId { get; } = diagramId;
    }
    private static object? Optional(object value, string property) => value.GetType().GetProperty(property)?.GetValue(value);
    private static string Text(object value, string property) => Optional(value, property)?.ToString() ?? "";

    private static IEnumerable<GraphEntry> Graph(object model) => Visit(model, "", "", new HashSet<string>(StringComparer.Ordinal));
    private static IEnumerable<GraphEntry> Visit(object value, string parent, string diagramId, HashSet<string> visited)
    {
        string id = Text(value, "Id");
        if (string.IsNullOrEmpty(id) || !visited.Add(id)) yield break;
        string diagram = value.GetType().Name == "Collaboration" ? id : diagramId;
        // LaneSet is a runtime grouping with a new GUID on every native load, not a durable model identity.
        // Flatten it so operator-facing lanes retain their stable Process (or nested Lane) owner.
        bool laneSet = value.GetType().Name == "LaneSet", modelRoot = value.GetType().Name == "DiagramModel";
        if (!modelRoot && !laneSet) yield return new(value, parent, diagram);
        // The model's GUID is also regenerated for scratch storage on every load. Root-level
        // diagrams/resources have no durable native parent ID; never leak that runtime GUID as one.
        string childParent = laneSet || modelRoot ? parent : id;
        // Explicit domain containment, not unrestricted reflection over arbitrary object graphs.
        foreach (string property in new[] { "Diagrams", "Participants", "MessageFlows", "Artifacts", "DataStore", "ConversationNodes",
            "FlowElements", "LaneSets", "Lanes", "Milestones", "Resources" })
            if (Optional(value, property) is IEnumerable children)
                foreach (object child in children)
                    foreach (var entry in Visit(child, childParent, diagram, visited)) yield return entry;
        foreach (string property in new[] { "Process", "ChildLaneSet" })
            if (Optional(value, property) is object child)
                foreach (var entry in Visit(child, childParent, diagram, visited)) yield return entry;
    }
    private static NativeElement Describe(GraphEntry entry)
    {
        object element = entry.Value;
        NativeGeometry? geometry = null;
        NativeGeometry? expandedGeometry = null;
        if (Optional(element, "GraphicalProperties") is object graphics)
        {
            double Number(string name) => Convert.ToDouble(Optional(graphics, name), CultureInfo.InvariantCulture);
            int? Color(string name) => Optional(graphics, name) is object color ? (int?)Call(color, "ToArgb") : null;
            geometry = new NativeGeometry
            {
                X = Number("X"),
                Y = Number("Y"),
                Width = Number("Width"),
                Height = Number("Height"),
                Expanded = (bool)Get(graphics, "Expanded"),
                BackgroundArgb = Color("BackgroundColor"),
                BorderArgb = Color("BorderColor")
            };
            if (Text(element, "ElementType") is "SubProcess" or "CallActivity") expandedGeometry = new NativeGeometry
            {
                X = geometry.X,
                Y = geometry.Y,
                Width = Number("ExpandedWidth"),
                Height = Number("ExpandedHeight"),
                Expanded = geometry.Expanded,
                BackgroundArgb = geometry.BackgroundArgb,
                BorderArgb = geometry.BorderArgb
            };
        }
        return new NativeElement
        {
            Id = Text(element, "Id"),
            BpmnId = Text(element, "BpmnId"),
            CallReference = DescribeCall(element),
            ActivityProperties = DescribeActivity(element), ActivityLoop = DescribeLoop(element), FlowCondition = DescribeCondition(element),
            SubProcess = DescribeSubProcess(element), Event = DescribeEvent(element),
            Data = DescribeData(element), Artifact = DescribeArtifact(element),
            Style = DescribeStyle(element),
            DataFlow = DescribeDataFlow(entry),
            EventGateway = element.GetType().Name == "EventBasedGateway" ? new NativeEventGatewayInfo
            { Instantiate = (bool)Get(element, "Instantiate"), Kind = Text(element, "EventGatewayType") } : null,
            GatewayDirection = Optional(element, "GatewayDirection")?.ToString(), DefaultSequenceFlowIds = DefaultFlowIds(element),
            Kind = element.GetType().Name,
            IsMainParticipant = element.GetType().Name == "Participant" ? (bool?)Get(element, "IsMainParticipant") : null,
            ElementType = Text(element, "ElementType"),
            Name = Text(element, "DisplayName"),
            ParentId = entry.ParentId,
            DiagramId = entry.DiagramId,
            Documentation = Text(element, "Documentation"),
            Geometry = geometry,
            ExpandedGeometry = expandedGeometry,
            SourceRef = Optional(element, "SourceRef") is string[] sourceRefs ? string.Join(" ", sourceRefs) : Text(element, "SourceRef"),
            TargetRef = Text(element, "TargetRef"),
            SourceId = Optional(element, "Source") is object source ? Text(source, "Id") : "",
            TargetId = Optional(element, "Target") is object target ? Text(target, "Id") : "",
            Points = Optional(element, "Points") is IEnumerable points ? points.Cast<object>().Select(p => new NativePoint
            { X = Convert.ToDouble(Get(p, "X")), Y = Convert.ToDouble(Get(p, "Y")) }).ToArray() : Array.Empty<NativePoint>()
        };
    }
}
