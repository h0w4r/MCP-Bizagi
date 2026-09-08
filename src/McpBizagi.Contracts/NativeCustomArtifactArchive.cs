using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace McpBizagi.Contracts;

/// <summary>Bounded read-only preflight shared by host and worker before native .bca extraction.</summary>
public static class NativeCustomArtifactArchive
{
    public sealed class Payload
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public byte[] EmbeddedPng { get; set; } = System.Array.Empty<byte>();
        public byte[] SidecarPng { get; set; } = System.Array.Empty<byte>();
    }

    public static Payload[] Read(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.LongLength > 64L * 1024 * 1024) throw new InvalidDataException("Custom artifact archive exceeds the input bound.");
        long expanded = 0;
        var outer = Entries(bytes, ref expanded, 2);
        if (outer.Count != 2 || !outer.ContainsKey("CustomArtifactDefinitions.xml") || !outer.ContainsKey("CustomArtifactDefinitionsImages.zip"))
            throw new InvalidDataException("Unsupported .bca members; no native extraction was attempted.");
        var root = Xml(outer["CustomArtifactDefinitions.xml"]).Root;
        if (root == null || root.Name != "CustomArtifactTypesDefinitions" || root.Attributes().Any(a => !a.IsNamespaceDeclaration) ||
            root.Nodes().Any(n => n is not XElement && n is not XText || n is XText x && !string.IsNullOrWhiteSpace(x.Value)))
            throw new InvalidDataException("Unsupported custom artifact collection XML.");
        var definitions = root.Elements().Select(ReadDefinition).ToArray();
        if (definitions.Length is < 1 or > 100 || definitions.Select(d => d.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != definitions.Length)
            throw new InvalidDataException("Custom artifact definitions must have 1-100 unique native GUIDs.");
        var images = Entries(outer["CustomArtifactDefinitionsImages.zip"], ref expanded, 100);
        if (images.Count != definitions.Length) throw new InvalidDataException("Custom artifact image count differs from the definitions.");
        foreach (var definition in definitions)
        {
            if (!images.TryGetValue(definition.Id + ".png", out var png)) throw new InvalidDataException("Missing custom artifact sidecar image.");
            RequirePng(png); definition.SidecarPng = png;
        }
        return definitions;
    }

    public static XDocument Xml(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = 64 * 1024 * 1024 });
        var xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        if (xml.Nodes().Any(n => n is not XElement && n is not XText)) throw new InvalidDataException("Unsupported custom artifact document nodes.");
        return xml;
    }

    public static Payload ReadDefinition(XElement element)
    {
        // Reject unknown information before a vendor deserializer could silently discard it.
        if (element.Name != "CustomArtifactType" || element.Attributes().Any(a => !a.IsNamespaceDeclaration && a.Name != "Id" && a.Name != "Name") ||
            element.Elements().Count() != 1 || element.Elements().Single().Name != "Image" ||
            element.Nodes().Any(n => n is not XElement && n is not XText || n is XText t && !string.IsNullOrWhiteSpace(t.Value)))
            throw new InvalidDataException("Unsupported custom artifact definition fields.");
        string id = (string?)element.Attribute("Id") ?? "", name = (string?)element.Attribute("Name") ?? "";
        if (!Guid.TryParseExact(id, "D", out var guid) || guid == Guid.Empty || id != guid.ToString() || name.Length > 4096 || element.Attribute("Name") == null)
            throw new InvalidDataException("Invalid native custom artifact identity or name.");
        var image = element.Element("Image")!;
        if (image.HasAttributes || image.HasElements || image.Nodes().Any(n => n is not XText)) throw new InvalidDataException("Unsupported custom artifact image XML.");
        byte[] png = Convert.FromBase64String(image.Value); RequirePng(png);
        return new Payload { Id = id, Name = name, EmbeddedPng = png };
    }

    private static void RequirePng(byte[] bytes)
    {
        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (bytes.LongLength is < 8 or > 32L * 1024 * 1024 || !bytes.Take(8).SequenceEqual(signature)) throw new InvalidDataException("Custom artifact payload is not a bounded PNG.");
    }

    private static Dictionary<string, byte[]> Entries(byte[] bytes, ref long expanded, int maxCount)
    {
        using var stream = new MemoryStream(bytes, false); using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        if (zip.Entries.Count > maxCount) throw new InvalidDataException("Custom artifact archive entry count exceeded.");
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            string name = entry.FullName;
            if (name.Length == 0 || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || name is "." or ".." || entries.ContainsKey(name) ||
                IsSymbolicLink(entry))
                throw new InvalidDataException("Custom artifact archive requires unique regular flat entries.");
            expanded += entry.Length;
            if (entry.Length > 64L * 1024 * 1024 || expanded > 128L * 1024 * 1024) throw new InvalidDataException("Custom artifact expanded-size bound exceeded.");
            using var input = entry.Open(); using var output = new MemoryStream(); byte[] buffer = new byte[65536]; int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > entry.Length) throw new InvalidDataException("Custom artifact archive exceeded its declared size.");
                output.Write(buffer, 0, read);
            }
            if (output.Length != entry.Length) throw new InvalidDataException("Truncated custom artifact archive member.");
            entries.Add(name, output.ToArray());
        }
        return entries;
    }
    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        // The netstandard2.0 reference surface omits this BCL member; both actual runtimes must expose it.
        var property = typeof(ZipArchiveEntry).GetProperty("ExternalAttributes") ?? throw new NotSupportedException("Archive runtime cannot inspect member attributes.");
        uint flags = unchecked((uint)(int)property.GetValue(entry)!);
        return (flags & 0xf0000000) == 0xa0000000;
    }
}
