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
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetDllDirectory(string? directory);

    private string[] Render(object model, EngineRequest request, Action<string> progress)
    {
        Guid diagramId = Guid.Parse(request.DiagramId);
        if (!Items(model, "Diagrams").Any(d => Text(d, "Id") == diagramId.ToString()))
            throw new InvalidDataException("Unknown native diagram ID.");
        WaitForNativeConfiguration(request.InactivitySeconds, progress);
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
        progress("native_render_svg");
        object handler = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.UI.dll",
            "Bizagi.ProcessModeler.UI.Controls.ModelProcessEditor.Utility.ImageUtils.IGeneralImageHandler"))!;
        object collection = Call(handler, "GetCollectionDto", model, diagramId, false)!;
        object serializer = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.UI.dll",
            "Bizagi.ProcessModeler.UI.Controls.ModelExplorer.Services.IHandlerResponseSerializer"))!;
        var serialize = serializer.GetType().GetMethods().Single(m => m.Name == "SerializeResponse" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
        string serialized = (string)serialize.MakeGenericMethod(collection.GetType()).Invoke(serializer, new[] { collection })!;
        if (Optional(collection, "DefaultConfiguration") == null)
            throw new InvalidOperationException("Native renderer DTO has no element configuration.");
        // Native renderDiagram dispatches asynchronous JavaScript work; its return value cannot report later errors.
        EvaluateInNativeBrowser("window.__mcpRenderErrors=[]; window.addEventListener('error', e => window.__mcpRenderErrors.push(String(e.message).slice(0,4096))); " +
            "window.addEventListener('unhandledrejection', e => window.__mcpRenderErrors.push(String(e.reason).slice(0,4096)));");
        // The vendor helper discards the render script's success flag. Check it before accepting any partial SVG.
        EvaluateInNativeBrowser("renderDiagram(" + serialized + ")");
        var graph = Graph(model).Where(e => e.DiagramId == request.DiagramId).ToArray();
        var expected = graph.Where(e => Optional(e.Value, "GraphicalProperties") != null &&
            !new[] { "Collaboration", "Process", "LaneSet", "Resource" }.Contains(e.Value.GetType().Name) &&
            !graph.Any(parent => Text(parent.Value, "Id") == e.ParentId && parent.Value.GetType().Name == "SubProcess"))
            .Select(e => Text(e.Value, "Id")).ToArray();
        string svg = WaitForCompleteSvg(expected, request.InactivitySeconds, progress);
        Directory.CreateDirectory(request.OutputPath);
        string output = Path.Combine(request.OutputPath, diagramId + ".svg");
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
        string path = Path.Combine(localSettings, "DefaultElementConfig.xml");
        while (true)
        {
            // The constructor starts an unexposed background Task. DocumentationConfigValue is assigned after
            // the dictionary is populated; the XML marker is written by the final native initialization step.
            Thread.MemoryBarrier();
            bool ready = Optional(manager, "DocumentationConfigValue") != null && Optional(manager, "ElementConfigValues") != null;
            string xml = "";
            try { if (File.Exists(path)) xml = File.ReadAllText(path); }
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
                throw new TimeoutException("Native graphical configuration did not finish initialization in the configured inactivity window.");
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
