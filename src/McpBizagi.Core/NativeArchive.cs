using System.IO.Compression;
using System.Xml;

namespace McpBizagi.Core;

/// <summary>Preflight for unencrypted v5 native containers, before handing them to vendor code.</summary>
public static class NativeArchive
{
    public static void Validate(byte[] bytes)
    {
        ReadEntries(bytes);
    }

    public static IReadOnlyDictionary<string, byte[]> ReadEntries(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes, writable: false);
        long expanded = 0;
        int entryCount = 0;
        var entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        Inspect(memory, 0, ref expanded, ref entryCount, entries, "");
        return entries;
    }
    private static void Inspect(Stream stream, int depth, ref long expanded, ref int entryCount, Dictionary<string, byte[]> entries, string prefix)
    {
        if (depth > 1) throw new InvalidDataException("Unsupported native archive nesting.");
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count > 10000) throw new InvalidDataException("Native archive entry limit exceeded.");
        if (depth == 0 && zip.GetEntry("ModelInfo.xml") == null) throw new InvalidDataException("Not a supported unencrypted native model container.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            if (++entryCount > 10000) throw new InvalidDataException("Native archive aggregate entry limit exceeded.");
            string name = entry.FullName.Replace('\\', '/');
            if (name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(p => p is ".." or ".") || !names.Add(name))
                throw new InvalidDataException("Unsafe or duplicate native archive entry.");
            expanded += entry.Length;
            if (expanded > 256 * 1024 * 1024 || entry.Length > 64 * 1024 * 1024)
                throw new InvalidDataException("Native archive expanded-size limit exceeded.");
            if (name.EndsWith('/')) continue;
            using var input = entry.Open();
            using var payload = new MemoryStream();
            byte[] buffer = new byte[65536];
            int count;
            // Count actual decompressed bytes too; never trust only an archive's advertised entry size.
            while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
            {
                if (payload.Length + count > entry.Length) throw new InvalidDataException("Native archive entry exceeds its declared size.");
                payload.Write(buffer, 0, count);
            }
            if (payload.Length != entry.Length) throw new InvalidDataException("Native archive entry length mismatch.");
            payload.Position = 0;
            bool embeddedFile = depth == 1 && (name.StartsWith("Files/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Actions/", StringComparison.OrdinalIgnoreCase));
            if (name.EndsWith(".diag", StringComparison.OrdinalIgnoreCase) && !embeddedFile)
            {
                Inspect(payload, depth + 1, ref expanded, ref entryCount, entries, prefix + name + "!/");
            }
            else
            {
                // Files are opaque embedded content, even when their names end in .xml.
                if (Path.GetExtension(name).Equals(".xml", StringComparison.OrdinalIgnoreCase) && !embeddedFile)
                {
                    using var reader = XmlReader.Create(payload, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null, MaxCharactersInDocument = BpmnDocument.MaxXmlCharacters });
                    while (reader.Read()) { } // Validate without extracting entries to disk.
                }
                if (!entries.TryAdd(prefix + name, payload.ToArray())) throw new InvalidDataException("Ambiguous flattened native entry name.");
            }
        }
    }
}
