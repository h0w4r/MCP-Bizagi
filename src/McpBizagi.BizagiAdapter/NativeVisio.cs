using System.Collections;
using McpBizagi.Contracts;
using Newtonsoft.Json;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private object VisioManager() => Resolve("Bizagi.ProcessModeler.BusinessEntities.Visio.Interfaces.IVisioManager");

    private string[] ImportVisio(object model, string path, Action<string> progress)
    {
        var tracker = new PublicationProgress(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Interfaces.IProgressTracker"),
            message => progress(message.Replace("native_documentation_progress:", "native_visio_progress:")));
        progress("native_visio_import");
        object result = Call(VisioManager(), "ImportFileAspose", path, tracker.GetTransparentProxy())
            ?? throw new InvalidDataException("Native Visio importer returned no result.");
        var conflicts = Items(Get(result, "Conflicts")).Select(c => new {
            unmappedShapes = Items(Get(c, "UnmappedShapes")).Select(s => s.ToString()).ToArray() }).ToArray();
        // Keep the real conflict evidence even when the native manager silently skips a whole page.
        File.WriteAllText(Path.Combine(workRoot, "visio-import-conflicts.json"), JsonConvert.SerializeObject(conflicts));
        if (conflicts.Length != 0) throw new InvalidDataException("Native Visio import has unmapped shapes; no partial model will be published. Inspect visio-import-conflicts.json.");
        var diagrams = Items(Get(result, "Diagrams")).ToArray();
        if (diagrams.Length is < 1 or > 100) throw new InvalidDataException("Visio import must produce 1-100 diagrams.");
        foreach (object diagram in diagrams) { Call(Get(model, "Diagrams"), "Add", diagram); Set(diagram, "HasChanged", true); }
        ValidateExchangeIdentities(model, new Dictionary<string, object>(StringComparer.Ordinal));
        File.WriteAllText(Path.Combine(workRoot, "visio-transient-graph.json"), JsonConvert.SerializeObject(Graph(model).Select(Describe).ToArray()));
        var adjustments = new List<string>();
        var values = (IDictionary)Get(Get(model, "ExtendedAttributes"), "Values");
        foreach (object diagram in diagrams)
        {
            // Like native model creation, initialize missing new-model defaults before FIRST persistence.
            // Never apply this to a pre-existing .bpm or weaken the whole-archive fidelity verifier.
            var diagramId = (Guid)Get(diagram, "Id");
            if (!values.Contains(diagramId))
            {
                values.Add(diagramId, New(DocumentationType("DiagramAttributeValues")));
                adjustments.Add("visio_empty_attribute_collection_initialized:" + diagramId);
            }
            foreach (object participant in Items(Get(diagram, "Participants")).Where(p => (bool)Get(p, "IsMainParticipant")))
            {
                object graphics = Get(participant, "GraphicalProperties");
                if (Convert.ToDouble(Get(graphics, "Width")) == 0 && Convert.ToDouble(Get(graphics, "Height")) == 0)
                {
                    Set(graphics, "Size", Get(Get(participant, "DefaultGraphicalProperties"), "Size"));
                    adjustments.Add("visio_invisible_pool_native_default_size:" + Text(participant, "Id"));
                }
            }
        }
        return adjustments.ToArray();
    }

    private string ExportVisio(object model, EngineRequest request, Action<string> progress)
    {
        var diagrams = Items(Get(model, "Diagrams")).ToDictionary(d => Text(d, "Id"), StringComparer.Ordinal);
        string[] selected = request.SelectedDiagramIds;
        if (selected.Length is < 1 or > 100 || selected.Distinct().Count() != selected.Length || selected.Any(id => !diagrams.ContainsKey(id)))
            throw new InvalidDataException("Select 1-100 distinct existing native diagrams for Visio export.");
        // A separate collection preserves model membership and follows the explicit selection order.
        object collection = New(Get(model, "Diagrams").GetType());
        foreach (string id in selected) Call(collection, "Add", diagrams[id]);
        if (!request.OutputPath.EndsWith(".vdx", StringComparison.Ordinal) || request.OutputPath.Contains(".vsd"))
            throw new InvalidDataException("The installed Visio exporter requires an unambiguous .vdx output path.");
        if (File.Exists(request.OutputPath)) throw new IOException("Visio export refuses to overwrite an existing artifact.");
        var tracker = new PublicationProgress(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Interfaces.IProgressTracker"),
            message => progress(message.Replace("native_documentation_progress:", "native_visio_progress:")));
        progress("native_visio_export");
        Call(VisioManager(), "ExportModelAspose", collection, request.OutputPath, tracker.GetTransparentProxy());
        if (!File.Exists(request.OutputPath) || new FileInfo(request.OutputPath).Length == 0) throw new IOException("Native Visio export produced no artifact.");
        return request.OutputPath;
    }
}
