using System.Collections;
using System.Security.Cryptography;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private object DiagramActions(object model, string diagram) => Call(Get(model, "PresentationActions"), "GetDiagramActions", Guid.Parse(diagram))!;
    private string ActionFolder(object model, string diagram)
    {
        object facade = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.Persistence.IPersistenceUtilFacade");
        object special = Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Common.ModelerSpecialFolder"), "Actions");
        string folder = Path.GetFullPath((string)Call(facade, "GetFolderPath", model, Guid.Parse(diagram), special)!);
        if (!folder.StartsWith(Path.Combine(workRoot, "models") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Presentation action folder escaped native scratch storage.");
        return folder;
    }

    private NativePresentationSnapshot Presentation(object model)
    {
        var actions = new List<NativePresentationAction>(); var files = new List<NativeAttachmentInfo>();
        foreach (object diagram in Items(model, "Diagrams"))
        {
            string id = Text(diagram, "Id");
            foreach (object item in Items(DiagramActions(model, id)))
            {
                var action = new NativePresentationAction { DiagramId = id, ElementId = Text(item, "ElementId"), Type = Text(item, "Type"),
                    TypeValue = Text(item, "TypeValue"), ExtendedAttributeId = Text(item, "ExtendedAttributeId"),
                    DisplayName = Text(item, "DisplayName"), Content = Text(item, "Content") };
                if (action.TypeValue == "Normal" && (action.Type == "File" || action.Type == "Image") && action.Content != "")
                {
                    string name = Path.GetFileName(action.Content), path = Path.GetFullPath(Path.Combine(ActionFolder(model, id), name));
                    if (!string.Equals(path, Path.GetFullPath(action.Content), StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                        throw new InvalidDataException("Native presentation payload is missing or outside its owned folder.");
                    using var stream = File.OpenRead(path); using var hash = SHA256.Create();
                    files.Add(new NativeAttachmentInfo { DiagramId = id, ElementId = action.ElementId, FileName = name, Length = stream.Length,
                        Sha256 = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() });
                    action.Content = "action-file:" + name;
                }
                if (action.TypeValue == "ExtendedAttribute" && action.ExtendedAttributeId != "" && action.Content != "")
                {
                    var definition = Items(Get(Get(model, "ExtendedAttributes"), "Definitions")).SingleOrDefault(d => Text(d, "Id") == action.ExtendedAttributeId);
                    if (definition != null && (Text(definition, "AttributeType") == "Image" || Text(definition, "AttributeType") == "FileEmbedded"))
                        action.Content = "attachment:" + Path.GetFileName(action.Content);
                }
                actions.Add(action);
            }
        }
        return new NativePresentationSnapshot { Actions = actions.ToArray(), Files = files.ToArray() };
    }

    private void EditPresentation(object model, NativePresentationActionChange[] changes, Action<string> progress)
    {
        var graph = Graph(model).ToDictionary(e => Text(e.Value, "Id"));
        foreach (var change in changes)
        {
            var a = change.Action;
            if (!graph.TryGetValue(a.ElementId, out var owner) || owner.DiagramId != a.DiagramId ||
                !Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.GraphicalElementBase").IsInstanceOfType(owner.Value))
                throw new InvalidDataException("Presentation owner must be an existing native graphical element in its explicit diagram.");
            var collection = (IList)DiagramActions(model, a.DiagramId);
            object? old = Items(collection).SingleOrDefault(x => Text(x, "ElementId") == a.ElementId);
            string? oldFile = old != null && Text(old, "TypeValue") == "Normal" && (Text(old, "Type") == "File" || Text(old, "Type") == "Image")
                ? Path.GetFileName(Text(old, "Content")) : null;
            int index = old == null ? collection.Count : collection.IndexOf(old);
            if (change.Operation == "delete")
            {
                if (old == null) throw new InvalidDataException("Presentation action to delete is absent.");
                collection.Remove(old);
            }
            else
            {
                object value = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Actions.PresentationAction"));
                Set(value, "ElementId", Guid.Parse(a.ElementId)); Set(value, "DisplayName", a.DisplayName);
                Set(value, "Type", Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.ActionType"), a.Type));
                Set(value, "TypeValue", Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.ActionTypeValue"), a.TypeValue));
                Set(value, "Content", a.Content);
                if (a.ExtendedAttributeId != "") Set(value, "ExtendedAttributeId", Guid.Parse(a.ExtendedAttributeId));
                if (a.TypeValue == "Normal" && (a.Type == "File" || a.Type == "Image"))
                {
                    string name = a.Content.Substring("action-file:".Length), folder = ActionFolder(model, a.DiagramId);
                    Directory.CreateDirectory(folder); byte[] bytes = Convert.FromBase64String(change.DataBase64);
                    if (a.Type == "Image") { using var input = new MemoryStream(bytes); using var image = System.Drawing.Image.FromStream(input, false, true); CheckImageSize(image); }
                    File.WriteAllBytes(Path.Combine(folder, name), bytes); Set(value, "Content", Path.Combine(folder, name));
                }
                if (a.TypeValue != "Normal")
                {
                    // Invoke only the installed selected-action resolver. Do not
                    // refresh unrelated caches or activate any external target.
                    object util = Call(injector!, "Resolve", Type("Bizagi.ProcessModeler.Persistence.dll", "Bizagi.ProcessModeler.Persistence.Util.IPresentationActionsContentUtil"))!;
                    Call(util, a.TypeValue == "Description" ? "UpdateDescriptionPresentationActionContent" : "UpdateExtendedAttributePresentationActionContent",
                        model, Guid.Parse(a.DiagramId), value);
                }
                if (old == null) collection.Add(value); else collection[index] = value;
            }
            if (!string.IsNullOrEmpty(oldFile) && !Items(collection).Any(x => Text(x, "TypeValue") == "Normal" &&
                (Text(x, "Type") == "File" || Text(x, "Type") == "Image") && string.Equals(Path.GetFileName(Text(x, "Content")), oldFile, StringComparison.OrdinalIgnoreCase)))
                File.Delete(Path.Combine(ActionFolder(model, a.DiagramId), oldFile));
            progress("native_presentation_action:" + change.Operation + ":" + a.ElementId);
        }
    }
}
