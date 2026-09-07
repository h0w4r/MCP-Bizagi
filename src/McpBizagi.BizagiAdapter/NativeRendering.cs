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
        // The vendor helper discards the render script's success flag. Check it before accepting any partial SVG.
        EvaluateInNativeBrowser("renderDiagram(" + serialized + ")");
        string svg = EvaluateInNativeBrowser("exportSubprocessToSVG(false,false)");
        // Reject an empty renderer response instead of producing a plausible substitute diagram.
        var xml = new XmlDocument { XmlResolver = null };
        using (var input = new StringReader(svg))
        using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) xml.Load(reader);
        if (xml.DocumentElement?.LocalName != "svg" || !xml.DocumentElement.HasChildNodes)
            throw new InvalidDataException("The native renderer did not return an SVG diagram.");
        var graph = Graph(model).Where(e => e.DiagramId == request.DiagramId).ToArray();
        var expected = graph.Where(e => Optional(e.Value, "GraphicalProperties") != null &&
            !new[] { "Collaboration", "Process", "LaneSet", "Resource" }.Contains(e.Value.GetType().Name) &&
            !graph.Any(parent => Text(parent.Value, "Id") == e.ParentId && parent.Value.GetType().Name == "SubProcess"));
        foreach (var element in expected)
            if (xml.SelectNodes("//*[@data-element-id]")!.Cast<XmlElement>().All(e => e.GetAttribute("data-element-id") != Text(element.Value, "Id")))
                throw new InvalidDataException("Native renderer omitted a top-level graphical element: " + Text(element.Value, "Id"));
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
