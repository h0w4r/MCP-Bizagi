using System.Collections;
using System.Reflection;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static IEnumerable<object> OptionalItems(object owner, string property) => Optional(owner, property) is IEnumerable values ? values.Cast<object>() : Enumerable.Empty<object>();
    private static NativeDataFlowInfo? DescribeDataFlow(GraphEntry entry)
    {
        object owner = entry.Value;
        if (Optional(owner, "DataInputAssociations") == null && Optional(owner, "DataOutputAssociations") == null) return null;
        object? spec = Optional(owner, "IoSpecification");
        NativeElement[] Nodes(object source, string property) => OptionalItems(source, property).Select(p => Describe(new GraphEntry(p, Text(owner, "Id"), entry.DiagramId))).ToArray();
        string[][] Sets(string multiple, string single, string values) => (spec != null ? OptionalItems(spec, multiple) : Optional(owner, single) is object set ? new[] { set } : Enumerable.Empty<object>())
            .Select(s => OptionalItems(s, values).Select(v => Text(v, "Id")).ToArray()).ToArray();
        return new NativeDataFlowInfo { HasSpecification = spec != null, Inputs = Nodes(spec ?? owner, "DataInputs"), Outputs = Nodes(spec ?? owner, "DataOutputs"),
            InputAssociations = Nodes(owner, "DataInputAssociations"), OutputAssociations = Nodes(owner, "DataOutputAssociations"),
            InputSets = Sets("InputSets", "InputSet", "DataInputs"), OutputSets = Sets("OutputSets", "OutputSet", "DataOutputs") };
    }
    private static IEnumerable<GraphEntry> DataFlowNodes(GraphEntry entry)
    {
        object owner = entry.Value, ports = Optional(owner, "IoSpecification") ?? owner;
        foreach (var property in new[] { "DataInputs", "DataOutputs" })
            foreach (var value in OptionalItems(ports, property)) yield return new(value, Text(owner, "Id"), entry.DiagramId);
        foreach (var property in new[] { "DataInputAssociations", "DataOutputAssociations" })
            foreach (var value in OptionalItems(owner, property)) yield return new(value, Text(owner, "Id"), entry.DiagramId);
    }
    private static Dictionary<string, (object Owner, object Item, bool Input)> RequiredDataLinks(IEnumerable<GraphEntry> graph)
    {
        var links = new Dictionary<string, (object, object, bool)>(StringComparer.Ordinal);
        void Add(object? owner, object item, bool input)
        {
            if (owner == null || Optional(owner, input ? "DataInputAssociations" : "DataOutputAssociations") == null) return;
            if (owner.GetType().GetProperty("IoSpecification") == null)
                throw new InvalidDataException("Automatic association data bindings currently require an activity owner, not an event.");
            links[Text(owner, "Id") + ":" + Text(item, "Id") + ":" + input] = (owner, item, input);
        }
        void Connect(object? item, object? owner, bool input)
        {
            if (item == null || owner == null || item.GetType().Name is not "DataObject" and not "DataStoreReference") return;
            if (owner.GetType().Name == "SequenceFlow") { Add(Optional(owner, "Source"), item, false); Add(Optional(owner, "Target"), item, true); }
            else Add(owner, item, input);
        }
        foreach (object association in graph.Select(e => e.Value).Where(e => e.GetType().Name == "Association"))
        {
            Connect(Optional(association, "Source"), Optional(association, "Target"), true);
            Connect(Optional(association, "Target"), Optional(association, "Source"), false);
        }
        return links;
    }
    private void SynchronizeDataLinks(object model, Dictionary<string, (object Owner, object Item, bool Input)> before)
    {
        var after = RequiredDataLinks(Graph(model));
        var util = Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Util.BPMN20ModelUtil");
        // Reconcile logical pairs, not connector count. Parallel graphical associations
        // share a native data binding until their final graphical reference is removed.
        foreach (var key in before.Keys.Except(after.Keys))
        {
            var link = before[key]; util.GetMethod(link.Input ? "RemoveDataInput" : "RemoveDataOutput", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new[] { link.Owner, link.Item });
        }
        foreach (var key in after.Keys.Except(before.Keys))
        {
            var link = after[key]; util.GetMethod(link.Input ? "AddDataInput" : "AddDataOutput", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new[] { link.Owner, link.Item });
        }
    }
    private object? NativeValueClone(object? value)
    {
        if (value == null) return null;
        if (value is ICloneable native)
        {
            object copy = native.Clone();
            // Native collection cloning copies items by reference unless annotated otherwise.
            // Detach the explicitly owned I/O collections before changing port IDs or memberships.
            if (value.GetType().Name is "InputOutputSpecification" or "InputSet" or "OutputSet")
                foreach (string property in new[] { "DataInputs", "DataOutputs", "InputSets", "OutputSets" })
                    if (value.GetType().GetProperty(property) is { CanWrite: true }) Set(copy, property, NativeValueClone(Get(value, property))!);
            return copy;
        }
        if (value is IEnumerable list && value is not string)
        {
            // CloneUtil.CopyObject tries to read an ElementCollection indexer without an
            // index when invoked directly on the collection. Clone its native items instead.
            object copy = New(value.GetType());
            foreach (object item in list) Call(copy, "Add", NativeValueClone(item));
            return copy;
        }
        return Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Util.CloneUtil")
            .GetMethod("Clone", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new[] { value });
    }
    private object CloneWithoutSharedDataFlow(object source, object parameters)
    {
        var preserved = new List<(object Owner, string Property, object? Value)>();
        try
        {
            // The vendor association cloner adds implicit ports to shallow-copied InputSets.
            // Temporarily isolate ONLY these I/O properties, restore every original reference
            // in finally, then install an independently cloned/mapped original I/O graph.
            foreach (var owner in Visit(source, "", "", new HashSet<string>(StringComparer.Ordinal)).Where(e => DescribeDataFlow(e) != null).ToArray())
                foreach (string property in new[] { "IoSpecification", "DataInputs", "DataOutputs", "InputSet", "OutputSet", "DataInputAssociations", "DataOutputAssociations" })
                    if (owner.Value.GetType().GetProperty(property) is { CanWrite: true } p)
                    {
                        object? value = p.GetValue(owner.Value); preserved.Add((owner.Value, property, value));
                        Set(owner.Value, property, value is IEnumerable ? New(p.PropertyType) : null!);
                    }
            return Call(NativeCloner("ICollaborationCloner"), "Clone", source, parameters)!;
        }
        finally { foreach (var item in preserved) Set(item.Owner, item.Property, item.Value!); }
    }
    private void CloneDataFlows(object source, object clone, IDictionary map)
    {
        var original = Visit(source, "", "", new HashSet<string>(StringComparer.Ordinal)).ToArray();
        var copied = Visit(clone, "", "", new HashSet<string>(StringComparer.Ordinal)).ToDictionary(e => Text(e.Value, "Id"));
        object Mapped(object element)
        {
            object id = Get(element, "Id");
            string key = (map[id] ?? id).ToString()!;
            return copied.TryGetValue(key, out var target) ? target.Value : throw new InvalidDataException("Native cloned data endpoint is unresolved: " + element.GetType().Name + " " + id + ".");
        }
        foreach (var owner in original.Where(e => DescribeDataFlow(e) != null))
        {
            object target = copied[map[Get(owner.Value, "Id")]!.ToString()!].Value;
            // Replace the vendor cloner's implicit I/O reconstruction with independent native
            // copies of the exact original state. Remap every owned identity explicitly below.
            foreach (string property in new[] { "IoSpecification", "DataInputs", "DataOutputs", "InputSet", "OutputSet", "DataInputAssociations", "DataOutputAssociations" })
                if (owner.Value.GetType().GetProperty(property) is { CanWrite: true }) Set(target, property, NativeValueClone(Optional(owner.Value, property))!);
            foreach (var node in DataFlowNodes(new GraphEntry(target, "", Text(clone, "Id"))))
            {
                Guid old = (Guid)Get(node.Value, "Id"), id = Guid.NewGuid(); map.Add(old, id); Set(node.Value, "Id", id);
                copied.Add(id.ToString(), node);
            }
        }
        foreach (var owner in copied.Values.Where(e => DescribeDataFlow(e) != null).ToArray())
        {
            object ports = Optional(owner.Value, "IoSpecification") ?? owner.Value;
            foreach (string direction in new[] { "Input", "Output" })
            {
                var sets = OptionalItems(ports, direction + "Sets").Concat(Optional(owner.Value, direction + "Set") is object singleSet ? new[] { singleSet } : Enumerable.Empty<object>());
                foreach (object set in sets)
                {
                    object values = Get(set, "Data" + direction + "s"), detached = New(values.GetType());
                    foreach (object item in Items(values)) Call(detached, "Add", Mapped(item));
                    Set(set, "Data" + direction + "s", detached);
                }
                foreach (object association in OptionalItems(owner.Value, "Data" + direction + "Associations"))
                {
                    foreach (string side in new[] { "Source", "Target" })
                    {
                        object endpoint = Get(association, side);
                        object mapped = Mapped(endpoint); Set(association, side, mapped);
                        Set(association, side + "Ref", side == "Source" ? (object)new[] { Text(mapped, "Id") } : Text(mapped, "Id"));
                    }
                }
            }
        }
    }
}
