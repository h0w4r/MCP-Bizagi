using Microsoft.Win32;

namespace McpBizagi.Server;

/// <summary>Operator-owned configuration; clients cannot select executables or engine paths.</summary>
public sealed record ServerOptions(string Workspace, string State, string? Installation, string Worker, bool ExperimentalNative, int InactivitySeconds)
{
    public static ServerOptions FromEnvironment()
    {
        string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MCP-Bizagi");
        return new(Environment.GetEnvironmentVariable("MCP_BIZAGI_ROOT") ?? Path.Combine(local, "workspace"),
            Environment.GetEnvironmentVariable("MCP_BIZAGI_STATE") ?? Path.Combine(local, "state"),
            Environment.GetEnvironmentVariable("BIZAGI_MODELER_PATH") ?? FindInstallation(),
            Environment.GetEnvironmentVariable("MCP_BIZAGI_WORKER") ?? Path.Combine(AppContext.BaseDirectory, "worker", "McpBizagi.Worker.exe"),
            Environment.GetEnvironmentVariable("MCP_BIZAGI_EXPERIMENTAL_NATIVE") == "1",
            int.TryParse(Environment.GetEnvironmentVariable("MCP_BIZAGI_INACTIVITY_SECONDS"), out int seconds) && seconds >= 10 ? seconds : 120);
    }

    private static string? FindInstallation()
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall == null) continue;
                foreach (string name in uninstall.GetSubKeyNames())
                {
                    using var entry = uninstall.OpenSubKey(name);
                    if (entry?.GetValue("DisplayName") is string display && display.Contains("Bizagi Modeler", StringComparison.OrdinalIgnoreCase)
                        && entry.GetValue("InstallLocation") is string path && File.Exists(Path.Combine(path, "BizagiModeler.exe"))) return path;
                }
            }
        return null;
    }
}
