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
            // The task refactoring path consumes the native size service that the
            // desktop normally assigns to ElementManager. Resolve the installed
            // service without creating an editor or loading personal preferences.
            if (changes.Any(c => tasks.Contains(c.ExpectedType) && c.TargetType == "CallActivity"))
                Set(manager, "ElementConfigurationManager", Call(injector!, "Resolve",
                    Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.General.IElementConfigurationManager"))!);
            foreach (var change in changes)
            {
                bool toCall = tasks.Contains(change.ExpectedType) && change.TargetType == "CallActivity";
                bool fromCall = change.ExpectedType == "CallActivity" && tasks.Contains(change.TargetType);
                bool eventChange = change.ExpectedEventMode is "Start" or "End" or "Catch" or "Throw" or "Boundary";
                if (!(tasks.Contains(change.ExpectedType) && tasks.Contains(change.TargetType) || gateways.Contains(change.ExpectedType) && gateways.Contains(change.TargetType) || toCall || fromCall || eventChange) || change.ExpectedType == change.TargetType)
                    throw new NotSupportedException("Explicit conversions require different task/gateway types in the same category, or task/unbound-call conversion.");
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
                if (eventChange && DescribeEvent(old)?.Mode != change.ExpectedEventMode)
                    throw new InvalidDataException("Native event mode differs from the explicit conversion expectation.");
                if (fromCall)
                {
                    // Enforce the explicit boundary again inside the worker, not
                    // only in the host's archive preflight.
                    var reference = DescribeCall(old)!;
                    if (reference.CatalogProcessId != "" || reference.BpmnName != "" || reference.BpmnNamespace != "" || reference.External != null)
                        throw new InvalidDataException("Unlink the call explicitly before converting it to a task.");
                    double width = Convert.ToDouble(Get(graphics, "ExpandedWidth")), height = Convert.ToDouble(Get(graphics, "ExpandedHeight"));
                    object defaults = Get(old, "DefaultGraphicalProperties");
                    bool neutral = width == 0 && height == 0 || width == Convert.ToDouble(Get(defaults, "ExpandedWidth")) && height == Convert.ToDouble(Get(defaults, "ExpandedHeight"));
                    if ((bool)Get(graphics, "Expanded") || !neutral)
                        throw new InvalidDataException("Call conversion cannot retire nondefault expanded layout.");
                }
                object converted;
                if (toCall)
                {
                    // Invoke the installed editor command; this converts one root/nested
                    // task to an unbound call. It does not create or choose a process.
                    object args = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.EventArgs.RefactorEventArgs"), "RefactorElements");
                    Set(args, "DiagramId", Guid.Parse(entry.DiagramId));
                    Set(args, "Type", Enum.Parse(args.GetType().GetProperty("Type")!.PropertyType, "TasksToReusableSubProcess"));
                    ((IList)Get(args, "Elements")).Add(old);
                    if (IsNativeSubProcess(parent)) Set(args, "SubProcessId", Guid.Parse(entry.ParentId));
                    object command = New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Command.RefactorElementsCommand"), args, model, manager);
                    progress("native_task_to_call:" + change.ElementId + ":" + change.ExpectedType);
                    if (Call(command, "Execute") is not true) throw new InvalidOperationException("Installed task-to-call refactoring command did not execute.");
                    converted = collection.Cast<object>().Single(e => Text(e, "Id") == change.ElementId);
                    // The menu command resizes the shape. The MCP conversion contract
                    // preserves explicit source geometry; use real native graphics setters.
                    Set(Get(converted, "GraphicalProperties"), "Size", Get(graphics, "Size"));
                }
                else
                {
                    object descriptor = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementDescriptor"),
                        Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementType"), change.TargetType));
                    if (eventChange && change.TargetType.EndsWith("Intermediate", StringComparison.Ordinal))
                        Set(descriptor, "Options", Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementTypeOptions"), "IntermediateEvent" + change.ExpectedEventMode));
                    object args = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.EventArgs.GraphicalElementEventArgs"), "ChangeElementType", Guid.Parse(entry.DiagramId));
                    Set(args, "GraphicalElement", old); Set(args, "ElementType", descriptor);
                    if (IsNativeSubProcess(parent)) Set(args, "SubProcessId", Guid.Parse(entry.ParentId));
                    object command = New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Command.ChangeElementTypeCommand"), args, model, manager);
                    progress("native_convert:" + change.ElementId + ":" + change.ExpectedType + ":" + change.TargetType);
                    if (Call(command, "Execute") is not true) throw new InvalidOperationException("Installed ChangeElementTypeCommand did not execute.");
                    converted = Get(args, "GraphicalElement");
                }
                if (Text(converted, "Id") != change.ElementId || ConversionType(converted) != change.TargetType)
                    throw new InvalidDataException("Native conversion did not preserve identity or produce the requested type.");
                if (eventChange)
                {
                    var oldInfo = DescribeEvent(old)!;
                    if (DescribeEvent(converted)?.Mode != change.ExpectedEventMode) throw new InvalidDataException("Native conversion changed event role.");
                    // Preserve interruption/attachment through native setters; a target
                    // kind that cannot represent these settings must reject the batch.
                    if (oldInfo.Mode is "Start" or "Boundary")
                        ApplyEventProperties(converted, new NativeEventProperties { IsInterrupting = oldInfo.IsInterrupting,
                            AttachedToActivityId = oldInfo.Mode == "Boundary" ? oldInfo.AttachedToActivityId : null },
                            Graph(model).ToDictionary(e => Text(e.Value, "Id"), StringComparer.Ordinal));
                }
                // The native command appends the replacement and CopyValues omits label geometry.
                // Restore only the original ordinal and actual graphics properties, never rewrite archive XML.
                collection.Remove(converted); collection.Insert(index, converted);
                object targetGraphics = Get(converted, "GraphicalProperties");
                foreach (string property in new[] { "TextLocation", "TextSize", "ExpandedSize", "Expanded", "IsHorizontal", "TextAlign", "TextDirection", "TextBackgroundColor" })
                    targetGraphics.GetType().GetProperty(property)!.SetValue(targetGraphics, Optional(graphics, property));
                SynchronizeDataLinks(model, links);
                ResolveCompensationReferences(model);
            }
            ValidateSubProcessContexts(model, changes.Where(c => c.ExpectedEventMode != null).Select(c => new NativeMutation { Operation = "create", ElementId = c.ElementId }).ToArray());
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
