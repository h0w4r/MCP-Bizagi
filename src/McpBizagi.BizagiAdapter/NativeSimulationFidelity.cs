using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static NativeSimulationLimitation[] SimulationLimitations(object model, string diagramId) => CallSimulationLimitations(model, diagramId)
        .Concat(Graph(model).Where(e => e.DiagramId == diagramId && IsNativeActivity(e.Value))
            .Select(e => new { Entry = e, Properties = DescribeActivity(e.Value)! })
            .Where(e => e.Properties.StartQuantity != 1 || e.Properties.CompletionQuantity != 1)
            .Select(e => new NativeSimulationLimitation
            {
                Code = "nondefault_token_quantities_unaccredited", DiagramId = diagramId, ElementId = Text(e.Entry.Value, "Id"), BpmnId = Text(e.Entry.Value, "BpmnId"),
                ActivityProperties = e.Properties,
                Message = "Nondefault activity token quantities are preserved in the native model and checked against the actual simulation input. Their execution semantics are not accredited for this engine; inspect actual result metrics instead of inferring completion counts from these settings.",
                DocumentationUrl = "https://help.bizagi.com/platform/en/simulation_in_bizagi.htm"
            })).Concat(Graph(model).Where(e => e.DiagramId == diagramId && IsNativeActivity(e.Value) && Text(e.Value, "LoopType") != "None")
            .Select(e => new NativeSimulationLimitation
            {
                Code = Text(e.Value, "LoopType") == "MultiInstance" ? "multi_instance_simulation_unsupported" : "standard_loop_simulation_unaccredited",
                DiagramId = diagramId, ElementId = Text(e.Value, "Id"), BpmnId = Text(e.Value, "BpmnId"), ActivityLoop = DescribeLoop(e.Value),
                Message = Text(e.Value, "LoopType") == "MultiInstance"
                    ? "Bizagi documents multi-instance task/subprocess simulation as unsupported. Persisted loop metadata does not accredit iteration behavior."
                    : "Standard-loop editing/persistence is separate from simulation behavior; this server does not evaluate loop expressions or accredit native iteration behavior.",
                DocumentationUrl = "https://help.bizagi.com/platform/en/simulation_in_bizagi.htm"
            })).ToArray();

    private static NativeSimulationActivityInput[] ExpectedSimulationActivities(object model, string diagramId) => Graph(model)
        .Where(e => e.DiagramId == diagramId && IsNativeActivity(e.Value)).Select(e => new NativeSimulationActivityInput
        {
            ElementId = Text(e.Value, "Id"), BpmnId = Text(e.Value, "BpmnId"),
            StartQuantity = (int)Get(e.Value, "StartQuantity"), CompletionQuantity = (int)Get(e.Value, "CompletionQuantity")
        }).ToArray();

    private static NativeSimulationInputReadback[] VerifySimulationInputs(string[] artifacts, NativeSimulationActivityInput[] expected)
    {
        XNamespace ns = "http://www.omg.org/spec/BPMN/20100524/MODEL";
        var files = artifacts.Where(p => Path.GetFileName(p) is "Input.xml" or "WhatIfInput.xml").ToArray();
        if (files.Length != 1) throw new InvalidDataException("Expected exactly one actual native simulation input artifact.");
        using var reader = XmlReader.Create(files[0], new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 64 * 1024 * 1024 });
        var input = XDocument.Load(reader);
        foreach (var activity in expected)
        {
            var matches = input.Descendants().Where(e => e.Name.Namespace == ns && (string?)e.Attribute("id") == activity.BpmnId).ToArray();
            if (matches.Length != 1 || ((int?)matches[0].Attribute("startQuantity") ?? 1) != activity.StartQuantity ||
                ((int?)matches[0].Attribute("completionQuantity") ?? 1) != activity.CompletionQuantity)
                throw new InvalidDataException("Native activity identity or quantities were lost before simulation: " + activity.ElementId);
        }
        // Evidence of input fidelity is deliberately separate from evidence of simulator behavior.
        return new[] { new NativeSimulationInputReadback { Artifact = files[0], Activities = expected } };
    }
}
