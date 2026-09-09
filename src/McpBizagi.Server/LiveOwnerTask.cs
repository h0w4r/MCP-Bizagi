using System.Runtime.InteropServices;
using System.Security.Principal;
using McpBizagi.Core;

namespace McpBizagi.Server;

/// <summary>Fixed on-demand Windows activation. No time trigger, password, elevation or caller-provided command.</summary>
internal static class LiveOwnerTask
{
    public static void Launch(LiveOwnerRequest request)
    {
        using var objects = new ComObjects();
        dynamic service = objects.Keep(Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true)!)!);
        service.Connect();
        dynamic folder = objects.Keep(service.GetFolder("\\"));
        dynamic definition = objects.Keep(service.NewTask(0));
        dynamic information = objects.Keep(definition.RegistrationInfo);
        information.Author = "h0w4r";
        information.Description = "MCP-Bizagi managed live session " + request.SessionId + ". On-demand only; do not stop while work is unsaved.";
        dynamic principal = objects.Keep(definition.Principal);
        principal.UserId = request.UserSid; principal.LogonType = 3; principal.RunLevel = 0;
        dynamic settings = objects.Keep(definition.Settings);
        settings.Enabled = true; settings.AllowDemandStart = true;
        // Scheduler defaults to background priority 7. A real interactive editor
        // and Chromium need ordinary priority, not starvation under desktop load.
        settings.Priority = 4; // NORMAL_PRIORITY_CLASS; never elevated/high priority.
        settings.ExecutionTimeLimit = "PT0S"; // Override the scheduler's default 72-hour termination.
        settings.DisallowStartIfOnBatteries = false; settings.StopIfGoingOnBatteries = false;
        settings.RunOnlyIfIdle = false; settings.RunOnlyIfNetworkAvailable = false;
        settings.MultipleInstances = 2; settings.AllowHardTerminate = false;
        dynamic actions = objects.Keep(definition.Actions);
        dynamic action = objects.Keep(actions.Create(0));
        action.Path = request.OwnerExecutable;
        action.Arguments = Quote(request.Directory);
        action.WorkingDirectory = Path.GetDirectoryName(request.OwnerExecutable);
        // Create-only: never modify an existing task. SYSTEM retains scheduler access.
        string acl = "D:P(A;;FA;;;SY)(A;;FA;;;" + request.UserSid + ")";
        dynamic registered = objects.Keep(folder.RegisterTaskDefinition(request.TaskName, definition, 2, request.UserSid, null, 3, acl));
        LiveOwnerProtocol.WriteNew(request.Directory, "task-registered.json", new { request.TaskName, request.UserSid, at = DateTimeOffset.UtcNow });
        dynamic running = objects.Keep(registered.Run(null));
        LiveOwnerProtocol.WriteNew(request.Directory, "task-started.json", new { request.TaskName, instanceGuid = (string)running.InstanceGuid, at = DateTimeOffset.UtcNow });
    }

    public static void RemoveOwnDefinition(LiveOwnerRequest request)
    {
        using var objects = new ComObjects();
        dynamic service = objects.Keep(Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true)!)!);
        service.Connect();
        dynamic folder = objects.Keep(service.GetFolder("\\"));
        dynamic task = objects.Keep(folder.GetTask(request.TaskName));
        dynamic definition = objects.Keep(task.Definition);
        dynamic principal = objects.Keep(definition.Principal);
        dynamic actions = objects.Keep(definition.Actions);
        dynamic action = objects.Keep(actions.Item(1));
        string principalId = (string)principal.UserId;
        string principalSid = principalId.StartsWith("S-1-", StringComparison.Ordinal) ? new SecurityIdentifier(principalId).Value
            : ((SecurityIdentifier)new NTAccount(principalId).Translate(typeof(SecurityIdentifier))).Value;
        if ((int)actions.Count != 1 || principalSid != request.UserSid ||
            !string.Equals((string)action.Path, request.OwnerExecutable, StringComparison.OrdinalIgnoreCase) || (string)action.Arguments != Quote(request.Directory))
            throw new InvalidDataException("Registered task changed; refusing to remove an unrecognized definition.");
        folder.DeleteTask(request.TaskName, 0);
    }

    private static string Quote(string value)
    {
        if (value.Contains('"') || value.EndsWith('\\')) throw new ArgumentException("Invalid owner argument path.");
        return "\"" + value + "\"";
    }

    private sealed class ComObjects : IDisposable
    {
        private readonly List<object> values = [];
        public dynamic Keep(object value) { values.Add(value); return value; }
        public void Dispose()
        {
            // Each RCW here was obtained by this operation; release in reverse dependency order.
            for (int i = values.Count - 1; i >= 0; --i) if (Marshal.IsComObject(values[i])) Marshal.ReleaseComObject(values[i]);
        }
    }
}
