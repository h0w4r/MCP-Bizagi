using System.Text;

namespace McpBizagi.Core;

public sealed record FileResult(string Path, string Revision, string? BackupPath, long Bytes);

/// <summary>Confines paths and uses staged writes with optimistic revision checks.</summary>
public sealed class WorkspaceFiles
{
    public string Root { get; }
    public WorkspaceFiles(string root)
    {
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        Directory.CreateDirectory(Root);
        RejectReparsePoints(Root);
    }

    public string Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A workspace path is required.");
        string full = Path.GetFullPath(path, Root);
        string relative = Path.GetRelativePath(Root, full);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new UnauthorizedAccessException("Path is outside the configured workspace.");
        // Windows alternate data streams are not model files.
        if (relative.Contains(':')) throw new UnauthorizedAccessException("Alternate data streams are not supported.");
        RejectReparsePoints(full);
        return full;
    }

    private static void RejectReparsePoints(string path)
    {
        for (string? current = path; current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Reparse points are not allowed in workspace paths.");
    }

    public byte[] Read(string path)
    {
        string full = Resolve(path);
        using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > BpmnDocument.MaxXmlCharacters * 4) throw new InvalidDataException("Model exceeds this release's size limit.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public (string Text, string Revision) ReadBpmn(string path)
    {
        RequireBpmn(path);
        byte[] bytes = Read(path);
        // StreamReader honors the XML file's common Unicode BOMs; XML parsing checks its structure.
        using var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false, true), true);
        string text = reader.ReadToEnd();
        BpmnDocument.Parse(text);
        return (text, BpmnDocument.Revision(bytes));
    }

    public FileResult SaveBpmn(string path, string xml, string? expectedRevision = null)
    {
        RequireBpmn(path);
        BpmnDocument.Parse(xml);
        return Commit(path, BpmnDocument.Encode(xml), expectedRevision);
    }

    public FileResult Commit(string path, byte[] bytes, string? expectedRevision)
    {
        string full = Resolve(path);
        // Cooperating MCP processes share a gate; stale writers fail rather than queue a blind overwrite.
        string identity = OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
        using var gate = new Mutex(false, "McpBizagi-" + BpmnDocument.Revision(Encoding.UTF8.GetBytes(identity)));
        bool acquired;
        try { acquired = gate.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; } // Revision checking still runs after a crashed writer.
        if (!acquired) throw new IOException("Destination is busy in another MCP writer; inspect its revision before retrying.");
        try { return CommitLocked(full, bytes, expectedRevision); }
        finally { gate.ReleaseMutex(); }
    }

    private FileResult CommitLocked(string full, byte[] bytes, string? expectedRevision)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        Resolve(full);
        bool exists = File.Exists(full);
        if (exists && expectedRevision == null) throw new IOException("Destination exists; supply its expected revision to replace it.");
        if (!exists && expectedRevision != null) throw new IOException("Revision conflict: destination no longer exists.");
        string stage = Path.Combine(Path.GetDirectoryName(full)!, ".mcp-bizagi-" + Guid.NewGuid().ToString("N") + ".tmp");
        string? backup = null;
        try
        {
            using (var write = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { write.Write(bytes); write.Flush(true); }
            if (exists)
            {
                // Keep a read handle denying writes until replacement; DELETE share permits atomic replacement.
                using var guard = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                using var copy = new MemoryStream();
                guard.CopyTo(copy);
                if (BpmnDocument.Revision(copy.ToArray()) != expectedRevision) throw new IOException("Revision conflict: file changed since inspection.");
                Resolve(full);
                backup = full + "." + Guid.NewGuid().ToString("N") + ".bak";
                File.Replace(stage, full, backup);
                // External programs do not honor our mutex. Detect a racing atomic replacement and retain both versions.
                if (BpmnDocument.Revision(File.ReadAllBytes(backup)) != expectedRevision)
                    throw new IOException("Concurrent external replacement detected. Both versions are retained; inspect backup " + backup + " before retrying.");
            }
            else File.Move(stage, full, false);
            byte[] persisted = Read(full);
            if (!persisted.AsSpan().SequenceEqual(bytes)) throw new IOException("Post-write verification failed; inspect the backup before retrying.");
            return new(full, BpmnDocument.Revision(persisted), backup, persisted.LongLength);
        }
        finally { if (File.Exists(stage)) File.Delete(stage); }
    }

    public static void RequireBpmn(string path)
    {
        if (!Path.GetExtension(path).Equals(".bpmn", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("This operation requires .bpmn; native .bpm is a separate engine capability.");
    }
}
