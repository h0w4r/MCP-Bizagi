using System.Collections;
using System.Drawing;
using System.Xml;
using McpBizagi.Contracts;
using Newtonsoft.Json;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private NativeSelectionCopyReceipt CopySelection(object model, object persistence, EngineRequest request, Action<string> progress)
    {
        var intent = request.SelectionCopy ?? throw new InvalidDataException("Missing selection copy request.");
        if (intent.ElementIds == null || intent.ElementIds.Length is < 1 or > 1000 || intent.ElementIds.Distinct().Count() != intent.ElementIds.Length || intent.Position == null)
            throw new InvalidDataException("Missing, duplicate or unbounded selection roots.");
        if ((bool)Get(Get(model, "ModelInfo"), "IsInCollaboration")) throw new NotSupportedException("Shared enterprise models are outside local selection copying.");
        var originalGraph = Graph(model).ToDictionary(e => Text(e.Value, "Id"));
        var original = originalGraph.Values.Concat(originalGraph.Values.SelectMany(DataFlowNodes)).Select(Describe).ToDictionary(e => e.Id);
        string originalJson = JsonConvert.SerializeObject(originalGraph.Values.Select(Describe));
        var targetParent = originalGraph[intent.TargetParentId];
        if (targetParent.Value.GetType().Name != "Process" && !IsNativeSubProcess(targetParent.Value)) throw new InvalidDataException("Invalid copy target parent.");
        object target = originalGraph[targetParent.DiagramId].Value;
        object? subprocess = IsNativeSubProcess(targetParent.Value) ? targetParent.Value : null;
        // PasteInPlace mutates its inputs. Use a separately loaded native snapshot, never
        // source object aliases or the operator's system clipboard.
        string snapshotPath = Path.Combine(workRoot, "selection-snapshot.bpm"); File.Copy(request.InputPath, snapshotPath);
        object candidate = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.DiagramModel"));
        Set(candidate, "Path", snapshotPath); object snapshot = Call(persistence, "Load", candidate)!;
        if (ReferenceEquals(snapshot, model)) throw new InvalidDataException("Native snapshot isolation failed.");
        DetachLoadedImages(snapshot, progress); DetachLoadedCustomArtifacts(snapshot, progress);
        ResolveCompensationReferences(snapshot); RestoreNestedDataFlows(snapshot, progress);
        var source = Graph(snapshot).ToDictionary(e => Text(e.Value, "Id"));
        var roots = intent.ElementIds.Select(id => source[id]).ToArray();
        if (roots.Any(e => e.DiagramId != intent.SourceDiagramId)) throw new InvalidDataException("Copy selection crosses source diagrams.");
        var closure = new HashSet<string>(intent.ElementIds); bool added;
        do { added = false; foreach (var entry in source.Values) if (closure.Contains(entry.ParentId)) added |= closure.Add(Text(entry.Value, "Id")); } while (added);
        var args = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.EventArgs.PasteEventArgs"), "PasteElements", Guid.Parse(targetParent.DiagramId));
        if (subprocess != null) Set(args, "SubProcessId", Get(subprocess, "Id"));
        foreach (var root in roots) Call(Get(args, "Elements"), "Add", root.Value);
        Set(args, "PasteInPlace", true); Set(args, "PasteLocation", new PointF((float)intent.Position.X, (float)intent.Position.Y));
        Set(args, "PasteDefaultFormating", false); Set(args, "PasteSwimlanes", false); Set(args, "PasteElements", true);
        Set(args, "DiagramSize", new SizeF(1000, 1000)); Set(args, "AllowedDiagramSize", 36000000f);
        var manager = New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.ElementManager"), null, persistence);
        var factory = Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Command.CommandFactory").GetProperty("Instance")!.GetValue(null)!;
        var command = Call(factory, "Create", args, model, manager)!;
        try
        {
            progress("native_copy_prepare");
            object context = Call(Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.IDiagramContextFactory"), "Create", target, subprocess)!;
            object elements = Get(args, "Elements");
            object cleaner = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Validation.RuntimePropsReferenceCleaner.IDiagramRuntimePropsReferenceCleaner"))!;
            Call(cleaner, "CleanReferences", elements);
            var elementType = elements.GetType().GetGenericArguments()[0];
            Call(elements, "Sort", New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Runtime.GraphicalElementComparer`1").MakeGenericType(elementType)));
            if (Call(command, "PasteInPlace", target, elements, true) is not true) throw new InvalidDataException("Native copy placement was rejected.");
            var map = (IDictionary)New(typeof(Dictionary<,>).MakeGenericType(typeof(Guid), elementType));
            progress("native_copy_clone");
            Call(command, "CloneElements", context, elements, map);
            if (!closure.SetEquals(map.Keys.Cast<Guid>().Select(id => id.ToString()))) throw new InvalidDataException("Native cloner omitted part of the selection.");
            Call(command, "CopyExtendedAttributeValues", Guid.Parse(targetParent.DiagramId), map);
            Call(command, "CopyPresentationActions", Guid.Parse(targetParent.DiagramId), map);
            Call(command, "CreateSubCommands", context, map, elements, subprocess == null ? null : Get(subprocess, "Id"));
            Call(command, "UpdateReferences", map);
            if (Call(command, "Execute") is not true) throw new InvalidDataException("Native copy command failed.");
            // AddElementCommand recreates objects from serialized command data. Corrections
            // must address actual model objects, not the earlier clone-map object instances.
            var current = Graph(model).ToDictionary(e => Text(e.Value, "Id"));
            var ids = (IDictionary)new Dictionary<Guid, Guid>();
            foreach (Guid id in map.Keys) ids.Add(id, Guid.Parse(Text(map[id]!, "Id")));
            var reverse = ids.Keys.Cast<Guid>().ToDictionary(id => ids[id]!.ToString()!, id => id.ToString());
            if (reverse.Keys.Any(id => !current.ContainsKey(id))) throw new InvalidDataException("Native command skipped a mapped element.");
            foreach (string root in intent.ElementIds)
                if (current[ids[Guid.Parse(root)]!.ToString()!].ParentId != intent.TargetParentId) throw new InvalidDataException("Native placement did not honor the explicit target parent.");
            progress("native_copy_preserve_payloads");
            CloneDataFlows(originalGraph.Values.Where(e => closure.Contains(Text(e.Value, "Id"))).ToArray(),
                current.Where(e => reverse.ContainsKey(e.Key)).ToDictionary(e => e.Key, e => e.Value), ids);
            foreach (var image in DescribeImageFilesForDiagram(model, intent.SourceDiagramId).Where(e => ids.Contains(Guid.Parse(e.ElementId))))
            {
                // Preserve the encoded bytes too, not just visually equivalent re-encoding.
                string id = ids[Guid.Parse(image.ElementId)]!.ToString()!;
                string folder = ImageFolder(model, targetParent.DiagramId); Directory.CreateDirectory(folder);
                File.Copy(Path.Combine(ImageFolder(model, intent.SourceDiagramId), image.FileName), Path.Combine(folder, id + Path.GetExtension(image.FileName)), true);
                Set(current[id].Value, "Picture", IndependentPicture((Bitmap)Get(originalGraph[image.ElementId].Value, "Picture")));
            }
            foreach (var pair in reverse)
            {
                object item = current[pair.Key].Value, originalItem = originalGraph[pair.Value].Value;
                if (DescribeEvent(originalItem) != null)
                {
                    object definitions = Get(originalItem, "EventDefinitions"), copies = New(definitions.GetType());
                    foreach (object definition in Items(definitions))
                    {
                        object copy = NativeValueClone(definition)!;
                        if (ReferenceEquals(copy, definition)) throw new InvalidDataException("Event definition cloning reused its source.");
                        if (Text(definition, "EventDefinitionType") == "Compensation" && Optional(definition, "Activity") is object activity)
                        {
                            object mapped = ids[Get(activity, "Id")] ?? throw new InvalidDataException("Compensation target is outside the copy selection.");
                            object targetActivity = current[mapped.ToString()!].Value;
                            SetCompensationTarget(copy, targetActivity); Set(copy, "ActivityRef", new XmlQualifiedName(Text(targetActivity, "Id")));
                            if (Optional(definition, "ActivityCatalogRef") == null) Set(copy, "ActivityCatalogRef", null!);
                        }
                        Call(copies, "Add", copy);
                    }
                    Set(item, "EventDefinitions", copies);
                }
                if (Optional(originalItem, "DefaultSequenceFlow") is object defaultFlow)
                {
                    object mapped = ids[Get(defaultFlow, "Id")] ?? throw new InvalidDataException("Default flow is outside the copy selection.");
                    Set(item, "DefaultSequenceFlow", current[mapped.ToString()!].Value);
                }
                if (item.GetType().Name is "SequenceFlow" or "Association" or "MessageFlow")
                    foreach (string side in new[] { "Source", "Target" })
                    {
                        object endpoint = Optional(item, side) ?? throw new InvalidDataException("Copied connector lost an endpoint.");
                        string id = Text(endpoint, "Id");
                        Set(item, side + "Ref", item.GetType().Name == "SequenceFlow" ? (object)id : new XmlQualifiedName(id));
                    }
                if (item.GetType().Name == "BoundaryEvent") Set(item, "AttachedToRef", new XmlQualifiedName(Text(Get(item, "AttachedToRefActivity"), "Id")));
            }
            var graph = Graph(model).ToArray();
            if (JsonConvert.SerializeObject(graph.Where(e => originalGraph.ContainsKey(Text(e.Value, "Id"))).Select(Describe)) != originalJson)
                throw new InvalidDataException("Native copy modified an original element.");
            var final = graph.Concat(graph.SelectMany(DataFlowNodes)).Select(Describe).ToDictionary(e => e.Id);
            var receipt = new NativeSelectionCopyReceipt { SourceDiagramId = intent.SourceDiagramId, TargetDiagramId = targetParent.DiagramId, TargetParentId = intent.TargetParentId,
                Identities = ids.Keys.Cast<Guid>().Select(id => new NativeCloneIdentity { SourceId = id.ToString(), TargetId = ids[id]!.ToString()!,
                    SourceBpmnId = original[id.ToString()].BpmnId, TargetBpmnId = final[ids[id]!.ToString()!].BpmnId }).ToArray() };
            File.WriteAllText(Path.Combine(workRoot, "native-copy-receipt.json"), JsonConvert.SerializeObject(receipt));
            return receipt;
        }
        finally { (command as IDisposable)?.Dispose(); (manager as IDisposable)?.Dispose(); }
    }
}
