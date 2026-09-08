using System.Collections;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private NativeExtractionReceipt ExtractSubProcess(object model, object persistence, NativeSubProcessExtraction request, Action<string> progress)
    {
        RequireExportLabel(request.NewDiagramName);
        var original = Graph(model).ToDictionary(e => Text(e.Value, "Id"), StringComparer.Ordinal);
        if (!original.TryGetValue(request.ElementId, out var entry) || entry.Value.GetType().Name != "SubProcess")
            throw new InvalidDataException("Extraction requires an existing ordinary embedded SubProcess.");
        if (Items(model, "Diagrams").Any(d => string.Equals(Text(d, "DisplayName"), request.NewDiagramName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Extraction diagram name already exists.");
        if ((bool)Get(Get(model, "ModelInfo"), "IsInCollaboration")) throw new NotSupportedException("Shared enterprise models are outside the local extraction contract.");
        var moved = new HashSet<string>(StringComparer.Ordinal);
        bool Inside(GraphEntry candidate)
        {
            string parent = candidate.ParentId;
            while (parent != "")
            {
                if (parent == request.ElementId) return true;
                parent = original.TryGetValue(parent, out var owner) ? owner.ParentId : "";
            }
            return false;
        }
        foreach (var child in original.Values.Where(Inside)) moved.Add(Text(child.Value, "Id"));
        object old = entry.Value, parentObject = original[entry.ParentId].Value;
        var ownerCollection = (IList)Get(parentObject, "FlowElements"); int ordinal = ownerCollection.IndexOf(old);
        if (ordinal < 0) throw new InvalidDataException("Subprocess is absent from its actual native owner.");
        var rootFlows = Items(old, "FlowElements").ToArray(); var rootArtifacts = Items(old, "Artifacts").ToArray();
        object oldGraphics = Get(old, "GraphicalProperties");
        var diagramIds = new HashSet<string>(Items(model, "Diagrams").Select(d => Text(d, "Id")));
        // The installed command moves only top-level image files. Capture every nested image before
        // calling it, so completed native moves and missing nested moves are handled byte-exactly.
        var pictures = DescribeImageFiles(model).Where(f => f.DiagramId == entry.DiagramId && moved.Contains(f.ElementId))
            .ToDictionary(f => f.FileName, f => File.ReadAllBytes(Path.Combine(ImageFolder(model, entry.DiagramId), f.FileName)), StringComparer.OrdinalIgnoreCase);
        object manager = New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.ElementManager"), null, persistence);
        try
        {
            object args = New(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.EventArgs.RefactorEventArgs"), "RefactorElements");
            Set(args, "DiagramId", Guid.Parse(entry.DiagramId));
            Set(args, "Type", Enum.Parse(args.GetType().GetProperty("Type")!.PropertyType, "EmbeddedToReusableSubProcess"));
            ((IList)Get(args, "Elements")).Add(old);
            if (IsNativeSubProcess(parentObject)) Set(args, "SubProcessId", Guid.Parse(entry.ParentId));
            object command = New(Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.Command.RefactorElementsCommand"), args, model, manager);
            progress("native_extract_subprocess:" + request.ElementId);
            if (Call(command, "Execute") is not true) throw new InvalidOperationException("Installed RefactorElementsCommand did not execute.");
        }
        finally { if (manager is IDisposable disposable) disposable.Dispose(); }

        object target = Items(model, "Diagrams").Single(d => !diagramIds.Contains(Text(d, "Id")));
        string targetId = Text(target, "Id"); Set(target, "DisplayName", request.NewDiagramName);
        object participant = Items(target, "Participants").Single(), process = Get(participant, "Process");
        InitializeNewStyle(participant);
        Set(Get(participant, "GraphicalProperties"), "Size", Get(Get(participant, "DefaultGraphicalProperties"), "Size"));
        // Preserve collection order, source shape geometry and label fields omitted by the native command.
        foreach (var pair in new[] { (Name: "FlowElements", Values: rootFlows), (Name: "Artifacts", Values: rootArtifacts) })
        {
            var collection = (IList)Get(process, pair.Name);
            if (collection.Count != pair.Values.Length || pair.Values.Any(v => !collection.Contains(v))) throw new InvalidDataException("Native extraction changed the immediate content set.");
            collection.Clear(); foreach (object value in pair.Values) collection.Add(value);
        }
        object call = Items(ownerCollection).Single(e => Text(e, "Id") == request.ElementId);
        if (call.GetType().Name != "CallActivity") throw new InvalidDataException("Extraction did not produce a native call activity.");
        // The installed serializer materializes this legacy QName on reload. Set the same native
        // representation now, rather than exempting an unexplained restart change from verification.
        Set(call, "CalledElement", new System.Xml.XmlQualifiedName(Text(process, "Id")));
        ownerCollection.Remove(call); ownerCollection.Insert(ordinal, call);
        object graphics = Get(call, "GraphicalProperties");
        foreach (string property in new[] { "TextLocation", "TextSize", "ExpandedSize", "Expanded", "IsHorizontal", "TextAlign", "TextDirection", "TextBackgroundColor" })
            graphics.GetType().GetProperty(property)!.SetValue(graphics, Optional(oldGraphics, property));

        foreach (var picture in pictures)
        {
            string sourceFile = Path.Combine(ImageFolder(model, entry.DiagramId), picture.Key), targetFile = Path.Combine(ImageFolder(model, targetId), picture.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            if (File.Exists(targetFile) && !File.ReadAllBytes(targetFile).SequenceEqual(picture.Value)) throw new InvalidDataException("Native image move changed the captured bytes.");
            if (!File.Exists(targetFile)) File.WriteAllBytes(targetFile, picture.Value);
            if (File.Exists(sourceFile)) File.Delete(sourceFile);
        }
        // Relocate native value objects, not lossy DTO reconstructions; linked files are never followed.
        var values = (IDictionary)Get(Get(model, "ExtendedAttributes"), "Values");
        object targetValues = New(DocumentationType("DiagramAttributeValues")); values.Add(Guid.Parse(targetId), targetValues);
        if (values.Contains(Guid.Parse(entry.DiagramId)))
        {
            var sourceValues = (IList)values[Guid.Parse(entry.DiagramId)]!;
            foreach (object value in Items(sourceValues).Where(v => moved.Contains(Text(v, "ElementId"))).ToArray())
            {
                string elementId = Text(value, "ElementId"), sourceFolder = AttachmentFolder(model, entry.DiagramId, elementId), targetFolder = AttachmentFolder(model, targetId, elementId);
                foreach (object attribute in AttributeValues(value))
                {
                    if (Text(attribute, "AttributeType") is not ("FileEmbedded" or "Image") || Text(attribute, "Content") == "") continue;
                    string sourcePath = Path.GetFullPath(Text(attribute, "Content"));
                    if (!string.Equals(Path.GetDirectoryName(sourcePath), sourceFolder, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Embedded value path does not belong to the moved native owner.");
                    Set(attribute, "Content", Path.Combine(targetFolder, Path.GetFileName(sourcePath)));
                }
                sourceValues.Remove(value); ((IList)targetValues).Add(value);
            }
        }
        foreach (string elementId in moved)
        {
            string sourceFolder = AttachmentFolder(model, entry.DiagramId, elementId), targetFolder = AttachmentFolder(model, targetId, elementId);
            // Both resolved absolute targets were confined by AttachmentFolder to the private model.
            // Move the complete owner folder so unrepresented nested sidecars are not discarded.
            if (Directory.Exists(sourceFolder))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetFolder)!);
                if (Directory.Exists(targetFolder)) throw new IOException("Extraction attachment destination already exists.");
                Directory.Move(sourceFolder, targetFolder);
            }
        }
        ResolveCompensationReferences(model);
        var opened = DiagramState(model).OpenedItems;
        if (opened.Any(i => i.DiagramId == entry.DiagramId && (i.SubProcessId == request.ElementId || moved.Contains(i.SubProcessId))))
        {
            // Update persisted current-user tab references without opening a GUI or changing selection order.
            foreach (var item in opened.Where(i => i.DiagramId == entry.DiagramId && (i.SubProcessId == request.ElementId || moved.Contains(i.SubProcessId))))
            {
                item.DiagramId = targetId;
                if (item.SubProcessId == request.ElementId) item.SubProcessId = "";
            }
            EditDiagrams(model, persistence, new NativeDiagramPatch { OpenedItems = opened }, progress);
        }
        progress("native_extract_content_relocated:" + moved.Count);
        return new NativeExtractionReceipt { ElementId = request.ElementId, SourceDiagramId = entry.DiagramId, TargetDiagramId = targetId,
            TargetParticipantId = Text(participant, "Id"), TargetProcessId = Text(process, "Id"), MovedElementIds = moved.OrderBy(id => id, StringComparer.Ordinal).ToArray() };
    }
}
