using System.Globalization;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Typed payload intent and independent readback; native execution is a separate acceptance gate.</summary>
public static partial class NativeEventPayloadPolicy
{
    public static readonly string[] Kinds = ["Message", "Timer", "Conditional", "Link", "Signal", "Error", "Escalation", "Compensation"];
    public static void Validate(NativeMutation change)
    {
        if (change.EventPayloads is not { } patches) return;
        if (change.Operation is not "create" and not "update" || patches.Length is < 1 or > 8 || patches.Any(p => p == null) || patches.Select(p => p.Kind).Distinct().Count() != patches.Length)
            throw new InvalidDataException("EventPayloads requires unique, nonempty definition patches on create/update.");
        foreach (var p in patches)
        {
            if (!Kinds.Contains(p.Kind) || p.Name == null && p.Condition == null && p.Timer == null && p.ErrorCode == null && p.EscalationCode == null && p.Compensation == null)
                throw new InvalidDataException("Supply a known event definition kind and an actual payload change.");
            if (p.Name != null && p.Kind is not "Message" and not "Conditional" and not "Link" and not "Signal" || p.Condition != null && p.Kind != "Conditional" ||
                p.Timer != null && p.Kind != "Timer" || p.ErrorCode != null && p.Kind != "Error" || p.EscalationCode != null && p.Kind != "Escalation" || p.Compensation != null && p.Kind != "Compensation")
                throw new InvalidDataException("Event payload fields must match their selected definition kind.");
            foreach (string? text in new[] { p.Name, p.Condition, p.ErrorCode, p.EscalationCode, p.Timer?.Text })
                if (text?.Length > 1024 * 1024) throw new InvalidDataException("Event payload text exceeds the native operation bound.");
            if (p.Timer is { } t && (t.Text == null || t.Kind is not "None" and not "Cycle" and not "Date" || t.Kind == "None" && t.Text != "" ||
                t.Kind == "Cycle" && (!t.Text.StartsWith("R", StringComparison.Ordinal) || !t.Text.Contains('/')) ||
                t.Kind == "Date" && !DateTime.TryParseExact(t.Text, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
                throw new InvalidDataException("Timer requires None/empty, native canonical Cycle (R.../...), or Date (yyyy-MM-ddTHH:mm:ss without a zone). This is not timer-expression execution validation.");
            if (p.Compensation is { } c)
            {
                if (c.ActivityId == null && c.WaitForCompletion == null) throw new InvalidDataException("Compensation requires an actual property patch.");
                if (c.ActivityId is { Length: > 0 }) NativeMetadataPolicy.RequireId(c.ActivityId);
            }
        }
    }

    public static void Verify(NativeMutation change, NativeElement element, NativeElement[] graph)
    {
        if (change.EventPayloads is not { } patches) return;
        foreach (var p in patches)
        {
            var matches = element.Event?.Definitions.Where(d => d.Kind == p.Kind).ToArray() ?? [];
            if (matches.Length != 1) throw new InvalidDataException("Native event definition kind is absent or ambiguous after restart.");
            var a = matches[0];
            if (p.Name != null && p.Name != a.Name || p.Condition != null && p.Condition != a.Condition || p.ErrorCode != null && p.ErrorCode != a.ErrorCode ||
                p.EscalationCode != null && p.EscalationCode != a.EscalationCode || p.Timer != null && (a.Timer == null || p.Timer.Kind != a.Timer.Kind || p.Timer.Text != a.Timer.Text))
                throw new InvalidDataException("Native event payload differs after fresh-worker readback.");
            if (p.Compensation is { } c)
            {
                if (a.Compensation is not { } actual || c.WaitForCompletion.HasValue && c.WaitForCompletion != actual.WaitForCompletion)
                    throw new InvalidDataException("Native compensation wait flag differs after restart.");
                if (c.ActivityId is { } id)
                {
                    var target = graph.Where(e => e.Id == id).ToArray();
                    if (actual.ActivityId != id || actual.BpmnNamespace != "" || actual.CatalogActivityId != "" && actual.CatalogActivityId != id ||
                        actual.BpmnName != "" && actual.BpmnName != id && actual.BpmnName != "Id_" + id ||
                        id != "" && (target.Length != 1 || target[0].ActivityProperties == null || target[0].ParentId != element.ParentId || target[0].DiagramId != element.DiagramId))
                        throw new InvalidDataException("Native compensation target did not survive as a consistent same-container activity reference.");
                }
            }
        }
    }
}
