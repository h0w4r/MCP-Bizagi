using System.Xml.Linq;

namespace McpBizagi.Core;

/// <summary>Reports observable normalization; it is deliberately not a complete equivalence proof.</summary>
public static class BpmnFidelity
{
    public static ValidationFinding[] Compare(string before, string after)
    {
        var left = BpmnDocument.Parse(before); var right = BpmnDocument.Parse(after);
        var findings = new List<ValidationFinding>();
        var originalIds = left.Descendants().Attributes("id").Select(a => a.Value).ToHashSet();
        var resultingIds = right.Descendants().Attributes("id").Select(a => a.Value).ToHashSet();
        if (!originalIds.SetEquals(resultingIds)) findings.Add(new("identifiers_changed", "warning", "The native engine regenerated or changed element identifiers."));
        foreach (string kind in new[] { "process", "participant", "lane", "subProcess", "task", "userTask", "serviceTask", "startEvent", "endEvent", "sequenceFlow", "messageFlow" })
        {
            var original = left.Descendants(BpmnDocument.Bpmn + kind).ToArray();
            var resulting = right.Descendants(BpmnDocument.Bpmn + kind).ToArray();
            if (original.Length != resulting.Length)
                findings.Add(new("element_count_changed", "warning", $"{kind}: {original.Length} input elements, {resulting.Length} output elements."));
            if (!original.Select(e => (string?)e.Attribute("name") ?? "").Order().SequenceEqual(resulting.Select(e => (string?)e.Attribute("name") ?? "").Order()))
                findings.Add(new("names_changed", "warning", kind + " names were normalized or changed."));
        }
        if (left.Descendants(BpmnDocument.Bpmn + "extensionElements").Any())
            findings.Add(new("extensions_require_review", "warning", "Input extensions require a dedicated native preservation check; this comparison does not certify them."));
        findings.Add(new("fidelity_scope", "info", "Identifier, selected element count and name checks only. Connections, layout, all element types, native attributes and attachments require separate checks."));
        return findings.ToArray();
    }
}
