using System.Xml;
using System.Collections;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static void CloneDataStoreCatalog(object source, object clone, IDictionary map, NativeElement[] original)
    {
        // The installed collaboration cloner copies catalog values but does not allocate
        // identities or bind the independently cloned references to those catalog objects.
        // Use each native object's clone implementation and an independent collection;
        // never clear or mutate a potentially shared vendor collection in place.
        object catalog = Get(clone, "DataStore");
        object detached = New(catalog.GetType());
        var stores = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (object store in Items(source, "DataStore"))
        {
            object copy = ((ICloneable)store).Clone();
            Guid id = Guid.NewGuid();
            map.Add(Get(store, "Id"), id); Set(copy, "Id", id);
            Call(detached, "Add", copy); stores.Add(Text(store, "Id"), copy);
        }
        Set(clone, "DataStore", detached);
        var nodes = Visit(clone, "", "", new HashSet<string>(StringComparer.Ordinal)).ToDictionary(e => Text(e.Value, "Id"));
        foreach (var reference in original.Where(e => e.Kind == "DataStoreReference"))
        {
            if (!stores.TryGetValue(reference.Data!.StoreId, out var store) ||
                map[Guid.Parse(reference.Id)] is not Guid id || !nodes.TryGetValue(id.ToString(), out var node))
                throw new InvalidDataException("Native clone cannot resolve a store reference and its catalog identity.");
            Set(node.Value, "DataStore", store);
            Set(node.Value, "DataStoreRef", new XmlQualifiedName(Text(store, "Id")));
        }
    }
    private static NativeDataInfo? DescribeData(object element)
    {
        string kind = element.GetType().Name;
        if (kind is not "DataObject" and not "DataStore" and not "DataStoreReference" and not "DataInput" and not "DataOutput") return null;
        var store = kind == "DataStoreReference" ? Optional(element, "DataStore") : null;
        var q = Optional(element, "DataStoreRef") as XmlQualifiedName;
        return new NativeDataInfo { State = Optional(store ?? element, "DataState") is object s ? Text(s, "DisplayName") : "",
            IsCollection = kind is "DataObject" or "DataInput" or "DataOutput" ? (bool?)Get(element, "IsCollection") : null,
            Capacity = kind == "DataStore" ? Text(element, "Capacity") : null,
            IsUnlimited = kind == "DataStore" ? (bool?)Get(element, "IsUnlimited") : null,
            StoreId = store == null ? "" : Text(store, "Id"), StoreBpmnName = q?.Name ?? "", StoreBpmnNamespace = q?.Namespace ?? "" };
    }
    private void ApplyDataProperties(object element, NativeDataProperties patch, Dictionary<string, GraphEntry> graph)
    {
        string kind = element.GetType().Name;
        if (DescribeData(element) == null) throw new InvalidDataException("DataProperties requires a native data object, store or store reference.");
        if (patch.IsCollection is bool collection)
        {
            if (kind != "DataObject") throw new InvalidDataException("IsCollection requires a native DataObject.");
            // An imported item definition may override the local collection flag. Do not
            // change a shared definition implicitly or claim a shadowed assignment worked.
            if (Optional(element, "ItemSubjectRef") != null) throw new InvalidDataException("Collection is controlled by a shared native item definition.");
            Set(element, "IsCollection", collection);
        }
        if (patch.State is { } state)
        {
            if (kind is not "DataObject" and not "DataStore") throw new InvalidDataException("State belongs to the data object or shared store catalog, not a single store reference.");
            Set(Get(element, "DataState"), "DisplayName", state);
        }
        if (patch.Capacity != null || patch.IsUnlimited != null)
        {
            if (kind != "DataStore") throw new InvalidDataException("Capacity and IsUnlimited require the shared native DataStore catalog.");
            if (patch.Capacity != null) Set(element, "Capacity", patch.Capacity == "" ? null! : patch.Capacity);
            if (patch.IsUnlimited.HasValue) Set(element, "IsUnlimited", patch.IsUnlimited.Value);
        }
        if (patch.StoreId is { } id)
        {
            if (kind != "DataStoreReference" || !graph.TryGetValue(id, out var target) || target.Value.GetType().Name != "DataStore" ||
                target.DiagramId != graph[Text(element, "Id")].DiagramId)
                throw new InvalidDataException("StoreId requires an existing native DataStore in the same diagram.");
            if (DescribeData(element)!.StoreId != id) RequireStoreStateCarrier(graph.Values, element);
            Set(element, "DataStore", target.Value); Set(element, "DataStoreRef", new XmlQualifiedName(Text(target.Value, "Id")));
        }
    }
    private static void RequireStoreStateCarrier(IEnumerable<GraphEntry> graph, object element)
    {
        if (element.GetType().Name != "DataStoreReference") return;
        var data = DescribeData(element)!;
        if (data.State != "" && graph.Count(e => e.Value.GetType().Name == "DataStoreReference" && DescribeData(e.Value)!.StoreId == data.StoreId) == 1)
            throw new InvalidDataException("Clear the shared store state before removing or relinking its final reference.");
    }
    private static void RequireNoStoreReferences(IEnumerable<GraphEntry> graph, string removed)
    {
        foreach (var e in graph.Where(e => e.Value.GetType().Name == "DataStoreReference"))
        {
            var data = DescribeData(e.Value)!;
            if (data.StoreId == removed || data.StoreBpmnName == removed || data.StoreBpmnName == "Id_" + removed)
                throw new InvalidDataException("Delete or relink store references before deleting their shared store catalog.");
        }
    }
    private static void ValidateDataStateOwnership(object model, NativeMutation[] changes)
    {
        var graph = Graph(model).ToArray();
        foreach (var patch in changes.Where(c => c.DataProperties?.State != null))
        {
            var node = graph.Single(e => Text(e.Value, "Id") == patch.ElementId);
            // Store state is serialized on every visible reference, not the catalog record.
            if (node.Value.GetType().Name == "DataStore" && !graph.Any(e => DescribeData(e.Value)?.StoreId == patch.ElementId))
                throw new InvalidDataException("A native store state requires at least one durable store reference.");
        }
    }
}
