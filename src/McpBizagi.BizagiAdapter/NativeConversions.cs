using System.Collections;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private void ConvertElements(object model, object persistence, NativeTypeConversion[] changes, Action<string> progress)
    {
        string[] tasks = { "AbstractTask", "UserTask", "ManualTask", "ServiceTask", "ScriptTask", "SendTask", "ReceiveTask", "BusinessRuleTask" };
        string[] gateways = { "ExclusiveGateway", "InclusiveGateway", "ParallelGateway", "ComplexGateway", "EventBasedGateway", "EventBasedGatewayExclusive", "EventBasedGatewayParallel" };
        if (changes.Length is < 1 or > 1000 || changes.Select(c => c.ElementId).Distinct().Count() != changes.Length)
            throw new InvalidDataException("Conversions require distinct native identities, 1-1000 items.");
        // The installed command receives its real model directly. Do not SetDiagramModel on the manager:
        // that GUI lifecycle method can insert defaults or initialize collaboration on imported models.
        object manager = New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.ElementManager"), null, persistence);
        try
        {
            foreach (var change in changes)
            {
                if (!(tasks.Contains(change.ExpectedType) && tasks.Contains(change.TargetType) || gateways.Contains(change.ExpectedType) && gateways.Contains(change.TargetType)) || change.ExpectedType == change.TargetType)
                    throw new NotSupportedException("Explicit conversions currently require different task types or different gateway types in the same category.");
                var graph = Graph(model).ToDictionary(e => Text(e.Value, "Id"), StringComparer.Ordinal);
                if (!graph.TryGetValue(change.ElementId, out var entry) || ConversionType(entry.Value) != change.ExpectedType)
                    throw new InvalidDataException("Conversion source identity or expected native type does not match.");
                object old = entry.Value, parent = graph[entry.ParentId].Value;
                if (parent.GetType().Name != "Process" && !IsNativeSubProcess(parent)) throw new InvalidDataException("Conversion requires a native process or embedded subprocess owner.");
                if (Get(parent, "FlowElements") is not IList collection) throw new NotSupportedException("Native flow collection does not expose verified ordinal preservation.");
                int index = collection.IndexOf(old);
                if (index < 0) throw new InvalidDataException("Conversion source is absent from its native owner.");
                var links = RequiredDataLinks(graph.Values);
                object graphics = Get(old, "GraphicalProperties");
                object descriptor = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementDescriptor"),
                    Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementType"), change.TargetType));
                object args = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.EventArgs.GraphicalElementEventArgs"), "ChangeElementType", Guid.Parse(entry.DiagramId));
                Set(args, "GraphicalElement", old); Set(args, "ElementType", descriptor);
                if (IsNativeSubProcess(parent)) Set(args, "SubProcessId", Guid.Parse(entry.ParentId));
                object command = New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Command.ChangeElementTypeCommand"), args, model, manager);
                progress("native_convert:" + change.ElementId + ":" + change.ExpectedType + ":" + change.TargetType);
                if (Call(command, "Execute") is not true) throw new InvalidOperationException("Installed ChangeElementTypeCommand did not execute.");
                object converted = Get(args, "GraphicalElement");
                if (Text(converted, "Id") != change.ElementId || ConversionType(converted) != change.TargetType)
                    throw new InvalidDataException("Native conversion did not preserve identity or produce the requested type.");
                // The native command appends the replacement and CopyValues omits label geometry.
                // Restore only the original ordinal and actual graphics properties, never rewrite archive XML.
                collection.Remove(converted); collection.Insert(index, converted);
                object targetGraphics = Get(converted, "GraphicalProperties");
                foreach (string property in new[] { "TextLocation", "TextSize", "ExpandedSize", "Expanded", "IsHorizontal", "TextAlign", "TextDirection", "TextBackgroundColor" })
                    targetGraphics.GetType().GetProperty(property)!.SetValue(targetGraphics, Optional(graphics, property));
                SynchronizeDataLinks(model, links);
                ResolveCompensationReferences(model);
            }
        }
        finally { if (manager is IDisposable disposable) disposable.Dispose(); }
    }

    private static string ConversionType(object element)
    {
        string type = Text(element, "ElementType");
        if (element.GetType().Name != "EventBasedGateway" || !(bool)Get(element, "Instantiate")) return type;
        return "EventBasedGateway" + Text(element, "EventGatewayType");
    }
}
