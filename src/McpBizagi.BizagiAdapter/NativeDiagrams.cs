using System.Collections;
using System.Reflection;
using System.Xml;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private object NativeCloner(string name) => Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.BusinessLogic.dll",
        "Bizagi.ProcessModeler.BusinessLogic.Util.Cloning." + name))!;

    private object? DiagramDefault(string method, params object[] args) => Type("Bizagi.ProcessModeler.BusinessLogic.dll",
        "Bizagi.ProcessModeler.BusinessLogic.Command.CommandHelper").GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, args);

    private NativeDiagramSnapshot DiagramState(object model) => new()
    {
        Diagrams = Items(model, "Diagrams").Select(d => new NativeDiagramInfo { Id = Text(d, "Id"), Name = Text(d, "DisplayName") }).ToArray(),
        OpenedItems = Items(Get(model, "UserPreferences"), "OpenedItems").Select(i => new NativeOpenedItem
        { DiagramId = Text(i, "DiagramId"), SubProcessId = Text(i, "SubProcessId"), IsSelected = (bool)Get(i, "IsSelected") }).ToArray(),
        PreferenceEntries = DiagramPreferenceEntries(model)
    };

    private string[] DiagramPreferenceEntries(object model)
    {
        // Derive the exact native write scope. Other users' preferences are never projected away.
        object facade = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.Persistence.IPersistenceUtilFacade");
        var special = Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Common.ModelerSpecialFolder");
        string defaultFolder = (string)Call(facade, "GetFolderPath", model, Enum.Parse(special, "DefaultUser"))!;
        string usersFolder = (string)Call(facade, "GetFolderPath", model, Enum.Parse(special, "Users"))!;
        string user = Text(Get(Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.IUserManager"), "CurrentUser"), "Name");
        if (string.IsNullOrWhiteSpace(user) || user == "." || user == ".." || user.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("Native preference user is not a confined archive folder name.");
        string root = Path.GetFullPath((string)Call(model, "GetTempPath")!).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return new[] { defaultFolder, Path.Combine(usersFolder, user) }.Select(folder =>
        {
            string file = Path.GetFullPath(Path.Combine(folder, "UserPreferences.xml"));
            if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Native preference path escaped the isolated model.");
            return file.Substring(root.Length).Replace('\\', '/');
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private NativeDiagramClone[] EditDiagrams(object model, object persistence, NativeDiagramPatch patch, Action<string> progress)
    {
        var clones = new List<NativeDiagramClone>();
        foreach (var change in patch.Changes)
        {
            progress("native_diagram:" + change.Operation + ":" + change.DiagramId);
            var diagrams = Items(model, "Diagrams").ToArray();
            object? target = diagrams.SingleOrDefault(d => Text(d, "Id") == change.DiagramId);
            if (change.Name != null)
            {
                RequireExportLabel(change.Name);
                if (diagrams.Any(d => (change.Operation != "rename" || d != target) && string.Equals(Text(d, "DisplayName"), change.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Diagram names must be unique ignoring case, to prevent export collisions.");
            }
            switch (change.Operation)
            {
                case "create":
                    if (target != null || Text(model, "Id") == change.DiagramId || Graph(model).Any(e => Text(e.Value, "Id") == change.DiagramId))
                        throw new InvalidDataException("New diagram identity already belongs to the model.");
                    target = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.Collaboration"));
                    Set(target, "Id", Guid.Parse(change.DiagramId)); Set(target, "DisplayName", change.Name!);
                    // Reuse the installed desktop command's domain defaults, without its UI event dispatcher.
                    object main = DiagramDefault("CreateMainParticipant")!;
                    // Persist the same default size the native XPDL reader materializes for the invisible pool.
                    Set(Get(main, "GraphicalProperties"), "Size", Get(Get(main, "DefaultGraphicalProperties"), "Size"));
                    Call(Get(target, "Participants"), "Add", main);
                    DiagramDefault("AddDefaultSimulationScenario", Get(target, "BPSimData"));
                    foreach (object scenario in Items(Get(target, "BPSimData"), "Scenarios"))
                    {
                        object settings = Get(scenario, "ScenarioParameters");
                        // XmlSerializer materializes collection properties during readback, even when empty.
                        Set(settings, "PropertyParameters", New(settings.GetType().GetProperty("PropertyParameters")!.PropertyType));
                    }
                    object pool = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.Participant"));
                    Set(pool, "DisplayName", "Process"); Set(Get(pool, "Process"), "DisplayName", "Process");
                    Call(Get(target, "Participants"), "Add", pool); Call(Get(model, "Diagrams"), "Add", target);
                    ((IDictionary)Get(Get(model, "ExtendedAttributes"), "Values")).Add(Guid.Parse(change.DiagramId), New(DocumentationType("DiagramAttributeValues")));
                    break;
                case "rename":
                    if (target == null) throw new InvalidDataException("Diagram rename target does not exist.");
                    Set(target, "DisplayName", change.Name!); break;
                case "delete":
                    if (target == null || diagrams.Length <= 1) throw new InvalidDataException("Cannot remove a missing or final diagram.");
                    var deletedIds = new HashSet<string>(Graph(model).Where(e => e.DiagramId == change.DiagramId).Select(e => Text(e.Value, "Id")));
                    RequireNoIncomingCalls(Graph(model), deletedIds);
                    if (patch.OpenedItems == null && DiagramState(model).OpenedItems.Any(i => i.DiagramId == change.DiagramId))
                        throw new InvalidDataException("Supply complete OpenedItems to remove references to the deleted diagram.");
                    Call(persistence, "RemoveDiagram", model, target);
                    break;
                case "clone":
                    if (target == null) throw new InvalidDataException("Clone source diagram does not exist.");
                    clones.Add(CloneDiagram(model, target, change.Name!, progress)); break;
                default: throw new NotSupportedException("Unknown native diagram operation.");
            }
        }
        if (patch.OpenedItems != null)
        {
            var graph = Graph(model).ToDictionary(e => Text(e.Value, "Id"));
            object opened = Get(Get(model, "UserPreferences"), "OpenedItems"); Call(opened, "Clear");
            foreach (var item in patch.OpenedItems)
            {
                if (!graph.TryGetValue(item.DiagramId, out var diagram) || diagram.Value.GetType().Name != "Collaboration") throw new InvalidDataException("Opened-item diagram does not exist.");
                if (item.SubProcessId != "" && (!graph.TryGetValue(item.SubProcessId, out var sub) || sub.DiagramId != item.DiagramId || Text(sub.Value, "ElementType") != "SubProcess"))
                    throw new InvalidDataException("Opened subprocess does not belong to this diagram.");
                object native = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.ModelItem"));
                Set(native, "DiagramId", Guid.Parse(item.DiagramId)); Set(native, "IsSelected", item.IsSelected);
                Set(native, "ItemType", Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.ModelItemType"), item.SubProcessId == "" ? "Diagram" : "Subprocess"));
                if (item.SubProcessId != "") Set(native, "SubProcessId", Guid.Parse(item.SubProcessId));
                Call(opened, "Add", native);
            }
        }
        return clones.ToArray();
    }

    private NativeDiagramClone CloneDiagram(object model, object source, string name, Action<string> progress)
    {
        object parameters = New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Util.Cloning.CloneParameters"));
        Set(parameters, "Source", model); Set(parameters, "Target", model);
        var map = (IDictionary)Get(parameters, "Guids"); object attributes = Get(model, "ExtendedAttributes");
        foreach (object definition in Items(attributes, "Definitions")) map.Add(Get(definition, "Id"), Get(definition, "Id"));
        string sourceId = Text(source, "Id"); var originalGraph = Graph(model).Where(e => e.DiagramId == sourceId).ToArray();
        var original = originalGraph.Concat(originalGraph.SelectMany(DataFlowNodes)).Select(Describe).ToArray();
        object sourceSimulation = Get(source, "BPSimData");
        object clone = CloneWithoutSharedDataFlow(source, parameters);
        CloneDataStoreCatalog(source, clone, map, original);
        CloneDataFlows(source, clone, map);
        foreach (var invisible in original.Where(e => e.IsMainParticipant == true))
        {
            // The installed participant cloner resets an invisible pool's size to zero, which
            // the next reader materializes as defaults. Preserve the actual source bounds in
            // the cloned object before persistence, rather than ignoring that archive change.
            string mapped = map[Guid.Parse(invisible.Id)]!.ToString()!;
            object participant = Items(clone, "Participants").Single(p => Text(p, "Id") == mapped);
            var bounds = invisible.Geometry ?? throw new InvalidDataException("Invisible participant is missing native bounds.");
            Set(Get(participant, "GraphicalProperties"), "Size", new System.Drawing.SizeF((float)bounds.Width, (float)bounds.Height));
        }
        // The installed cloner assigns a deep copy to its source. Restore the original source object;
        // retain the independent copy for the target instead of sharing mutable scenario state.
        object clonedSimulation = Get(source, "BPSimData"); Set(source, "BPSimData", sourceSimulation); Set(clone, "BPSimData", clonedSimulation);
        Set(clone, "DisplayName", name); string targetId = Text(clone, "Id");
        var values = (IDictionary)Get(attributes, "Values");
        if (values.Contains(Guid.Parse(sourceId)))
        {
            object sourceValues = values[Guid.Parse(sourceId)]!;
            object vendorValues = Call(NativeCloner("ExtendedAttributes.IDiagramAttributeValuesCloner"), "Clone", sourceValues, Guid.Parse(targetId), parameters)!;
            var byOwner = Items(vendorValues).ToDictionary(v => Text(v, "ElementId"));
            object orderedValues = New(DocumentationType("DiagramAttributeValues"));
            // The vendor cloner omits empty element-value containers. Preserve their explicit presence
            // and original collection order; rich values and attached files still use the native cloner.
            foreach (object entry in Items(sourceValues))
            {
                object targetOwner = map[Get(entry, "ElementId")] ?? throw new InvalidDataException("Attribute owner is absent from the native clone map.");
                object copy;
                if (!Items(Get(entry, "Values")).Any()) copy = New(DocumentationType("ElementAttributeValues"), targetOwner);
                else if (!byOwner.TryGetValue(targetOwner.ToString()!, out copy)) throw new InvalidDataException("Native clone omitted nonempty element values.");
                Call(orderedValues, "Add", copy);
            }
            values.Add(Guid.Parse(targetId), orderedValues);
        }
        var actions = (IDictionary)Get(model, "PresentationActions");
        if (actions.Contains(Guid.Parse(sourceId))) actions.Add(Guid.Parse(targetId),
            Call(NativeCloner("PresentationActions.IDiagramActionsCloner"), "Clone", Guid.Parse(sourceId), actions[Guid.Parse(sourceId)], parameters));
        Call(Get(model, "Diagrams"), "Add", clone);
        CloneImageFiles(model, sourceId, targetId, map);
        // The low-level collaboration cloner does not remap called-process links. Invoke the
        // installed recursive updater with its real identity map, not a string replacement pass.
        Call(NativeCloner("CallActivity.IRerefenceUpdater"), "Update", model, clone, parameters);
        RemapCompensationTargets(model, sourceId, targetId, map);
        var clonedGraph = Graph(model).Where(e => e.DiagramId == targetId).ToArray();
        var cloned = clonedGraph.Concat(clonedGraph.SelectMany(DataFlowNodes)).Select(Describe).ToDictionary(e => e.Id);
        var identities = original.Select(e =>
        {
            string id = map[Guid.Parse(e.Id)]?.ToString() ?? throw new InvalidDataException("Native clone omitted an identity mapping for " + e.Kind + " " + e.Id + ".");
            if (!cloned.TryGetValue(id, out var copy)) throw new InvalidDataException("Mapped native clone identity is absent.");
            return new NativeCloneIdentity { SourceId = e.Id, TargetId = id, SourceBpmnId = e.BpmnId, TargetBpmnId = copy.BpmnId };
        }).ToArray();
        var references = identities.Where(i => i.SourceBpmnId != "").GroupBy(i => i.SourceBpmnId).ToDictionary(g => g.Key, g => g.Select(i => i.TargetBpmnId).Distinct().Single());
        foreach (var scenario in Items(clonedSimulation, "Scenarios"))
            foreach (var element in Items(scenario, "ElementParameters"))
                if (references.TryGetValue(Text(element, "ElementRef"), out string reference)) Set(element, "ElementRef", reference);
        progress("native_diagram_cloned:" + targetId);
        return new NativeDiagramClone { SourceId = sourceId, TargetId = targetId, Identities = identities };
    }
}
