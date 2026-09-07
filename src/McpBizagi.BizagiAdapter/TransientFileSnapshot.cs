using System.Text;

namespace McpBizagi.BizagiAdapter;

/// <summary>Bounded, non-mutating snapshots of files an asynchronous native initializer may replace.</summary>
internal static class TransientFileSnapshot
{
    // Observation must not deny the native writer WRITE or DELETE access. Snapshots may be partial;
    // callers must validate their content and retry, never infer readiness from existence alone.
    internal static FileStream Open(string path) => new(path, FileMode.Open, FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);

    internal static string ReadText(string path, int maxBytes = 1024 * 1024)
    {
        if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        using var input = Open(path);
        if (input.Length > maxBytes) throw new InvalidDataException("Native configuration snapshot exceeds its size limit.");
        using var bytes = new MemoryStream();
        var buffer = new byte[4096]; int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
        {
            if (bytes.Length + count > maxBytes) throw new InvalidDataException("Native configuration grew beyond its snapshot limit.");
            bytes.Write(buffer, 0, count);
        }
        bytes.Position = 0;
        using var reader = new StreamReader(bytes, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }
}
