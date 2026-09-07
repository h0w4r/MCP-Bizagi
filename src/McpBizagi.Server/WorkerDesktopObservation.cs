using System.Runtime.InteropServices;

namespace McpBizagi.Server;

/// <summary>Read-only observation of the owned worker. Never changes focus, sends input, or records other apps' titles.</summary>
internal static class WorkerDesktopObservation
{
    internal sealed record Sample(bool VisibleWindow, bool OwnsForeground);
    public static Sample Read(int pid)
    {
        bool visible = false;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner == pid && IsWindowVisible(window)) visible = true;
            return true;
        }, 0);
        GetWindowThreadProcessId(GetForegroundWindow(), out uint foregroundOwner);
        return new(visible, foregroundOwner == pid);
    }
    private delegate bool WindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowCallback callback, nint parameter);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
}
