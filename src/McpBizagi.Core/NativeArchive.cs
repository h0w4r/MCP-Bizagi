using System.IO.Compression;
using System.Xml;

namespace McpBizagi.Core;

/// <summary>Preflight for unencrypted v5 native containers, before handing them to vendor code.</summary>
public static class NativeArchive
{
    public static void Validate(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes, writable: false);
        long expanded = 0;
        Inspect(memory, 0, ref expanded);
    }
    private static void Inspect(Stream stream, int depth, ref long expanded)
    {
        if (depth > 1) throw new InvalidDataException("Unsupported native archive nesting.");
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count > 10000) throw new InvalidDataException("Native archive entry limit exceeded.");
        if (depth == 0 && zip.GetEntry("ModelInfo.xml") == null) throw new InvalidDataException("Not a supported unencrypted native model container.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            string name = entry.FullName.Replace('\\', '/');
            if (name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(p => p == "..") || !names.Add(name))
                throw new InvalidDataException("Unsafe or duplicate native archive entry.");
            expanded += entry.Length;
            if (expanded > 256 * 1024 * 1024 || entry.Length > 64 * 1024 * 1024)
                throw new InvalidDataException("Native archive expanded-size limit exceeded.");
            if (name.EndsWith('/')) continue;
            using var input = entry.Open();
            if (name.EndsWith(".diag", StringComparison.OrdinalIgnoreCase))
            {
                using var nested = new MemoryStream(); input.CopyTo(nested); nested.Position = 0;
                Inspect(nested, depth + 1, ref expanded);
            }
            else if (Path.GetExtension(name).Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null, MaxCharactersInDocument = BpmnDocument.MaxXmlCharacters });
                while (reader.Read()) { } // Validate without extracting entries to disk.
            }
        }
    }
}
