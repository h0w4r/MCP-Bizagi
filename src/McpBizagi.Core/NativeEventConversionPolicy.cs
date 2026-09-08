using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Role-preserving native event conversion. Only exact neutral definition payloads may be replaced.</summary>
public static class NativeEventConversionPolicy
{
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    public static readonly string[] Modes = ["Start", "End", "Catch", "Throw", "Boundary"];

    public static string[] TypesFor(string mode) => mode switch
    {
        "Start" => ["NoneStart", "MessageStart", "TimerStart", "ConditionalStart", "SignalStart", "MultipleStart", "ParallelMultipleStart", "ErrorStart", "EscalationStart", "CompensationStart"],
        "End" => ["NoneEnd", "MessageEnd", "TerminateEnd", "EscalationEnd", "ErrorEnd", "CompensationEnd", "SignalEnd", "MultipleEnd", "CancelEnd"],
        "Catch" => ["MessageIntermediate", "TimerIntermediate", "ConditionalIntermediate", "LinkIntermediate", "SignalIntermediate", "MultipleIntermediate", "ParallelMultipleIntermediate"],
        "Throw" => ["NoneIntermediate", "MessageIntermediate", "EscalationIntermediate", "LinkIntermediate", "CompensationIntermediate", "SignalIntermediate", "MultipleIntermediate"],
        "Boundary" => ["MessageIntermediate", "TimerIntermediate", "EscalationIntermediate", "ConditionalIntermediate", "ErrorIntermediate", "CompensationIntermediate", "SignalIntermediate", "MultipleIntermediate", "ParallelMultipleIntermediate", "CancelIntermediate"],
        _ => []
    };

    public static void Validate(NativeTypeConversion change)
    {
        bool isEvent = Modes.Any(m => TypesFor(m).Contains(change.ExpectedType) || TypesFor(m).Contains(change.TargetType));
        if (!isEvent && change.ExpectedEventMode == null) return;
        if (!isEvent || change.ExpectedEventMode is not { } mode || !TypesFor(mode).Contains(change.ExpectedType) || !TypesFor(mode).Contains(change.TargetType))
            throw new InvalidDataException("Event conversion requires explicit ExpectedEventMode and two event types supported in that same role.");
    }

    private static string Trigger(string type) => type.EndsWith("Intermediate", StringComparison.Ordinal) ? type[..^12] : type.EndsWith("Start", StringComparison.Ordinal) ? type[..^5] : type[..^3];
    private static string[] Kinds(string type, string mode)
    {
        string kind = Trigger(type);
        if (kind == "None") return [];
        if (kind is not "Multiple" and not "ParallelMultiple") return [kind];
        // These are the installed factory's observed collections, not a BPMN-spec
        // guess: the intermediate catch palette differs from the start palette.
        return mode switch
        {
            "Start" => ["Message", "Timer", "Conditional", "Signal"],
            "Boundary" => ["Message", "Timer", "Compensation", "Conditional"],
            _ => ["Message", "Error", "Compensation", "Signal"]
        };
    }

    private static NativeEventDefinitionInfo NeutralDefinition(string kind) => new()
    {
        Kind = kind,
        Name = kind is "Message" or "Conditional" or "Link" or "Signal" ? "" : null,
        Condition = kind == "Conditional" ? "" : null,
        Timer = kind == "Timer" ? new() : null,
        ErrorCode = kind == "Error" ? "" : null,
        EscalationCode = kind == "Escalation" ? "" : null,
        Compensation = kind == "Compensation" ? new() { WaitForCompletion = false } : null
    };

    public static void Verify(NativeElement before, NativeElement after, NativeTypeConversion change, JsonObject a, JsonObject b)
    {
        void Check(NativeElement element, string type, JsonObject projection)
        {
            var info = element.Event ?? throw new InvalidDataException("Missing native event readback.");
            var kinds = Kinds(type, change.ExpectedEventMode!);
            if (info.Mode != change.ExpectedEventMode || !info.DefinitionKinds.SequenceEqual(kinds) ||
                !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(info.Definitions), JsonSerializer.SerializeToNode(kinds.Select(NeutralDefinition).ToArray())) ||
                info.IsParallelMultiple != (info.Mode is "End" or "Throw" ? (bool?)null : Trigger(type) == "ParallelMultiple"))
                throw new InvalidDataException("Event conversion cannot retire nondefault definitions or change the requested role: " + element.Id + " / " + type);
            // Only verified selectors/neutral definitions are projected. Interruption,
            // all attachment aliases, I/O and the rest of the graph remain compared.
            foreach (string key in new[] { "DefinitionKinds", "Definitions", "IsParallelMultiple" }) projection["Event"]!.AsObject().Remove(key);
        }
        Check(before, change.ExpectedType, a); Check(after, change.TargetType, b);
    }

    public static IReadOnlyDictionary<string, int> CountIdentities(IReadOnlyDictionary<string, byte[]> entries)
    {
        // Scan once per bounded archive, not once per converted message. Include
        // opaque payload text conservatively rather than assuming references occur
        // only in diagram records.
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var bytes in entries.Values)
            foreach (Match match in Regex.Matches(Encoding.UTF8.GetString(bytes), "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant))
                counts[match.Value] = counts.GetValueOrDefault(match.Value) + 1;
        return counts;
    }

    public static void Project(XElement owner, string type, string mode, IReadOnlyDictionary<string, int> identities)
    {
        ProjectNeutralRuntime(owner, type, mode);
        var wrappers = owner.Elements(Ns + "Event").ToArray();
        if (wrappers.Length != 1) throw new InvalidDataException("Expected one native event wrapper.");
        string tag = mode is "Start" or "End" ? mode + "Event" : "IntermediateEvent";
        var nodes = wrappers[0].Elements().ToArray();
        if (nodes.Length != 1 || nodes[0].Name != Ns + tag) throw new InvalidDataException("Native event role differs from conversion intent.");
        var node = nodes[0]; string trigger = Trigger(type), selector = mode == "End" ? "Result" : "Trigger";
        if (((string?)node.Attribute(selector) ?? "None") != trigger ||
            ((string?)node.Attribute("IsAttached") ?? "false") != (mode == "Boundary" ? "true" : "false"))
            throw new InvalidDataException("Native event selector or boundary role differs from conversion intent.");

        var payload = node.Elements().ToArray();
        var expected = Payloads(type, mode).ToArray();
        if (payload.Length != expected.Length) throw new InvalidDataException("Event conversion cannot retire an unknown definition collection.");
        for (int i = 0; i < payload.Length; i++)
        {
            var copy = new XElement(payload[i]);
            foreach (var message in copy.DescendantsAndSelf(Ns + "Message"))
            {
                string id = (string?)message.Attribute("Id") ?? "";
                NativeMetadataPolicy.RequireId(id);
                // Factory messages have generated identities. Only unreferenced,
                // otherwise empty identities may be retired; references anywhere
                // in the native archive cause a conservative rejection.
                if (identities.GetValueOrDefault(id) != 1) throw new InvalidDataException("Event conversion cannot retire a shared or referenced message identity.");
                message.Attribute("Id")!.Remove();
            }
            // Insignificant XML indentation is the only formatting normalization.
            // Comments, text payload, unknown attributes and xml:space survive and fail.
            foreach (var parent in copy.DescendantsAndSelf())
                if (parent.HasElements)
                    foreach (var text in parent.Nodes().OfType<XText>().Where(t => string.IsNullOrWhiteSpace(t.Value)).ToArray()) text.Remove();
            if (!XNode.DeepEquals(copy, expected[i])) throw new InvalidDataException("Event conversion cannot retire nondefault or unknown definition payloads; reconfigure them explicitly first.");
            NativeComparisonProjection.RemoveVerifiedNode(payload[i]);
        }
        node.SetAttributeValue(selector, "None");
    }

    private static void ProjectNeutralRuntime(XElement owner, string type, string mode)
    {
        // Actual 4.3 constructor/save baselines. A configured cost/priority or any
        // unknown runtime key remains in the archive comparison; it is not waived.
        if (type != "NoneStart" && type != "TimerStart" && !(type == "TimerIntermediate" && mode == "Catch") && !(type == "NoneIntermediate" && mode == "Throw")) return;
        var nodes = owner.Elements(Ns + "ExtendedAttributes").Elements(Ns + "ExtendedAttribute").Where(e => (string?)e.Attribute("Name") == "RuntimeProperties").ToArray();
        if (nodes.Length > 1) throw new InvalidDataException("Ambiguous event runtime record.");
        if (nodes.Length == 0) return;
        var node = nodes[0];
        var value = JsonNode.Parse((string?)node.Attribute("Value") ?? "") as JsonObject ?? throw new InvalidDataException("Invalid event runtime object.");
        foreach (string key in type == "NoneStart" ? Array.Empty<string>() : type == "NoneIntermediate" ? new[] { "cost", "priority" } : new[] { "cost" })
            if (value[key] is JsonValue scalar && scalar.TryGetValue<decimal>(out var number) && number == 0) value.Remove(key);
        if (value.Count == 0 && node.Attributes().All(a => a.Name == "Name" || a.Name == "Value") && !node.Nodes().Any()) NativeComparisonProjection.RemoveVerifiedNode(node);
        else node.SetAttributeValue("Value", value.ToJsonString());
    }

    // Own structural baselines of the installed serializer. No vendor implementation
    // or proprietary schema is included. Comparison copies are never written to .bpm.
    private static IEnumerable<XElement> Payloads(string type, string mode)
    {
        string trigger = Trigger(type); bool throwing = mode is "End" or "Throw";
        XElement Payload(string kind)
        {
            var node = new XElement(Ns + (kind switch
            {
                "Message" => "TriggerResultMessage", "Timer" => "TriggerTimer", "Conditional" => "TriggerConditional", "Signal" => "TriggerResultSignal",
                "Link" => "TriggerResultLink", "Error" => "ResultError", "Escalation" => "TriggerEscalation", "Compensation" => "TriggerResultCompensation", _ => throw new InvalidDataException("Unexpected event definition.")
            }));
            if (throwing && kind is "Message" or "Signal" or "Link") node.SetAttributeValue("CatchThrow", "THROW");
            if (kind == "Compensation") node.SetAttributeValue("WaitForCompletion", "false");
            if (kind == "Message") node.Add(new XElement(Ns + "Message"));
            if (kind == "Conditional") node.Add(new XElement(Ns + "Expression"));
            return node;
        }
        if (trigger is "None" or "Terminate" or "Cancel") return [];
        if (trigger is not "Multiple" and not "ParallelMultiple") return [Payload(trigger)];
        var multiple = new XElement(Ns + (mode == "Start" ? "TriggerMultiple" : mode == "End" ? "ResultMultiple" : "TriggerIntermediateMultiple"));
        if (mode == "Throw") multiple.SetAttributeValue("IsThrow", "true");
        // The XPDL EndEvent serializer orders compensation before error, while
        // native load reconstructs the collection with error before compensation.
        var persistedKinds = mode == "End" ? new[] { "Message", "Compensation", "Error", "Signal" } : Kinds(type, mode);
        multiple.Add(persistedKinds.Select(Payload));
        return [multiple];
    }
}
