using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Microsoft.Win32.SafeHandles;
using StreamJsonRpc;

namespace McpBizagi.Server;

/// <summary>Operator-configured live endpoints. MCP callers cannot choose executable or registry paths.</summary>
public sealed record LiveSessionOptions(string Root, string Executable, string OwnerExecutable = "")
{
    public static LiveSessionOptions FromEnvironment(ServerOptions options) => new(
        Environment.GetEnvironmentVariable("MCP_BIZAGI_LIVE_ROOT") ?? Path.Combine(options.State, "live"),
        Environment.GetEnvironmentVariable("MCP_BIZAGI_LIVE_HOST") ?? Path.Combine(AppContext.BaseDirectory, "live", "McpBizagi.LiveHost.exe"),
        Environment.GetEnvironmentVariable("MCP_BIZAGI_LIVE_OWNER") ?? Path.Combine(AppContext.BaseDirectory, "owner", "McpBizagi.LiveOwner.exe"));
}

/// <summary>A connection record is a locator, not evidence that the document is ready or the pipe belongs to it.</summary>
public sealed record LiveConnection(int ProtocolVersion, string SessionId, string PipeName, int ProcessId, DateTimeOffset StartedAt, string Executable, string State);

/// <summary>Connects to an independently owned editor; never kills it on cancellation or transport disposal.</summary>
public sealed partial class LiveSessionClient(LiveSessionOptions live, ServerOptions options)
{
    private static readonly JsonSerializerOptions DescriptorJson = new() { PropertyNameCaseInsensitive = true };
    public string SessionDirectory(string sessionId)
    {
        if (!Guid.TryParseExact(sessionId, "D", out var id) || id == Guid.Empty) throw new ArgumentException("A canonical live session UUID is required.");
        return new WorkspaceFiles(live.Root).Resolve(id.ToString("D"));
    }

    public Task<LiveSessionReply> Execute(LiveSessionRequest request, Action<string> progress, CancellationToken token)
    {
        LiveSessionProtocol.Validate(request);
        return Invoke(request.SessionId, request.OperationId, "execute", request, progress, token);
    }

    public Task<LiveSessionReply> Receipt(string sessionId, string operationId, Action<string> progress, CancellationToken token)
    {
        if (!Guid.TryParseExact(operationId, "D", out var id) || id == Guid.Empty) throw new ArgumentException("A canonical live operation UUID is required.");
        // Query a retained receipt; NEVER send execute again to infer an interrupted write's outcome.
        return Invoke(sessionId, operationId, "receipt", operationId, progress, token);
    }

    private async Task<LiveSessionReply> Invoke(string sessionId, string operationId, string method, object parameter, Action<string> progress, CancellationToken token)
    {
        if (!options.ExperimentalNative) throw new InvalidOperationException("Experimental native operations are disabled.");
        string directory = SessionDirectory(sessionId);
        var files = new WorkspaceFiles(directory);
        var descriptor = JsonSerializer.Deserialize<LiveConnection>(files.Read("connection.json"), DescriptorJson)
            ?? throw new InvalidDataException("Empty live connection descriptor.");
        if (descriptor.ProtocolVersion != 1 || !sessionId.Equals(descriptor.SessionId, StringComparison.OrdinalIgnoreCase) ||
            descriptor.ProcessId <= 0 || descriptor.PipeName == null || !descriptor.PipeName.StartsWith("mcp-bizagi-live-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(descriptor.PipeName[16..], "N", out _) ||
            !Path.GetFullPath(descriptor.Executable).Equals(Path.GetFullPath(live.Executable), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Live descriptor does not match the configured session and executable.");
        using var process = Process.GetProcessById(descriptor.ProcessId);
        _ = process.SafeHandle; // Pin identity so a recycled PID cannot pass later checks.
        if (process.HasExited || process.StartTime.ToUniversalTime() != descriptor.StartedAt.UtcDateTime ||
            !string.Equals(process.MainModule?.FileName, Path.GetFullPath(live.Executable), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The recorded native editor process is no longer this session.");
        progress("live_connecting");
        using var pipe = new NamedPipeClientStream(".", descriptor.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using (var connection = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            connection.CancelAfter(TimeSpan.FromSeconds(options.ConnectionSeconds));
            try { await pipe.ConnectAsync(connection.Token); }
            catch (OperationCanceledException error) when (!token.IsCancellationRequested)
            { throw new TimeoutException("Initial live pipe handshake expired before request dispatch; the independent editor was not stopped.", error); }
        }
        if (GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint actual) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        if (actual != descriptor.ProcessId || process.HasExited) throw new InvalidOperationException("Live pipe server process does not match the pinned native editor.");
        using var rpc = new JsonRpc(pipe, pipe); rpc.StartListening();
        progress("live_connected_identity_verified");
        // Pin the independent owner before dispatching Close. An absent PID after
        // the fact cannot substitute for this identity or an owned-job exit journal.
        using var owner = parameter is LiveSessionRequest { Action: "close" } ? PinCloseOwner(files, descriptor) : null;
        var invocation = rpc.InvokeWithCancellationAsync<LiveSessionReply>(method, [parameter], token);
        if (parameter is LiveSessionRequest { Action: "close" } close)
            return await ObserveClose(close, descriptor, process, owner, invocation, progress, token);
        // Evidence files track actual native phases/callbacks, not an invented percentage.
        // Inactivity only disconnects our request; it never terminates the live editor.
        string previous = ""; var inactivity = Stopwatch.StartNew();
        TimeSpan previousCpu = process.TotalProcessorTime;
        while (!invocation.IsCompleted)
        {
            await Task.WhenAny(invocation, Task.Delay(250, token));
            token.ThrowIfCancellationRequested();
            if (process.HasExited) throw new IOException("Native editor exited during the request; reconcile its receipt before any further write.");
            string activity = string.Join(";", Directory.EnumerateFiles(directory, operationId + ".*").Order(StringComparer.Ordinal)
                .Select(file => { var info = new FileInfo(file); return info.Name + ":" + info.Length + ":" + info.LastWriteTimeUtc.Ticks; }));
            if (activity != previous) { previous = activity; inactivity.Restart(); progress("live_native_evidence_changed"); }
            // CPU activity is not reported as semantic progress, but a busy native
            // Save must not be disconnected just because no phase file was flushed.
            process.Refresh();
            TimeSpan cpu = process.TotalProcessorTime;
            if (cpu != previousCpu) { previousCpu = cpu; inactivity.Restart(); }
            if (inactivity.Elapsed.TotalSeconds > options.InactivitySeconds)
                throw new TimeoutException("No operation evidence advanced within the live inactivity window. The editor remains open; query the receipt instead of replaying the request.");
        }
        var reply = await invocation;
        ValidateReplyIdentity(reply, sessionId, operationId);
        progress("live_reply_received");
        return reply;
    }

    private static void ValidateReplyIdentity(LiveSessionReply reply, string sessionId, string operationId)
    {
        if (reply.OperationId != operationId || (reply.Snapshot != null && !sessionId.Equals(reply.Snapshot.SessionId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Native reply identity does not match the requested operation/session.");
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);
}
