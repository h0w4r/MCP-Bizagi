using System.Collections;
using System.Xml;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static string ExpressionText(object? expression) => expression == null ? "" : string.Concat(Optional(expression, "Text") as string[] ?? Array.Empty<string>());
    private object? PayloadExpression(string text, bool retainEmpty = false)
    {
        if (text == "" && !retainEmpty) return null;
        var expression = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.Expression"));
        Set(expression, "Text", new[] { text }); return expression;
    }
    private static NativeEventDefinitionInfo DescribeEventDefinition(object definition)
    {
        string kind = Text(definition, "EventDefinitionType");
        var result = new NativeEventDefinitionInfo { Kind = kind };
        if (kind is "Message" or "Conditional" or "Link" or "Signal") result.Name = Text(definition, "DisplayName");
        if (kind == "Conditional") result.Condition = ExpressionText(Optional(definition, "Condition"));
        if (kind == "Timer")
        {
            string text = ExpressionText(Optional(definition, "TimerDefinition"));
            result.Timer = new NativeEventTimer { Kind = text == "" ? "None" : Text(definition, "TimerDefinitionType") switch { "timeDate" => "Date", "timeCycle" => "Cycle", var other => other }, Text = text };
        }
        if (kind == "Error") result.ErrorCode = Optional(definition, "Error") is object error ? Text(error, "ErrorCode") : "";
        if (kind == "Escalation") result.EscalationCode = Optional(definition, "Escalation") is object escalation ? Text(escalation, "EscalationCode") : "";
        if (kind == "Compensation")
        {
            var q = Optional(definition, "ActivityRef") as XmlQualifiedName;
            string catalog = Optional(definition, "ActivityCatalogRef") is object r ? Text(Get(r, "BaRef"), "Ref") : "";
            result.Compensation = new NativeCompensationInfo { WaitForCompletion = (bool)Get(definition, "WaitForCompletion"),
                ActivityId = Optional(definition, "Activity") is object activity ? Text(activity, "Id") : "", BpmnName = q?.Name ?? "", BpmnNamespace = q?.Namespace ?? "",
                CatalogActivityId = catalog == Guid.Empty.ToString() ? "" : catalog };
        }
        return result;
    }
    private void SetCompensationTarget(object definition, object? activity)
    {
        Set(definition, "Activity", activity!);
        Set(definition, "ActivityRef", activity == null ? null! : new XmlQualifiedName(Text(activity, "BpmnId")));
        Set(definition, "ActivityCatalogRef", activity == null ? null! : New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Common.CatalogReference"), Guid.Parse(Text(activity, "Id"))));
    }
    private void ApplyEventPayloads(object element, NativeEventPayloadPatch[] patches, Dictionary<string, GraphEntry> graph)
    {
        if (DescribeEvent(element) == null) throw new InvalidDataException("EventPayloads requires a native event.");
        foreach (var p in patches)
        {
            var definitions = Items(element, "EventDefinitions").Where(d => Text(d, "EventDefinitionType") == p.Kind).ToArray();
            if (definitions.Length != 1) throw new InvalidDataException("Selected native event definition is absent or ambiguous: " + p.Kind);
            object d = definitions[0];
            if (p.Name != null) Set(d, "DisplayName", p.Name);
            // The native conditional loader materializes an empty Expression. Persist that
            // same representation when clearing, so a later unrelated save remains stable.
            if (p.Condition != null) Set(d, "Condition", PayloadExpression(p.Condition, retainEmpty: true)!);
            if (p.Timer is { } timer)
            {
                Set(d, "TimerDefinition", PayloadExpression(timer.Text)!);
                if (timer.Kind != "None") Set(d, "TimerDefinitionType", Enum.Parse(d.GetType().GetProperty("TimerDefinitionType")!.PropertyType, timer.Kind == "Date" ? "timeDate" : "timeCycle"));
            }
            foreach (var code in new[] { (Name: "Error", Value: p.ErrorCode), (Name: "Escalation", Value: p.EscalationCode) })
                if (code.Value != null)
                {
                    var value = Optional(d, code.Name) ?? New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20." + code.Name));
                    Set(value, code.Name + "Code", code.Value); Set(d, code.Name, value);
                }
            if (p.Compensation is { } c)
            {
                if (c.WaitForCompletion is bool wait) Set(d, "WaitForCompletion", wait);
                if (c.ActivityId is { } id)
                {
                    object? activity = null;
                    if (id != "")
                    {
                        var owner = graph[Text(element, "Id")];
                        if (!graph.TryGetValue(id, out var target) || !IsNativeActivity(target.Value) || target.ParentId != owner.ParentId || target.DiagramId != owner.DiagramId)
                            throw new InvalidDataException("Compensation target requires a native activity in the same flow container.");
                        activity = target.Value;
                    }
                    SetCompensationTarget(d, activity);
                }
            }
        }
    }

    private void ResolveCompensationReferences(object model)
    {
        var graph = Graph(model).ToArray();
        foreach (var entry in graph.Where(e => DescribeEvent(e.Value) != null))
            foreach (var definition in Items(entry.Value, "EventDefinitions").Where(d => Text(d, "EventDefinitionType") == "Compensation"))
            {
                var info = DescribeEventDefinition(definition).Compensation!;
                if (info.ActivityId != "" || info.BpmnName == "" || info.BpmnNamespace != "") continue;
                var matches = graph.Where(e => e.ParentId == entry.ParentId && e.DiagramId == entry.DiagramId && IsNativeActivity(e.Value) &&
                    (Text(e.Value, "Id") == info.BpmnName || Text(e.Value, "BpmnId") == info.BpmnName)).ToArray();
                // XPDL loads a QName but persists the Activity object. Resolve only one exact
                // same-container native identity; unresolved aliases remain visible and protected.
                if (matches.Length == 1 && (info.CatalogActivityId == "" || info.CatalogActivityId == Text(matches[0].Value, "Id"))) Set(definition, "Activity", matches[0].Value);
            }
    }
    private static void RequireNoCompensationTargets(IEnumerable<GraphEntry> graph, string removedId)
    {
        foreach (var entry in graph.Where(e => Text(e.Value, "Id") != removedId && DescribeEvent(e.Value) != null))
            foreach (var definition in Items(entry.Value, "EventDefinitions").Where(d => Text(d, "EventDefinitionType") == "Compensation"))
            {
                var c = DescribeEventDefinition(definition).Compensation!;
                if (c.ActivityId == removedId || c.CatalogActivityId == removedId || c.BpmnName == removedId || c.BpmnName == "Id_" + removedId)
                    throw new InvalidDataException("Clear or redirect compensation event references before deleting their activity.");
            }
    }
    private void RemapCompensationTargets(object model, string sourceId, string targetId, IDictionary map)
    {
        var graph = Graph(model).ToDictionary(e => Text(e.Value, "Id"));
        foreach (var source in graph.Values.Where(e => e.DiagramId == sourceId && DescribeEvent(e.Value) != null))
        {
            var sourceDefinitions = Items(source.Value, "EventDefinitions").Where(d => Text(d, "EventDefinitionType") == "Compensation").ToArray();
            if (sourceDefinitions.Length == 0) continue;
            var copy = graph[map[Guid.Parse(Text(source.Value, "Id"))]!.ToString()!];
            // The native collaboration cloner can share definition objects with its source.
            // Detach the collection and use each compensation definition's installed Clone
            // method before changing references; never mutate the source through a shared object.
            var detached = New(Get(copy.Value, "EventDefinitions").GetType());
            foreach (var definition in Items(copy.Value, "EventDefinitions"))
            {
                object independent = definition;
                if (Text(definition, "EventDefinitionType") == "Compensation")
                {
                    independent = ((ICloneable)definition).Clone();
                    if (ReferenceEquals(independent, definition)) throw new InvalidDataException("Native compensation cloning returned the original definition.");
                }
                Call(detached, "Add", independent);
            }
            Set(copy.Value, "EventDefinitions", detached);
            foreach (var d in sourceDefinitions)
            {
                string id = DescribeEventDefinition(d).Compensation!.ActivityId; if (id == "") continue;
                var defs = Items(copy.Value, "EventDefinitions").Where(v => Text(v, "EventDefinitionType") == "Compensation").ToArray();
                string mapped = map[Guid.Parse(id)]?.ToString() ?? throw new InvalidDataException("Native clone omitted its compensation target.");
                if (defs.Length != 1 || !graph.TryGetValue(mapped, out var target) || target.DiagramId != targetId || target.ParentId != copy.ParentId)
                    throw new InvalidDataException("Native clone compensation mapping is not a unique same-container reference.");
                SetCompensationTarget(defs[0], target.Value);
            }
        }
    }
}
