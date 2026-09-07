using System.Collections;
using System.Diagnostics;
using System.Reflection;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

/// <summary>
/// Experimental 4.3 adapter. All proprietary code stays in the user's installation.
/// Registration does not initialize the desktop application or any CefSharp window.
/// </summary>
public sealed class NativeEngine
{
    private readonly string installation;
    private readonly string workRoot;
    private object? injector;
    private readonly Dictionary<string, Assembly> assemblies = new(StringComparer.OrdinalIgnoreCase);
    public string Version { get; }

    public NativeEngine(string installation, string workRoot)
    {
        this.installation = Path.GetFullPath(installation);
        this.workRoot = Path.GetFullPath(workRoot);
        Version = FileVersionInfo.GetVersionInfo(Path.Combine(this.installation, "BizagiModeler.exe")).FileVersion ?? "unknown";
        if (Version != "4.3.0.008")
            throw new NotSupportedException("Native adapter has not been investigated for this engine version: " + Version);
        // This hook resolves only dependencies within the selected local installation.
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
    }

    private Assembly? ResolveAssembly(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name!;
        if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)) return null;
        string file = Path.Combine(installation, name + ".dll");
        return File.Exists(file) ? System.Reflection.Assembly.LoadFrom(file) : null;
    }

    private Assembly Assembly(string name)
    {
        if (!assemblies.TryGetValue(name, out var value))
            assemblies[name] = value = System.Reflection.Assembly.LoadFrom(Path.Combine(installation, name));
        return value;
    }

    private Type Type(string assembly, string type) => Assembly(assembly).GetType(type, true)!;
    private static object New(Type type, params object?[] args) => Activator.CreateInstance(type,
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, args, null)!;

    private static object? Call(object instance, string name, params object?[] args)
    {
        var candidates = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name == name && !m.ContainsGenericParameters && m.GetParameters().Length == args.Length)
            .Where(m => m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x))
            .ToArray();
        if (candidates.Length != 1) throw new MissingMethodException(instance.GetType().FullName, name + " (unambiguous contract required)");
        return candidates[0].Invoke(instance, args);
    }

    private static object Get(object instance, string name) => instance.GetType().GetProperty(name)!.GetValue(instance)!;
    private static void Set(object instance, string name, object value) => instance.GetType().GetProperty(name)!.SetValue(instance, value);
    private object Resolve(string interfaceName) => Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.BusinessEntities.dll", interfaceName))!;

    public void Initialize(Action<string> progress)
    {
        if (injector != null) return;
        Directory.CreateDirectory(workRoot);
        progress("native_registration");
        // Use the manufacturer's module registration, but deliberately do not build or initialize its GUI application.
        object configuration = New(Type("BizAgi.DA.dll", "Bizagi.DA.CConfiguration"));
        object builder = New(Type("BizagiModeler.exe", "BizagiProcessModeler.ProcessModelerApplicationBuilder"), configuration);
        object candidate = New(Type("Bizagi.DependencyInjector.dll", "Bizagi.DependencyInjector.SimpleInjectorImplementation"));
        foreach (object module in (IEnumerable)Call(builder, "GetApplicationModules")!)
        {
            progress("register:" + module.GetType().Name);
            Call(module, "Configure", candidate);
        }
        object locator = Type("Bizagi.Injection.dll", "Bizagi.Injection.ServiceLocator.ServiceLocatorInstance")
            .GetProperty("Instance")!.GetValue(null)!;
        Set(locator, "Injector", candidate);
        injector = candidate;
        // Override only the native model scratch directory, never the operator's profile.
        Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.DiagramModel")
            .GetField("TEMP_PATH")!.SetValue(null, Path.Combine(workRoot, "models") + Path.DirectorySeparatorChar);
        progress("native_registered");
    }

    public EngineReply Execute(EngineRequest request, Action<string> progress)
    {
        if (request.ProtocolVersion != 1) throw new NotSupportedException("Unsupported worker protocol version.");
        RequireExportLabel(request.ModelName);
        Initialize(progress);
        object model = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.DiagramModel"));
        Set(model, "Name", request.ModelName);
        var reply = new EngineReply { OperationId = request.OperationId, EngineVersion = Version };
        if (request.Action == "probe")
        {
            progress("native_resolve_importer");
            Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.IBpmnInteropManager");
            progress("native_resolve_persistence");
            Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.File.IFileSystemPersistenceManager");
            reply.Success = true;
            reply.Code = "services_resolved_not_accredited";
            reply.Message = "Native services resolved. No file operation has been accredited by this probe.";
            return reply;
        }
        if (request.Action != "import_save" && request.Action != "read_export" && request.Action != "edit_save")
            throw new NotSupportedException("Unknown native operation.");
        progress("native_resolve_persistence");
        object persistence = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.File.IFileSystemPersistenceManager");
        if (request.Action == "import_save")
        {
            progress("native_import_bpmn");
            object interop = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.IBpmnInteropManager");
            foreach (string input in request.InputPaths.Length > 0 ? request.InputPaths : new[] { request.InputPath })
            {
                object diagram = Call(interop, "Import", input, model)!;
                Call(Get(model, "Diagrams"), "Add", diagram);
            }
            Set(model, "Path", request.OutputPath);
            progress("native_persist_bpm");
            Call(persistence, "Persist", model);
            if (!File.Exists(request.OutputPath) || new FileInfo(request.OutputPath).Length == 0)
                throw new IOException("Native persistence returned without producing a model.");
            reply.Artifacts = new[] { request.OutputPath };
        }
        else
        {
            Set(model, "Path", request.InputPath);
            progress("native_load_bpm");
            model = Call(persistence, "Load", model)!;
            if (request.Action == "edit_save")
            {
                progress("native_edit_names");
                var indexed = Elements(model).ToLookup(e => Get(e, "Id").ToString());
                // Validate the entire batch before mutating any native object.
                foreach (var change in request.Changes)
                    if (indexed[change.ElementId].Count() != 1) throw new InvalidDataException("Expected one native element: " + change.ElementId);
                foreach (var change in request.Changes) Set(indexed[change.ElementId].Single(), "DisplayName", change.Name);
                foreach (object diagram in (IEnumerable)Get(model, "Diagrams")) Set(diagram, "HasChanged", true);
                Set(model, "Path", request.OutputPath);
                progress("native_persist_edited_bpm");
                Call(persistence, "Persist", model);
                if (!File.Exists(request.OutputPath) || new FileInfo(request.OutputPath).Length == 0)
                    throw new IOException("Native editing returned without a persisted model.");
                reply.Artifacts = new[] { request.OutputPath };
            }
            else
            {
                object interop = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.IBpmnInteropManager");
                // Fail explicitly on unsafe output labels rather than silently renaming or escaping the operation directory.
                RequireExportLabel(Get(model, "Name").ToString()!);
                foreach (object diagram in (IEnumerable)Get(model, "Diagrams")) RequireExportLabel(Get(diagram, "DisplayName").ToString()!);
                Directory.CreateDirectory(request.OutputPath);
                progress("native_export_bpmn");
                Call(interop, "Export", model, request.OutputPath);
                reply.Artifacts = Directory.GetFiles(request.OutputPath, "*.bpmn", SearchOption.AllDirectories);
                if (reply.Artifacts.Length == 0) throw new IOException("Native export produced no BPMN files.");
            }
        }
        reply.Diagrams = ((IEnumerable)Get(model, "Diagrams")).Cast<object>()
            .Select(d => Get(d, "DisplayName")?.ToString() ?? "").ToArray();
        reply.Elements = Elements(model).Select(e => new NativeElement { Id = Get(e, "Id").ToString()!,
            Kind = e.GetType().Name, Name = Get(e, "DisplayName")?.ToString() ?? "" }).ToArray();
        reply.Success = true;
        reply.Code = "native_operation_completed";
        reply.Message = "Native operation completed; verify artifacts in a fresh worker before accreditation.";
        return reply;
    }

    private static void RequireExportLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "." || value == ".." || value.Length > 120 ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.EndsWith(".") || value.EndsWith(" "))
            throw new InvalidDataException("Native model and diagram export labels must be nonempty Windows file names (maximum 120 characters).");
        string stem = value.Split('.')[0].ToUpperInvariant();
        if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem))
            throw new InvalidDataException("Reserved Windows export label.");
    }

    private static IEnumerable<object> Elements(object model)
    {
        foreach (object diagram in (IEnumerable)Get(model, "Diagrams"))
            foreach (object participant in (IEnumerable)Get(diagram, "Participants"))
                foreach (object element in FlowElements(Get(participant, "Process"))) yield return element;
    }

    private static IEnumerable<object> FlowElements(object container)
    {
        foreach (object element in (IEnumerable)Get(container, "FlowElements"))
        {
            yield return element;
            // BPMN sub-processes expose their own collection; walk it instead of flattening it away.
            if (element.GetType().GetProperty("FlowElements") != null)
                foreach (object child in FlowElements(element)) yield return child;
        }
    }
}
