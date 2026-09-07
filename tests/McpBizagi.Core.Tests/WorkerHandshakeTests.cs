using System.IO.Pipes;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Real OS pipe component tests. These do not accredit a native engine operation.</summary>
public sealed class WorkerHandshakeTests
{
    [Fact]
    public async Task ConnectionDeadlineIsTimeoutNotOperatorCancellation()
    {
        using var pipe = new NamedPipeClientStream(".", "mcp-bizagi-test-" + Guid.NewGuid().ToString("N"), PipeDirection.InOut, PipeOptions.Asynchronous);
        var error = await Assert.ThrowsAsync<TimeoutException>(() => WorkerHandshake.ConnectAsync(pipe, TimeSpan.FromMilliseconds(40), CancellationToken.None));
        Assert.Contains("before any engine request", error.Message);
    }

    [Fact]
    public async Task ExplicitCancellationRetainsCancellationSemantics()
    {
        using var pipe = new NamedPipeClientStream(".", "mcp-bizagi-test-" + Guid.NewGuid().ToString("N"), PipeDirection.InOut, PipeOptions.Asynchronous);
        using var operation = new CancellationTokenSource(); operation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WorkerHandshake.ConnectAsync(pipe, TimeSpan.FromSeconds(30), operation.Token));
    }

    [Fact]
    public async Task ConnectedPipeOutlivesItsInitialDeadline()
    {
        string name = "mcp-bizagi-test-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        var accept = server.WaitForConnectionAsync();
        await WorkerHandshake.ConnectAsync(client, TimeSpan.FromSeconds(1), CancellationToken.None); await accept;
        // Only this component test waits past a connection deadline; live work has no total timeout.
        await Task.Delay(1100);
        // Issue the peer read first: Windows byte pipes may not complete a write until
        // a reader consumes it. This atomic exchange has its own diagnostic deadline.
        using var exchange = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var bytes = new byte[1]; var read = server.ReadAsync(bytes, exchange.Token);
        await client.WriteAsync(new byte[] { 37 }, exchange.Token);
        Assert.Equal(1, await read); Assert.Equal(37, bytes[0]);
    }
}
