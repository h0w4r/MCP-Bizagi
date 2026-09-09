using System.Collections;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private void PrepareInlineSubProcess(object model, object persistence, NativeSubProcessInlining request, Action<string> progress)
    {
        var graph = Graph(model).ToDictionary(e => Text(e.Value, "Id"), StringComparer.Ordinal);
        if ((bool)Get(Get(model, "ModelInfo"), "IsInCollaboration")) throw new NotSupportedException("Shared enterprise models are outside local inlining.");
        if (!graph.TryGetValue(request.ElementId, out var entry) || entry.Value.GetType().Name != "CallActivity" ||
            !graph.TryGetValue(request.ExpectedProcessId, out var process) || process.Value.GetType().Name != "Process")
            throw new InvalidDataException("Inlining requires an existing native call and local process.");
        var reference = DescribeCall(entry.Value)!;
        if (reference.CatalogProcessId != request.ExpectedProcessId || reference.External != null || reference.BpmnNamespace != "" ||
            reference.BpmnName != "" && reference.BpmnName != request.ExpectedProcessId)
            throw new InvalidDataException("Inlining source binding differs from explicit intent.");
        object original = entry.Value, parent = graph[entry.ParentId].Value;
        if (parent.GetType().Name != "Process" && !IsNativeSubProcess(parent)) throw new InvalidDataException("Invalid native call owner.");
        var collection = (IList)Get(parent, "FlowElements"); int index = collection.IndexOf(original);
        if (index < 0) throw new InvalidDataException("Call absent from its actual owner.");
        object graphics = Get(original, "GraphicalProperties"); var dataLinks = RequiredDataLinks(graph.Values);
        object manager = New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.ElementManager"), null, persistence);
        try
        {
            // Invoke the installed command rather than manufacturing a replacement BPMN object.
            object descriptor = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementDescriptor"),
                Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementType"), "SubProcess"));
            object args = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.EventArgs.GraphicalElementEventArgs"), "ChangeElementType", Guid.Parse(entry.DiagramId));
            Set(args, "GraphicalElement", original); Set(args, "ElementType", descriptor);
            if (IsNativeSubProcess(parent)) Set(args, "SubProcessId", Guid.Parse(entry.ParentId));
            object command = New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Command.ChangeElementTypeCommand"), args, model, manager);
            try
            {
                progress("native_inline_type_conversion:" + request.ElementId);
                if (Call(command, "Execute") is not true) throw new InvalidDataException("Native call-to-subprocess conversion failed.");
                object converted = Get(args, "GraphicalElement");
                if (Text(converted, "Id") != request.ElementId || converted.GetType().Name != "SubProcess") throw new InvalidDataException("Native inlining lost identity or produced the wrong type.");
                // Native CopyFrom omits label properties and appends the replacement. Restore
                // these through the established native setters before durable verification.
                collection.Remove(converted); collection.Insert(index, converted);
                object targetGraphics = Get(converted, "GraphicalProperties");
                foreach (string property in new[] { "TextLocation", "TextSize", "ExpandedSize", "Expanded", "IsHorizontal", "TextAlign", "TextDirection", "TextBackgroundColor" })
                    targetGraphics.GetType().GetProperty(property)!.SetValue(targetGraphics, Optional(graphics, property));
                SynchronizeDataLinks(model, dataLinks); ResolveCompensationReferences(model);
            }
            finally { (command as IDisposable)?.Dispose(); }
        }
        finally { (manager as IDisposable)?.Dispose(); }
    }
}
