using System.Diagnostics;
using System.Text.Json;

namespace McpBizagi.Core;

/// <summary>One live host owns a state directory; another host cannot rewrite its active journals.</summary>
public sealed class StateLease : IDisposable
{
    private readonly FileStream stream;
    public StateLease(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(Path.GetFullPath(directory), ".host.lock");
        try { stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read); }
        catch (IOException error) { throw new IOException("Another MCP-Bizagi host owns this state directory, or its lease is unavailable. Use a separate MCP_BIZAGI_STATE.", error); }
        try
        {
            using var process = Process.GetCurrentProcess();
            stream.SetLength(0);
            JsonSerializer.Serialize(stream, new { pid = process.Id, startedAt = process.StartTime.ToUniversalTime(), acquiredAt = DateTimeOffset.UtcNow });
            stream.Flush(flushToDisk: true);
        }
        catch { stream.Dispose(); throw; }
    }
    public void Dispose() => stream.Dispose();
}
