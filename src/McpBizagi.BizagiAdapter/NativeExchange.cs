using System.Collections;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private object XpdlManager(object model)
    {
        object manager = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.IXpdlInteropManager");
        Call(manager, "SetCustomArtifactTypes", Get(model, "CustomArtifactTypes"));
        return manager;
    }

    private NativeExchangeArtifact[] ImportXpdl(object model, string[] paths, Action<string> progress)
    {
        if (paths.Length is < 1 or > 100) throw new InvalidDataException("XPDL import requires 1-100 documents.");
        object manager = XpdlManager(model), diagrams = Get(model, "Diagrams");
        var receipts = new List<NativeExchangeArtifact>();
        foreach (string path in paths)
        {
            progress("native_xpdl_import:" + Path.GetFileName(path));
            object diagram = Call(manager, "Import", path, model) ?? throw new InvalidDataException("Native XPDL import returned no diagram.");
            if (((IEnumerable)diagrams).Cast<object>().Any(d => Text(d, "Id") == Text(diagram, "Id")))
                throw new InvalidDataException("XPDL diagram identity collision; no existing diagram can be replaced implicitly.");
            Call(diagrams, "Add", diagram);
            receipts.Add(new NativeExchangeArtifact { DiagramId = Text(diagram, "Id"), Path = path });
        }
        ValidateExchangeIdentities(model, new Dictionary<string, object>(StringComparer.Ordinal));
        return receipts.ToArray();
    }

    private static void ValidateExchangeIdentities(object value, Dictionary<string, object> visited)
    {
        // The ordinary graph view deduplicates shared references. Before accepting imported data,
        // distinguish a shared object from two different native objects with the same identity.
        string id = Text(value, "Id");
        if (string.IsNullOrEmpty(id)) return;
        if (visited.TryGetValue(id, out object previous))
        {
            if (!ReferenceEquals(previous, value)) throw new InvalidDataException("Imported interchange has ambiguous native identities: " + id);
            return;
        }
        visited.Add(id, value);
        foreach (string property in GraphCollections)
            if (Optional(value, property) is IEnumerable children)
                foreach (object child in children) ValidateExchangeIdentities(child, visited);
        foreach (string property in GraphChildren)
            if (Optional(value, property) is object child) ValidateExchangeIdentities(child, visited);
    }

    private NativeExchangeArtifact[] ExportXpdl(object model, EngineRequest request, Action<string> progress)
    {
        var diagrams = ((IEnumerable)Get(model, "Diagrams")).Cast<object>().ToDictionary(d => Text(d, "Id"), StringComparer.Ordinal);
        var selected = request.SelectedDiagramIds;
        if (selected.Length is < 1 or > 100 || selected.Distinct().Count() != selected.Length || selected.Any(id => !diagrams.ContainsKey(id)))
            throw new InvalidDataException("Select 1-100 distinct existing diagrams for XPDL export.");
        Directory.CreateDirectory(request.OutputPath);
        object manager = XpdlManager(model); var result = new List<NativeExchangeArtifact>();
        foreach (string id in selected)
        {
            // The stream overload internally encodes ASCII. Use the installed Unicode file serializer
            // and explicit ID-based names, not the vendor's label-derived collision-prone folder export.
            string file = Path.Combine(request.OutputPath, Guid.Parse(id).ToString() + ".xpdl");
            if (File.Exists(file)) throw new IOException("XPDL export refuses to overwrite an existing artifact.");
            progress("native_xpdl_export:" + id);
            Call(manager, "Export", model, file, diagrams[id]);
            if (!File.Exists(file) || new FileInfo(file).Length == 0) throw new IOException("Native XPDL exporter produced no document.");
            result.Add(new NativeExchangeArtifact { DiagramId = id, Path = file });
        }
        return result.ToArray();
    }
}
