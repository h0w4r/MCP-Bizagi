using System.Collections;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private void RelocateReparentedContent(object model, object persistence, Dictionary<string, GraphEntry> before,
        Dictionary<string, GraphEntry> after, NativeImageFile[] images, Action<string> progress)
    {
        var moved = before.Keys.Where(id => before[id].DiagramId != after[id].DiagramId).ToArray();
        if (moved.Length == 0) return;
        var values = (IDictionary)Get(Get(model, "ExtendedAttributes"), "Values");
        foreach (string id in moved)
        {
            string sourceDiagram = before[id].DiagramId, targetDiagram = after[id].DiagramId;
            Guid sourceKey = Guid.Parse(sourceDiagram), targetKey = Guid.Parse(targetDiagram);
            string sourceFolder = AttachmentFolder(model, sourceDiagram, id), targetFolder = AttachmentFolder(model, targetDiagram, id);
            if (values.Contains(sourceKey))
            {
                var sourceValues = (IList)values[sourceKey]!;
                var owners = Items(sourceValues).Where(v => Text(v, "ElementId") == id).ToArray();
                if (owners.Length > 1) throw new InvalidDataException("Ambiguous moved native attribute owner.");
                if (owners.Length == 1)
                {
                    if (!values.Contains(targetKey)) values.Add(targetKey, New(DocumentationType("DiagramAttributeValues")));
                    var targetValues = (IList)values[targetKey]!;
                    if (Items(targetValues).Any(v => Text(v, "ElementId") == id)) throw new InvalidDataException("Destination already contains this attribute owner.");
                    object owner = owners[0];
                    foreach (object attribute in AttributeValues(owner))
                    {
                        if (Text(attribute, "AttributeType") is not ("FileEmbedded" or "Image") || Text(attribute, "Content") == "") continue;
                        string file = Path.GetFullPath(Text(attribute, "Content"));
                        if (!string.Equals(Path.GetDirectoryName(file), sourceFolder, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Moved embedded content is outside its confined native owner folder.");
                        Set(attribute, "Content", Path.Combine(targetFolder, Path.GetFileName(file)));
                    }
                    // Retain actual native value objects, including unrepresented
                    // fields and table values. Linked files are never opened.
                    sourceValues.Remove(owner); targetValues.Add(owner);
                }
            }
            // AttachmentFolder confines both resolved absolute paths to this
            // isolated model. Move the complete owner folder, never operator files.
            if (Directory.Exists(sourceFolder))
            {
                if (Directory.Exists(targetFolder)) throw new IOException("Destination attachment folder already exists.");
                Directory.CreateDirectory(Path.GetDirectoryName(targetFolder)!);
                Directory.Move(sourceFolder, targetFolder);
            }
            foreach (var image in images.Where(i => i.ElementId == id && i.DiagramId == sourceDiagram))
            {
                string sourceFile = Path.Combine(ImageFolder(model, sourceDiagram), image.FileName);
                string targetFile = Path.Combine(ImageFolder(model, targetDiagram), image.FileName);
                if (File.Exists(targetFile)) throw new IOException("Destination native image already exists.");
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Move(sourceFile, targetFile);
            }
            progress("native_reparent_content:" + id);
        }
        var opened = DiagramState(model).OpenedItems; bool changed = false;
        foreach (var tab in opened)
            if (tab.SubProcessId != "" && before.TryGetValue(tab.SubProcessId, out var old) && after.TryGetValue(tab.SubProcessId, out var current) &&
                tab.DiagramId == old.DiagramId && current.DiagramId != old.DiagramId)
            { tab.DiagramId = current.DiagramId; changed = true; }
        if (changed) EditDiagrams(model, persistence, new NativeDiagramPatch { OpenedItems = opened }, progress);
    }
}
