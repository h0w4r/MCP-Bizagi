using System.ComponentModel;
using System.Text.Json;
using McpBizagi.Contracts;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpBizagi.Server;

/// <summary>Fixed live-session use cases; no executable selection, script execution or arbitrary reflection.</summary>
[McpServerToolType]
public sealed class LiveTools(LiveWorkflows live)
{
    [McpServerTool(Name = "live_open", OpenWorld = false), Description("Open an existing workspace .bpm in a dedicated visible native editor using a staged copy and an independent current-user Windows owner. Requires source SHA-256. Creates an on-demand Task Scheduler definition, no password, elevation or recurring trigger. Waits for real native readiness. Cancellation does not close the independent editor; inspect live_sessions_list before retrying launch. Poll operation_get.")]
    public CallToolResult Open(string path, string expectedRevision) => Guard(() => live.Open(path, expectedRevision));

    [McpServerTool(Name = "live_sessions_list", ReadOnly = true, Destructive = false, OpenWorld = false), Description("List retained managed session launch identities and observed lifecycle status in the configured registry. Never attach to arbitrary Modeler processes or relaunch interrupted sessions. Running process presence alone is not document readiness.")]
    public CallToolResult Sessions() => Guard(live.ListSessions);

    private static CallToolResult Guard(Func<object> action)
    {
        object result; bool error = false;
        try { result = action(); }
        catch (Exception failure) { result = new { error = failure.Message }; error = true; }
        return new() { IsError = error, StructuredContent = JsonSerializer.SerializeToElement(result),
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(result) }] };
    }

    [McpServerTool(Name = "live_read", Destructive = false, OpenWorld = false), Description("Experimental managed live editor read, including synchronization of pending canvas labels into the native in-memory model. Requires an independently running configured companion and its session UUID. This may complete a pending native label edit; it is not a disk-only read. Poll operation_get.")]
    public CallToolResult Read(string sessionId) => Guard(() => live.Execute(sessionId, "read"));

    [McpServerTool(Name = "live_apply", OpenWorld = false), Description("Apply explicit Name/Documentation batches to graphical elements in the managed editor's active diagram, using native commands and an observed live revision. No file save. Null preserves a property; empty text clears it. Native batch failure can be uncertain, not rolled back. Poll operation_get; reconcile after interruption.")]
    public CallToolResult Apply(string sessionId, string expectedRevision, LiveElementPatch[] changes)
        => Guard(() => live.Execute(sessionId, "update", expectedRevision, changes: changes));

    [McpServerTool(Name = "live_history", OpenWorld = false), Description("Execute undo or redo in the managed native editor and await the native callback. Requires the current live revision; does not save. Poll operation_get.")]
    public CallToolResult History(string sessionId, string expectedRevision, string action) => Guard(() =>
    {
        if (action is not "undo" and not "redo") throw new ArgumentException("History action must be undo or redo.");
        return live.Execute(sessionId, action, expectedRevision);
    });

    [McpServerTool(Name = "live_checkpoint", OpenWorld = false), Description("Save only the managed native working copy, retain its previous complete archive and a durable checkpoint artifact. Requires current live and working-copy disk revisions. Native Save clears undo and stops this companion's autosave timer. Does NOT publish to the original destination or independently accredit fidelity. Poll operation_get; reconcile interruptions instead of retrying.")]
    public CallToolResult Checkpoint(string sessionId, string expectedRevision, string expectedDiskRevision)
        => Guard(() => live.Execute(sessionId, "checkpoint", expectedRevision, expectedDiskRevision));

    [McpServerTool(Name = "live_reconcile", Destructive = false, OpenWorld = false), Description("Query the retained receipt of a terminal live operation after cancellation, disconnect or MCP restart. Never dispatches the original request again. An unknown receipt is not proof that an action did not happen. Editor operations require the same running session; close uses the durable OS exit observation without a live pipe. Poll operation_get.")]
    public CallToolResult Reconcile(string operationId) => Guard(() => live.Reconcile(operationId));

    [McpServerTool(Name = "live_publish", Destructive = true, OpenWorld = false), Description("Publish an exact retained native checkpoint to an existing workspace .bpm using its expected destination SHA-256. Supply the complete expected Name/Documentation differences from that destination; empty changes requires semantic equivalence. Rejects unexplained whole-archive differences, preserves a backup, and verifies through fresh native readers. Never changes the live editor or publishes later unsaved edits. Poll operation_get; after interruption use native_commit_reconcile, not another write.")]
    public CallToolResult Publish(string checkpointOperationId, string destinationPath, string expectedDestinationRevision, LiveElementPatch[] expectedChanges)
        => Guard(() => live.Publish(checkpointOperationId, destinationPath, expectedDestinationRevision, expectedChanges));

    [McpServerTool(Name = "live_close", Destructive = true, OpenWorld = false), Description("Close only the managed native editor after validating a current clean checkpoint, live revision and disk SHA-256. Requires the checkpoint MCP operation ID. Refuses dirty/stale/unretained state, uses native closing and verifies actual editor exit and retained file hashes. Sessions from live_open also require independent-owner job cleanup and task removal. Does not publish to the original or force-kill the editor. Poll operation_get; reconcile interruptions instead of replaying close.")]
    public CallToolResult Close(string sessionId, string expectedRevision, string expectedDiskRevision, string checkpointOperationId) => Guard(() =>
    {
        if (!Guid.TryParseExact(checkpointOperationId, "N", out var id) || id == Guid.Empty) throw new ArgumentException("A checkpoint MCP operation ID is required.");
        return live.Execute(sessionId, "close", expectedRevision, expectedDiskRevision, checkpointOperationId: id.ToString("D"));
    });
}
