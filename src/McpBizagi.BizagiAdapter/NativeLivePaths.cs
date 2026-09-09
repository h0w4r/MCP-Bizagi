namespace McpBizagi.BizagiAdapter;

/// <summary>Confines native working-copy writes even after a different document is opened in the editor.</summary>
public static class NativeLivePaths
{
    public static string RequireWorkingCopy(string root, string path)
    {
        string directory = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Path.IsPathRooted(path)) throw new InvalidOperationException("A native working copy must have an absolute path.");
        string file = Path.GetFullPath(path);
        if (!file.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetExtension(file).Equals(".bpm", StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
            throw new InvalidOperationException("Native checkpoints require an existing .bpm working copy inside the managed session directory.");
        if (file.Substring(directory.Length + 1).Contains(':'))
            throw new InvalidOperationException("Native working copies cannot use alternate data streams.");
        // Reject every existing reparse ancestor, not just the final filename.
        for (string? current = file; current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Native working-copy paths cannot contain reparse points.");
        return file;
    }
}
