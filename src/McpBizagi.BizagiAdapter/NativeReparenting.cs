using System.Collections;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private void Reparent(object model, object persistence, NativeReparenting[] changes, Action<string> progress)
    {
        var graph = Graph(model).ToDictionary(e => Text(e.Value, "Id"), StringComparer.Ordinal);
        var imageFiles = DescribeImageFiles(model);
        if ((bool)Get(Get(model, "ModelInfo"), "IsInCollaboration")) throw new NotSupportedException("Shared enterprise models are outside local reparenting.");
        // Work with the installed domain collections and actual objects, never cut/paste XML
        // or a clipboard command. Capture the original owners before performing any move.
        foreach (var change in changes)
        {
            var entry = graph[change.ElementId]; var target = graph[change.TargetParentId];
            if (entry.ParentId != change.ExpectedParentId || change.ExpectedDiagramId != null && change.ExpectedDiagramId != entry.DiagramId ||
                target.Value.GetType().Name != "Process" && !IsNativeSubProcess(target.Value))
                throw new InvalidDataException("Native reparenting owner or diagram precondition failed.");
            string property = CollectionFor(target.Value, entry.Value);
            if (property is not ("FlowElements" or "Artifacts")) throw new NotSupportedException("Reparenting requires a native flow element or contained artifact.");
            var sourceCollection = (IList)MutationCollection(graph[entry.ParentId].Value, entry.Value);
            var targetCollection = (IList)Get(target.Value, property);
            if (!sourceCollection.Contains(entry.Value) || targetCollection.Contains(entry.Value)) throw new InvalidDataException("Native collection ownership is ambiguous.");
            sourceCollection.Remove(entry.Value); targetCollection.Add(entry.Value);
            if (change.Position is { } position)
            {
                if ((bool?)Optional(entry.Value, "IsConnector") == true) throw new InvalidDataException("A connector cannot receive node coordinates.");
                Set(Get(entry.Value, "GraphicalProperties"), "X", (float)position.X);
                Set(Get(entry.Value, "GraphicalProperties"), "Y", (float)position.Y);
            }
            progress("native_reparent:" + change.ElementId);
        }
        var resulting = Graph(model).ToDictionary(e => Text(e.Value, "Id"), StringComparer.Ordinal);
        foreach (var change in changes)
        {
            string sourceDiagram = graph[change.ElementId].DiagramId, destination = resulting[change.ElementId].DiagramId;
            if (sourceDiagram != destination && (change.ExpectedDiagramId != sourceDiagram || change.TargetDiagramId != destination) ||
                change.TargetDiagramId != null && change.TargetDiagramId != destination)
                throw new InvalidDataException("Native reparenting diagram expectation does not match final ownership.");
        }
        RelocateReparentedContent(model, persistence, graph, resulting, imageFiles, progress);
        // The same context checks used by native creation apply to the final moved graph.
        // Connector/reference closure is checked independently before dispatch and after restart.
        ValidateSubProcessContexts(model, changes.Select(c => new NativeMutation { Operation = "create", ElementId = c.ElementId }).ToArray());
        ValidateArtifactContainment(model);
        ResolveCompensationReferences(model);
    }
}
