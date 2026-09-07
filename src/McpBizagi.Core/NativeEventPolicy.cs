using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Explicit event modes, native readback and narrow event-attribute fidelity projection.</summary>
public static class NativeEventPolicy
{
    public static readonly string[] AdditionalTypes = ["ConditionalStart", "SignalStart", "MultipleStart", "ParallelMultipleStart",
        "EscalationIntermediate", "ConditionalIntermediate", "LinkIntermediate", "ErrorIntermediate", "CompensationIntermediate",
        "SignalIntermediate", "MultipleIntermediate", "ParallelMultipleIntermediate", "EscalationEnd", "ErrorEnd", "CompensationEnd",
        "SignalEnd", "MultipleEnd", "EventBasedGatewayExclusive", "EventBasedGatewayParallel"];
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";

    public static string Mode(NativeMutation c) => c.EventMode ?? (c.ElementType == "NoneIntermediate" ? "Throw" :
        c.ElementType is "MessageIntermediate" or "TimerIntermediate" ? "Catch" : "");

    public static void Validate(NativeMutation c)
    {
        bool intermediate = c.Operation == "create" && c.ElementType.EndsWith("Intermediate", StringComparison.Ordinal);
        if (c.EventMode != null && (!intermediate || c.EventMode is not "Catch" and not "Throw" and not "Boundary"))
            throw new InvalidDataException("EventMode is an explicit Catch, Throw or Boundary creation option for intermediate events only.");
        if (intermediate)
        {
            string mode = Mode(c);
            string[] allowed = mode switch
            {
                "Catch" => ["MessageIntermediate", "TimerIntermediate", "ConditionalIntermediate", "LinkIntermediate", "SignalIntermediate", "MultipleIntermediate", "ParallelMultipleIntermediate"],
                "Throw" => ["NoneIntermediate", "MessageIntermediate", "EscalationIntermediate", "LinkIntermediate", "CompensationIntermediate", "SignalIntermediate", "MultipleIntermediate"],
                "Boundary" => ["MessageIntermediate", "TimerIntermediate", "EscalationIntermediate", "ConditionalIntermediate", "ErrorIntermediate", "CompensationIntermediate", "SignalIntermediate", "MultipleIntermediate", "ParallelMultipleIntermediate"],
                _ => []
            };
            if (!allowed.Contains(c.ElementType)) throw new InvalidDataException("Unsupported event kind/mode combination; additional intermediate types require explicit EventMode.");
            if (mode == "Boundary" && c.EventProperties?.AttachedToActivityId == null)
                throw new InvalidDataException("Boundary creation requires an explicit AttachedToActivityId.");
        }
        if (c.EventProperties is not { } p) return;
        if (c.Operation is not "create" and not "update" || p.IsInterrupting == null && p.AttachedToActivityId == null)
            throw new InvalidDataException("EventProperties requires a nonempty create/update patch.");
        if (p.AttachedToActivityId is { } id && (!Guid.TryParseExact(id, "D", out var guid) || guid == Guid.Empty || guid.ToString() != id))
            throw new InvalidDataException("AttachedToActivityId must be a canonical, nonempty native GUID. Detachment is not an event-mode conversion.");
        if (c.Operation == "create")
        {
            bool start = c.ElementType.EndsWith("Start", StringComparison.Ordinal), boundary = intermediate && Mode(c) == "Boundary";
            if (!start && !boundary || p.AttachedToActivityId != null && !boundary)
                throw new InvalidDataException("Interrupting applies to start/boundary events; attachment applies only to boundaries.");
            if (p.IsInterrupting == false && c.ElementType is "ErrorIntermediate" or "CompensationIntermediate")
                throw new InvalidDataException("Error and compensation boundaries do not support a noninterrupting flag.");
        }
    }

    public static void Verify(NativeMutation c, NativeElement element, NativeElement[] graph)
    {
        var actual = element.Event;
        if (c.Operation == "create" && c.ElementType.EndsWith("Intermediate", StringComparison.Ordinal) &&
            (actual == null || actual.Mode != Mode(c))) throw new InvalidDataException("Native event mode differs after fresh-worker readback.");
        if (c.EventProperties is not { } p) return;
        if (actual == null || p.IsInterrupting.HasValue && p.IsInterrupting != actual.IsInterrupting)
            throw new InvalidDataException("Native event interruption differs after fresh-worker readback.");
        if (p.AttachedToActivityId is { } id)
        {
            var targets = graph.Where(e => e.Id == id).ToArray();
            if (actual.Mode != "Boundary" || actual.AttachedToActivityId != id || targets.Length != 1 || targets[0].ActivityProperties == null ||
                targets[0].ParentId != element.ParentId || targets[0].DiagramId != element.DiagramId ||
                actual.AttachedToCatalogActivityId != "" && actual.AttachedToCatalogActivityId != id ||
                actual.AttachedToBpmnNamespace != "" || actual.AttachedToBpmnName != "" && actual.AttachedToBpmnName != id && actual.AttachedToBpmnName != "Id_" + id)
                throw new InvalidDataException("Native boundary attachment did not survive as a consistent same-container activity reference.");
        }
    }

    public static void Project(XElement before, XElement after, NativeEventProperties patch)
    {
        if (before.Name != Ns + "Activity" || after.Name != before.Name || !NativeFidelity.IsNativeNameOwner(before) || !NativeFidelity.IsNativeNameOwner(after))
            throw new InvalidDataException("Event projection requires actual native Activity/Event XML.");
        XElement Event(XElement owner)
        {
            var wrappers = owner.Elements(Ns + "Event").ToArray();
            if (wrappers.Length != 1) throw new InvalidDataException("Expected one native event wrapper.");
            var events = wrappers[0].Elements().Where(e => e.Name == Ns + "StartEvent" || e.Name == Ns + "IntermediateEvent").ToArray();
            if (events.Length != 1) throw new InvalidDataException("Expected one native start or intermediate event.");
            return events[0];
        }
        var a = Event(before); var b = Event(after);
        if (a.Name != b.Name) throw new InvalidDataException("An event property patch cannot replace its native event kind.");
        void Scalar(string key, string expected, string absent)
        {
            if (((string?)b.Attribute(key) ?? absent) != expected) throw new InvalidDataException("Native event attribute differs: " + key);
            b.SetAttributeValue(key, (string?)a.Attribute(key));
        }
        // Only explicitly requested attributes are projected. Trigger payloads, unknown
        // attributes, comments, namespaces and sibling content remain fully compared.
        if (patch.IsInterrupting is bool interrupting) Scalar("Interrupting", interrupting ? "true" : "false", "true");
        if (patch.AttachedToActivityId is { } id)
        {
            if (a.Name != Ns + "IntermediateEvent" || (string?)a.Attribute("IsAttached") != "true" || (string?)b.Attribute("IsAttached") != "true")
                throw new InvalidDataException("Attachment projection requires two native boundary events.");
            Scalar("Target", id, "");
        }
    }
}
