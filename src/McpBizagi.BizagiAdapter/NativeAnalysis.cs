using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Xml;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static IEnumerable<object> Items(object value, string property) =>
        (Optional(value, property) as IEnumerable)?.Cast<object>() ?? Enumerable.Empty<object>();

    private static IEnumerable<NativeScenario> Scenarios(object model) => Items(model, "Diagrams").SelectMany(diagram =>
        Items(Get(diagram, "BPSimData"), "Scenarios").Select(scenario => new NativeScenario
        {
            Id = Text(scenario, "Id"), Name = Text(scenario, "Name"), DiagramId = Text(diagram, "Id"),
            ConfiguredElements = Items(scenario, "ElementParameters").Count()
        }));

    private NativeValidationMessage[] ValidateModel(object model, Action<string> progress)
    {
        progress("native_validate_model");
        object validator = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.IBpmnModelValidator");
        return ((IEnumerable)Call(validator, "Validate", model)!).Cast<object>().Select(message => new NativeValidationMessage
        {
            Severity = Text(message, "MessageType"), Description = Text(message, "Description"),
            DiagramId = Optional(message, "Diagram") is object diagram ? Text(diagram, "Id") : "",
            ElementIds = Items(message, "Elements").Select(e => Text(e, "Id")).ToArray()
        }).ToArray();
    }

    // Build typed bridges only for known native events. No reflection invocation is exposed to MCP clients.
    private static IDisposable Subscribe(object source, string eventName, Action<object> callback)
    {
        EventInfo info = source.GetType().GetEvent(eventName) ?? throw new MissingMemberException(eventName);
        var parameters = info.EventHandlerType!.GetMethod("Invoke")!.GetParameters()
            .Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
        var handler = Expression.Lambda(info.EventHandlerType,
            Expression.Invoke(Expression.Constant(callback), Expression.Convert(parameters[1], typeof(object))), parameters).Compile();
        info.AddEventHandler(source, handler);
        return new EventSubscription(() => info.RemoveEventHandler(source, handler));
    }
    private sealed class EventSubscription(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }

    private string[] Simulate(object model, EngineRequest request, Action<string> progress)
    {
        if (request.SimulationLevel < 1 || request.SimulationLevel > 4) throw new ArgumentOutOfRangeException(nameof(request.SimulationLevel));
        object diagram = Items(model, "Diagrams").Single(d => Text(d, "Id") == request.DiagramId);
        object data = Get(diagram, "BPSimData");
        // Empty scenario ID explicitly opts into the vendor's default scenario and initialization.
        object scenario = string.IsNullOrEmpty(request.ScenarioId) ? Call(data, "GetActiveScenario")! :
            Items(data, "Scenarios").Single(s => Text(s, "Id") == request.ScenarioId);
        Call(data, "SetActiveScenario", scenario);
        progress("native_resolve_simulation");
        object manager = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.Simulation.ISimulationManager");
        Type levelType = Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Simulation.BPSim.SimulationLevel");
        Exception? failure = null;
        using var finished = new ManualResetEventSlim(false);
        var elapsed = Stopwatch.StartNew();
        long lastProgress = 0;
        using var completed = Subscribe(manager, "SimulationCompleted", _ => finished.Set());
        using var failed = Subscribe(manager, "SimulationFailed", e => { failure = Optional(e, "Exception") as Exception ?? new InvalidOperationException("Native simulation failed without exception details."); finished.Set(); });
        using var started = Subscribe(manager, "SimulationStarted", _ => progress("native_simulation_started"));
        using var time = Subscribe(manager, "ElapsedTimeChanged", e =>
        {
            long now = elapsed.ElapsedMilliseconds;
            if (now - Interlocked.Read(ref lastProgress) < 1000) return;
            Interlocked.Exchange(ref lastProgress, now);
            progress("native_simulation_time:" + Text(e, "Item"));
        });
        progress("native_simulation_starting");
        Call(manager, "StartSimulation", model, diagram, Enum.ToObject(levelType, request.SimulationLevel), scenario);
        // The host supervises measured inactivity and cancellation by terminating this owned worker.
        // A living simulation is never stopped just because an arbitrary total duration elapsed.
        finished.Wait();
        if (failure != null) throw new InvalidOperationException("Native simulation failed.", failure);
        progress("native_simulation_readback");
        string directory = Path.Combine(workRoot, "models", "BPSim");
        Directory.CreateDirectory(request.OutputPath);
        var artifacts = new List<string>();
        foreach (string name in new[] { "Input.xml", "Results.xml" })
        {
            string source = Path.Combine(directory, name), output = Path.Combine(request.OutputPath, name);
            if (!File.Exists(source) || new FileInfo(source).Length == 0) throw new InvalidDataException("Native simulation did not produce " + name);
            using (var reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                while (reader.Read()) { /* Verify generated XML without enabling external entities. */ }
            File.Copy(source, output, false); artifacts.Add(output);
        }
        return artifacts.ToArray();
    }
}
