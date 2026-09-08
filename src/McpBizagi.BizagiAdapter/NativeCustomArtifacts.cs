using System.Drawing;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private readonly List<NativeCustomArtifactReceipt> customArtifactImports = new();
    private Type CustomType(string name) => Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Common." + name);
    private static bool SamePicture(NativeImageInfo a, NativeImageInfo b) => a.Width == b.Width && a.Height == b.Height && a.PixelSha256 == b.PixelSha256;
    private static Bitmap DecodeCustomPng(byte[] png)
    {
        using var stream = new MemoryStream(png, false); using var decoded = (Bitmap)Image.FromStream(stream, false, true);
        return IndependentPicture(decoded);
    }
    private string CustomFolder(object model)
    {
        object facade = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.Persistence.IPersistenceUtilFacade");
        object special = Enum.Parse(CustomType("ModelerSpecialFolder"), "Artifacts");
        string folder = Path.GetFullPath((string)Call(facade, "GetFolderPath", model, special)!);
        string root = Path.GetFullPath((string)Call(model, "GetTempPath")!).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!folder.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Custom artifact folder escaped the isolated model.");
        return folder;
    }
    private static NativeCustomArtifactDefinition DescribeCustom(object definition) => new()
    {
        Id = Text(definition, "Id"), Name = Text(definition, "Name"), Image = DescribePicture((Bitmap)Get(definition, "Image")),
        SerializedImageSha256 = ImageHash((byte[])Get(definition, "SerializableImage"))
    };
    private NativeCustomArtifactDefinition[] DescribeCustomArtifacts(object model) => Items(Get(model, "CustomArtifactTypes")).Select(DescribeCustom).ToArray();

    private string[] DetachLoadedCustomArtifacts(object model, Action<string> progress)
    {
        var adjustments = new List<string>();
        foreach (object definition in Items(Get(model, "CustomArtifactTypes")))
        {
            string id = Text(definition, "Id"), path = Path.Combine(CustomFolder(model), id + ".xml");
            var payload = NativeCustomArtifactArchive.ReadDefinition(NativeCustomArtifactArchive.Xml(File.ReadAllBytes(path)).Root!);
            if (payload.Id != id || payload.Name != Text(definition, "Name")) throw new InvalidDataException("Loaded custom definition differs from the native model file.");
            var old = (Bitmap)Get(definition, "Image"); var actual = DecodeCustomPng(payload.EmbeddedPng);
            if (!SamePicture(DescribePicture(old), DescribePicture(actual))) { actual.Dispose(); throw new InvalidDataException("Loaded custom artifact pixels differ from their original payload."); }
            Set(definition, "Image", actual); old.Dispose();
            string phase = "native_custom_image_stream_lifetime_detached:" + id; adjustments.Add(phase); progress(phase);
        }
        VerifyCustomReferences(model);
        return adjustments.ToArray();
    }
    private static void VerifyCustomReferences(object model)
    {
        var catalog = Items(Get(model, "CustomArtifactTypes")).ToDictionary(d => Text(d, "Id"));
        foreach (var entry in Graph(model).Where(e => e.Value.GetType().Name == "CustomArtifact"))
        {
            string id = Text(entry.Value, "CustomArtifactTypeId");
            if (!catalog.TryGetValue(id, out var definition) || !ReferenceEquals(Optional(entry.Value, "CustomArtifactType"), definition))
                throw new InvalidDataException("A custom artifact has a missing or inconsistent model-owned definition.");
        }
    }
    private void NormalizeCustomImage(object definition, NativeImageImportReceipt source, bool allow)
    {
        // Exactly one installed serializer conversion is permitted, never an iterative lossy convergence loop.
        byte[] encoded = (byte[])Get(definition, "SerializableImage"); var normalized = DecodeCustomPng(encoded);
        var actual = DescribePicture(normalized); bool changed = !SamePicture(source.Image, actual);
        if (changed && !allow) { normalized.Dispose(); throw new InvalidDataException("Native custom artifact rasterization changes source pixels; explicit AllowNativeRasterization is required."); }
        var old = (Bitmap)Get(definition, "Image"); Set(definition, "Image", normalized); old.Dispose();
        byte[] repeated = (byte[])Get(definition, "SerializableImage");
        using var repeatedPicture = DecodeCustomPng(repeated);
        if (!SamePicture(actual, DescribePicture(repeatedPicture)) || !encoded.SequenceEqual(repeated))
            throw new InvalidDataException("Native custom image serialization is unstable; no model result may be published.");
        source.PngReencoded = true; source.PayloadEncoder = "Bizagi.CustomArtifactType.SerializableImage";
        customArtifactImports.Add(new NativeCustomArtifactReceipt { Id = Text(definition, "Id"), Source = source, Result = DescribeCustom(definition),
            PixelsChanged = changed, NativeRasterizationAcknowledged = allow, RepeatedSerializationStable = true });
    }
    private void RegisterCustomDefinition(object model, object definition)
    {
        Call(Get(model, "CustomArtifactTypes"), "Add", definition);
        object types = Get(Get(model, "ExtendedAttributes"), "ElementTypes");
        var type = DocumentationType("AttributeElementTypeItem`1").MakeGenericType(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.BPMN20.ElementType"));
        Call(types, "Add", New(type, Guid.Parse(Text(definition, "Id"))));
    }
    private void EditCustomArtifacts(object model, NativeCustomArtifactPatch patch, Action<string> progress)
    {
        foreach (var change in patch.Changes)
        {
            object collection = Get(model, "CustomArtifactTypes");
            object? current = Items(collection).SingleOrDefault(d => Text(d, "Id") == change.Id);
            if (change.Operation == "create" && current != null || change.Operation != "create" && current == null)
                throw new InvalidDataException("Custom artifact definition existence conflicts with the request.");
            if (change.Operation == "delete")
            {
                if (Graph(model).Any(e => e.Value.GetType().Name == "CustomArtifact" && Text(e.Value, "CustomArtifactTypeId") == change.Id))
                    throw new InvalidDataException("Custom artifact definition is still referenced by an instance.");
                object attributes = Get(model, "ExtendedAttributes");
                if (Items(Get(attributes, "Definitions")).SelectMany(d => Items(Get(d, "ElementTypes"))).Any(t => Text(t, "CustomArtifactTypeId") == change.Id))
                    throw new InvalidDataException("Custom artifact definition is still used by attribute applicability.");
                string order = Path.Combine((string)Call(model, "GetTempPath")!, "Documentation", "CustomArtifact_" + change.Id + ".order");
                if (File.Exists(order)) throw new InvalidDataException("Custom artifact attribute order must be explicitly removed before deleting its type.");
                // Native Persist only upserts definition XML. Remove this exact owned scratch leaf to prevent resurrection.
                File.Delete(Path.Combine(CustomFolder(model), change.Id + ".xml")); Call(collection, "Remove", current!);
                object types = Get(attributes, "ElementTypes");
                foreach (object item in Items(types).Where(t => Text(t, "CustomArtifactTypeId") == change.Id).ToArray()) Call(types, "Remove", item);
                (current as IDisposable)?.Dispose();
            }
            else
            {
                object definition = current ?? New(CustomType("CustomArtifactType"));
                if (current == null) { Set(definition, "Id", Guid.Parse(change.Id)); RegisterCustomDefinition(model, definition); }
                if (change.Name != null) Set(definition, "Name", change.Name);
                if (change.Image != null)
                {
                    var image = DecodeImageImport(change.Image, out var receipt); var old = Optional(definition, "Image") as Bitmap;
                    Set(definition, "Image", image); old?.Dispose(); NormalizeCustomImage(definition, receipt, change.AllowNativeRasterization);
                }
            }
            progress("native_custom_artifact_" + change.Operation + ":" + change.Id);
        }
        VerifyCustomReferences(model);
    }
    private string ExportCustomArtifacts(object model, EngineRequest request, Action<string> progress)
    {
        // The installed exporter removes every literal '.bca', not just the extension. Reject a
        // conflicting parent path before its folder deletion/creation could escape the intended artifact.
        if (!request.OutputPath.EndsWith(".bca", StringComparison.Ordinal) ||
            request.OutputPath.Replace(".bca", "") != request.OutputPath.Substring(0, request.OutputPath.Length - 4))
            throw new InvalidDataException("Native .bca export requires a parent path without the literal .bca substring.");
        var collection = New(CustomType("CustomArtifactTypeCollection"));
        foreach (string id in request.CustomArtifactIds)
            Call(collection, "Add", Items(Get(model, "CustomArtifactTypes")).Single(d => Text(d, "Id") == id));
        progress("native_custom_artifact_bca_export");
        Call(Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.ICustomArtifactTypeManager"), "SaveCustomArtifactTypesToFile", request.OutputPath, collection);
        NativeCustomArtifactArchive.Read(File.ReadAllBytes(request.OutputPath));
        return request.OutputPath;
    }
    private void ImportCustomArtifacts(object model, EngineRequest request, Action<string> progress)
    {
        var payloads = NativeCustomArtifactArchive.Read(File.ReadAllBytes(request.CustomArtifactArchivePath));
        // Vendor scratch cleanup uses Path.GetTempPath(); ensure it remains inside this owned worker before dispatch.
        if (!Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar).Equals(workRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Native .bca extraction requires verified worker-only temporary storage.");
        progress("native_custom_artifact_bca_import");
        object imported = Call(Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.ICustomArtifactTypeManager"), "LoadCustomArtifactTypesFromFile", request.CustomArtifactArchivePath)
            ?? throw new InvalidDataException("Native .bca importer returned no definitions.");
        var definitions = Items(imported).ToArray();
        if (definitions.Length != payloads.Length) throw new InvalidDataException("Native .bca import lost definitions.");
        foreach (var payload in payloads)
        {
            object definition = definitions.Single(d => Text(d, "Id") == payload.Id);
            if (Text(definition, "Name") != payload.Name) throw new InvalidDataException("Native .bca definition name changed.");
            using var embedded = DecodeCustomPng(payload.EmbeddedPng);
            var detached = DecodeImageImport(new NativeImageImport { ExpectedRevision = ImageHash(payload.SidecarPng), AllowPngReencoding = true }, payload.SidecarPng, out var imageReceipt);
            if (!SamePicture(DescribePicture((Bitmap)Get(definition, "Image")), DescribePicture(detached)))
            { detached.Dispose(); throw new InvalidDataException("Native .bca sidecar and loaded image pixels conflict."); }
            var old = (Bitmap)Get(definition, "Image"); Set(definition, "Image", detached); old.Dispose();
            NormalizeCustomImage(definition, imageReceipt, request.AllowCustomArtifactRasterization);
            // Vendor exports contain a direct image sidecar and a serialized (rasterized) embedded image.
            // Any discrepancy must be exactly explained by the acknowledged native conversion, not ignored.
            if (!SamePicture(DescribePicture(embedded), DescribePicture((Bitmap)Get(definition, "Image"))))
                throw new InvalidDataException("Native .bca embedded image differs from the verified sidecar serialization.");
            object? existing = Items(Get(model, "CustomArtifactTypes")).SingleOrDefault(d => Text(d, "Id") == payload.Id);
            if (existing == null) RegisterCustomDefinition(model, definition);
            else
            {
                if (!request.ReplaceCustomArtifacts) throw new InvalidDataException("Native .bca identity already exists; replacement was not authorized.");
                // Preserve the existing definition object's identity so every root/nested instance stays linked.
                var previous = (Bitmap)Get(existing, "Image"); Set(existing, "Name", Text(definition, "Name")); Set(existing, "Image", Get(definition, "Image")); previous.Dispose();
            }
        }
        VerifyCustomReferences(model);
    }
}
