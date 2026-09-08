using System.Collections;
using System.Drawing;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private object PublicationService(string name) => Call(injector!, "Resolve",
        Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Documentation." + name))!;

    private string[] Publish(object model, EngineRequest request, EngineReply reply, Action<string> progress)
    {
        string extension = request.PublicationFormat switch
        {
            "excel" => ".xlsx",
            "word" => ".docx",
            "pdf" => ".pdf",
            "web" => "",
            _ => throw new NotSupportedException("Supported local publication formats: excel, word, pdf, web.")
        };
        bool web = request.PublicationFormat == "web";
        string nativeFormat = web ? "Html" : request.PublicationFormat == "pdf" ? "PDF" : request.PublicationFormat == "word" ? "Word" : "Excel";
        Directory.CreateDirectory(request.OutputPath);
        string output = Path.Combine(request.OutputPath, "documentation" + extension);
        // The native Web generator deletes target subfolders. It may only receive a
        // new operation-owned directory, never an existing user publication tree.
        if (web && (Directory.Exists(output) || File.Exists(output))) throw new IOException("Web publication requires a fresh output directory.");
        // A v5 archive does not persist the file-level display title. Publication receives it explicitly.
        Set(model, "Name", request.PublicationTitle);
        object settings = Get(model, "DocumentationSettings");
        Call(Get(settings, "SelectedDiagramsInDocumentation"), "Clear");
        Call(Get(settings, "DiagramDocumentationSettings"), "Clear");
        // Never follow an absolute template path embedded in an imported model or load operator preferences.
        Set(settings, "WordTemplatePath", Path.Combine(installation, "DocTemplates", "BizagiTemplate.dot"));
        var diagrams = Items(model, "Diagrams").Where(d => request.SelectedDiagramIds.Length == 0 || request.SelectedDiagramIds.Contains(Text(d, "Id"))).ToArray();
        if (diagrams.Length == 0 || (request.SelectedDiagramIds.Length > 0 && diagrams.Length != request.SelectedDiagramIds.Length))
            throw new InvalidDataException("Publication selection contains missing or duplicate diagram IDs.");
        var graph = Graph(model).ToArray();
        var images = new Dictionary<string, string>();
        var bounds = new Dictionary<string, RectangleF>();
        foreach (object diagram in diagrams)
        {
            string diagramId = Text(diagram, "Id");
            Call(Get(settings, "SelectedDiagramsInDocumentation"), "Add", diagramId);
            object selection = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Documentation.DiagramDocumentationSettings"));
            Set(selection, "Element", diagram);
            foreach (var container in graph.Where(e => e.DiagramId == diagramId && (e.Value.GetType().Name == "Participant" || IsNativeSubProcess(e.Value))))
            {
                object process = container.Value.GetType().Name == "Participant" ? Get(container.Value, "Process") : container.Value;
                object selectedProcess = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Documentation.ProcessDocSettings"));
                Set(selectedProcess, "Process", container.Value);
                foreach (var element in graph.Where(e => e.ParentId == Text(process, "Id") && e.Value.GetType().Name != "LaneSet" && Optional(e.Value, "GraphicalProperties") != null))
                    Call(Get(selectedProcess, "Elements"), "Add", element.Value);
                Call(Get(selection, "Settings"), "Add", selectedProcess);
            }
            Call(Get(settings, "DiagramDocumentationSettings"), "Add", selection);
            if (request.PublicationFormat != "excel")
            {
                var surfaces = new[] { "" }.Concat(graph.Where(e => e.DiagramId == diagramId && IsNativeSubProcess(e.Value) && Items(e.Value, "FlowElements").Any()).Select(e => Text(e.Value, "Id")));
                foreach (string subProcessId in surfaces)
                {
                    string surfaceId = subProcessId.Length == 0 ? diagramId : subProcessId;
                    progress("native_publication_image:" + surfaceId);
                    string png = Render(model, new EngineRequest
                    {
                        DiagramId = diagramId,
                        SubProcessId = subProcessId,
                        OutputPath = Path.Combine(request.OutputPath, "images"),
                        AtomicStepSeconds = request.AtomicStepSeconds,
                        InactivitySeconds = request.InactivitySeconds
                    }, progress).Single(p => p.EndsWith(".png"));
                    images.Add(surfaceId, png);
                    using var bitmap = new Bitmap(png); bounds.Add(surfaceId, new RectangleF(0, 0, bitmap.Width, bitmap.Height));
                }
            }
        }
        object parameters = New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Documentation.Generators.Model.DocumentationParameters"));
        Set(parameters, "Model", model); Set(parameters, "ExportPath", output); Set(parameters, "Orientation", "Landscape");
        object format = Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.DocumentationGenerationType"), nativeFormat);
        Set(parameters, "DocumentType", format);
        if (web)
        {
            // The mapper serializes PageImages verbatim. Stage actual native PNGs
            // under the generator's temporal tree and supply portable site-relative paths.
            string temporalImages = Path.Combine(workRoot, "publication", "files", "diagrams");
            Directory.CreateDirectory(temporalImages);
            foreach (string id in images.Keys.ToArray())
            {
                string fileName = id + ".png";
                File.Copy(images[id], Path.Combine(temporalImages, fileName), false);
                images[id] = "files/diagrams/" + fileName;
            }
            object loader = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.Cloud.CompanyLogo.ICompanyLogoLoader");
            object logo = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Documentation.DocumentationLogoManager"), loader);
            Set(logo, "DocLogoType", Enum.Parse(logo.GetType().GetProperty("DocLogoType")!.PropertyType, "Default"));
            Set(parameters, "LogoManager", logo);
        }
        object environment = New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Documentation.Application.DocumentationEnvironment"),
            parameters, Path.Combine(workRoot, "attachments"), Path.Combine(workRoot, "publication"), bounds, images);
        Directory.CreateDirectory(Path.Combine(workRoot, "attachments")); Directory.CreateDirectory(Path.Combine(workRoot, "publication"));
        progress("native_publication_generate:" + request.PublicationFormat);
        if (request.PublicationFormat == "excel")
        {
            // The vendor's public Excel WriteToFile also opens the default desktop application.
            // Compose its real mapper, generator and persistence steps, deliberately excluding that launcher.
            object mapped = Call(PublicationService("Mappers.IPublicationModelMapper"), "CreateDocumentationModel", model, environment)!;
            // The native sheet maker omits participant sheets with no mapped child
            // elements, including visible pools. Preserve its actual projection so
            // the host can verify each omission against the source graph and report it.
            reply.ExcelPoolProjection = Items(mapped, "Pages").SelectMany(page => Items(page, "Elements")
                .Where(element => Text(element, "ElementType") == "Participant")
                .Select(element => new NativeExcelPoolProjection
                {
                    DiagramId = Text(page, "Id"), ElementId = Text(element, "Id"),
                    MappedElementIds = Items(element, "PageElements").Select(child => Text(child, "Id")).ToArray()
                })).ToArray();
            object tables = Call(PublicationService("Mappers.Excel.IMainMapper"), "MapToExcelModel", mapped, model, false, environment)!;
            object writer = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Util.Excel.IAsposeExcelWriter"))!;
            Call(writer, "GenerateFile", tables); Call(writer, "SaveFile", output);
            Call(PublicationService("Generators.IAttachmentPublisher"), "CopyAttachments", new FileInfo(output), "documentation_files", environment);
        }
        else
        {
            object generator = Call(PublicationService("Generators.IGeneratorFactory"), "GetInstance", format)!;
            Set(generator, "Environment", environment); Set(generator, "ExportPath", output);
            // The native interface is a four-property progress sink, not a graphical control or substitute engine.
            var tracker = new PublicationProgress(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Interfaces.IProgressTracker"), progress);
            Set(generator, "ProgressTracker", tracker.GetTransparentProxy());
            Call(generator, "GenerateDocumentation", parameters);
            if (web)
            {
                // Modeler 4.3 filters its search map to root pages even though it
                // emits subprocess pages. Reuse its actual native search mapper,
                // restricted to the selected rendered surfaces; do not invent an index
                // or alter installed viewer code. Retain the original data as diagnostics.
                string configuration = Path.Combine(output, "libs", "js", "json", "configuration.json.js");
                File.Copy(configuration, Path.Combine(workRoot, "native-web-original-configuration.json.js"), false);
                var webModel = NativeWebPublicationReader.ReadConfiguration(output);
                var search = ((IEnumerable)Call(generator, "CreateSearchMap", model)!).Cast<object>()
                    .Where(item => images.ContainsKey(Text(item, "ContainerId"))).ToArray();
                // Native DTO attributes belong to the installed JSON assembly, not
                // necessarily our pinned JSON version. Let its serializer honor them.
                var nativeSerializer = Type("Newtonsoft.Json.dll", "Newtonsoft.Json.JsonConvert").GetMethod("SerializeObject", new[] { typeof(object) })!;
                string nativeSearchJson = (string)nativeSerializer.Invoke(null, new object[] { search })!;
                webModel["searchMap"] = Newtonsoft.Json.Linq.JArray.Parse(nativeSearchJson);
                File.WriteAllText(configuration, "Bizagi.AppModel = " + webModel.ToString(Newtonsoft.Json.Formatting.None));
                progress("native_web_search_index_selected_surfaces:" + search.Length);
            }
        }
        string entry = web ? Path.Combine(output, "index.html") : output;
        if (!File.Exists(entry) || new FileInfo(entry).Length == 0) throw new IOException("Native publication did not produce a durable document.");
        progress("native_publication_persisted");
        return new[] { entry }.Concat(Directory.GetFiles(request.OutputPath, "*", SearchOption.AllDirectories).Where(p => p != entry)).ToArray();
    }

    private NativePublicationReadback ReadPublication(EngineRequest request, Action<string> progress)
    {
        progress("native_publication_readback:" + request.PublicationFormat);
        if (request.PublicationFormat == "web") return NativeWebPublicationReader.Read(request.InputPath);
        // Use the installed application's normal component initialization; no license material is copied or exported.
        PublicationService("Generators.WordGeneration.Settings.IWordPublicationEnvironment");
        var result = new NativePublicationReadback { Format = request.PublicationFormat };
        if (request.PublicationFormat == "excel")
        {
            object workbook = New(Type("Aspose.Cells.dll", "Aspose.Cells.Workbook"), request.InputPath);
            var text = new List<string>();
            var rows = new List<NativeExcelRow>();
            foreach (object sheet in (IEnumerable)Get(workbook, "Worksheets"))
            {
                result.PagesOrSheets++;
                var identities = new Dictionary<int, string>(); var names = new Dictionary<int, string>();
                bool visible = Text(sheet, "VisibilityType") == "Visible";
                foreach (object cell in (IEnumerable)Get(sheet, "Cells"))
                {
                    string value = Text(cell, "StringValue"); text.Add(value);
                    if (!visible) continue; // Hidden native index entries cannot prove visible data rows.
                    int row = (int)Get(cell, "Row"), column = (int)Get(cell, "Column");
                    if (row == 0) continue; // Installed native column headers are not model data.
                    if (column == 0 && Guid.TryParseExact(value, "D", out _)) identities[row] = value;
                    if (column == 1) names[row] = value;
                }
                foreach (var row in identities)
                    rows.Add(new NativeExcelRow { Sheet = Text(sheet, "Name"), RowNumber = row.Key, ElementId = row.Value,
                        Name = names.TryGetValue(row.Key, out var name) ? name : "" });
            }
            result.ExcelRows = rows.ToArray();
            result.Text = string.Join("\n", text);
            if (workbook is IDisposable disposable) disposable.Dispose();
        }
        else if (request.PublicationFormat == "word")
        {
            object document = New(Type("Aspose.Words.dll", "Aspose.Words.Document"), request.InputPath);
            result.Text = (string)Call(document, "GetText")!;
            result.PagesOrSheets = (int)Get(document, "PageCount");
            object shapeType = Enum.Parse(Type("Aspose.Words.dll", "Aspose.Words.NodeType"), "Shape");
            result.ImageSizes = ((IEnumerable)Call(document, "GetChildNodes", shapeType, true)!).Cast<object>()
                .Where(s => (bool)Get(s, "HasImage")).Select(s => Get(Get(s, "ImageData"), "ImageSize"))
                .Select(s => new NativeImageSize { Width = (int)Get(s, "WidthPixels"), Height = (int)Get(s, "HeightPixels") }).ToArray();
            result.Images = result.ImageSizes.Length;
        }
        else if (request.PublicationFormat == "pdf")
        {
            object document = New(Type("Aspose.PDF.dll", "Aspose.Pdf.Document"), request.InputPath);
            try
            {
                object absorber = New(Type("Aspose.PDF.dll", "Aspose.Pdf.Text.TextAbsorber"));
                Call(Get(document, "Pages"), "Accept", absorber);
                result.Text = Text(absorber, "Text");
                result.PagesOrSheets = (int)Get(Get(document, "Pages"), "Count");
                var sizes = new List<NativeImageSize>();
                foreach (object page in (IEnumerable)Get(document, "Pages"))
                    foreach (object item in (IEnumerable)Get(Get(page, "Resources"), "Images"))
                        sizes.Add(new NativeImageSize { Width = (int)Get(item, "Width"), Height = (int)Get(item, "Height") });
                result.ImageSizes = sizes.ToArray(); result.Images = sizes.Count;
            }
            finally { if (document is IDisposable disposable) disposable.Dispose(); }
        }
        else throw new NotSupportedException("Unknown publication readback format.");
        return result;
    }

    private sealed class PublicationProgress(Type contract, Action<string> progress) : RealProxy(contract)
    {
        private readonly Dictionary<string, object> values = new() { ["Maximun"] = 0, ["Minimun"] = 0, ["ProgressText"] = "", ["Value"] = 0 };
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            try
            {
                string property = call.MethodName.Substring(4);
                if (!values.ContainsKey(property)) throw new NotSupportedException("Unexpected native progress property.");
                if (call.MethodName.StartsWith("get_")) return new ReturnMessage(values[property], null, 0, call.LogicalCallContext, call);
                if (!call.MethodName.StartsWith("set_") || call.ArgCount != 1) throw new NotSupportedException("Unexpected native progress method.");
                values[property] = call.Args[0];
                progress("native_documentation_progress:" + property + "=" + values[property]);
                return new ReturnMessage(null, null, 0, call.LogicalCallContext, call);
            }
            catch (Exception error) { return new ReturnMessage(error, call); }
        }
    }
}
