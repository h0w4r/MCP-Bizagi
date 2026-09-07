using Microsoft.Win32;

namespace McpBizagi.Server;

/// <summary>Operator-owned configuration; clients cannot select executables or engine paths.</summary>
public sealed record ServerOptions(string Workspace, string State, string? Installation, string Worker, bool ExperimentalNative, int InactivitySeconds,
    int ConnectionSeconds = 30, int CleanupSeconds = 3, int AtomicStepSeconds = 30)
{
    public static ServerOptions FromEnvironment()
    {
        string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MCP-Bizagi");
        return new(Environment.GetEnvironmentVariable("MCP_BIZAGI_ROOT") ?? Path.Combine(local, "workspace"),
            Environment.GetEnvironmentVariable("MCP_BIZAGI_STATE") ?? Path.Combine(local, "state"),
            Environment.GetEnvironmentVariable("BIZAGI_MODELER_PATH") ?? FindInstallation(),
            Environment.GetEnvironmentVariable("MCP_BIZAGI_WORKER") ?? Path.Combine(AppContext.BaseDirectory, "worker", "McpBizagi.Worker.exe"),
            Environment.GetEnvironmentVariable("MCP_BIZAGI_EXPERIMENTAL_NATIVE") == "1",
            Seconds("MCP_BIZAGI_INACTIVITY_SECONDS", 120, 10), Seconds("MCP_BIZAGI_CONNECTION_SECONDS", 30, 1),
            Seconds("MCP_BIZAGI_CLEANUP_SECONDS", 3, 1), Seconds("MCP_BIZAGI_ATOMIC_STEP_SECONDS", 30, 1));
    }
    private static int Seconds(string name, int fallback, int minimum) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int seconds) && seconds >= minimum ? seconds : fallback;

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
