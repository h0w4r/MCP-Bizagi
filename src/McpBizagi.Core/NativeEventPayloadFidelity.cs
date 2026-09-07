using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

public static partial class NativeEventPayloadPolicy
{
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static string PayloadName(string kind) => kind switch
    {
        "Message" => "TriggerResultMessage", "Timer" => "TriggerTimer", "Conditional" => "TriggerConditional", "Link" => "TriggerResultLink",
        "Signal" => "TriggerResultSignal", "Error" => "ResultError", "Escalation" => "TriggerEscalation", "Compensation" => "TriggerResultCompensation", _ => throw new InvalidDataException("Unknown payload kind.")
    };
    private static XElement Payload(XElement activity, string kind)
    {
        if (activity.Name != Ns + "Activity" || !NativeFidelity.IsNativeNameOwner(activity)) throw new InvalidDataException("Event payload requires an actual native Activity owner.");
        var wrappers = activity.Elements(Ns + "Event").ToArray();
        if (wrappers.Length != 1) throw new InvalidDataException("Expected one native Event wrapper.");
        var modes = wrappers[0].Elements().Where(e => e.Name == Ns + "StartEvent" || e.Name == Ns + "IntermediateEvent" || e.Name == Ns + "EndEvent").ToArray();
        if (modes.Length != 1) throw new InvalidDataException("Expected one native event mode.");
        var multiple = modes[0].Elements().Where(IsMultipleWrapper).ToArray();
        var direct = modes[0].Elements(Ns + PayloadName(kind)).ToArray();
        var nested = multiple.SelectMany(e => e.Elements(Ns + PayloadName(kind))).ToArray();
        if (multiple.Length > 1 || direct.Length + nested.Length != 1 || multiple.Length > 0 && direct.Length > 0)
            throw new InvalidDataException("Native event payload is absent or ambiguous.");
        return direct.Concat(nested).Single();
    }
    internal static bool IsCompensationReference(XElement node)
    {
        if (node.Name != Ns + "TriggerResultCompensation") return false;
        var mode = node.Parent;
        if (mode != null && IsMultipleWrapper(mode)) mode = mode.Parent;
        return mode != null && (mode.Name == Ns + "StartEvent" || mode.Name == Ns + "IntermediateEvent" || mode.Name == Ns + "EndEvent") &&
            mode.Parent?.Name == Ns + "Event" && mode.Parent.Parent?.Name == Ns + "Activity" && NativeFidelity.IsNativeNameOwner(mode.Parent.Parent);
    }
    // Native intermediate catch/throw/boundary serialization has its own wrapper,
    // distinct from start TriggerMultiple and end ResultMultiple.
    private static bool IsMultipleWrapper(XElement node) => node.Name == Ns + "TriggerMultiple" && node.Parent?.Name == Ns + "StartEvent" ||
        node.Name == Ns + "ResultMultiple" && node.Parent?.Name == Ns + "EndEvent" ||
        node.Name == Ns + "TriggerIntermediateMultiple" && node.Parent?.Name == Ns + "IntermediateEvent";
    public static void Project(XElement before, XElement after, NativeEventPayloadPatch[] patches)
    {
        foreach (var p in patches)
        {
            var a = Payload(before, p.Kind); var b = Payload(after, p.Kind);
            if (a.Parent!.Name != b.Parent!.Name) throw new InvalidDataException("A payload patch cannot change its native event mode or multiple wrapper.");
            void Scalar(XElement old, XElement current, string field, string expected, string absent = "")
            {
                if (((string?)current.Attribute(field) ?? absent) != expected) throw new InvalidDataException("Durable event payload differs: " + field);
                current.SetAttributeValue(field, (string?)old.Attribute(field));
            }
            if (p.Name != null)
            {
                if (p.Kind == "Message")
                {
                    var old = a.Elements(Ns + "Message").ToArray(); var updated = b.Elements(Ns + "Message").ToArray();
                    if (old.Length != 1 || updated.Length != 1) throw new InvalidDataException("Expected one native message payload.");
                    Scalar(old[0], updated[0], "Name", p.Name);
                }
                // Only Link.Name is native XmlAttribute(DataType="NMTOKEN"). Compare its
                // exact framework encoding, not a blanket decoded normalization of XML.
                else Scalar(a, b, p.Kind == "Conditional" ? "ConditionName" : "Name", p.Kind == "Link" ? System.Xml.XmlConvert.EncodeNmToken(p.Name) : p.Name);
            }
            if (p.ErrorCode != null) Scalar(a, b, "ErrorCode", p.ErrorCode);
            if (p.EscalationCode != null) Scalar(a, b, "EscalationCode", p.EscalationCode);
            if (p.Timer is { } timer)
            {
                Scalar(a, b, "TimeCycle", timer.Kind == "Cycle" ? timer.Text : "");
                Scalar(a, b, "TimeDate", timer.Kind == "Date" ? timer.Text : "");
            }
            if (p.Compensation is { } c)
            {
                if (c.WaitForCompletion is bool wait) Scalar(a, b, "WaitForCompletion", wait ? "true" : "false", "true");
                if (c.ActivityId != null) Scalar(a, b, "ActivityId", c.ActivityId);
            }
            if (p.Condition != null)
            {
                var old = a.Elements(Ns + "Expression").ToArray(); var updated = b.Elements(Ns + "Expression").ToArray();
                if (old.Length > 1 || updated.Length > 1 || (updated.FirstOrDefault()?.Value ?? "") != p.Condition ||
                    old.Concat(updated).Any(e => e.HasAttributes || e.Nodes().Any(n => n.GetType() != typeof(XText))))
                    throw new InvalidDataException("Condition text differs or contains unknown native expression content.");
                // Restore only the requested expression for comparison, never the payload
                // subtree. Unknown attributes, identities, namespaces and siblings survive.
                if (old.Length == 1 && updated.Length == 1) updated[0].ReplaceWith(new XElement(old[0]));
                else if (old.Length == 1) b.AddFirst(new XElement(old[0]));
                else if (updated.Length == 1) updated[0].Remove();
            }
        }
    }
}
