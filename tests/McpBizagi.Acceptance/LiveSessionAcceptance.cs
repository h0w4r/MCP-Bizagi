using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

/// <summary>Actual stdio MCP calls into an independently launched native companion, never server implementation calls.</summary>
internal static class LiveSessionAcceptance
{
    internal static async Task Run(string repo, string sessionId, string liveRoot, string run)
    {
        var env = new Dictionary<string, string?>
        {
            ["MCP_BIZAGI_ROOT"] = run, ["MCP_BIZAGI_STATE"] = Path.Combine(run, "state"),
            ["MCP_BIZAGI_LIVE_ROOT"] = liveRoot, ["MCP_BIZAGI_EXPERIMENTAL_NATIVE"] = "1",
            ["MCP_BIZAGI_LIVE_HOST"] = Path.Combine(repo, "src", "McpBizagi.LiveHost", "bin", "Release", "net48", "McpBizagi.LiveHost.exe"),
            ["MCP_BIZAGI_WORKER"] = Path.Combine(repo, "src", "McpBizagi.Worker", "bin", "Release", "net48", "McpBizagi.Worker.exe")
        };
        StdioClientTransport Transport() => new(new StdioClientTransportOptions { Name = "MCP-Bizagi live acceptance", Command = "dotnet",
            Arguments = [Path.Combine(repo, "src", "McpBizagi.Server", "bin", "Release", "net10.0-windows", "McpBizagi.Server.dll")], EnvironmentVariables = env });
        var transcript = new List<object>();
        async Task<JsonElement> Call(McpClient client, string tool, Dictionary<string, object?> input, bool expectError = false)
        {
            var result = await client.CallToolAsync(tool, input);
            string text = result.Content.OfType<TextContentBlock>().First().Text;
            transcript.Add(new { tool, input, result.IsError, text });
            File.WriteAllText(Path.Combine(run, "live-mcp-transcript.json"), JsonSerializer.Serialize(transcript));
            if ((result.IsError == true) != expectError) throw new InvalidOperationException(tool + ": " + text);
            return JsonSerializer.Deserialize<JsonElement>(text);
        }
        async Task<JsonElement> Wait(McpClient client, JsonElement operation, string expected = "completed")
        {
            string id = operation.GetProperty("OperationId").GetString()!, phase = "";
            while (true)
            {
                var view = await Call(client, "operation_get", new() { ["operationId"] = id });
                string current = view.GetProperty("Phase").GetString()!;
                if (current != phase) { Console.WriteLine("live MCP phase=" + current); phase = current; }
                string state = view.GetProperty("State").GetString()!;
                if (state == expected) return view;
                if (state is "completed" or "failed" or "cancelled" or "interrupted") throw new InvalidOperationException(view.GetRawText());
                await Task.Delay(200);
            }
        }
        async Task<JsonElement> Execute(McpClient client, string tool, Dictionary<string, object?> input, string state = "completed")
            => await Wait(client, await Call(client, tool, input), state);
        static JsonElement Snapshot(JsonElement operation) => operation.GetProperty("Result").GetProperty("Snapshot");
        static string Revision(JsonElement snapshot) => snapshot.GetProperty("Revision").GetString()!;
        static string Name(JsonElement snapshot, string id) => snapshot.GetProperty("Elements").EnumerateArray()
            .Single(e => e.GetProperty("Id").GetString() == id).GetProperty("Name").GetString()!;
        string editedRevision, elementId, diskRevision, updateId;
        string name = "MCP live unsaved Ω " + Guid.NewGuid().ToString("N")[..8];
        await using (var client = await McpClient.CreateAsync(Transport()))
        {
            await Call(client, "live_read", new() { ["sessionId"] = "../not-a-session" }, true);
            await Call(client, "live_history", new() { ["sessionId"] = sessionId, ["expectedRevision"] = "not-dispatched", ["action"] = "execute" }, true);
            await Call(client, "live_checkpoint", new() { ["sessionId"] = sessionId, ["expectedRevision"] = "not-dispatched", ["expectedDiskRevision"] = "bad-hash" }, true);
            var before = Snapshot(await Execute(client, "live_read", new() { ["sessionId"] = sessionId }));
            elementId = before.GetProperty("Elements").EnumerateArray().First(e => e.GetProperty("Kind").GetString() == "CallActivity" &&
                e.GetProperty("DiagramId").GetString() == before.GetProperty("ActiveDiagramId").GetString()).GetProperty("Id").GetString()!;
            // Exercise native documentation notification independently, then undo it
            // before the name-only whole-container fidelity trajectory below.
            var documented = Snapshot(await Execute(client, "live_apply", new() { ["sessionId"] = sessionId, ["expectedRevision"] = Revision(before),
                ["changes"] = new[] { new { ElementId = elementId, Documentation = "MCP native documentation Ω" } } }));
            if (documented.GetProperty("Elements").EnumerateArray().Single(e => e.GetProperty("Id").GetString() == elementId).GetProperty("Documentation").GetString() != "MCP native documentation Ω")
                throw new InvalidOperationException("Native documentation edit was not observed through MCP.");
            before = Snapshot(await Execute(client, "live_history", new() { ["sessionId"] = sessionId, ["expectedRevision"] = Revision(documented), ["action"] = "undo" }));
            string previousName = Name(before, elementId); diskRevision = before.GetProperty("DiskRevision").GetString()!;
            var update = await Execute(client, "live_apply", new() { ["sessionId"] = sessionId, ["expectedRevision"] = Revision(before),
                ["changes"] = new[] { new { ElementId = elementId, Name = name } } });
            updateId = update.GetProperty("OperationId").GetString()!;
            var after = Snapshot(update);
            if (Name(after, elementId) != name || after.GetProperty("DiskRevision").GetString() != diskRevision) throw new InvalidOperationException("MCP did not produce a real unsaved native edit.");
            await Execute(client, "live_apply", new() { ["sessionId"] = sessionId, ["expectedRevision"] = Revision(before),
                ["changes"] = new[] { new { ElementId = elementId, Name = "Must not overwrite" } } }, "failed");
            var undo = Snapshot(await Execute(client, "live_history", new() { ["sessionId"] = sessionId, ["expectedRevision"] = Revision(after), ["action"] = "undo" }));
            if (Name(undo, elementId) != previousName) throw new InvalidOperationException("MCP native undo mismatch.");
            var redo = Snapshot(await Execute(client, "live_history", new() { ["sessionId"] = sessionId, ["expectedRevision"] = Revision(undo), ["action"] = "redo" }));
            if (Name(redo, elementId) != name) throw new InvalidOperationException("MCP native redo mismatch.");
            editedRevision = Revision(redo);
        }
        Console.WriteLine("FIRST_STDIO_MCP_HOST_DISPOSED_WITH_UNSAVED_NATIVE_EDIT");
        // A fresh MCP process must see the original session and retained receipt.
        await using (var client = await McpClient.CreateAsync(Transport()))
        {
            var reconnected = Snapshot(await Execute(client, "live_read", new() { ["sessionId"] = sessionId }));
            if (Revision(reconnected) != editedRevision || Name(reconnected, elementId) != name || reconnected.GetProperty("DiskRevision").GetString() != diskRevision)
                throw new InvalidOperationException("Native unsaved state did not survive MCP process restart.");
            var receipt = (await Execute(client, "live_reconcile", new() { ["operationId"] = updateId })).GetProperty("Result");
            if (receipt.GetProperty("writeReplayed").GetBoolean() || receipt.GetProperty("receipt").GetProperty("State").GetString() != "completed")
                throw new InvalidOperationException("Live reconciliation did not return the original native receipt without replay.");
            var saved = await Execute(client, "live_checkpoint", new() { ["sessionId"] = sessionId, ["expectedRevision"] = editedRevision, ["expectedDiskRevision"] = diskRevision });
            if (Snapshot(saved).GetProperty("Dirty").GetBoolean()) throw new InvalidOperationException("Native checkpoint remained dirty.");
            var checkpoint = saved.GetProperty("Result").GetProperty("Checkpoint");
            string artifact = checkpoint.GetProperty("ArtifactPath").GetString()!;
            // Workspace adoption is not publication: a private byte copy allows a
            // second real MCP tool to invoke its independent installed-engine worker.
            string copy = Path.Combine(run, "checkpoint.bpm"); File.Copy(artifact, copy);
            var readback = await Execute(client, "native_inspect", new() { ["path"] = copy });
            var reread = readback.GetProperty("Result").GetProperty("result");
            if (reread.GetProperty("Elements").EnumerateArray().Single(e => e.GetProperty("Id").GetString() == elementId).GetProperty("Name").GetString() != name)
                throw new InvalidOperationException("Fresh MCP native reader lost the live edit.");
            string previous = Path.Combine(run, "before-checkpoint.bpm"); File.Copy(checkpoint.GetProperty("PreviousArtifactPath").GetString()!, previous);
            var fidelity = await Call(client, "native_compare", new() { ["path"] = previous, ["otherPath"] = copy,
                ["expectedNames"] = new[] { new { ElementId = elementId, Name = name } } });
            if (!fidelity.GetProperty("Preserved").GetBoolean()) throw new InvalidOperationException("Whole-archive live checkpoint fidelity failed.");
            File.WriteAllText(Path.Combine(run, "live-mcp-result.json"), JsonSerializer.Serialize(new { sessionId, elementId, name,
                saved, readback, fidelity, stdioRestartPreservedUnsaved = true, originalDestinationPublished = false, managedLaunchAccredited = false }));
            Console.WriteLine("LIVE_STDIO_MCP_EDIT_HISTORY_RESTART_RECEIPT_CHECKPOINT_NATIVE_READER_PASS");
        }
    }
}
