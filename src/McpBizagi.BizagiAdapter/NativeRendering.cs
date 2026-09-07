using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.Threading;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private bool offscreenInitialized;
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetDllDirectory(string? directory);

    private string[] Render(object model, EngineRequest request, Action<string> progress)
    {
        Guid diagramId = Guid.Parse(request.DiagramId);
        if (!Items(model, "Diagrams").Any(d => Text(d, "Id") == diagramId.ToString()))
            throw new InvalidDataException("Unknown native diagram ID.");
        var graph = Graph(model).Where(e => e.DiagramId == request.DiagramId).ToArray();
        if (request.SubProcessId.Length > 0 && !graph.Any(e => Text(e.Value, "Id") == request.SubProcessId && IsNativeSubProcess(e.Value)))
            throw new InvalidDataException("Unknown native subprocess ID in the selected diagram.");
        WaitForNativeConfiguration(request.InactivitySeconds, progress);
        if (!offscreenInitialized)
        {
            progress("native_offscreen_initialize");
            // DLL search is changed only inside this disposable worker, never in the host or operator process.
            if (SetDllDirectory(installation) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            object settings = New(Type("CefSharp.OffScreen.dll", "CefSharp.OffScreen.CefSettings"));
            Set(settings, "BrowserSubprocessPath", Path.Combine(installation, "CefSharp.BrowserSubprocess.exe"));
            Set(settings, "ResourcesDirPath", installation);
            Set(settings, "LocalesDirPath", Path.Combine(installation, "locales"));
            Set(settings, "RootCachePath", Path.Combine(workRoot, "renderer-cache"));
            Set(settings, "LogFile", Path.Combine(workRoot, "renderer.log"));
            Set(settings, "WindowlessRenderingEnabled", true);
            Call(Get(settings, "CefCommandLineArgs"), "Add", "disable-gpu", "1");
            Call(Get(settings, "CefCommandLineArgs"), "Add", "disable-background-networking", "1");
            Type("CefSharp.dll", "CefSharp.CefSharpSettings").GetProperty("WcfEnabled")!.SetValue(null, true);
            var cef = Type("CefSharp.Core.dll", "CefSharp.Cef");
            var initialize = cef.GetMethods().Single(m => m.Name == "Initialize" && m.GetParameters().Length == 2);
            if (!(bool)initialize.Invoke(null, new[] { settings, true })!) throw new InvalidOperationException("Native offscreen Chromium initialization failed.");
            offscreenInitialized = true;
        }
        string surface = request.SubProcessId.Length == 0 ? request.DiagramId : request.SubProcessId;
        var byId = graph.ToDictionary(e => Text(e.Value, "Id"));
        object adapter = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.UI.dll",
            "Bizagi.ProcessModeler.UI.Controls.ModelProcessEditor.PresentationModel.ModelAdapter.IDiagramModelAdapter"))!;
        object generator = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.UI.Controls.dll",
            "Bizagi.ProcessModeler.UI.Controls.DiagramImageUtility.IDiagramImageGenerator"))!;
        object configuration = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.BusinessLogic.dll",
            "Bizagi.ProcessModeler.BusinessLogic.General.IElementConfigurationManager"))!;
        object serializer = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.UI.dll",
            "Bizagi.ProcessModeler.UI.Controls.ModelExplorer.Services.IHandlerResponseSerializer"))!;
        var serialize = serializer.GetType().GetMethods().Single(m => m.Name == "SerializeResponse" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
        object diagram = Items(model, "Diagrams").Single(d => Text(d, "Id") == request.DiagramId);
        var rendering = new HashSet<string>();
        var completed = new Dictionary<string, string>();
        string RenderSurface(string id)
        {
            if (completed.TryGetValue(id, out var cached)) return cached;
            if (!rendering.Add(id)) throw new InvalidDataException("Recursive expanded diagram reference cannot be rendered without a bounded view policy.");
            progress("native_render_surface:" + id);
            object collection = id == request.DiagramId ? Call(adapter, "GetDataCollectionForView", model, diagramId)!
                : Call(adapter, "GetSubProcessDataCollectionForView", model, diagramId, Guid.Parse(id))!;
            collection = Call(generator, "GetUpdatedHtmlTextCollection", collection, diagram)!;
            Set(collection, "DefaultConfiguration", Get(configuration, "ElementConfigValues"));
            // The vendor convenience helper exports immediately after asynchronous renderDiagram.
            // Compose the same native DTO APIs bottom-up, but wait for each child's real renderer completion.
            foreach (object child in Items(collection, "Subprocesses"))
            {
                string childId = Text(child, "Id");
                object nativeChild = byId[childId].Value;
                if (!(bool)Get(Get(nativeChild, "GraphicalProperties"), "Expanded")) continue;
                if (!IsNativeSubProcess(nativeChild))
                    throw new NotSupportedException("Expanded reusable call-activity rendering requires a separately verified reference contract.");
                Set(child, "SvgImage", RenderSurface(childId));
            }
            string serialized = (string)serialize.MakeGenericMethod(collection.GetType()).Invoke(serializer, new[] { collection })!;
            if (Optional(collection, "DefaultConfiguration") == null)
                throw new InvalidOperationException("Native renderer DTO has no element configuration.");
            // Register listeners once; each surface resets only the evidence collected by those listeners.
            EvaluateInNativeBrowser("window.__mcpRenderErrors=[]; if(!window.__mcpRenderListeners){window.__mcpRenderListeners=true; " +
                "window.addEventListener('error', e => window.__mcpRenderErrors.push(String(e.message).slice(0,4096))); " +
                "window.addEventListener('unhandledrejection', e => window.__mcpRenderErrors.push(String(e.reason).slice(0,4096)));}");
            EvaluateInNativeBrowser("renderDiagram(" + serialized + ")");
            bool InSurface(GraphEntry element)
            {
                string parent = element.ParentId;
                while (byId.TryGetValue(parent, out var owner))
                {
                    if (parent == id) return true;
                    if (IsNativeSubProcess(owner.Value)) return false;
                    parent = owner.ParentId;
                }
                return false;
            }
            var expected = graph.Where(e => Optional(e.Value, "GraphicalProperties") != null &&
                !new[] { "Collaboration", "Process", "LaneSet", "Resource", "DataStore" }.Contains(e.Value.GetType().Name) && InSurface(e) &&
                // Native main participants have BoundaryVisible=false. An empty invisible shell need
                // not have an SVG identity, but every graphical child remains required independently.
                !(e.Value.GetType().Name == "Participant" && (bool)Get(e.Value, "IsMainParticipant")))
                .Select(e => Text(e.Value, "Id")).ToArray();
            File.WriteAllLines(Path.Combine(workRoot, "render-required-identities-" + id + ".txt"), expected);
            File.WriteAllLines(Path.Combine(workRoot, "render-invisible-main-participants-" + id + ".txt"), graph
                .Where(e => e.Value.GetType().Name == "Participant" && (bool)Get(e.Value, "IsMainParticipant") && InSurface(e)).Select(e => Text(e.Value, "Id")));
            string result = WaitForCompleteSvg(expected, request.InactivitySeconds, progress);
            VerifyRenderedImages(result, id, graph.Where(e => expected.Contains(Text(e.Value, "Id"))).ToArray(), progress);
            rendering.Remove(id); completed.Add(id, result);
            return result;
        }
        string svg = RenderSurface(surface);
        Directory.CreateDirectory(request.OutputPath);
        string output = Path.Combine(request.OutputPath, surface + ".svg");
        File.WriteAllText(output, svg, new UTF8Encoding(false));
        // Keep native SVG semantics in Chromium. The installed GDI SVG parser is not assumed to match browser CSS rendering.
        byte[] pixels = RasterizeInNativeBrowser(svg, request.AtomicStepSeconds);
        using var pngStream = new MemoryStream(pixels, writable: false);
        using var bitmap = new Bitmap(pngStream);
        if (new[] { bitmap.GetPixel(0, 0), bitmap.GetPixel(bitmap.Width - 1, 0), bitmap.GetPixel(0, bitmap.Height - 1), bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1) }.Any(c => c.A != 0))
            throw new InvalidDataException("Native raster output does not have transparent corners; no transparent-image claim is made.");
        string png = Path.ChangeExtension(output, ".png"); bitmap.Save(png, ImageFormat.Png);
        progress("native_svg_persisted");
        return new[] { output, png };
    }

    private void WaitForNativeConfiguration(int inactivitySeconds, Action<string> progress)
    {
        progress("native_configuration_initialize");
        object manager = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.BusinessLogic.dll",
            "Bizagi.ProcessModeler.BusinessLogic.General.IElementConfigurationManager"))!;
        var inactivity = Stopwatch.StartNew();
        string previous = "";
        string previousState = "";
        string path = Path.Combine(localSettings, "DefaultElementConfig.xml");
        while (true)
        {
            // The constructor starts an unexposed background Task. DocumentationConfigValue is assigned after
            // the dictionary is populated; the XML marker is written by the final native initialization step.
            Thread.MemoryBarrier();
            bool ready = Optional(manager, "DocumentationConfigValue") != null && Optional(manager, "ElementConfigValues") != null;
            int count = Optional(manager, "ElementConfigValues") is object values ? Convert.ToInt32(Optional(values, "Count")) : 0;
            string state = ready + ":" + count;
            if (state != previousState) { previousState = state; inactivity.Restart(); progress("native_configuration_state:" + state); }
            string xml = "";
            // Do not touch the file during the native remove/deserialize/reset phase. The completed
            // in-memory values precede the final XML marker; even then, observe with writer-compatible sharing.
            try { if (ready && File.Exists(path)) xml = TransientFileSnapshot.ReadText(path); }
            catch (IOException) { /* A native write is still in progress; readiness is not inferred from existence. */ }
            if (xml != previous) { previous = xml; inactivity.Restart(); }
            if (ready && xml.Contains("<ID>TextAttributeVisualization</ID>"))
            {
                var document = new XmlDocument { XmlResolver = null };
                try
                {
                    using var input = new StringReader(xml);
                    using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 });
                    document.Load(reader);
                    progress("native_configuration_ready");
                    return;
                }
                catch (XmlException) { /* Native serialization may not yet have closed the document. */ }
            }
            if (inactivity.Elapsed.TotalSeconds > inactivitySeconds)
            {
                // Retain the exact last observation for asynchronous initializer failures, without resetting defaults.
                File.WriteAllText(Path.Combine(workRoot, "incomplete-native-configuration.xml"), xml);
                File.WriteAllText(Path.Combine(workRoot, "native-configuration-state.txt"), "ready=" + ready + "; elements=" + count);
                throw new TimeoutException("Native graphical configuration did not finish initialization in the configured inactivity window.");
            }
            Thread.Sleep(50);
        }
    }

    private string WaitForCompleteSvg(string[] expected, int inactivitySeconds, Action<string> progress)
    {
        var inactivity = Stopwatch.StartNew();
        string previous = "";
        int stableSamples = 0, lastCount = -1;
        while (true)
        {
            string error = EvaluateInNativeBrowser("window.__mcpRenderErrors.join(' | ')");
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Native asynchronous rendering failed: " + error);
            string svg = EvaluateInNativeBrowser("exportSubprocessToSVG(false,false)");
            var xml = new XmlDocument { XmlResolver = null };
            using (var input = new StringReader(svg))
            using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 })) xml.Load(reader);
            if (xml.DocumentElement?.LocalName != "svg") throw new InvalidDataException("Native renderer returned a non-SVG document.");
            var present = new HashSet<string>(xml.SelectNodes("//*[@data-element-id]")!.Cast<XmlElement>().Select(e => e.GetAttribute("data-element-id")));
            string[] missing = expected.Where(id => !present.Contains(id)).ToArray();
            int count = expected.Length - missing.Length;
            if (svg != previous) { inactivity.Restart(); stableSamples = 0; previous = svg; }
            else stableSamples++;
            if (count != lastCount) { progress("native_render_elements:" + count + "/" + expected.Length); lastCount = count; }
            // Vendor rendering emits work through its event system. Method return is not completion.
            if (missing.Length == 0 && stableSamples >= 2 && EvaluateInNativeBrowser("document.fonts.status") == "loaded") return svg;
            if (inactivity.Elapsed.TotalSeconds > inactivitySeconds)
            {
                File.WriteAllText(Path.Combine(workRoot, "incomplete-render.svg"), svg, new UTF8Encoding(false));
                throw new TimeoutException("Native renderer stopped advancing with missing graphical IDs: " + string.Join(", ", missing));
            }
            Thread.Sleep(100);
        }
    }

    private byte[] RasterizeInNativeBrowser(string svg, int atomicSeconds)
    {
        string uri = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        string setup = "window.__mcpRaster = {state:'pending'}; (() => { const image = new Image(); " +
            "image.onload = () => { try { const canvas = document.createElement('canvas'); canvas.width = image.naturalWidth; canvas.height = image.naturalHeight; " +
            "canvas.getContext('2d').drawImage(image,0,0); window.__mcpRaster = {state:'done',png:canvas.toDataURL('image/png')}; } " +
            "catch(e) { window.__mcpRaster = {state:'error',error:String(e)}; } }; image.onerror = () => { window.__mcpRaster = {state:'error',error:'SVG image decode failed'}; }; image.src = '" + uri + "'; })();";
        EvaluateInNativeBrowser(setup);
        var wait = Stopwatch.StartNew();
        while (true)
        {
            string state = EvaluateInNativeBrowser("window.__mcpRaster.state");
            if (state == "done") return Convert.FromBase64String(EvaluateInNativeBrowser("window.__mcpRaster.png").Substring("data:image/png;base64,".Length));
            if (state == "error") throw new InvalidDataException(EvaluateInNativeBrowser("window.__mcpRaster.error"));
            // Image decoding is an atomic renderer handshake, not an operation-wide timeout.
            if (wait.Elapsed.TotalSeconds > atomicSeconds) throw new TimeoutException("Native SVG image decode handshake exceeded its configured limit.");
            Thread.Sleep(50);
        }
    }
    private string EvaluateInNativeBrowser(string script)
    {
        object browser = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.UI.Controls.dll",
            "Bizagi.ProcessModeler.UI.Controls.DiagramImageUtility.IBrowserImage"))!;
        object response = Call(browser, "EvaluateScript", script) ?? throw new InvalidOperationException("Native browser returned no script response.");
        if (!(bool)Get(response, "Success")) throw new InvalidOperationException("Native rendering failed: " + Text(response, "Message"));
        return Text(response, "Result");
    }
}
