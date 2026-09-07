using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Native data intent, restart verification and exact requested-field comparison projection.</summary>
public static class NativeDataPolicy
{
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    public static void Validate(NativeMutation c)
    {
        if (c.Operation == "create" && c.ElementType == "DataStore" && c.Geometry != null)
            throw new InvalidDataException("DataStore is a diagram catalog, not a graphical node.");
        if (c.Operation == "create" && c.ElementType == "DataStoreReference" && c.DataProperties?.StoreId == null)
            throw new InvalidDataException("Store reference creation requires an explicit existing StoreId.");
        if (c.DataProperties is not { } p) return;
        if (c.Operation is not "create" and not "update" || p.State == null && p.IsCollection == null && p.Capacity == null && p.IsUnlimited == null && p.StoreId == null)
            throw new InvalidDataException("Supply actual data property changes on create/update.");
        if (p.State?.Length > 10000) throw new InvalidDataException("Data state text exceeds the operation bound.");
        if (p.StoreId != null) NativeMetadataPolicy.RequireId(p.StoreId);
        if (p.Capacity is { Length: > 0 } capacity && (capacity.Length > 1000 || capacity.Any(ch => ch is < '0' or > '9') || capacity.Length > 1 && capacity[0] == '0'))
            throw new InvalidDataException("Capacity requires a canonical nonnegative integer or empty to clear.");
        if (c.Operation == "create") RequireKind(c.ElementType, p);
    }
    private static void RequireKind(string kind, NativeDataProperties p)
    {
        if (kind is not "DataObject" and not "DataStore" and not "DataStoreReference" ||
            p.State != null && kind is not "DataObject" and not "DataStore" || p.IsCollection != null && kind != "DataObject" ||
            (p.Capacity != null || p.IsUnlimited != null) && kind != "DataStore" || p.StoreId != null && kind != "DataStoreReference")
            throw new InvalidDataException("Data property fields do not match their native data kind.");
    }
    public static void Verify(NativeMutation c, NativeElement e, NativeElement[] graph)
    {
        if (c.DataProperties is not { } p) return;
        RequireKind(e.Kind, p);
        var d = e.Data ?? throw new InvalidDataException("Native data observation is absent after restart.");
        if (p.State != null && p.State != d.State || p.IsCollection != null && p.IsCollection != d.IsCollection || p.Capacity != null && p.Capacity != d.Capacity ||
            p.IsUnlimited != null && p.IsUnlimited != d.IsUnlimited)
            throw new InvalidDataException("Native data properties differ after fresh-worker readback.");
        if (p.StoreId is { } id && (d.StoreId != id || d.StoreBpmnNamespace != "" || d.StoreBpmnName != id && d.StoreBpmnName != "Id_" + id ||
            graph.Count(s => s.Id == id && s.Kind == "DataStore" && s.DiagramId == e.DiagramId) != 1))
            throw new InvalidDataException("Native store reference differs or is unresolved after restart.");
        if (e.Kind == "DataStore" && p.State != null)
        {
            var references = graph.Where(r => r.Kind == "DataStoreReference" && r.Data?.StoreId == e.Id).ToArray();
            if (references.Length == 0 || references.Any(r => r.Data!.State != p.State)) throw new InvalidDataException("Shared store state was not retained on every native reference.");
        }
    }
    private static void Scalar(XElement a, XElement b, string field, string expected, string absent = "")
    {
        if (((string?)b.Attribute(field) ?? absent) != expected) throw new InvalidDataException("Durable native data field differs: " + field);
        b.SetAttributeValue(field, (string?)a.Attribute(field));
    }
    public static void Project(XElement a, XElement b, NativeMutation c, NativeElement[] graph)
    {
        if (!NativeFidelity.IsNativeNameOwner(a) || !NativeFidelity.IsNativeNameOwner(b) || a.Name != b.Name) throw new InvalidDataException("Data projection requires exact native identities.");
        if (c.DataProperties is { } p)
        {
            RequireKind(a.Name.LocalName, p);
            if (p.State != null && a.Name == Ns + "DataObject") Scalar(a, b, "State", p.State);
            if (p.IsCollection is bool collection)
            {
                var left = a.Elements(Ns + "DataField").ToArray(); var right = b.Elements(Ns + "DataField").ToArray();
                if (left.Length != 1 || right.Length != 1) throw new InvalidDataException("Missing or ambiguous native DataField.");
                Scalar(left[0], right[0], "IsArray", collection ? "true" : "false", "false");
            }
            if (p.Capacity != null) Scalar(a, b, "Capacity", p.Capacity);
            // XPDL defaults to false even though the BPMN DataStore constructor defaults to true.
            if (p.IsUnlimited is bool unlimited) Scalar(a, b, "IsUnlimited", unlimited ? "true" : "false", "false");
            if (p.StoreId != null)
            {
                if ((string?)a.Attribute("DataStoreRef") != p.StoreId)
                {
                    var target = graph.Single(e => e.Id == p.StoreId && e.Kind == "DataStore");
                    Scalar(a, b, "State", target.Data!.State);
                }
                Scalar(a, b, "DataStoreRef", p.StoreId);
            }
        }
        if (c.Documentation != null && a.Name is var n && (n == Ns + "DataObject" || n == Ns + "DataStore"))
        {
            var old = a.Elements(Ns + "Object").Single().Elements(Ns + "Documentation").Single();
            var updated = b.Elements(Ns + "Object").Single().Elements(Ns + "Documentation").Single();
            if (old.HasAttributes || updated.HasAttributes || old.Nodes().Concat(updated.Nodes()).Any(v => v.GetType() != typeof(XText)) || updated.Value != c.Documentation)
                throw new InvalidDataException("Native data documentation differs or contains unknown content.");
            updated.ReplaceWith(new XElement(old));
        }
    }
    public static void ProjectStoreStates(XDocument a, XDocument b, NativeMutation[] changes, NativeElement[] graph)
    {
        foreach (var c in changes.Where(c => c.Operation == "update" && c.DataProperties?.State != null && graph.Any(e => e.Id == c.ElementId && e.Kind == "DataStore")))
            foreach (var reference in a.Descendants(Ns + "DataStoreReference").Where(e => NativeFidelity.IsNativeNameOwner(e) && (string?)e.Attribute("DataStoreRef") == c.ElementId))
            {
                var other = b.Descendants(Ns + "DataStoreReference").Where(e => NativeFidelity.IsNativeNameOwner(e) && (string?)e.Attribute("Id") == (string?)reference.Attribute("Id")).ToArray();
                // Deleted/relinked references remain governed by their own explicit mutation.
                if (other.Length == 1 && (string?)other[0].Attribute("DataStoreRef") == c.ElementId) Scalar(reference, other[0], "State", c.DataProperties!.State!);
            }
    }
}
