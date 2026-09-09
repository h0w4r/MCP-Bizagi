using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Linq.Expressions;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static string AlignmentHash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    private NativeAlignmentReceipt Align(object model, object persistence, EngineRequest request, Action<string> progress)
    {
        var alignment = request.Alignment ?? throw new InvalidDataException("Missing native alignment request.");
        string mode = alignment.Mode, diagramId = alignment.DiagramId;
        if (!new[] { "Top", "Bottom", "Left", "Right", "Horizontal", "Vertical", "HorizontalEvenly", "VerticalEvenly", "Calculated" }.Contains(mode) ||
            alignment.ElementIds.Length < 2 || alignment.ElementIds.Length > 1000 || alignment.ElementIds.Distinct().Count() != alignment.ElementIds.Length)
            throw new InvalidDataException("Invalid native alignment selection or mode.");
        if (mode == "Calculated" && (alignment.Placements == null ||
            !alignment.Placements.Select(p => p.ElementId).SequenceEqual(alignment.ElementIds) ||
            alignment.Placements.Any(p => double.IsNaN(p.X) || double.IsInfinity(p.X) || double.IsNaN(p.Y) || double.IsInfinity(p.Y) ||
                p.X < 0 || p.Y < 0 || p.X > 1000000 || p.Y > 1000000 || p.X != Math.Truncate(p.X) || p.Y != Math.Truncate(p.Y))))
            throw new InvalidDataException("Invalid calculated native layout positions.");
        object diagram = Items(model, "Diagrams").Single(d => Text(d, "Id") == diagramId);
        string asset = Path.Combine(installation, "ModelerProcessEditor", "output", "modeler-bpmn-editor.min.js");
        string assetHash = AlignmentHash(File.ReadAllBytes(asset));
        if (assetHash != "eec7db9e1474632e0e712c5df29ddc5b93aecb765cd8bc93422a1007ad8de1a9")
            throw new NotSupportedException("Installed editor asset differs from the investigated 4.3 adapter; writes require revalidation.");
        var receipt = new NativeAlignmentReceipt { Mode = mode, SelectedElementIds = alignment.ElementIds, EditorAssetSha256 = assetHash };
        // CEF rejects Task-returning bindings unless enabled before the first browser.
        // Concurrency applies to transport callbacks only; native commands stay serialized.
        Type("CefSharp.dll", "CefSharp.CefSharpSettings").GetProperty("ConcurrentTaskExecution")!.SetValue(null, true);
        // Initialize the same installed offscreen runtime and prove that the input renders first.
        Render(model, new EngineRequest { DiagramId = diagramId, SubProcessId = alignment.SubProcessId, OutputPath = Path.Combine(workRoot, "alignment-before"),
            InactivitySeconds = request.InactivitySeconds, AtomicStepSeconds = request.AtomicStepSeconds }, progress);
        object adapter = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.UI.dll", "Bizagi.ProcessModeler.UI.Controls.ModelProcessEditor.PresentationModel.ModelAdapter.IDiagramModelAdapter"))!;
        object data = alignment.SubProcessId == "" ? Call(adapter, "GetDataCollectionForView", model, Guid.Parse(diagramId))! : Call(adapter, "GetSubProcessDataCollectionForView", model, Guid.Parse(diagramId), Guid.Parse(alignment.SubProcessId))!;
        object config = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.General.IElementConfigurationManager"))!;
        Set(data, "DefaultConfiguration", Get(config, "ElementConfigValues"));
        object serializer = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.UI.dll", "Bizagi.ProcessModeler.UI.Controls.ModelExplorer.Services.IHandlerResponseSerializer"))!;
        string Serialize(object value) => (string)serializer.GetType().GetMethods().Single(m => m.Name == "SerializeResponse" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
            .MakeGenericMethod(value.GetType()).Invoke(serializer, new[] { value })!;
        string json = Serialize(data); File.WriteAllText(Path.Combine(workRoot, "actual-native-editor-data.json"), json);
        object imageBrowser = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.UI.Controls.dll", "Bizagi.ProcessModeler.UI.Controls.DiagramImageUtility.IBrowserImage"))!;
        object browser = imageBrowser.GetType().GetField("_browser", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(imageBrowser)!;
        try
        {
            object repository = Get(browser, "JavascriptObjectRepository");
            object initialProvider = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.UI.Controls.dll", "Bizagi.ProcessModeler.UI.Controls.DiagramImageUtility.Handler.IGeneralSynchronousHandler"))!;
            var bridge = new NativeAlignmentBridge(workRoot, json, "{\"content\":" + Serialize(Get(config, "ElementConfigValues")) + "}", () => Call(initialProvider, "GetInitialConfiguration")!);
            var consoleEvent = browser.GetType().GetEvent("ConsoleMessage")!;
            var consoleArgs = consoleEvent.EventHandlerType!.GetMethod("Invoke")!.GetParameters()
                .Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
            var consoleCall = Expression.Call(Expression.Constant(bridge), typeof(NativeAlignmentBridge).GetMethod(nameof(NativeAlignmentBridge.OnConsole))!,
                consoleArgs.Select(p => Expression.Convert(p, typeof(object))));
            consoleEvent.AddEventHandler(browser, Expression.Lambda(consoleEvent.EventHandlerType, consoleCall, consoleArgs).Compile());
            foreach (string name in new[] { "diagramEditorElementsHandler", "diagramEditorViewEventsHandler", "diagramEditorSynchronousHandler", "diagramEditorKeyboardCommandHandler" })
            {
                Call(repository, "UnRegister", name);
                object binding = Type("CefSharp.dll", "CefSharp.BindingOptions").GetProperty("DefaultBinder")!.GetValue(null)!;
                Call(repository, "Register", name, bridge, name != "diagramEditorSynchronousHandler", binding);
            }
            string baseUrl = System.Net.WebUtility.HtmlEncode(new Uri(Path.Combine(installation, "ModelerProcessEditor", "output") + Path.DirectorySeparatorChar).AbsoluteUri);
            string html = "<!doctype html><html><head><meta charset='UTF-8'><base href='" + baseUrl + "'><script>window.__probeErrors=[];window.__probeDocument='" + bridge.Token + "';addEventListener('error',e=>__probeErrors.push(e.message));addEventListener('unhandledrejection',e=>__probeErrors.push(String(e.reason)));</script><link rel='stylesheet' href='styles.css'><script src='modeler-bpmn-editor.min.js'></script></head><body><div id='bz-ml-bpmn-editor-container'><bz-process-viewer></bz-process-viewer><canvas id='measureText' style='visibility:hidden'></canvas></div></body></html>";
            string page = Path.Combine(workRoot, "editor-probe.html"); File.WriteAllText(page, html);
            Call(browser, "Load", new Uri(page).AbsoluteUri);
            void Wait(string phase, string expression)
            {
                progress(phase); var watch = Stopwatch.StartNew(); string previous = "";
                // This limit applies only to the named frontend handshake, never total job duration.
                int seconds = request.AtomicStepSeconds;
                while (true)
                {
                    string result = EvaluateInNativeBrowser(expression);
                    if (result != previous) { progress(phase + ":" + result); previous = result; }
                    if (result == "true") return;
                    if (watch.Elapsed.TotalSeconds > seconds)
                    {
                        string diagnostic = EvaluateInNativeBrowser("JSON.stringify({url:location.href,state:document.readyState,element:!!document.querySelector('bz-process-viewer'),show:typeof document.querySelector('bz-process-viewer')?.showDiagram,align:typeof window.alignSelectedShapes,cef:typeof window.CefSharp,bind:window.__bindStage,cefKeys:Object.keys(window.CefSharp||{}),errors:window.__probeErrors||[]})");
                        File.WriteAllText(Path.Combine(workRoot, phase + "-failure.json"), diagnostic);
                        File.WriteAllText(Path.Combine(workRoot, "editor-dom.html"), EvaluateInNativeBrowser("document.documentElement.outerHTML"));
                        throw new InvalidOperationException(phase + " handshake failed: " + diagnostic);
                    }
                    Thread.Sleep(100);
                }
            }
            // LoadingState is asynchronous: the old viewer exposes the same custom element.
            // The nonce proves this is the newly loaded editor context, not the previous page.
            Wait("native_editor_component", "String(window.__probeDocument==='" + bridge.Token + "' && document.readyState==='complete' && !!document.querySelector('bz-process-viewer')?.showDiagram)");
            // Positive RPC handshake must traverse the registered .NET object, not a mock JS handler.
            EvaluateInNativeBrowser("window.__probeHandshake=false;window.__bindStage='requested';var names=['diagramEditorElementsHandler','diagramEditorViewEventsHandler','diagramEditorSynchronousHandler','diagramEditorKeyboardCommandHandler'];names.forEach(n=>{CefSharp.DeleteBoundObject(n);CefSharp.RemoveObjectFromCache(n);});Promise.all(names.map(n=>CefSharp.BindObjectAsync(n).then(()=>n==='diagramEditorSynchronousHandler'?window[n].probeHandshake():window[n].asyncProbeHandshake()))).then(tokens=>{window.__bindStage='callbacks:'+JSON.stringify(tokens);window.__probeHandshake=tokens.every(x=>x==='" + bridge.Token + "');}).catch(e=>window.__bindStage='error:'+String(e));void 0;");
            Wait("native_editor_binding", "String(window.__probeHandshake===true)");
            EvaluateInNativeBrowser("document.querySelector('bz-process-viewer').showDiagram.emit(" + json + ");");
            Wait("native_editor_api", "String(typeof window.alignSelectedShapes==='function')");
            string[] ids = alignment.ElementIds;
            if (ids.Length < 2) throw new InvalidDataException("Alignment requires at least two actual native nodes.");
            File.WriteAllText(Path.Combine(workRoot, "layout-request.json"), JsonConvert.SerializeObject(new { mode, ids }));
            Wait("native_editor_shapes", "String(" + JsonConvert.SerializeObject(ids) + ".every(id=>document.querySelector('[data-element-id=\"'+id+'\"]')))");
            EvaluateInNativeBrowser("window.__mcpAlignmentSubProcess=" + (alignment.SubProcessId != "" ? "true" : "false") + ";");
            using (var script = new StreamReader(typeof(NativeEngine).Assembly.GetManifestResourceStream("McpBizagi.BizagiAdapter.NativeAlignmentBridge.js")!))
                EvaluateInNativeBrowser(script.ReadToEnd());
            Wait("native_editor_geometry_policy", "String(window.__mcpLayoutPolicy?.installed===true)");
            File.WriteAllText(Path.Combine(workRoot, "native-editor-initial-identities.json"), EvaluateInNativeBrowser("JSON.stringify(window.__mcpLayoutInitial)"));
            bridge.Phase = "requested_alignment";
            string command = mode == "Calculated"
                ? "window.__mcpApplyCalculatedLayout(" + JsonConvert.SerializeObject(alignment.Placements) + ")"
                : "alignSelectedShapes(" + JsonConvert.SerializeObject(mode) + ")";
            EvaluateInNativeBrowser("selectElementsById(" + JsonConvert.SerializeObject(ids) + ");window.__mcpAlignmentActive=true;try{" + command + ";}finally{window.__mcpAlignmentActive=false;}");
            if (request.AlignmentExpected.Length == 0)
            {
                // The host independently proved zero requested deltas on the immutable input.
                // Invoke the real editor, but never fabricate a mutation callback for a no-op.
                if (bridge.RequestedCallbacks != 0) throw new InvalidDataException("Native editor emitted a mutation for an independently verified no-op.");
                receipt.NoOp = true;
                File.WriteAllText(Path.Combine(workRoot, "native-layout-policy.json"), EvaluateInNativeBrowser("JSON.stringify(window.__mcpLayoutPolicy)"));
                progress("native_alignment_no_op");
                return receipt;
            }
            progress("native_editor_callback"); var callbackWait = Stopwatch.StartNew();
            int callbackSeconds = request.AtomicStepSeconds;
            while (bridge.RequestedCallbacks == 0)
            {
                if (callbackWait.Elapsed.TotalSeconds > callbackSeconds) throw new InvalidOperationException("No actual requested alignment callback: " + EvaluateInNativeBrowser("JSON.stringify(window.__probeErrors||[])"));
                Thread.Sleep(100);
            }
            try
            {
                // Marshal the actual callback onto this engine's owning execution thread.
                // Never run native model commands on a CEF callback thread.
                receipt.Changes = ApplyAlignmentCallback(model, diagram, persistence, serializer, bridge.PendingValue!, alignment, request.AlignmentExpected, progress);
                receipt.CallbackSha256 = AlignmentHash(System.Text.Encoding.UTF8.GetBytes(bridge.PendingValue!));
                receipt.AutomaticLabelsPreserved = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(Path.Combine(workRoot, "automatic-labels-preserved.json")))!;
                File.WriteAllText(Path.Combine(workRoot, "after-elements.json"), JsonConvert.SerializeObject(Graph(model).Select(Describe)));
                bridge.Complete(null);
            }
            catch (Exception error) { bridge.Complete(error); throw; }
            Wait("native_editor_acknowledgment", "String(window.__mcpLayoutPolicy?.acknowledged===true)");
            File.WriteAllText(Path.Combine(workRoot, "native-layout-policy.json"), EvaluateInNativeBrowser("JSON.stringify(window.__mcpLayoutPolicy)"));
            File.WriteAllText(Path.Combine(workRoot, "editor-after.svg"), EvaluateInNativeBrowser("exportCurrentDiagramToSVG(false,false)"));
            progress("native_alignment_commands_applied");
            return receipt;
        }
        finally
        {
            // Release only this worker's browser on its initialization thread, on success or failure.
            // The host job still owns all native subprocesses if shutdown itself fails.
            ((IDisposable)browser).Dispose();
            Type("CefSharp.Core.dll", "CefSharp.Cef").GetMethod("Shutdown", System.Type.EmptyTypes)!.Invoke(null, null);
        }
    }

    private NativeMutation[] ApplyAlignmentCallback(object model, object diagram, object persistence, object serializer, string payload, NativeAlignmentRequest alignment, NativeMutation[] expected, Action<string> progress)
    {
        if (payload.Length > 16 * 1024 * 1024) throw new InvalidDataException("Native layout callback exceeds the bounded transaction size.");
        using var reader = new JsonTextReader(new StringReader(payload)) { MaxDepth = 64, DateParseHandling = DateParseHandling.None };
        var updates = JArray.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (updates.Count > 1000) throw new InvalidDataException("Native layout callback has too many element changes.");
        var changes = new List<NativeMutation>(expected);
        var affected = new HashSet<string>(alignment.ElementIds.Concat(expected.Select(c => c.ElementId)), StringComparer.Ordinal);
        if (updates.Count == 0) throw new InvalidDataException("Empty native editor callback.");
        var nativeType = Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.ModelerProcessEditor.BaseEditorElement");
        var elements = (IList)New(typeof(List<>).MakeGenericType(nativeType));
        var deserialize = serializer.GetType().GetMethods().Single(m => m.Name == "Deserialize" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
        var known = Graph(model).Where(e => e.DiagramId == Text(diagram, "Id")).ToDictionary(e => Text(e.Value, "Id"));
        var initialIdentities = alignment.SubProcessId == "" ? null : JArray.Parse(File.ReadAllText(Path.Combine(workRoot, "native-editor-initial-identities.json")))
            .ToDictionary(e => (string)e["id"]!, e => (string?)e["participantId"]);
        var automaticLabels = new Dictionary<string, (object Location, object Size)>();
        foreach (var update in updates)
        {
            if (update["changed"]?.Type is not (null or JTokenType.Null)) throw new NotSupportedException("Semantic editor actions are not part of geometry-only alignment.");
            var native = deserialize.MakeGenericMethod(nativeType).Invoke(serializer, new object[] { (string)update["element"]! })!;
            if (!known.ContainsKey(Text(native, "Id"))) throw new InvalidDataException("Native layout callback attempted semantic creation of unknown element " + Text(native, "Id") + ".");
            var original = known[Text(native, "Id")];
            var dto = JObject.Parse((string)update["element"]!);
            var observation = Describe(original);
            if (observation.Kind == "BoundaryEvent" && Text(native, "AttachedToRefId") != observation.Event?.AttachedToActivityId)
                throw new InvalidDataException("Native layout callback changed the boundary attachment.");
            if (observation.Style?.LabelBounds is { } label && label.X == 0 && label.Y == 0 && label.Width == 0 && label.Height == 0)
            {
                object graphics = Get(original.Value, "GraphicalProperties");
                automaticLabels.Add(observation.Id, (Get(graphics, "TextLocation"), Get(graphics, "TextSize")));
            }
            // Alignment is geometry-only. The real editor may detach flows or reassign
            // a participant when a shape crosses a pool boundary; reject that whole result.
            foreach (var endpoint in new[] { new { Name = "sourceRef", Id = observation.SourceId }, new { Name = "targetRef", Id = observation.TargetId } })
                if (observation.Kind is "SequenceFlow" or "MessageFlow" or "Association" && ((string?)dto[endpoint.Name] ?? "") != endpoint.Id)
                    throw new InvalidDataException("Layout callback changed semantic " + endpoint.Name + " for " + observation.Id);
            // Embedded DTOs have zero participant IDs; the frontend maps them to a
            // transient canvas pool. Require that actual pre-command alias to remain
            // unchanged, while the full archive gate checks durable native ownership.
            if (alignment.SubProcessId != "")
            {
                if (!initialIdentities!.TryGetValue(observation.Id, out var participant) || (string?)dto["participantId"] != participant)
                    throw new InvalidDataException("Layout callback changed embedded participant ownership for " + observation.Id);
            }
            string ancestor = alignment.SubProcessId == "" ? original.ParentId : "";
            while (known.TryGetValue(ancestor, out var owner))
            {
                if (owner.Value.GetType().Name == "Participant")
                {
                    if ((string?)dto["participantId"] != ancestor) throw new InvalidDataException("Layout callback changed participant ownership for " + observation.Id);
                    break;
                }
                ancestor = owner.ParentId;
            }
            if (observation.Kind is "SequenceFlow" or "MessageFlow" or "Association")
            {
                if (!affected.Contains(observation.SourceId) && !affected.Contains(observation.TargetId))
                    throw new InvalidDataException("Native layout changed an unrelated connection.");
                changes.Add(new NativeMutation { Operation = "reconnect", ElementId = observation.Id, SourceId = observation.SourceId, TargetId = observation.TargetId,
                    // Read the actual deserialized command properties. The host separately
                    // parses the original callback JSON and compares the complete intent.
                    SourcePort = Text(Get(native, "GraphicalElementProperties"), "SourcePort"),
                    TargetPort = Text(Get(native, "GraphicalElementProperties"), "TargetPort"),
                    Points = ((JArray?)dto["waypoints"] ?? throw new InvalidDataException("Native route callback has no waypoints.")).Select(p => new NativePoint
                    { X = (double)p["x"]!, Y = (double)p["y"]! }).ToArray() });
            }
            else if (!affected.Contains(observation.Id)) throw new InvalidDataException("Native layout changed an unrelated shape.");
            elements.Add(native);
        }
        progress("native_editor_translate_commands");
        var presentation = New(Type("Bizagi.ProcessModeler.UI.dll", "Bizagi.ProcessModeler.UI.Controls.ModelProcessEditor.PresentationModel.DiagramEditorPresentationModel"));
        Set(presentation, "DiagramModel", model); Set(presentation, "EditedDiagram", diagram);
        if (alignment.SubProcessId != "") Set(presentation, "EmbeddedSubProcess", known[alignment.SubProcessId].Value);
        var handler = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.UI.dll", "Bizagi.ProcessModeler.UI.Controls.ModelProcessEditor.Services.ShapeElementServices.IElementEditorHandler"))!;
        var args = Call(handler, "GetUpdateCommands", elements, presentation)!;
        File.WriteAllText(Path.Combine(workRoot, "native-command-types.json"), JsonConvert.SerializeObject(Items(args, "SubCommandEventArgs").Select(c => new { type = c.GetType().FullName, name = Text(c, "CommandName") })));
        var commands = Items(args, "SubCommandEventArgs").ToArray();
        if (commands.Length == 0 || commands.Any(c => !new[] { "ChangeElementLocation", "ChangeElementSize", "ChangeConnector", "ChangeTextLocation" }.Contains(Text(c, "CommandName"))))
            throw new NotSupportedException("Native layout produced an unexpected command set.");
        // Reuse the installed command factory with its actual model, without the GUI
        // SetDiagramModel lifecycle which can insert default simulation/collaboration state.
        var manager = New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.ElementManager"), null, persistence);
        try
        {
            var factory = Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Command.CommandFactory").GetProperty("Instance")!.GetValue(null)!;
            foreach (object commandArgs in commands)
            {
                var command = Call(factory, "Create", commandArgs, model, manager)!;
                progress("native_editor_execute:" + Text(commandArgs, "CommandName"));
                try { if (Call(command, "Execute") is not true) throw new InvalidOperationException("Actual native layout command returned false."); }
                finally { (command as IDisposable)?.Dispose(); }
            }
            // A zero rectangle is native automatic label positioning, not an explicit
            // manual rectangle. Layout commands materialize frontend fallback bounds,
            // including fractional coordinates the native label serializer rounds.
            // Preserve only this exact original automatic state; manual labels are not reset.
            foreach (var pair in automaticLabels)
            {
                object graphics = Get(known[pair.Key].Value, "GraphicalProperties");
                Set(graphics, "TextLocation", pair.Value.Location); Set(graphics, "TextSize", pair.Value.Size);
                progress("native_editor_preserve_automatic_label:" + pair.Key);
            }
            // Use the existing typed native style adapter to retain each selected
            // manual label's original size and node-relative offset. This explicit
            // host-derived intent is independently checked after persistence.
            foreach (var change in expected.Where(e => e.Style?.LabelBounds != null))
            {
                ApplyStyle(known[change.ElementId].Value, change.Style!);
                progress("native_editor_translate_manual_label:" + change.ElementId);
            }
            File.WriteAllText(Path.Combine(workRoot, "automatic-labels-preserved.json"), JsonConvert.SerializeObject(automaticLabels.Keys));
        }
        finally { (manager as IDisposable)?.Dispose(); }
        return changes.ToArray();
    }
}

/// <summary>Native editor bridge. It is internal to the owned worker and never an arbitrary-script MCP tool.</summary>
internal sealed class NativeAlignmentBridge
{
    private readonly string root, data, configuration;
    private readonly Func<object> initial;
    public string Token { get; } = Guid.NewGuid().ToString("N");
    public string Phase { get; set; } = "initialization";
    private int requestedCallbacks;
    private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string? PendingValue { get; private set; }
    public int RequestedCallbacks => Volatile.Read(ref requestedCallbacks);
    public NativeAlignmentBridge(string root, string data, string configuration, Func<object> initial)
    { this.root = root; this.data = data; this.configuration = configuration; this.initial = initial; }
    private void Trace(string method, object? payload = null)
    { lock (this) File.AppendAllText(Path.Combine(root, "actual-cef-callbacks.jsonl"), JsonConvert.SerializeObject(new { phase = Phase, method, payload, at = DateTime.UtcNow }) + "\n"); }
    public string ProbeHandshake() { Trace(nameof(ProbeHandshake)); return Token; }
    public Task<string> AsyncProbeHandshake() { Trace(nameof(AsyncProbeHandshake)); return Task.FromResult(Token); }
    public string GetModelData() { Trace(nameof(GetModelData)); return data; }
    public string GetElementsConfiguration() { Trace(nameof(GetElementsConfiguration)); return configuration; }
    public object GetInitialConfiguration() { Trace(nameof(GetInitialConfiguration)); return initial(); }
    public string GetModelerCurrentCulture() { Trace(nameof(GetModelerCurrentCulture)); return System.Globalization.CultureInfo.CurrentUICulture.Name; }
    public void OnConsole(object sender, object args) { Trace("Console", args.GetType().GetProperty("Message")!.GetValue(args)); }
    public Task UpdateElementShape(string value)
    {
        Trace(nameof(UpdateElementShape), value);
        lock (completion)
        {
            if (Phase != "requested_alignment" || PendingValue != null) throw new NotSupportedException("Unsolicited or repeated native editor callback.");
            PendingValue = value;
            Interlocked.Increment(ref requestedCallbacks);
        }
        return completion.Task;
    }
    public void Complete(Exception? error)
    {
        if (error == null) { Trace("NativeCommandsCompleted"); completion.TrySetResult(true); }
        else { Trace("NativeCommandsFailed", error.ToString()); completion.TrySetException(error); }
    }
}
