using System.Globalization;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Explicit semantic validation and exact native serialization projection, never a model writer.</summary>
public static class NativeSemanticPolicy
{
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    public static void ProjectDerivedQuantities(XDocument before, XDocument after, NativeMutation[] changes, NativeElement[] graph)
    {
        var oldFlows = before.Descendants(Ns + "Transition").Where(NativeFidelity.IsNativeNameOwner).ToLookup(e => (string?)e.Attribute("Id"));
        foreach (var flow in after.Descendants(Ns + "Transition").Where(NativeFidelity.IsNativeNameOwner))
        {
            string? id = (string?)flow.Attribute("Id"), source = (string?)flow.Attribute("From");
            var old = oldFlows[id].ToArray();
            if (old.Length == 0) continue; // Newly created flows have their own creation projection.
            bool reconnected = changes.Any(c => c.ElementId == id && c.Operation == "reconnect");
            bool quantityRequested = changes.Any(c => c.ElementId == source && c.ActivityProperties?.CompletionQuantity != null);
            if (!reconnected && !quantityRequested) continue;
            if (old.Length != 1) throw new InvalidDataException("Ambiguous derived native sequence-flow quantity.");
            var sources = graph.Where(e => e.Id == source).ToArray();
            if (sources.Length != 1) throw new InvalidDataException("Missing or ambiguous native source for transition quantity verification.");
            var sourceElement = sources[0];
            int quantity = sourceElement.ActivityProperties?.CompletionQuantity ?? 1;
            if (((string?)flow.Attribute("Quantity") ?? "1") != quantity.ToString(CultureInfo.InvariantCulture))
                throw new InvalidDataException("Native transition quantity differs from its verified source activity completion quantity.");
            // The installed XPDL adapter derives this exact attribute from source.CompletionQuantity.
            // Every other transition field stays compared, including unknown same-named extensions.
            flow.SetAttributeValue("Quantity", (string?)old[0].Attribute("Quantity"));
        }
    }

    public static void Validate(NativeMutation c)
    {
        if (c.ActivityProperties is { } a)
        {
            if (a.StartQuantity == null && a.CompletionQuantity == null && a.IsForCompensation == null && a.State == null)
                throw new InvalidDataException("ActivityProperties must specify a field.");
            if (a.StartQuantity is <= 0 || a.CompletionQuantity is <= 0) throw new InvalidDataException("Activity token quantities must be positive integers.");
            if (a.State != null && !new[] { "None", "Ready", "Active", "Completing", "Completed", "Aborted", "Aborting" }.Contains(a.State))
                throw new InvalidDataException("Unknown native activity state.");
            if (c.Operation == "create" && !c.ElementType.EndsWith("Task", StringComparison.Ordinal) && c.ElementType is not "SubProcess" and not "CallActivity")
                throw new InvalidDataException("ActivityProperties applies only to native activities.");
        }
        if (c.GatewayDirection != null && (!new[] { "Unspecified", "Converging", "Diverging", "Mixed" }.Contains(c.GatewayDirection) ||
            c.Operation == "create" && !c.ElementType.EndsWith("Gateway", StringComparison.Ordinal))) throw new InvalidDataException("Supply a known native gateway direction on a gateway.");
        if (c.FlowCondition is { } f)
        {
            if (!new[] { "None", "Expression", "Default" }.Contains(f.Kind) || f.Text == null || f.Text.Length > 1024 * 1024 ||
                f.Kind != "Expression" && f.Text != "" || c.Operation == "create" && c.ElementType != "SequenceFlow")
                throw new InvalidDataException("FlowCondition requires a sequence flow and None, Expression or Default; only Expression accepts text.");
        }
    }

    public static void Verify(NativeMutation c, NativeElement e, NativeElement[] graph)
    {
        if (c.ActivityProperties is { } a)
        {
            var b = e.ActivityProperties;
            if (b == null || a.StartQuantity.HasValue && a.StartQuantity != b.StartQuantity || a.CompletionQuantity.HasValue && a.CompletionQuantity != b.CompletionQuantity ||
                a.IsForCompensation.HasValue && a.IsForCompensation != b.IsForCompensation || a.State != null && a.State != b.State)
                throw new InvalidDataException("Native activity properties differ after fresh-worker readback.");
        }
        if (c.GatewayDirection != null && c.GatewayDirection != e.GatewayDirection) throw new InvalidDataException("Native gateway direction differs after readback.");
        if (c.FlowCondition is { } f)
        {
            if (e.Kind != "SequenceFlow" || e.FlowCondition == null || e.FlowCondition.Kind != f.Kind || e.FlowCondition.Text != f.Text)
                throw new InvalidDataException("Native flow condition differs after readback.");
            if (f.Kind == "Default" && graph.Single(g => g.Id == e.SourceId).DefaultSequenceFlowIds is var ids && !ids.SequenceEqual(new[] { e.Id }))
                throw new InvalidDataException("Native default flow did not survive as the source's unique default.");
        }
    }

    public static void Project(XElement before, XElement after, NativeMutation change)
    {
        if (!NativeFidelity.IsNativeNameOwner(before) || !NativeFidelity.IsNativeNameOwner(after)) throw new InvalidDataException("Semantic projection requires an actual native owner.");
        void Scalar(XElement a, XElement b, string name, string expected, string absent)
        {
            if (((string?)b.Attribute(name) ?? absent) != expected) throw new InvalidDataException("Durable native semantic value differs: " + name);
            b.SetAttributeValue(name, (string?)a.Attribute(name));
        }
        if (change.ActivityProperties is { } properties)
        {
            if (before.Name != Ns + "Activity" || after.Name != before.Name) throw new InvalidDataException("Activity semantic projection requires native Activity XML.");
            if (properties.StartQuantity is int start) Scalar(before, after, "StartQuantity", start.ToString(CultureInfo.InvariantCulture), "1");
            if (properties.CompletionQuantity is int count) Scalar(before, after, "CompletionQuantity", count.ToString(CultureInfo.InvariantCulture), "1");
            if (properties.IsForCompensation is bool compensation) Scalar(before, after, "IsForCompensation", compensation ? "true" : "false", "false");
            if (properties.State != null) Scalar(before, after, "Status", properties.State, "None");
        }
        if (change.GatewayDirection != null)
        {
            if (before.Name != Ns + "Activity" || after.Name != before.Name) throw new InvalidDataException("Gateway direction requires native Activity/Route XML.");
            Scalar(before.Elements(Ns + "Route").Single(), after.Elements(Ns + "Route").Single(), "GatewayDirection", change.GatewayDirection, "Unspecified");
        }
        if (change.FlowCondition is { } condition)
        {
            if (before.Name != Ns + "Transition" || after.Name != before.Name) throw new InvalidDataException("Flow conditions require native Transition XML.");
            var oldConditions = before.Elements(Ns + "Condition").ToArray(); var newConditions = after.Elements(Ns + "Condition").ToArray();
            if (oldConditions.Length != 1 || newConditions.Length != 1) throw new InvalidDataException("Expected one native sequence-flow Condition.");
            var a = oldConditions[0]; var b = newConditions[0];
            Scalar(a, b, "Type", condition.Kind == "Expression" ? "CONDITION" : condition.Kind == "Default" ? "OTHERWISE" : "", "");
            var oldExpressions = a.Elements(Ns + "Expression").ToArray(); var newExpressions = b.Elements(Ns + "Expression").ToArray();
            if (oldExpressions.Length > 1 || newExpressions.Length > 1 || newExpressions.Length != (condition.Kind == "Expression" ? 1 : 0))
                throw new InvalidDataException("Unexpected native flow expression shape.");
            // An explicit complete condition replaces only its native text expression. Unknown
            // attributes/elements/comments are not considered authorized collateral changes.
            bool TextOnly(XElement e) => !e.HasAttributes && e.Nodes().All(n => n is XText);
            if (oldExpressions.Any(e => !TextOnly(e)) || newExpressions.Any(e => !TextOnly(e))) throw new InvalidDataException("Unknown native expression content cannot be discarded by this contract.");
            if (newExpressions.Length == 1 && newExpressions[0].Value != condition.Text) throw new InvalidDataException("Durable native condition text differs.");
            if (newExpressions.Length == 1) newExpressions[0].Remove();
            if (oldExpressions.Length == 1) b.Add(new XElement(oldExpressions[0]));
            // Removing a newly created expression from the comparison copy can leave the
            // native serializer's child indentation as apparent text in an otherwise empty
            // Condition. Restore that scaffold only when no content or xml:space is hidden.
            bool PreservesSpace(XElement e) => e.AncestorsAndSelf().Select(n => (string?)n.Attribute(XNamespace.Xml + "space")).FirstOrDefault(v => v != null) == "preserve";
            if (oldExpressions.Length == 0 && newExpressions.Length == 1 && !PreservesSpace(a) && !PreservesSpace(b) &&
                a.Nodes().All(n => n.GetType() == typeof(XText) && string.IsNullOrWhiteSpace(((XText)n).Value)) && b.Nodes().All(n => n.GetType() == typeof(XText) && string.IsNullOrWhiteSpace(((XText)n).Value)))
                b.ReplaceNodes(a.Nodes().OfType<XText>().Select(t => new XText(t.Value)));
        }
    }
}
