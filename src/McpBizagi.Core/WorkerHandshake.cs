using System.IO.Pipes;

namespace McpBizagi.Core;

/// <summary>A bounded pipe-connection step; its deadline is never a native operation duration limit.</summary>
public static class WorkerHandshake
{
    public static async Task ConnectAsync(NamedPipeClientStream pipe, TimeSpan deadline, CancellationToken operation)
    {
        using var connection = CancellationTokenSource.CreateLinkedTokenSource(operation);
        connection.CancelAfter(deadline);
        try { await pipe.ConnectAsync(connection.Token); }
        catch (OperationCanceledException error) when (!operation.IsCancellationRequested)
        {
            // The connection's own deadline is a failure, not an operator cancellation.
            throw new TimeoutException("Native worker pipe connection deadline exceeded before any engine request was dispatched.", error);
        }
    }
}
