using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using McpBizagi.Contracts;
using StreamJsonRpc;

namespace McpBizagi.Server;

/// <summary>Owns only its child process tree. Long-running calls are limited by inactivity, not duration.</summary>
public sealed class WorkerClient(ServerOptions options)
{
    private readonly SemaphoreSlim serial = new(1, 1);
    private sealed class Notifications(Action<string> report)
    {
        [JsonRpcMethod("phase")]
        public void Phase(string phase) => report(phase);
    }

    public async Task<EngineReply> Execute(EngineRequest request, string directory, Action<string> report, CancellationToken token)
    {
        await serial.WaitAsync(token);
        try { return await ExecuteCore(request, directory, report, token); }
        finally { serial.Release(); }
    }

    private async Task<EngineReply> ExecuteCore(EngineRequest request, string directory, Action<string> report, CancellationToken token)
    {
        if (!options.ExperimentalNative) throw new InvalidOperationException("Experimental native operations are disabled. Enable MCP_BIZAGI_EXPERIMENTAL_NATIVE explicitly.");
        if (options.Installation == null) throw new FileNotFoundException("Bizagi Modeler installation not found.");
        if (!File.Exists(options.Worker)) throw new FileNotFoundException("Worker not found; set MCP_BIZAGI_WORKER or use a release package.");
        Directory.CreateDirectory(directory);
        string pipeName = "mcp-bizagi-" + Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo(Path.GetFullPath(options.Worker))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true,
            WorkingDirectory = directory
        };
        start.ArgumentList.Add(Path.GetFullPath(options.Installation)); start.ArgumentList.Add(directory); start.ArgumentList.Add(pipeName);
        start.Environment["TEMP"] = directory; start.Environment["TMP"] = directory;
        using var job = new WorkerJob();
        using var process = Process.Start(start) ?? throw new IOException("Worker could not start.");
        var errors = new StringBuilder();
        var stdout = new StringBuilder();
        var desktopSamples = new List<WorkerDesktopObservation.Sample>();
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (errors) errors.AppendLine(e.Data); };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (stdout) stdout.AppendLine(e.Data); };
        process.BeginErrorReadLine(); process.BeginOutputReadLine();
        report("worker_started");
        try
        {
            // Assign before sending native work; the worker is only waiting for its pipe at this point.
            job.Assign(process);
            desktopSamples.Add(WorkerDesktopObservation.Read(process.Id));
            File.WriteAllText(Path.Combine(directory, "worker-process.json"), System.Text.Json.JsonSerializer.Serialize(new
            { pid = process.Id, startedAt = process.StartTime.ToUniversalTime(), createNoWindow = true, jobObject = true }));
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(token);
            handshake.CancelAfter(TimeSpan.FromSeconds(30)); // Connection only, never the operation duration.
            await pipe.ConnectAsync(handshake.Token);
            long lastActivity = Stopwatch.GetTimestamp();
            var notifications = new Notifications(phase => { Interlocked.Exchange(ref lastActivity, Stopwatch.GetTimestamp()); report(phase); });
            using var rpc = new JsonRpc(pipe, pipe);
            rpc.AddLocalRpcTarget(notifications); rpc.StartListening();
            var invocation = rpc.InvokeAsync<EngineReply>("Execute", request);
            TimeSpan previousCpu = process.TotalProcessorTime;
            int previousLogLength = 0;
            while (!invocation.IsCompleted)
            {
                await Task.WhenAny(invocation, Task.Delay(1000, token));
                token.ThrowIfCancellationRequested();
                if (invocation.IsCompleted) break;
                process.Refresh();
                if (process.HasExited) throw new IOException("Worker exited before returning a result.");
                var desktop = WorkerDesktopObservation.Read(process.Id);
                desktopSamples.Add(desktop);
                if (desktop.VisibleWindow || desktop.OwnsForeground)
                    throw new InvalidOperationException("Native worker unexpectedly exposed a visible window or acquired foreground; diagnostic stopped.");
                TimeSpan cpu = process.TotalProcessorTime;
                int length; lock (errors) length = errors.Length;
                if (cpu > previousCpu || length != previousLogLength)
                    Interlocked.Exchange(ref lastActivity, Stopwatch.GetTimestamp());
                previousCpu = cpu; previousLogLength = length;
                if (Stopwatch.GetElapsedTime(Interlocked.Read(ref lastActivity)).TotalSeconds > options.InactivitySeconds)
                    throw new TimeoutException("Native worker inactivity window exceeded with no CPU, phase or diagnostic-log activity.");
            }
            return await invocation;
        }
        finally
        {
            // Closing the RPC pipe normally ends the worker. Force cleanup is limited to this owned process tree.
            if (!process.HasExited)
            {
                var exited = process.WaitForExitAsync();
                if (await Task.WhenAny(exited, Task.Delay(3000)) != exited)
                { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
            }
            process.WaitForExit();
            File.WriteAllText(Path.Combine(directory, "worker-exit.json"), System.Text.Json.JsonSerializer.Serialize(new
            { pid = process.Id, exited = process.HasExited, exitCode = process.ExitCode }));
            File.WriteAllText(Path.Combine(directory, "desktop-observation.json"), System.Text.Json.JsonSerializer.Serialize(new
            { samples = desktopSamples.Count, visibleWindowObserved = desktopSamples.Any(s => s.VisibleWindow),
                workerForegroundObserved = desktopSamples.Any(s => s.OwnsForeground), method = "read_only_periodic_owned_pid_observation_not_continuous_proof" }));
            lock (errors) File.WriteAllText(Path.Combine(directory, "worker.stderr.log"), errors.ToString());
            lock (stdout) File.WriteAllText(Path.Combine(directory, "worker.stdout.log"), stdout.ToString());
        }
    }
}
