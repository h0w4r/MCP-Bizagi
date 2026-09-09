using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows.Forms;
using McpBizagi.BizagiAdapter;
using McpBizagi.Contracts;
using Newtonsoft.Json;
using StreamJsonRpc;

namespace McpBizagi.LiveHost;

/// <summary>Companion native desktop process. Its owner is independent of any particular MCP connection.</summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(Required("MCP_BIZAGI_LIVE_DIRECTORY"));
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("The session owner must prepare its private directory.");
        using var diagnostics = new StreamWriter(Path.Combine(root, "live-host.log"), append: true) { AutoFlush = true };
        Console.SetOut(diagnostics); Console.SetError(diagnostics);
        try
        {
            if (args.Length != 1 || !File.Exists(args[0]) || !Path.GetExtension(args[0]).Equals(".bpm", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("LiveHost requires one existing native model path; owner configuration is environment-only.");
            string modelPath = Path.GetFullPath(args[0]);
            if (!modelPath.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The live owner must stage a model copy inside its session directory; direct operator originals are not accepted.");
            // Reject redirections which could turn native autosave of a working copy
            // into an unverified write to an operator document outside this session.
            for (string? path = modelPath; path != null; path = Path.GetDirectoryName(path))
                if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Live staging paths cannot contain reparse points.");
            Environment.SetEnvironmentVariable("TEMP", root); Environment.SetEnvironmentVariable("TMP", root);
            int connectionSeconds = int.TryParse(Environment.GetEnvironmentVariable("MCP_BIZAGI_CONNECTION_SECONDS"), out int n) && n > 0 ? n : 30;
            var handshake = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(root, "job-attached")))
            {
                if (handshake.Elapsed.TotalSeconds > connectionSeconds) throw new TimeoutException("Live process ownership handshake failed before native initialization.");
                Thread.Sleep(20);
            }
            if (File.ReadAllText(Path.Combine(root, "job-attached")).Trim() != Process.GetCurrentProcess().Id.ToString())
                throw new InvalidOperationException("Live ownership marker identifies another process.");
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            using var lifetime = new CancellationTokenSource();
            Task? listener = null;
            string sessionId = Required("MCP_BIZAGI_LIVE_SESSION");
            var engine = new NativeEngine(Required("BIZAGI_MODELER_PATH"), root);
            try
            {
                engine.RunLiveDesktop(sessionId, session => listener = Task.Run(async () =>
                {
                    try { await Listen(session, sessionId, root, lifetime.Token).ConfigureAwait(false); }
                    catch (Exception error) when (!lifetime.IsCancellationRequested)
                    { File.WriteAllText(Path.Combine(root, "transport-startup-error.txt"), error.ToString()); throw; }
                }),
                    phase => diagnostics.WriteLine(DateTime.UtcNow.ToString("O") + " " + phase));
            }
            finally
            {
                lifetime.Cancel();
                if (listener != null) try { listener.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
            }
            return 0;
        }
        catch (Exception error)
        { diagnostics.WriteLine(error); File.WriteAllText(Path.Combine(root, "startup-error.txt"), error.ToString()); return 1; }
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value : throw new InvalidOperationException("Missing owner configuration: " + name);

    private static async Task Listen(NativeEngine.LiveSession session, string id, string root, CancellationToken lifetime)
    {
        string pipeName = "mcp-bizagi-live-" + Guid.NewGuid().ToString("N");
        using var identity = WindowsIdentity.GetCurrent();
        // Some legitimate desktop launch tokens have no logon SID. Explicitly deny
        // network-logon tokens, then grant the current account local access only.
        var security = new PipeSecurity(); security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        using var process = Process.GetCurrentProcess();
        File.WriteAllText(Path.Combine(root, "connection.json"), JsonConvert.SerializeObject(new
        { protocolVersion = 1, sessionId = id, pipeName, processId = process.Id, startedAt = process.StartTime.ToUniversalTime(), executable = process.MainModule!.FileName, state = "starting" }));
        while (!lifetime.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous, 65536, 65536, security);
            await pipe.WaitForConnectionAsync(lifetime).ConfigureAwait(false);
            var formatter = new JsonMessageFormatter();
            formatter.JsonSerializer.MissingMemberHandling = MissingMemberHandling.Error;
            using var handler = new HeaderDelimitedMessageHandler(pipe, pipe, formatter);
            using var rpc = new JsonRpc(handler);
            rpc.AddLocalRpcMethod("execute", new Func<LiveSessionRequest, CancellationToken, Task<LiveSessionReply>>(session.ExecuteAsync));
            rpc.AddLocalRpcMethod("receipt", new Func<string, CancellationToken, Task<LiveSessionReply>>(session.ReceiptAsync));
            rpc.StartListening();
            using var stop = lifetime.Register(() => rpc.Dispose());
            try { await rpc.Completion.ConfigureAwait(false); }
            catch (Exception error) when (!lifetime.IsCancellationRequested)
            { File.AppendAllText(Path.Combine(root, "transport-errors.log"), error + Environment.NewLine); }
            // Never close the native editor or discard its state after an MCP disconnect.
        }
    }
}
