using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private readonly List<NativeImageImportReceipt> imageImports = new();
    private readonly List<NativeRenderedImage> renderedImages = new();
    private const long MaxImagePixels = 64L * 1024 * 1024;
    private static string ImageHash(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }

    private static NativeImageInfo DescribePicture(Bitmap bitmap)
    {
        CheckImageSize(bitmap);
        using var hash = SHA256.Create(); bool transparent = false;
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] row = new byte[checked(bitmap.Width * 4)];
            for (int y = 0; y < bitmap.Height; y++)
            {
                // Signed stride selects the actual row; padding never becomes part of the canonical fingerprint.
                Marshal.Copy(IntPtr.Add(data.Scan0, checked(y * data.Stride)), row, 0, row.Length);
                for (int x = 3; x < row.Length; x += 4) transparent |= row[x] != 255;
                hash.TransformBlock(row, 0, row.Length, row, 0);
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return new NativeImageInfo { Width = bitmap.Width, Height = bitmap.Height, HasTransparency = transparent,
                PixelSha256 = BitConverter.ToString(hash.Hash!).Replace("-", "").ToLowerInvariant() };
        }
        finally { bitmap.UnlockBits(data); }
    }
    private static void CheckImageSize(Image image)
    {
        if (image.Width <= 0 || image.Height <= 0 || (long)image.Width * image.Height > MaxImagePixels)
            throw new InvalidDataException("Decoded image exceeds the native bitmap operation bound.");
    }
    private static Bitmap IndependentPicture(Bitmap source)
    {
        // Copy decoded pixels, not a lazy Image that would outlive its source stream.
        // No Graphics scaling, compositing, resampling or hand-written image codec is used.
        CheckImageSize(source); var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        try
        {
            var area = new Rectangle(0, 0, source.Width, source.Height);
            var a = source.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var b = result.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] row = new byte[checked(source.Width * 4)];
                    for (int y = 0; y < source.Height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(a.Scan0, checked(y * a.Stride)), row, 0, row.Length);
                        Marshal.Copy(row, 0, IntPtr.Add(b.Scan0, checked(y * b.Stride)), row.Length);
                    }
                }
                finally { result.UnlockBits(b); }
            }
            finally { source.UnlockBits(a); }
            if (source.HorizontalResolution > 0 && source.VerticalResolution > 0) result.SetResolution(source.HorizontalResolution, source.VerticalResolution);
            return result;
        }
        catch { result.Dispose(); throw; }
    }
    private string ImageFolder(object model, string diagramId)
    {
        object facade = Resolve("Bizagi.ProcessModeler.BusinessEntities.Interfaces.Persistence.IPersistenceUtilFacade");
        object special = Enum.Parse(Type("Bizagi.ProcessModeler.BusinessEntities.dll", "Bizagi.ProcessModeler.BusinessEntities.Common.ModelerSpecialFolder"), "ImageArtifactImages");
        string folder = (string)Call(facade, "GetFolderPath", model, Guid.Parse(diagramId), special)!;
        string root = Path.GetFullPath((string)Call(model, "GetTempPath")!).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        folder = Path.GetFullPath(folder);
        if (!folder.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Native image folder escaped the isolated model.");
        return folder;
    }
    private string[] DetachLoadedImages(object model, Action<string> progress)
    {
        var adjustments = new List<string>();
        foreach (var entry in Graph(model).Where(e => e.Value.GetType().Name == "ImageArtifact"))
        {
            // The installed loader closes each picture stream. A first read can work, while a later
            // GDI+ clone/serialization fails. Retain native pixel evidence, then detach from a live stream.
            var file = DescribeImageFile(model, entry);
            var original = (Bitmap)Get(entry.Value, "Picture"); var expected = DescribePicture(original);
            using var stream = File.OpenRead(Path.Combine(ImageFolder(model, file.DiagramId), file.FileName));
            using var decoded = (Bitmap)Image.FromStream(stream, false, true);
            var detached = IndependentPicture(decoded); var actual = DescribePicture(detached);
            if (actual.Width != expected.Width || actual.Height != expected.Height || actual.PixelSha256 != expected.PixelSha256)
            { detached.Dispose(); throw new InvalidDataException("Native-loaded and independently detached image pixels differ."); }
            Set(entry.Value, "Picture", detached); original.Dispose();
            string phase = "native_image_stream_lifetime_detached:" + file.ElementId; adjustments.Add(phase); progress(phase);
        }
        return adjustments.ToArray();
    }
    private NativeImageFile[] DescribeImageFiles(object model) => Graph(model).Where(e => e.Value.GetType().Name == "ImageArtifact").Select(e => DescribeImageFile(model, e)).ToArray();
    private NativeImageFile DescribeImageFile(object model, GraphEntry e)
    {
        string id = Text(e.Value, "Id"), folder = ImageFolder(model, e.DiagramId);
        var matches = Directory.Exists(folder) ? Directory.GetFiles(folder).Where(p => Path.GetFileNameWithoutExtension(p).Equals(id, StringComparison.OrdinalIgnoreCase)).ToArray() : Array.Empty<string>();
        if (matches.Length != 1) throw new InvalidDataException("Native image requires exactly one durable payload: " + id);
        byte[] bytes = File.ReadAllBytes(matches[0]);
        if (Optional(e.Value, "Picture") is not Bitmap) throw new InvalidDataException("Native image payload was not loaded as a bitmap.");
        return new NativeImageFile { DiagramId = e.DiagramId, ElementId = id, FileName = Path.GetFileName(matches[0]), Length = bytes.LongLength, Sha256 = ImageHash(bytes) };
    }

    private void ApplyImage(object model, GraphEntry entry, NativeImageImport import)
    {
        if (entry.Value.GetType().Name != "ImageArtifact") throw new InvalidDataException("Image input requires an actual native ImageArtifact.");
        if (!import.AllowPngReencoding) throw new InvalidDataException("Native PNG re-encoding requires explicit acknowledgement.");
        byte[] bytes = File.ReadAllBytes(import.SourcePath);
        if (bytes.LongLength is <= 0 or > 32L * 1024 * 1024 || ImageHash(bytes) != import.ExpectedRevision) throw new InvalidDataException("Staged image length or SHA-256 differs from the captured input.");
        using var stream = new MemoryStream(bytes, writable: false);
        using var decoded = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
        if (decoded is not Bitmap bitmap) throw new InvalidDataException("This input is not a native raster bitmap; vector rasterization is a separate explicit capability.");
        CheckImageSize(bitmap);
        string Dimension(Guid id) => id == FrameDimension.Page.Guid ? "Page" : id == FrameDimension.Time.Guid ? "Time" : id == FrameDimension.Resolution.Guid ? "Resolution" : id.ToString();
        var dimensions = bitmap.FrameDimensionsList;
        var selected = import.FrameDimension == null ? dimensions : dimensions.Where(d => Dimension(d) == import.FrameDimension).ToArray();
        if (selected.Length != 1) throw new InvalidDataException("Select exactly one native image frame dimension: " + string.Join(", ", dimensions.Select(Dimension)));
        var dimension = new FrameDimension(selected[0]); int count = bitmap.GetFrameCount(dimension), frame = import.FrameIndex ?? 0;
        if (count > 1 && !import.FrameIndex.HasValue || frame < 0 || frame >= count) throw new InvalidDataException("An explicit in-range frame index is required for a multi-frame image (frames=" + count + ").");
        bitmap.SelectActiveFrame(dimension, frame);
        var expected = DescribePicture(bitmap);
        Bitmap? independent = IndependentPicture(bitmap);
        try
        {
            string elementId = Text(entry.Value, "Id"), folder = ImageFolder(model, entry.DiagramId);
            Directory.CreateDirectory(folder);
            var old = Directory.GetFiles(folder).Where(p => Path.GetFileNameWithoutExtension(p).Equals(elementId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (old.Length > 1) throw new InvalidDataException("Ambiguous existing native image payload.");
            string output = Path.Combine(folder, elementId + ".png");
            // The installed PersistBitmap helper redraws into premultiplied alpha and changes RGB values.
            // Use the mature PNG codec without that compositing step; native persistence still owns the .bpm archive.
            using (var payload = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None)) independent.Save(payload, ImageFormat.Png);
            if (old.Length == 1 && !old[0].Equals(output, StringComparison.OrdinalIgnoreCase)) File.Delete(old[0]);
            using var pngStream = File.OpenRead(output); using var png = (Bitmap)Image.FromStream(pngStream);
            var actual = DescribePicture(png);
            if (actual.Width != expected.Width || actual.Height != expected.Height || actual.PixelSha256 != expected.PixelSha256)
                throw new InvalidDataException("Native image encoding changed the selected frame's decoded pixels.");
            var previous = Optional(entry.Value, "Picture") as Bitmap;
            Set(entry.Value, "Picture", independent); independent = null; previous?.Dispose();
            byte[] persisted = File.ReadAllBytes(output);
            imageImports.Add(new NativeImageImportReceipt
            {
                ElementId = elementId, SourceSha256 = import.ExpectedRevision,
                SourceFormat = new[] { ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.Gif, ImageFormat.Tiff, ImageFormat.Bmp, ImageFormat.Icon }.FirstOrDefault(f => f.Guid == bitmap.RawFormat.Guid)?.ToString() ?? bitmap.RawFormat.Guid.ToString(),
                DecodedPixelFormat = bitmap.PixelFormat.ToString(),
                FrameDimension = Dimension(selected[0]), FrameCount = count, FrameIndex = frame, SourceMetadataIds = bitmap.PropertyIdList,
                PngReencoded = true, PayloadEncoder = "System.Drawing.PNG.straight-alpha", Image = expected,
                File = new NativeImageFile { DiagramId = entry.DiagramId, ElementId = elementId, FileName = Path.GetFileName(output), Length = persisted.LongLength, Sha256 = ImageHash(persisted) }
            });
            }
        finally { independent?.Dispose(); }
    }
    private void CloneImageFiles(object model, string sourceId, string targetId, System.Collections.IDictionary map)
    {
        // Native artifact Clone() creates identities but does not copy the separate payload files.
        // Preserve original encoded bytes instead of introducing an undocumented re-encoding during cloning.
        var graph = Graph(model).ToDictionary(e => Text(e.Value, "Id"));
        string target = ImageFolder(model, targetId);
        foreach (var original in DescribeImageFilesForDiagram(model, sourceId))
        {
            string id = map[Guid.Parse(original.ElementId)]?.ToString() ?? throw new InvalidDataException("Image is absent from the native clone map.");
            var entry = graph[id];
            if (entry.DiagramId != targetId || entry.Value.GetType().Name != "ImageArtifact") throw new InvalidDataException("Cloned image ownership differs from its native map.");
            Directory.CreateDirectory(target);
            File.Copy(Path.Combine(ImageFolder(model, sourceId), original.FileName), Path.Combine(target, id + Path.GetExtension(original.FileName)), false);
            // Never retain an alias to a source bitmap owned by another artifact.
            Set(entry.Value, "Picture", IndependentPicture((Bitmap)Get(graph[original.ElementId].Value, "Picture")));
        }
    }
    private NativeImageFile[] DescribeImageFilesForDiagram(object model, string diagramId)
    {
        // Describe only source images: cloned images do not have files until this pass finishes.
        return Graph(model).Where(e => e.DiagramId == diagramId && e.Value.GetType().Name == "ImageArtifact").Select(e => DescribeImageFile(model, e)).ToArray();
    }
    private void DeleteImageFile(object model, GraphEntry entry)
    {
        if (entry.Value.GetType().Name != "ImageArtifact") return;
        var file = DescribeImageFile(model, entry);
        File.Delete(Path.Combine(ImageFolder(model, entry.DiagramId), file.FileName));
    }
    private string ExportImage(object model, EngineRequest request)
    {
        var file = DescribeImageFiles(model).Single(e => e.DiagramId == request.DiagramId && e.ElementId == request.ImageElementId);
        File.Copy(Path.Combine(ImageFolder(model, file.DiagramId), file.FileName), request.OutputPath, overwrite: false);
        return request.OutputPath;
    }
    private void VerifyRenderedImages(string svg, string surface, GraphEntry[] expected, Action<string> progress)
    {
        var xml = new System.Xml.XmlDocument { XmlResolver = null };
        using (var input = new StringReader(svg))
        using (var reader = System.Xml.XmlReader.Create(input, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 })) xml.Load(reader);
        foreach (var entry in expected.Where(e => e.Value.GetType().Name == "ImageArtifact"))
        {
            string id = Text(entry.Value, "Id");
            var shapes = xml.SelectNodes("//*[@data-element-id]")!.Cast<System.Xml.XmlElement>().Where(e => e.GetAttribute("data-element-id") == id).ToArray();
            if (shapes.Length != 1) throw new InvalidDataException("Native rendered image identity is missing or ambiguous.");
            var images = shapes[0].SelectNodes(".//*[local-name()='image' and namespace-uri()='http://www.w3.org/2000/svg']")!.Cast<System.Xml.XmlElement>().ToArray();
            if (images.Length != 1) throw new InvalidDataException("Native image shape does not contain exactly one SVG image payload.");
            var links = images[0].Attributes.Cast<System.Xml.XmlAttribute>().Where(a => a.LocalName == "href" && (a.NamespaceURI == "" || a.NamespaceURI == "http://www.w3.org/1999/xlink")).ToArray();
            if (links.Length != 1) throw new InvalidDataException("Native SVG image requires one self-contained data URI.");
            var match = System.Text.RegularExpressions.Regex.Match(links[0].Value, @"\Adata:(image/[a-zA-Z0-9.+-]+);base64,(.+)\z");
            if (!match.Success) throw new InvalidDataException("Native SVG image is not an embedded raster payload.");
            byte[] bytes = Convert.FromBase64String(match.Groups[2].Value);
            using var stream = new MemoryStream(bytes, false); using var bitmap = (Bitmap)Image.FromStream(stream, false, true);
            var actual = DescribePicture(bitmap); var wanted = DescribePicture((Bitmap)Get(entry.Value, "Picture"));
            if (actual.Width != wanted.Width || actual.Height != wanted.Height || actual.PixelSha256 != wanted.PixelSha256)
                throw new InvalidDataException("Native SVG image pixels differ from the model picture.");
            renderedImages.Add(new NativeRenderedImage { SurfaceId = surface, ElementId = id, DeclaredMimeType = match.Groups[1].Value,
                EmbeddedSha256 = ImageHash(bytes), Image = actual });
            progress("native_render_image_pixels_verified:" + id);
        }
    }
}
