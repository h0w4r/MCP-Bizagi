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
        if (value.GetType().Name != "DiagramModel") yield return new(value, parent, diagram);
        // Explicit domain containment, not unrestricted reflection over arbitrary object graphs.
        foreach (string property in new[] { "Diagrams", "Participants", "MessageFlows", "Artifacts", "DataStore", "ConversationNodes",
            "FlowElements", "LaneSets", "Lanes", "Milestones", "Resources" })
            if (Optional(value, property) is IEnumerable children)
                foreach (object child in children)
                    foreach (var entry in Visit(child, id, diagram, visited)) yield return entry;
        foreach (string property in new[] { "Process", "ChildLaneSet" })
            if (Optional(value, property) is object child)
                foreach (var entry in Visit(child, id, diagram, visited)) yield return entry;
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
            if (geometry.Expanded) expandedGeometry = new NativeGeometry
            {
                X = geometry.X,
                Y = geometry.Y,
                Width = Number("ExpandedWidth"),
                Height = Number("ExpandedHeight"),
                Expanded = true,
                BackgroundArgb = geometry.BackgroundArgb,
                BorderArgb = geometry.BorderArgb
            };
        }
        return new NativeElement
        {
            Id = Text(element, "Id"),
            BpmnId = Text(element, "BpmnId"),
            Kind = element.GetType().Name,
            ElementType = Text(element, "ElementType"),
            Name = Text(element, "DisplayName"),
            ParentId = entry.ParentId,
            DiagramId = entry.DiagramId,
            Documentation = Text(element, "Documentation"),
            Geometry = geometry,
            ExpandedGeometry = expandedGeometry,
            SourceRef = Text(element, "SourceRef"),
            TargetRef = Text(element, "TargetRef"),
            SourceId = Optional(element, "Source") is object source ? Text(source, "Id") : "",
            TargetId = Optional(element, "Target") is object target ? Text(target, "Id") : "",
            Points = Optional(element, "Points") is IEnumerable points ? points.Cast<object>().Select(p => new NativePoint
            { X = Convert.ToDouble(Get(p, "X")), Y = Convert.ToDouble(Get(p, "Y")) }).ToArray() : Array.Empty<NativePoint>()
        };
    }
}
