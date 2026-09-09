using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using McpBizagi.BizagiAdapter;
using McpBizagi.Contracts;
using Newtonsoft.Json;
using StreamJsonRpc;

namespace McpBizagi.Worker;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try { return RunAsync(args).GetAwaiter().GetResult(); }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 3) throw new ArgumentException("Usage: worker <installation> <work-root> <pipe-name|--probe>");
        // Configure child-only temporary storage before loading any native assembly.
        Directory.CreateDirectory(args[1]);
        Environment.SetEnvironmentVariable("TEMP", args[1]);
        Environment.SetEnvironmentVariable("TMP", args[1]);
        File.WriteAllText(Path.Combine(args[1], "worker-temp-path.txt"), Path.GetFullPath(Path.GetTempPath()));
        // Keep the original long-path error when a native compatibility fallback replaces it
        // with an unrelated old-format/empty-document exception. Diagnostics stay local.
        int pathFailures = 0;
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            if (e.Exception is not PathTooLongException || Interlocked.Increment(ref pathFailures) > 16) return;
            try { File.WriteAllText(Path.Combine(args[1], "native-path-length-" + pathFailures + ".txt"), e.Exception.ToString()); }
            catch (IOException) { /* Preserve the original exception if diagnostic storage also fails. */ }
            catch (UnauthorizedAccessException) { /* Diagnostics must not replace the engine error. */ }
        };
        AppContext.TryGetSwitch("Switch.System.IO.UseLegacyPathHandling", out bool legacyPaths);
        AppContext.TryGetSwitch("Switch.System.IO.BlockLongPaths", out bool blockLongPaths);
        File.WriteAllText(Path.Combine(args[1], "worker-path-policy.json"), JsonConvert.SerializeObject(new { legacyPaths, blockLongPaths, target = AppContext.TargetFrameworkName }));
        var service = new EngineService(args[0], args[1]);
        if (args[2] == "--probe")
        {
            var reply = service.Execute(new EngineRequest { OperationId = Guid.NewGuid().ToString("N") });
            Console.WriteLine(JsonConvert.SerializeObject(reply));
            return reply.Success ? 0 : 2;
        }
        // Access is granted only to the current Windows identity; there is no network listener.
        var acl = new PipeSecurity();
        acl.SetAccessRuleProtection(true, false);
        acl.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        using var pipe = new NamedPipeServerStream(args[2], PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous, 65536, 65536, acl);
        await pipe.WaitForConnectionAsync();
        using var rpc = new JsonRpc(pipe, pipe);
        rpc.AddLocalRpcTarget(service);
        service.Progress = phase =>
        {
            Console.Error.WriteLine("phase=" + phase);
            // A notification acknowledges transport only. Fast native work could
            // finish while durable host phase handlers were still queued, hiding
            // the live cancellation boundary. Await the existing RPC handler's
            // response so the operator can observe the phase before work advances.
            rpc.InvokeAsync("phase", phase).GetAwaiter().GetResult();
        };
        rpc.StartListening();
        await rpc.Completion;
        return 0;
    }
}

/// <summary>Only one native call is admitted at a time; failures remain explicit data.</summary>
public sealed class EngineService
{
    private readonly string installation;
    private readonly string root;
    private readonly object sync = new();
    private NativeEngine? engine;
    public Action<string> Progress { get; set; } = phase => Console.Error.WriteLine("phase=" + phase);
    public EngineService(string installation, string root) { this.installation = installation; this.root = root; }

    public EngineReply Execute(EngineRequest request)
    {
        lock (sync)
        {
            try
            {
                engine ??= new NativeEngine(installation, root);
                var result = engine.Execute(request, Progress);
                // Record the actual loaded vendor modules, not merely files discovered on disk.
                string prefix = Path.GetFullPath(installation).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var modules = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.Location.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(a =>
                    {
                        using var hash = SHA256.Create(); using var file = File.OpenRead(a.Location); return new
                        { name = Path.GetFileName(a.Location), version = a.GetName().Version?.ToString(), sha256 = BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").ToLowerInvariant() };
                    }).ToArray();
                File.WriteAllText(Path.Combine(root, "loaded-engine-modules.json"), JsonConvert.SerializeObject(new
                { architecture = IntPtr.Size == 8 ? "x64" : "x86", clr = Environment.Version.ToString(), apartment = Thread.CurrentThread.GetApartmentState().ToString(), modules }, Formatting.Indented));
                if (request.Action is "render_svg" or "align_save" or "anchor_preview" or "port_query" || (request.Action == "publish" && request.PublicationFormat != "excel"))
                {
                    // Inventory the installed renderer assets separately from actually loaded managed modules.
                    var assets = Directory.GetFiles(Path.Combine(installation, "ModelerProcessEditor", "output"))
                        .Select(path =>
                        {
                            using var hash = SHA256.Create(); using var file = File.OpenRead(path); return new
                            { name = Path.GetFileName(path), sha256 = BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").ToLowerInvariant() };
                        }).ToArray();
                    File.WriteAllText(Path.Combine(root, "renderer-assets.json"), JsonConvert.SerializeObject(new
                    { scope = "installed_renderer_inventory_not_network_response_capture", assets }, Formatting.Indented));
                }
                return result;
            }
            catch (Exception error)
            {
                while (error is System.Reflection.TargetInvocationException && error.InnerException != null) error = error.InnerException;
                // Detailed diagnostics are local only and never masquerade as successful native results.
                Console.Error.WriteLine(error);
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "native-error.txt"), error.ToString());
                return new EngineReply
                {
                    OperationId = request.OperationId,
                    Success = false,
                    Code = "native_engine_error",
                    Message = error.GetType().Name + ": " + error.Message,
                    EngineVersion = engine?.Version ?? "unknown"
                };
            }
        }
    }
}
