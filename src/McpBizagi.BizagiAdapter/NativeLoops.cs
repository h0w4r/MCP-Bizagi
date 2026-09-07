using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static string? LoopExpression(object owner, string property) => Optional(owner, property) is object expression
        ? string.Concat(Optional(expression, "Text") as string[] ?? Array.Empty<string>()) : null;

    private static NativeActivityLoop? DescribeLoop(object element)
    {
        if (!IsNativeActivity(element)) return null;
        var loop = new NativeActivityLoop { Kind = Text(element, "LoopType") };
        if (loop.Kind == "Standard" && Optional(element, "StandardLoop") is object standard)
            loop.Standard = new NativeStandardLoop { Maximum = (int)Get(standard, "LoopMaximum"), Counter = (int)Get(standard, "LoopCounter"),
                TestBefore = (bool)Get(standard, "TestBefore"), Condition = LoopExpression(standard, "LoopCondition") };
        if (loop.Kind == "MultiInstance" && Optional(element, "MultiInstanceLoop") is object multi)
        {
            var complex = Items(multi, "ComplexBehaviorDefinition").ToArray();
            if (complex.Length > 1) throw new InvalidDataException("Multiple native complex loop conditions cannot be flattened into this contract.");
            loop.MultiInstance = new NativeMultiInstanceLoop { IsSequential = (bool)Get(multi, "IsSequential"), Counter = (int)Get(multi, "LoopCounter"),
                Behavior = Text(multi, "Behavior"), CompletionCondition = LoopExpression(multi, "CompletionCondition"),
                ComplexCondition = complex.Length == 1 ? LoopExpression(complex[0], "Condition") : null };
        }
        return loop;
    }

    private void ApplyLoop(object element, NativeActivityLoop patch)
    {
        if (!IsNativeActivity(element)) throw new InvalidDataException("ActivityLoop requires a native activity.");
        object Create(string name) => New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20." + name));
        object? Expression(string? value, bool formal = false)
        {
            if (value == null) return null;
            object expression = Create(formal ? "FormalExpression" : "Expression");
            Set(expression, "Text", new[] { value }); return expression;
        }
        object? standard = null, multi = null;
        if (patch.Standard is { } s)
        {
            standard = Create("StandardLoopCharacteristics");
            Set(standard, "LoopMaximum", s.Maximum); Set(standard, "LoopCounter", s.Counter);
            Set(standard, "TestBefore", s.TestBefore); Set(standard, "LoopCondition", Expression(s.Condition)!);
        }
        if (patch.MultiInstance is { } m)
        {
            multi = Create("MultiInstanceLoopCharacteristics");
            Set(multi, "IsSequential", m.IsSequential); Set(multi, "LoopCounter", m.Counter);
            Set(multi, "Behavior", Enum.Parse(multi.GetType().GetProperty("Behavior")!.PropertyType, m.Behavior));
            Set(multi, "CompletionCondition", Expression(m.CompletionCondition)!);
            if (m.ComplexCondition != null)
            {
                object complex = Create("ComplexBehaviorDefinition"); Set(complex, "Condition", Expression(m.ComplexCondition, true)!);
                Call(Get(multi, "ComplexBehaviorDefinition"), "Add", complex);
            }
        }
        // Persistence uses these native domain properties. Clear inactive aliases explicitly;
        // assigning only the BPMN XML alias would not persist a loop in a native .bpm.
        Set(element, "LoopType", Enum.Parse(element.GetType().GetProperty("LoopType")!.PropertyType, patch.Kind));
        Set(element, "StandardLoop", standard!); Set(element, "MultiInstanceLoop", multi!);
        Set(element, "LoopCharacteristics", (standard ?? multi)!);
    }
}
