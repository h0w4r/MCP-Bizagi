using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static bool IsNativeSubProcess(object element)
    {
        // Fixed domain inheritance includes Transaction and AdHocSubProcess, not reusable calls.
        for (var type = element.GetType(); type != null; type = type.BaseType)
            if (type.FullName == "Bizagi.ProcessModeler.BusinessEntities.BPMN20.SubProcess") return true;
        return false;
    }

    private static NativeSubProcessInfo? DescribeSubProcess(object element)
    {
        if (!IsNativeSubProcess(element)) return null;
        bool adHoc = Text(element, "SubProcessType") == "AdHoc", transaction = Text(element, "SubProcessType") == "Transaction";
        return new NativeSubProcessInfo
        {
            Kind = Text(element, "SubProcessType"), TriggeredByEvent = (bool)Get(element, "TriggeredByEvent"),
            AdHocOrdering = adHoc ? Text(element, "Ordering") : null,
            AdHocCompletionCondition = adHoc ? (Optional(element, "CompletionCondition") is object condition ? string.Concat(Optional(condition, "Text") as string[] ?? Array.Empty<string>()) : "") : null,
            CancelRemainingInstances = adHoc ? (bool?)Get(element, "CancelRemainingInstances") : null,
            TransactionMethod = transaction ? Text(element, "Method") : null
        };
    }

    private void ApplySubProcessProperties(object element, NativeSubProcessProperties patch)
    {
        var actual = DescribeSubProcess(element) ?? throw new InvalidDataException("SubProcessProperties requires a native embedded subprocess.");
        if (patch.TriggeredByEvent is bool triggered)
        {
            if (triggered && actual.Kind != "SubProcess") throw new InvalidDataException("Event-triggered subprocesses cannot also be Transaction or AdHoc.");
            Set(element, "TriggeredByEvent", triggered);
        }
        if (patch.AdHocOrdering != null || patch.AdHocCompletionCondition != null)
        {
            if (actual.Kind != "AdHoc") throw new InvalidDataException("Ad hoc properties require a native AdHoc subprocess.");
            if (patch.AdHocOrdering != null)
                Set(element, "Ordering", Enum.Parse(element.GetType().GetProperty("Ordering")!.PropertyType, patch.AdHocOrdering));
            if (patch.AdHocCompletionCondition is { } text)
            {
                object? expression = null;
                if (text != "")
                {
                    expression = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.Expression"));
                    Set(expression, "Text", new[] { text });
                }
                Set(element, "CompletionCondition", expression!);
            }
        }
    }

    private static void ValidateSubProcessContexts(object model, NativeMutation[] changes)
    {
        var graph = Graph(model).ToDictionary(e => Text(e.Value, "Id"), StringComparer.Ordinal);
        var contexts = changes.Where(c => c.SubProcessProperties != null || c.Operation == "create" && c.ElementType == "SubProcess").Select(c => c.ElementId).ToHashSet();
        var touched = changes.Where(c => c.Operation == "create" || c.EventProperties != null).Select(c => c.ElementId).ToHashSet();
        foreach (var entry in graph.Values)
        {
            string id = Text(entry.Value, "Id");
            if (contexts.Contains(id) && IsNativeSubProcess(entry.Value) && (bool)Get(entry.Value, "TriggeredByEvent") &&
                graph.Values.Any(e => e.Value.GetType().Name == "SequenceFlow" &&
                    (Optional(e.Value, "Source") is object source && Text(source, "Id") == id || Optional(e.Value, "Target") is object target && Text(target, "Id") == id)))
                throw new InvalidDataException("Event-triggered subprocesses cannot have incoming or outgoing sequence flows.");
            // Recheck existing child starts when their parent's trigger flag changes. This
            // validates final batch state, permitting an explicit child-first cleanup batch.
            if (!touched.Contains(id) && !contexts.Contains(entry.ParentId)) continue;
            var eventInfo = DescribeEvent(entry.Value); if (eventInfo == null) continue;
            graph.TryGetValue(entry.ParentId, out var owner);
            bool eventSubProcess = owner != null && IsNativeSubProcess(owner.Value) && (bool)Get(owner.Value, "TriggeredByEvent");
            if (eventInfo.Mode == "Start" && (Text(entry.Value, "ElementType") is "ErrorStart" or "EscalationStart" or "CompensationStart" || eventInfo.IsInterrupting == false) && !eventSubProcess)
                throw new InvalidDataException("This start event requires an event-triggered subprocess.");
            if (Text(entry.Value, "ElementType") == "CancelEnd" && (owner == null || DescribeSubProcess(owner.Value)?.Kind != "Transaction"))
                throw new InvalidDataException("Cancel end events require a native transaction subprocess.");
        }
    }
}
