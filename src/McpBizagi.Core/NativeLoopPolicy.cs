using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Loop intent/readback and exact XPDL comparison projection; never writes model bytes.</summary>
public static class NativeLoopPolicy
{
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    public static void Validate(NativeActivityLoop loop)
    {
        if (loop.Kind is not "None" and not "Standard" and not "MultiInstance" ||
            (loop.Standard != null) != (loop.Kind == "Standard") || (loop.MultiInstance != null) != (loop.Kind == "MultiInstance"))
            throw new InvalidDataException("ActivityLoop requires exactly the configuration matching None, Standard or MultiInstance.");
        void Text(string? value) { if (value?.Length > 1024 * 1024) throw new InvalidDataException("Loop expression exceeds the native text bound."); }
        if (loop.Standard is { } s)
        {
            if (s.Maximum < 0 || s.Counter < 0) throw new InvalidDataException("Native loop maximum/counter must be nonnegative.");
            Text(s.Condition);
        }
        if (loop.MultiInstance is { } m)
        {
            if (m.Counter < 0 || m.Behavior is not "All" and not "One" and not "None" and not "Complex" ||
                m.ComplexCondition != null && m.Behavior != "Complex") throw new InvalidDataException("Invalid native multi-instance behavior, counter or complex condition.");
            Text(m.CompletionCondition); Text(m.ComplexCondition);
        }
    }

    public static void Verify(NativeActivityLoop intended, NativeActivityLoop? actual)
    {
        if (actual == null || JsonSerializer.Serialize(intended) != JsonSerializer.Serialize(actual))
            throw new InvalidDataException("Native activity loop differs after independent readback.");
    }

    public static void Project(XElement before, XElement after, NativeActivityLoop intended)
    {
        if (before.Name != Ns + "Activity" || after.Name != before.Name || !NativeFidelity.IsNativeNameOwner(before) || !NativeFidelity.IsNativeNameOwner(after))
            throw new InvalidDataException("Loop projection requires an actual native activity XML owner.");
        var old = before.Elements(Ns + "Loop").ToArray(); var updated = after.Elements(Ns + "Loop").ToArray();
        if (old.Length > 1 || updated.Length != 1) throw new InvalidDataException("Expected exactly one persisted native loop definition.");
        if (old.Length == 1) ReadKnown(old[0]); // A complete replacement still cannot discard unknown native content.
        Verify(intended, ReadKnown(updated[0]));
        if (old.Length == 1) updated[0].ReplaceWith(new XElement(old[0])); else updated[0].Remove();
    }

    private static NativeActivityLoop ReadKnown(XElement loop)
    {
        void Shape(XElement node, string[] attributes, string[] children)
        {
            if (node.Attributes().Any(a => a.Name.Namespace != XNamespace.None || !attributes.Contains(a.Name.LocalName)) ||
                node.Elements().Any(e => e.Name.Namespace != Ns || !children.Contains(e.Name.LocalName)) ||
                node.Nodes().Any(n => n is not XElement && (n.GetType() != typeof(XText) || !string.IsNullOrWhiteSpace(((XText)n).Value))) ||
                node.AncestorsAndSelf().Select(e => (string?)e.Attribute(XNamespace.Xml + "space")).FirstOrDefault(v => v != null) == "preserve")
                throw new InvalidDataException("Unknown or significant native loop content cannot be discarded.");
            foreach (var name in children) if (node.Elements(Ns + name).Count() > 1) throw new InvalidDataException("Duplicate native loop child.");
        }
        string? Expression(XElement parent, string name)
        {
            var value = parent.Element(Ns + name); if (value == null) return null;
            if (value.HasAttributes || value.Nodes().Any(n => n.GetType() != typeof(XText))) throw new InvalidDataException("Unknown native loop expression content.");
            return value.Value;
        }
        int Number(XElement node, string field) => int.TryParse((string?)node.Attribute(field), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value : throw new InvalidDataException("Missing or invalid native loop integer: " + field);
        Shape(loop, ["LoopType"], ["LoopStandard", "LoopMultiInstance"]);
        string kind = (string?)loop.Attribute("LoopType") ?? "None";
        var result = new NativeActivityLoop { Kind = kind };
        var standard = loop.Element(Ns + "LoopStandard"); var multi = loop.Element(Ns + "LoopMultiInstance");
        if (kind == "Standard" && standard != null)
        {
            Shape(standard, ["LoopMaximum", "LoopCounter", "TestTime"], ["LoopCondition"]);
            string test = (string?)standard.Attribute("TestTime") ?? "After";
            if (test is not "After" and not "Before") throw new InvalidDataException("Unknown native loop test time.");
            result.Standard = new() { Maximum = Number(standard, "LoopMaximum"), Counter = Number(standard, "LoopCounter"), TestBefore = test == "Before", Condition = Expression(standard, "LoopCondition") };
        }
        if (kind == "MultiInstance" && multi != null)
        {
            Shape(multi, ["LoopCounter", "MI_Ordering", "MI_FlowCondition"], ["MI_Condition", "ComplexMI_FlowCondition"]);
            string order = (string?)multi.Attribute("MI_Ordering") ?? "Parallel";
            if (order is not "Parallel" and not "Sequential") throw new InvalidDataException("Unknown native loop ordering.");
            result.MultiInstance = new() { Counter = Number(multi, "LoopCounter"), IsSequential = order == "Sequential", Behavior = (string?)multi.Attribute("MI_FlowCondition") ?? "All",
                CompletionCondition = Expression(multi, "MI_Condition"), ComplexCondition = Expression(multi, "ComplexMI_FlowCondition") };
        }
        if (standard != null && kind != "Standard" || multi != null && kind != "MultiInstance") throw new InvalidDataException("Ambiguous native loop implementation.");
        Validate(result); return result;
    }
}
