using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static bool IsNativeActivity(object element)
    {
        for (var type = element.GetType(); type != null; type = type.BaseType)
            if (type.FullName == "Bizagi.ProcessModeler.BusinessEntities.BPMN20.Activity") return true;
        return false;
    }

    private static NativeActivityProperties? DescribeActivity(object element) => !IsNativeActivity(element) ? null : new NativeActivityProperties
    {
        StartQuantity = (int)Get(element, "StartQuantity"), CompletionQuantity = (int)Get(element, "CompletionQuantity"),
        IsForCompensation = (bool)Get(element, "IsForCompensation"), State = Text(element, "State")
    };

    private static NativeFlowCondition? DescribeCondition(object element) => element.GetType().Name != "SequenceFlow" ? null : new NativeFlowCondition
    {
        Kind = Text(element, "ConditionType"),
        Text = Optional(element, "ConditionExpression") is object expression ? string.Concat(Optional(expression, "Text") as string[] ?? Array.Empty<string>()) : ""
    };

    private static string[] DefaultFlowIds(object element) => Items(element, "OutgoingSequenceFlows")
        .Where(f => Text(f, "ConditionType") == "Default").Select(f => Text(f, "Id")).ToArray();

    private static void ApplyActivityProperties(object element, NativeActivityProperties patch)
    {
        if (!IsNativeActivity(element)) throw new InvalidDataException("ActivityProperties requires a native activity, not an event, gateway or container process.");
        // Explicit setters preserve every omitted field and do not interpret arbitrary property paths.
        if (patch.StartQuantity is int start) Set(element, "StartQuantity", start);
        if (patch.CompletionQuantity is int completed) Set(element, "CompletionQuantity", completed);
        if (patch.IsForCompensation is bool compensation) Set(element, "IsForCompensation", compensation);
        if (patch.State != null) Set(element, "State", patch.State);
    }

    private void ApplyFlowCondition(object element, NativeFlowCondition patch)
    {
        if (element.GetType().Name != "SequenceFlow") throw new InvalidDataException("FlowCondition requires a native sequence flow.");
        if (patch.Kind != "None" && (Optional(element, "Source") is not object source || !CanHaveConditions(source)))
            throw new InvalidDataException("Conditional/default flow requires an activity or an exclusive, inclusive or complex gateway source.");
        Set(element, "ConditionType", Enum.Parse(element.GetType().GetProperty("ConditionType")!.PropertyType, patch.Kind));
        object? expression = null;
        if (patch.Kind == "Expression")
        {
            expression = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.Expression"));
            Set(expression, "Text", new[] { patch.Text });
        }
        Set(element, "ConditionExpression", expression!);
    }

    private static bool CanHaveConditions(object source) => IsNativeActivity(source) || source.GetType().Name is "ExclusiveGateway" or "InclusiveGateway" or "ComplexGateway";

    private void SynchronizeDefaultFlow(object? source)
    {
        if (source == null) return;
        var flows = Items(source, "OutgoingSequenceFlows").ToArray();
        if (flows.Any(f => Text(f, "ConditionType") != "None") && !CanHaveConditions(source))
            throw new InvalidDataException("Conditional/default flow cannot be reconnected to this source kind.");
        var defaults = flows.Where(f => Text(f, "ConditionType") == "Default").ToArray();
        if (defaults.Length > 1) throw new InvalidDataException("A source cannot have multiple default sequence flows; clear the previous default explicitly first.");
        // Only touched connector sources are synchronized. This maintains native in-memory aliases
        // for rendering/export without silently changing another flow's requested condition.
        foreach (string property in new[] { "Default", "DefaultSequenceFlow", "DefaultSequenceCatalogRef" })
        {
            if (source.GetType().GetProperty(property) == null) continue;
            object? value = defaults.Length == 0 ? null : property == "Default" ? Text(defaults[0], "Id") : property == "DefaultSequenceFlow" ? defaults[0] :
                New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Common.CatalogReference"), Guid.Parse(Text(defaults[0], "Id")));
            Set(source, property, value!);
        }
    }
}
