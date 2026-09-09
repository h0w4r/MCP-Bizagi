using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;

namespace McpBizagi.Server;

/// <summary>Durable MCP-to-native requests. Connection failure is not permission to repeat a mutation.</summary>
public sealed partial class LiveWorkflows(LiveSessionClient client, ServerOptions options, Operations operations, NativeWorkflows native, LiveSessionOptions liveOptions)
{
    public OperationView Execute(string sessionId, string action, string revision = "", string diskRevision = "", LiveElementPatch[]? changes = null, string checkpointOperationId = "")
    {
        var request = new LiveSessionRequest { SessionId = sessionId, OperationId = Guid.NewGuid().ToString("D"), Action = action,
            ExpectedRevision = revision, ExpectedDiskRevision = diskRevision, Changes = changes ?? [], CheckpointOperationId = checkpointOperationId };
        LiveSessionProtocol.Validate(request);
        // Freeze a direct .NET caller's mutable array just as the native endpoint does.
        request = JsonSerializer.Deserialize<LiveSessionRequest>(JsonSerializer.Serialize(request))!;
        return operations.Start("live_" + action, async (id, progress, token) =>
        {
            request.OperationId = Guid.ParseExact(id, "N").ToString("D");
            string directory = DirectoryFor(id);
            WriteEvidence(directory, "request.json", request);
            var reply = await client.Execute(request, progress, token);
            WriteEvidence(directory, "reply.json", reply);
            if (reply.State != "completed") throw new InvalidOperationException(reply.Code + ": " + reply.Message);
            return reply;
        });
    }

    public OperationView Reconcile(string operationId)
    {
        var original = operations.Get(operationId);
        if (original.Kind is not ("live_read" or "live_update" or "live_undo" or "live_redo" or "live_checkpoint" or "live_close") || original.State is "running" or "cancelling")
            throw new InvalidOperationException("Reconciliation requires a terminal original live operation.");
        var request = JsonSerializer.Deserialize<LiveSessionRequest>(new WorkspaceFiles(DirectoryFor(operationId)).Read("request.json"))
            ?? throw new InvalidDataException("The original durable live request is unavailable.");
        LiveSessionProtocol.Validate(request);
        if (request.OperationId != Guid.ParseExact(operationId, "N").ToString("D")) throw new InvalidDataException("Live journal identity mismatch.");
        return operations.Start("live_reconcile", async (id, progress, token) =>
        {
            var receipt = request.Action == "close" ? client.RetainedCloseObservation(request)
                : await client.Receipt(request.SessionId, request.OperationId, progress, token);
            WriteEvidence(DirectoryFor(id), "reconciliation.json", new { operationId, receipt, writeReplayed = false });
            return new { originalOperationId = operationId, receipt, writeReplayed = false };
        });
    }

    private string DirectoryFor(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid operation identifier.");
        string path = new WorkspaceFiles(options.State).Resolve(Path.Combine("runs", id, "live"));
        Directory.CreateDirectory(path); return path;
    }

    private static void WriteEvidence(string directory, string name, object value)
    {
        // Immutable phase files survive cancellation between native acknowledgement and operation_get.
        string path = Path.Combine(directory, name), stage = path + ".tmp";
        using (var stream = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, value); stream.Flush(true); }
        File.Move(stage, path);
    }
}
