using System.Text;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Image intent, exact native-file evidence and narrow comparison-only payload projections.</summary>
public static class NativeImagePolicy
{
    public const long MaxSourceBytes = 32L * 1024 * 1024;
    private static readonly XNamespace Xpdl = "http://www.wfmc.org/2009/XPDL2.2";
    public static void Validate(NativeImageImport image)
    {
        if (string.IsNullOrWhiteSpace(image.SourcePath) || image.SourcePath.Length > 32767 || !image.AllowPngReencoding)
            throw new InvalidDataException("Supply an image path and explicitly acknowledge native PNG re-encoding/metadata loss.");
        RequireHash(image.ExpectedRevision);
        if (image.FrameDimension != null && image.FrameDimension is not "Page" and not "Time" and not "Resolution" || image.FrameIndex < 0)
            throw new InvalidDataException("Use a native Page/Time/Resolution dimension and a nonnegative selected frame.");
    }
    private static void RequireHash(string value)
    {
        if (value == null || value.Length != 64 || value.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvalidDataException("Use a lowercase SHA-256 revision.");
    }
    public static bool SamePixels(NativeImageInfo? a, NativeImageInfo? b) => a != null && b != null &&
        a.Width > 0 && a.Height > 0 && a.Width == b.Width && a.Height == b.Height && a.PixelSha256 == b.PixelSha256 && a.HasTransparency == b.HasTransparency;
    public static string Entry(NativeImageFile file)
    {
        NativeMetadataPolicy.RequireId(file.DiagramId); NativeMetadataPolicy.RequireId(file.ElementId);
        NativeDocumentationPolicy.RequireFileName(file.FileName); RequireHash(file.Sha256);
        if (Path.GetFileNameWithoutExtension(file.FileName) != file.ElementId || file.Length <= 0)
            throw new InvalidDataException("Native image payload must belong to its exact image identity.");
        return file.DiagramId + ".diag!/ImageArtifactImages/" + file.FileName;
    }
    public static void VerifyFiles(IReadOnlyDictionary<string, byte[]> entries, EngineReply reply)
    {
        var graph = reply.Elements.Where(e => e.Kind == "ImageArtifact").ToDictionary(e => e.Id);
        if (graph.Count != reply.ImageFiles.Length || reply.ImageFiles.Select(f => f.ElementId).Distinct().Count() != graph.Count)
            throw new InvalidDataException("Native image file inventory does not cover every actual image exactly once.");
        foreach (var file in reply.ImageFiles)
        {
            if (!graph.TryGetValue(file.ElementId, out var image) || image.DiagramId != file.DiagramId || image.Artifact?.Image is not { } pixels ||
                pixels.Width <= 0 || pixels.Height <= 0 || !entries.TryGetValue(Entry(file), out var bytes) || bytes.LongLength != file.Length || BpmnDocument.Revision(bytes) != file.Sha256)
                throw new InvalidDataException("Native-loaded image pixels/file differ from the persisted archive.");
            RequireHash(pixels.PixelSha256);
        }
    }
    public static void VerifyRestart(byte[] archive, EngineReply edited, EngineReply reopened)
    {
        var entries = NativeArchive.ReadEntries(archive); VerifyFiles(entries, edited); VerifyFiles(entries, reopened);
        var images = reopened.Elements.Where(e => e.Kind == "ImageArtifact").ToDictionary(e => e.Id);
        foreach (var image in edited.Elements.Where(e => e.Kind == "ImageArtifact"))
            if (!images.TryGetValue(image.Id, out var other) || image.DiagramId != other.DiagramId || !SamePixels(image.Artifact?.Image, other.Artifact?.Image))
                throw new InvalidDataException("Native image decoded pixels changed across worker restart.");
    }
    public static void Project(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after, NativeMutation[] changes,
        NativeElement[] reopened, NativeImageImportReceipt[] imports, NativeImageFile[] files)
    {
        var requested = changes.Where(c => c.ArtifactProperties?.Image != null).ToArray();
        if (imports.Length != requested.Length || imports.Select(i => i.ElementId).Distinct().Count() != imports.Length)
            throw new InvalidDataException("Image import receipts do not cover exactly the requested image inputs.");
        // Capture actual native image owners before the XML mutation comparator projects their subtrees.
        var oldOwners = before.Where(p => p.Key.EndsWith(".diag!/Diagram.xml", StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => NativeMetadataPolicy.Read(Encoding.UTF8.GetString(p.Value)).Descendants(Xpdl + "Artifact")
                .Where(e => NativeFidelity.IsNativeNameOwner(e) && (string?)e.Attribute("BizAgiArtifactType") == "Image")
                .Select(e => new { Id = (string)e.Attribute("Id")!, Prefix = p.Key[..^"Diagram.xml".Length] + "ImageArtifactImages/" }))
            .ToDictionary(e => e.Id);
        foreach (var c in changes)
        {
            oldOwners.TryGetValue(c.ElementId, out var owner);
            string[] oldFiles = owner == null ? [] : before.Keys.Where(p => p.StartsWith(owner.Prefix, StringComparison.OrdinalIgnoreCase) &&
                !p[owner.Prefix.Length..].Contains('/') && Path.GetFileNameWithoutExtension(p[owner.Prefix.Length..]).Equals(c.ElementId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (c.Operation == "delete" && owner != null)
            {
                if (oldFiles.Length != 1 || after.Keys.Any(p => p.StartsWith(owner.Prefix, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileNameWithoutExtension(p[owner.Prefix.Length..]).Equals(c.ElementId, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Image deletion did not remove exactly its own durable payload.");
                before.Remove(oldFiles[0]);
            }
            if (c.ArtifactProperties?.Image is not { } input) continue;
            var receipt = imports.Single(i => i.ElementId == c.ElementId);
            var file = files.Single(f => f.ElementId == c.ElementId);
            var image = reopened.Single(e => e.Id == c.ElementId);
            string key = Entry(file);
            if (image.Kind != "ImageArtifact" || receipt.SourceSha256 != input.ExpectedRevision || !receipt.PngReencoded || receipt.PayloadEncoder != "System.Drawing.PNG.straight-alpha" || receipt.FrameCount < 1 ||
                receipt.FrameIndex != (input.FrameIndex ?? 0) || receipt.FrameIndex >= receipt.FrameCount || receipt.FrameCount > 1 && input.FrameIndex == null ||
                input.FrameDimension != null && receipt.FrameDimension != input.FrameDimension ||
                !SamePixels(receipt.Image, image.Artifact?.Image) || Entry(receipt.File) != key || receipt.File.Sha256 != file.Sha256 || receipt.File.Length != file.Length ||
                file.FileName != c.ElementId + ".png" || !after.TryGetValue(key, out var bytes) || bytes.LongLength != file.Length || BpmnDocument.Revision(bytes) != file.Sha256)
                throw new InvalidDataException("Image source/frame/pixels/native-file readback differs from the explicit request.");
            if (c.Operation == "create" ? owner != null || before.ContainsKey(key) : owner == null || oldFiles.Length != 1 || owner.Prefix != file.DiagramId + ".diag!/ImageArtifactImages/")
                throw new InvalidDataException("Image mutation would overwrite an unrelated or ambiguous native payload.");
            foreach (string old in oldFiles) before.Remove(old);
            after.Remove(key);
        }
    }
}
