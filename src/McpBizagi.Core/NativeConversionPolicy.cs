using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Explicit native conversion with exact type selectors and whole-archive protection.</summary>
public static class NativeConversionPolicy
{
    public static readonly string[] TaskTypes = ["AbstractTask", "UserTask", "ManualTask", "ServiceTask", "ScriptTask", "SendTask", "ReceiveTask", "BusinessRuleTask"];
    public static readonly string[] GatewayTypes = ["ExclusiveGateway", "InclusiveGateway", "ParallelGateway", "ComplexGateway", "EventBasedGateway", "EventBasedGatewayExclusive", "EventBasedGatewayParallel"];
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";

    public static bool IsTaskToCall(NativeTypeConversion change) => TaskTypes.Contains(change.ExpectedType) && change.TargetType == "CallActivity";

    public static void Validate(NativeTypeConversion[] changes)
    {
        if (changes == null || changes.Length is < 1 or > 1000 || changes.Any(c => c == null)) throw new InvalidDataException("Supply 1-1000 explicit native conversions.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in changes)
        {
            if (!Guid.TryParseExact(c.ElementId, "D", out var id) || id == Guid.Empty || id.ToString() != c.ElementId || !ids.Add(c.ElementId))
                throw new InvalidDataException("Conversion identities must be distinct canonical native GUIDs.");
            if (c.ExpectedType == c.TargetType || !(TaskTypes.Contains(c.ExpectedType) && TaskTypes.Contains(c.TargetType) || GatewayTypes.Contains(c.ExpectedType) && GatewayTypes.Contains(c.TargetType) || IsTaskToCall(c)))
                throw new NotSupportedException("Conversion requires different task/gateway types within one category, or a task to an unbound CallActivity.");
        }
    }

    public static string TypeOf(NativeElement element) => element.ElementType == "EventBasedGateway" && element.EventGateway?.Instantiate == true
        ? "EventBasedGateway" + element.EventGateway.Kind : element.ElementType;

    public static void Verify(NativeElement[] before, NativeElement[] after, NativeTypeConversion[] changes)
    {
        Validate(changes);
        var source = before.ToDictionary(e => e.Id); var target = after.ToDictionary(e => e.Id); var requested = changes.ToDictionary(c => c.ElementId);
        if (!source.Keys.Order().SequenceEqual(target.Keys.Order())) throw new InvalidDataException("Conversion changed the graph identity set.");
        foreach (var id in source.Keys)
        {
            var a = JsonSerializer.SerializeToNode(source[id])!.AsObject(); var b = JsonSerializer.SerializeToNode(target[id])!.AsObject();
            if (requested.TryGetValue(id, out var c))
            {
                if (TypeOf(source[id]) != c.ExpectedType || TypeOf(target[id]) != c.TargetType ||
                    source[id].Kind != KindFor(c.ExpectedType) || target[id].Kind != KindFor(c.TargetType))
                    throw new InvalidDataException("Conversion type readback differs from requested intent.");
                // Category selectors may change. Every common field, relationship and style stays compared.
                foreach (string field in new[] { "Kind", "ElementType" }) { a.Remove(field); b.Remove(field); }
                if (IsTaskToCall(c))
                {
                    a["CallReference"] = JsonSerializer.SerializeToNode(new NativeCallReference());
                    // The graph exposes expanded geometry only for subprocess/call
                    // types. Coordinates, colors and expansion state remain derived
                    // from the unchanged source geometry; hidden dimensions are
                    // protected by the mandatory whole-archive comparison below.
                    var expanded = target[id].ExpandedGeometry ?? throw new InvalidDataException("Converted call lacks expanded geometry readback.");
                    if (!double.IsFinite(expanded.Width) || !double.IsFinite(expanded.Height) || expanded.Width < 0 || expanded.Height < 0)
                        throw new InvalidDataException("Invalid converted call expanded dimensions.");
                    var expected = JsonSerializer.Deserialize<NativeGeometry>(JsonSerializer.Serialize(source[id].Geometry))
                        ?? throw new InvalidDataException("Task conversion requires native source geometry.");
                    expected.Width = expanded.Width; expected.Height = expanded.Height;
                    a["ExpandedGeometry"] = JsonSerializer.SerializeToNode(expected);
                }
                if (GatewayTypes.Contains(c.ExpectedType)) { a.Remove("EventGateway"); b.Remove("EventGateway"); }
            }
            if (!JsonNode.DeepEquals(a, b)) throw new InvalidDataException("Conversion changed unrequested native graph properties: " + id);
        }
        if (requested.Keys.Any(id => !source.ContainsKey(id))) throw new InvalidDataException("Conversion refers to an absent native identity.");
    }

    // Native concrete class names differ from the palette for abstract tasks and event gateways.
    private static string KindFor(string type) => type == "AbstractTask" ? "Task" : type.StartsWith("EventBasedGateway", StringComparison.Ordinal) ? "EventBasedGateway" : type;

    public static void Preflight(byte[] source, NativeTypeConversion[] changes)
    {
        Validate(changes);
        Project(NativeArchive.ReadEntries(source), changes, false);
    }

    public static NativeFidelityReport Compare(byte[] before, byte[] after, NativeTypeConversion[] changes)
    {
        Validate(changes);
        var result = NativeFidelity.CompareEntries(Project(NativeArchive.ReadEntries(before), changes, false), Project(NativeArchive.ReadEntries(after), changes, true));
        return result;
    }

    private static Dictionary<string, byte[]> Project(IReadOnlyDictionary<string, byte[]> entries, NativeTypeConversion[] changes, bool result)
    {
        if (!result) VerifyAttributeScopes(entries, changes);
        var projected = new Dictionary<string, byte[]>(entries, StringComparer.OrdinalIgnoreCase); var found = new HashSet<string>();
        foreach (var pair in entries.Where(p => p.Key.EndsWith(".diag!/Diagram.xml", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = new MemoryStream(pair.Value, false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = BpmnDocument.MaxXmlCharacters });
            var xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            foreach (var c in changes)
            {
                var owners = xml.Descendants(Ns + "Activity").Where(e => (string?)e.Attribute("Id") == c.ElementId && NativeFidelity.IsNativeNameOwner(e)).ToArray();
                if (owners.Length == 0) continue;
                if (owners.Length != 1 || !found.Add(c.ElementId)) throw new InvalidDataException("Ambiguous native conversion identity.");
                string type = result ? c.TargetType : c.ExpectedType;
                if (result && IsTaskToCall(c)) ProjectUnboundCall(owners[0], c.ExpectedType);
                else if (TaskTypes.Contains(type)) ProjectTask(owners[0], type); else ProjectGateway(owners[0], type);
                if (IsTaskToCall(c))
                    foreach (var graphics in owners[0].Elements(Ns + "NodeGraphicsInfos").Elements(Ns + "NodeGraphicsInfo"))
                        foreach (var attribute in graphics.Attributes().Where(a => a.Name == "Expanded" && a.Value == "false" ||
                            (a.Name == "ExpandedWidth" || a.Name == "ExpandedHeight") && a.Value == "0").ToArray())
                            attribute.Remove(); // Exact neutral values materialized only by the call serializer.
            }
            projected[pair.Key] = Encoding.UTF8.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
        }
        if (found.Count != changes.Length) throw new InvalidDataException("Native conversion owner not found in the expected XPDL structure.");
        return projected;
    }

    private static void VerifyAttributeScopes(IReadOnlyDictionary<string, byte[]> entries, NativeTypeConversion[] changes)
    {
        // Keeping value bytes is not enough if the new type hides them. Apply this
        // rule to every conversion category, without mutating any definition scope.
        var targets = changes.ToDictionary(c => c.ElementId, c => c.TargetType, StringComparer.Ordinal);
        foreach (var pair in entries.Where(p => p.Key.EndsWith(".diag!/ExtendedAttributeValues.xml", StringComparison.OrdinalIgnoreCase)))
        {
            var document = NativeMetadataPolicy.Read(Encoding.UTF8.GetString(pair.Value));
            foreach (var owner in document.Root?.Elements("ElementAttributeValues") ?? [])
            {
                if (!targets.TryGetValue((string?)owner.Attribute("ElementId") ?? "", out string? target)) continue;
                foreach (var value in owner.Element("Values")?.Elements() ?? [])
                {
                    string definitionId = (string?)value.Attribute("Id") ?? "";
                    NativeMetadataPolicy.RequireId(definitionId);
                    if (!entries.TryGetValue("Documentation/" + definitionId + ".xml", out var bytes))
                        throw new InvalidDataException("Converted element has an attribute without its native definition.");
                    var definition = NativeMetadataPolicy.Read(Encoding.UTF8.GetString(bytes)).Root;
                    if (definition?.Name != "ExtendedAttribute" || (string?)definition.Attribute("Id") != definitionId ||
                        definition.Element("ElementTypes")?.Elements("AttributeElementType").Any(e => (string?)e.Attribute("Type") == target) != true)
                        throw new InvalidDataException("Converted element attributes must explicitly include " + target + " in their definition scopes before conversion.");
                }
            }
        }
    }

    private static void ProjectUnboundCall(XElement owner, string sourceTaskType)
    {
        // Only the native type selector changes. Reject a silently introduced call
        // target, external reference or any unknown payload instead of projecting it away.
        ProjectTaskRuntime(owner, sourceTaskType);
        ProjectTaskRuntime(owner, "CallActivity");
        var implementations = owner.Elements(Ns + "Implementation").ToArray();
        if (implementations.Length != 1) throw new InvalidDataException("Converted call must have one native Implementation.");
        var children = implementations[0].Elements().ToArray();
        if (children.Length != 1 || children[0].Name != Ns + "SubFlow") throw new InvalidDataException("Converted call must have one native SubFlow.");
        var call = children[0];
        if (call.Attributes().Any(a => a.Name != "Id" || a.Value != "") || call.Nodes().Any())
            throw new InvalidDataException("Task conversion cannot introduce a bound or unknown call payload.");
        call.ReplaceWith(new XElement(Ns + "Task"));
    }

    private static void ProjectTask(XElement owner, string type)
    {
        ProjectTaskRuntime(owner, type);
        var task = owner.Elements(Ns + "Implementation").Elements(Ns + "Task").SingleOrDefault() ?? throw new InvalidDataException("Expected a native task implementation.");
        string? marker = type == "AbstractTask" ? null : "Task" + type[..^4];
        var children = task.Elements().ToArray();
        if (marker == null)
        { if (children.Length != 0) throw new InvalidDataException("Abstract task has concrete or unknown implementation content."); return; }
        if (children.Length != 1 || children[0].Name != Ns + marker) throw new InvalidDataException("Native task selector differs from the expected type.");
        var child = children[0];
        // Only stateless factory defaults are retired. Scripts, service references and unknown
        // content need explicit future contracts; type conversion must never silently erase them.
        foreach (var attribute in child.Attributes())
            if (!(attribute.IsNamespaceDeclaration && attribute.Value == Ns.NamespaceName ||
                attribute.Name == "Implementation" && attribute.Value == "Unspecified" && type is "UserTask" or "ServiceTask" or "SendTask" or "ReceiveTask" ||
                attribute.Name == "BusinessRuleTaskImplementation" && attribute.Value == "Unspecified" && type == "BusinessRuleTask" ||
                attribute.Name == "Instantiate" && attribute.Value == "false" && type == "ReceiveTask"))
                throw new InvalidDataException("Task conversion would retire nondefault or unknown implementation settings.");
        if (type == "ScriptTask")
        {
            // The real factory persists an empty Script expression, not an absent child.
            // Whitespace inside Script is user text, not XML indentation: do not discard it.
            var scripts = child.Elements(Ns + "Script").ToArray();
            if (scripts.Length == 1 && !scripts[0].HasAttributes && !scripts[0].Nodes().Any()) scripts[0].Remove();
        }
        if (child.AncestorsAndSelf().Any(e => (string?)e.Attribute(XNamespace.Xml + "space") == "preserve"))
            throw new InvalidDataException("Conversion cannot retire content under explicit xml:space preservation.");
        if (child.Nodes().Any(n => n is not XText t || !string.IsNullOrWhiteSpace(t.Value)))
            throw new InvalidDataException("Task conversion would retire script or unknown implementation content.");
        NativeComparisonProjection.RemoveVerifiedNode(child);
    }

    private static void ProjectTaskRuntime(XElement owner, string type)
    {
        if (type is not "UserTask" and not "ManualTask" and not "ServiceTask" and not "CallActivity") return;
        var runtime = owner.Elements(Ns + "ExtendedAttributes").Elements(Ns + "ExtendedAttribute").Where(e => (string?)e.Attribute("Name") == "RuntimeProperties").ToArray();
        if (runtime.Length > 1) throw new InvalidDataException("Ambiguous native runtime record.");
        if (runtime.Length == 0) return;
        var element = runtime[0];
        using var json = JsonDocument.Parse((string?)element.Attribute("Value") ?? throw new InvalidDataException("Missing runtime value."));
        if (json.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Native runtime must be an object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in json.RootElement.EnumerateObject()) if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate native runtime property.");
        var data = JsonNode.Parse(json.RootElement.GetRawText())!.AsObject();
        // Remove only observed, type-owned neutral defaults in comparison copies. Preserve all
        // nondefault and unknown runtime fields; absence after native conversion then fails fidelity.
        if (type is "UserTask" or "ServiceTask")
            foreach (string name in new[] { "cost", "priority" })
                if (data[name] is JsonValue number && number.TryGetValue<decimal>(out var value) && value == 0) data.Remove(name);
        if (type == "CallActivity")
        {
            // Observed native call-constructor defaults, not a wildcard runtime
            // exemption. Unknown/nondefault fields survive and can fail fidelity.
            if (data["priority"] is JsonValue priority && priority.TryGetValue<decimal>(out var number) && number == 0) data.Remove("priority");
            foreach (var item in new[] { (Name: "subProcessType", Value: "None"), (Name: "inputMappingType", Value: "None"),
                (Name: "outputMappingType", Value: "None"), (Name: "exitMode", Value: "AllTokens") })
                if (data[item.Name] is JsonValue text && text.TryGetValue<string>(out var value) && value == item.Value) data.Remove(item.Name);
            if (data["asynchronousBehavior"] is JsonObject empty && empty.Count == 0) data.Remove("asynchronousBehavior");
        }
        foreach (string name in type == "UserTask" ? new[] { "notifyOnMobile", "isSingleton", "isConditional" } : type == "ServiceTask" ? new[] { "isBot" } : Array.Empty<string>())
            if (data[name] is JsonValue boolean && boolean.TryGetValue<bool>(out var value) && !value) data.Remove(name);
        if (type == "ServiceTask" && data["asynchronousBehavior"] is JsonObject behavior)
        {
            // The installed type-change command explicitly sets isAsynchronous=false for an ordinary
            // service task; the new-shape factory leaves that nullable value absent.
            if (behavior["isAsynchronous"] is JsonValue flag && flag.TryGetValue<bool>(out bool asynchronous) && !asynchronous) behavior.Remove("isAsynchronous");
            if (behavior.Count == 0) data.Remove("asynchronousBehavior");
        }
        if (data.Count == 0 && element.Attributes().All(a => a.Name == "Name" || a.Name == "Value") && !element.Nodes().Any()) NativeComparisonProjection.RemoveVerifiedNode(element);
        else element.SetAttributeValue("Value", data.ToJsonString());
    }

    private static void ProjectGateway(XElement owner, string type)
    {
        var route = owner.Elements(Ns + "Route").SingleOrDefault() ?? throw new InvalidDataException("Expected a native gateway route.");
        bool eventBased = type.StartsWith("EventBased", StringComparison.Ordinal);
        string gateway = type == "EventBasedGatewayParallel" ? "Parallel" : eventBased ? "Exclusive" : type[..^7];
        string? Value(string name) => (string?)route.Attribute(name);
        if ((Value("GatewayType") ?? "Exclusive") != gateway || (Value("ExclusiveType") ?? "Data") != (eventBased && gateway == "Exclusive" ? "Event" : "Data") ||
            (Value("Instantiate") ?? "false") != (type is "EventBasedGatewayExclusive" or "EventBasedGatewayParallel" ? "true" : "false") ||
            (Value("ParallelEventBased") ?? "false") != (type == "EventBasedGatewayParallel" ? "true" : "false"))
            throw new InvalidDataException("Native gateway selector differs from the requested type.");
        if (!string.IsNullOrEmpty(Value("IncomingCondition"))) throw new InvalidDataException("Gateway conversion cannot silently retire an activation condition.");
        foreach (string name in new[] { "GatewayType", "ExclusiveType", "Instantiate", "ParallelEventBased" }) route.Attribute(name)?.Remove();
        // A visible exclusive marker is presentation state, not a type-independent permission to lose content.
        if (Value("MarkerVisible") is "false" or null) route.Attribute("MarkerVisible")?.Remove();
    }
}
