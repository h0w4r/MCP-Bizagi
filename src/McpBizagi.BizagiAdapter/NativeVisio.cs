using System.Collections;
using McpBizagi.Contracts;
using Newtonsoft.Json;
using System.Drawing;
using System.Xml.Linq;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private readonly List<NativeVisioPageReceipt> visioPages = new();
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
            foreach (object participant in Items(Get(diagram, "Participants")))
            {
                // The installed persistence loader derives a partitioned pool's height from
                // its contiguous lanes. Initialize that same domain invariant BEFORE saving
                // a new foreign import, rather than excusing later native archive drift.
                var lanes = Items(Get(Get(participant, "Process"), "LaneSets")).SelectMany(set => Items(Get(set, "Lanes")))
                    .OrderBy(lane => Convert.ToSingle(Get(Get(lane, "GraphicalProperties"), "Y"))).ToArray();
                if (lanes.Length == 0) continue;
                float height = 0;
                foreach (object lane in lanes)
                {
                    object laneGraphics = Get(lane, "GraphicalProperties");
                    float y = Convert.ToSingle(Get(laneGraphics, "Y")), laneHeight = Convert.ToSingle(Get(laneGraphics, "Height"));
                    if (y != height || float.IsNaN(laneHeight) || float.IsInfinity(laneHeight) || laneHeight <= 0)
                        throw new InvalidDataException("Imported Visio lanes are not a contiguous positive native partition; no geometry is silently repacked.");
                    height += laneHeight;
                }
                if (float.IsInfinity(height)) throw new InvalidDataException("Imported Visio partition height is unbounded.");
                object graphics = Get(participant, "GraphicalProperties");
                float originalHeight = Convert.ToSingle(Get(graphics, "Height"));
                if (height != originalHeight)
                {
                    Set(graphics, "Height", height);
                    adjustments.Add("visio_partition_pool_height_initialized:" + Text(participant, "Id") + ":" +
                        originalHeight.ToString(System.Globalization.CultureInfo.InvariantCulture) + "->" + height.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
        foreach (string id in selected)
        {
            Call(collection, "Add", diagrams[id]);
            visioPages.Add(new NativeVisioPageReceipt { SourceDiagramId = id, PageName = Text(diagrams[id], "DisplayName") });
        }
        var graph = Graph(model).ToArray();
        var before = graph.Select(Describe).ToArray();
        string metadataBefore = JsonConvert.SerializeObject(new { metadata = Metadata(model), documentation = Documentation(model) });
        foreach (string diagramId in selected)
            foreach (var entry in graph.Where(e => e.DiagramId == diagramId && IsNativeSubProcess(e.Value) && Items(Get(e.Value, "GraphicalElements")).Any()))
            {
                if (visioPages.Count >= 100) throw new InvalidDataException("Selected Visio root and subprocess surfaces exceed the 100-page bound.");
                // A read-only native view gives the existing exporter a real process canvas for each
                // nested body. References are not removed/reparented in the original model or saved.
                object projection = CreateVisioSubProcessView(entry.Value, before);
                Call(collection, "Add", projection);
                visioPages.Add(new NativeVisioPageReceipt { SourceDiagramId = diagramId, SourceSubProcessId = Text(entry.Value, "Id"), PageName = Text(projection, "DisplayName") });
            }
        if (!request.OutputPath.EndsWith(".vdx", StringComparison.Ordinal) || request.OutputPath.Contains(".vsd"))
            throw new InvalidDataException("The installed Visio exporter requires an unambiguous .vdx output path.");
        if (File.Exists(request.OutputPath)) throw new IOException("Visio export refuses to overwrite an existing artifact.");
        var tracker = new PublicationProgress(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Interfaces.IProgressTracker"),
            message => progress(message.Replace("native_documentation_progress:", "native_visio_progress:")));
        progress("native_visio_export");
        Call(VisioManager(), "ExportModelAspose", collection, request.OutputPath, tracker.GetTransparentProxy());
        if (!File.Exists(request.OutputPath) || new FileInfo(request.OutputPath).Length == 0) throw new IOException("Native Visio export produced no artifact.");
        if (JsonConvert.SerializeObject(before) != JsonConvert.SerializeObject(Graph(model).Select(Describe).ToArray()) ||
            metadataBefore != JsonConvert.SerializeObject(new { metadata = Metadata(model), documentation = Documentation(model) }))
            throw new InvalidDataException("Native Visio projection changed the original graph or metadata.");
        // Read the actual IDs from the generated document; do not assume page numbering.
        XNamespace ns = "http://schemas.microsoft.com/visio/2003/core";
        var generated = XDocument.Load(request.OutputPath).Root!.Element(ns + "Pages")!.Elements(ns + "Page").ToArray();
        if (generated.Length < visioPages.Count) throw new InvalidDataException("Native Visio export omitted a requested surface.");
        for (int i = 0; i < visioPages.Count; i++)
        {
            if ((string?)generated[i].Attribute("Name") != visioPages[i].PageName) throw new InvalidDataException("Native Visio page order/label does not match its source surface.");
            visioPages[i].PageId = (string?)generated[i].Attribute("ID") ?? throw new InvalidDataException("Native Visio page has no identity.");
        }
        return request.OutputPath;
    }

    private object CreateVisioSubProcessView(object sub, NativeElement[] graph)
    {
        object projection = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.Collaboration"));
        object pool = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.Participant"));
        Set(pool, "DisplayName", Text(sub, "DisplayName"));
        Set(projection, "DisplayName", Text(sub, "DisplayName"));
        object process = Get(pool, "Process");
        foreach (string property in new[] { "FlowElements", "Artifacts", "LaneSets", "Milestones" })
            foreach (object item in Items(Get(sub, property))) Call(Get(process, property), "Add", item);
        Call(Get(projection, "Participants"), "Add", pool);
        var children = graph.Where(e => e.ParentId == Text(sub, "Id") && e.Geometry != null).ToArray();
        var xs = children.SelectMany(e => new[] { e.Geometry!.X, e.Geometry.X + e.Geometry.Width }.Concat(e.Points.Select(p => p.X))).ToArray();
        var ys = children.SelectMany(e => new[] { e.Geometry!.Y, e.Geometry.Y + e.Geometry.Height }.Concat(e.Points.Select(p => p.Y))).ToArray();
        if (xs.Length == 0 || xs.Concat(ys).Any(v => double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1_000_000))
            throw new InvalidDataException("Subprocess projection requires bounded finite native canvas coordinates.");
        if (xs.Min() < 0 || ys.Min() < 0)
            throw new InvalidDataException("Visio subprocess projection does not translate negative source coordinates. No partial export is published.");
        // Enclose the actual canvas, including connector vertices, rather than assuming nested DI is local.
        // The native loader removes pools outside the nonnegative canvas, including their contents.
        // Never add a negative margin or silently translate the source objects to hide that loss.
        double left = 0, top = 0;
        object graphics = Get(pool, "GraphicalProperties");
        Set(graphics, "X", (float)left); Set(graphics, "Y", (float)top);
        Set(graphics, "Size", new SizeF((float)(xs.Max() - left + 40), (float)(ys.Max() - top + 40)));
        return projection;
    }
}
