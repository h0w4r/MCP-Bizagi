using System.Collections;
using System.Threading;
using System.Xml;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private string[] WhatIf(object model, EngineRequest request, Action<string> progress)
    {
        if (request.SimulationLevel < 1 || request.SimulationLevel > 4 || request.ScenarioIds.Length == 0 || request.ScenarioIds.Distinct().Count() != request.ScenarioIds.Length)
            throw new InvalidDataException("What-if requires distinct explicit scenario IDs and level 1-4.");
        object diagram = Items(model, "Diagrams").Single(d => Text(d, "Id") == request.DiagramId);
        Type scenarioType = Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Simulation.BPSim.Scenario");
        var selected = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(scenarioType))!;
        foreach (string id in request.ScenarioIds) selected.Add(Items(Get(diagram, "BPSimData"), "Scenarios").Single(s => Text(s, "Id") == id));
        object manager = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.Simulation.IWhatIfSimulationManager");
        Type levelType = Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Simulation.BPSim.SimulationLevel");
        Directory.CreateDirectory(request.OutputPath);
        Exception? failure = null;
        var artifacts = new List<string>();
        var repetitions = new HashSet<string>();
        using var finished = new ManualResetEventSlim(false);
        using var completed = Subscribe(manager, "SimulationCompleted", _ => finished.Set());
        using var failed = Subscribe(manager, "SimulationFailed", e => { failure = Optional(e, "Exception") as Exception ?? new InvalidOperationException("Native what-if failed without exception details."); finished.Set(); });
        using var started = Subscribe(manager, "SimulationStarted", _ => progress("native_what_if_started"));
        using var replication = Subscribe(manager, "ScenarioReplicationCompleted", e =>
        {
            try
            {
                string scenario = Text(e, "ScenarioName"), number = Text(e, "ReplicationNumber");
                if (!request.ScenarioIds.Contains(scenario) || !repetitions.Add(scenario + ":" + number))
                    throw new InvalidDataException("Unexpected or duplicate what-if replication event.");
                // Capture each real native result synchronously before the next replication overwrites it.
                string output = Path.Combine(request.OutputPath, "replication-" + repetitions.Count + ".xml");
                CopySimulationXml(Text(e, "ResultsFileName"), output);
                File.WriteAllText(output + ".identity.xml", new System.Xml.Linq.XElement("Replication",
                    new System.Xml.Linq.XAttribute("scenarioId", scenario), new System.Xml.Linq.XAttribute("number", number)).ToString());
                artifacts.Add(output); artifacts.Add(output + ".identity.xml");
                progress("native_what_if_replication:" + scenario + ":" + number);
            }
            catch (Exception ex) { failure = ex; finished.Set(); }
        });
        progress("native_what_if_starting");
        if (!(bool)Call(manager, "RunSimulation", model, diagram, selected, Enum.ToObject(levelType, request.SimulationLevel))!)
            throw new InvalidOperationException("Native what-if could not start.", failure);
        // Host cancellation and measured inactivity supervision apply to the entire owned worker tree.
        finished.Wait();
        if (failure != null) throw new InvalidOperationException("Native what-if failed.", failure);
        int expected = selected.Cast<object>().Sum(s => (bool)Get(Get(s, "ScenarioParameters"), "ReplicationSpecified") ? (int)Get(Get(s, "ScenarioParameters"), "Replication") : 1);
        if (repetitions.Count != expected) throw new InvalidDataException("Native what-if completed without every requested replication.");
        foreach (object scenario in selected)
        {
            int count = (bool)Get(Get(scenario, "ScenarioParameters"), "ReplicationSpecified") ? (int)Get(Get(scenario, "ScenarioParameters"), "Replication") : 1;
            if (repetitions.Count(r => r.StartsWith(Text(scenario, "Id") + ":", StringComparison.Ordinal)) != count)
                throw new InvalidDataException("Native what-if replication distribution did not match the requested scenarios.");
        }
        string input = Path.Combine(request.OutputPath, "WhatIfInput.xml");
        CopySimulationXml(Path.Combine(workRoot, "models", "BPSim", "WhatIfInput.xml"), input); artifacts.Add(input);
        return artifacts.ToArray();
    }

    private static void CopySimulationXml(string source, string target)
    {
        if (!File.Exists(source) || new FileInfo(source).Length == 0) throw new InvalidDataException("Missing native simulation artifact.");
        using (var reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 64 * 1024 * 1024 }))
            while (reader.Read()) { /* Validate actual engine output without external entities. */ }
        File.Copy(source, target, false);
    }
}
