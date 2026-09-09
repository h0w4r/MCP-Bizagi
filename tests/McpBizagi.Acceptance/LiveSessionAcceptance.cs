using System.Text.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

/// <summary>Actual stdio MCP calls into an independently launched native companion, never server implementation calls.</summary>
internal static class LiveSessionAcceptance
{
    internal static async Task Run(string repo, string sessionId, string liveRoot, string run, string? managedModel = null, string? package = null)
    {
        var env = new Dictionary<string, string?>
        {
            ["MCP_BIZAGI_ROOT"] = run, ["MCP_BIZAGI_STATE"] = Path.Combine(run, "state"),
            ["MCP_BIZAGI_LIVE_ROOT"] = liveRoot, ["MCP_BIZAGI_EXPERIMENTAL_NATIVE"] = "1",
            ["MCP_BIZAGI_LIVE_OWNER"] = Path.Combine(repo, "src", "McpBizagi.LiveOwner", "bin", "Release", "net10.0-windows", "McpBizagi.LiveOwner.exe"),
            ["MCP_BIZAGI_LIVE_HOST"] = Path.Combine(repo, "src", "McpBizagi.LiveHost", "bin", "Release", "net48", "McpBizagi.LiveHost.exe"),
            ["MCP_BIZAGI_WORKER"] = Path.Combine(repo, "src", "McpBizagi.Worker", "bin", "Release", "net48", "McpBizagi.Worker.exe")
        };
        if (package != null)
        {
            // Exercise production sibling discovery, not hidden source overrides.
            foreach (string key in new[] { "MCP_BIZAGI_LIVE_OWNER", "MCP_BIZAGI_LIVE_HOST", "MCP_BIZAGI_WORKER" })
            { env.Remove(key); Environment.SetEnvironmentVariable(key, null, EnvironmentVariableTarget.Process); }
        }
        StdioClientTransport Transport() => new(new StdioClientTransportOptions { Name = "MCP-Bizagi live acceptance", Command = "dotnet",
            Arguments = [package == null ? Path.Combine(repo, "src", "McpBizagi.Server", "bin", "Release", "net10.0-windows", "McpBizagi.Server.dll")
                : Path.Combine(package, "McpBizagi.Server.dll")], EnvironmentVariables = env });
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
        string editedRevision, elementId, diskRevision, updateId, publicationId = "", checkpointId = "", checkpointHash = "";
        string closeRevision = "", closeDiskRevision = "", closeCheckpointId = "";
        string closedOperationId = "";
        string destination = Path.Combine(run, "Original model Ω.bpm");
        string name = "MCP live unsaved Ω " + Guid.NewGuid().ToString("N")[..8];
        string documentation = "Retained live documentation Ω " + Guid.NewGuid().ToString("N")[..8];
        await using (var client = await McpClient.CreateAsync(Transport()))
        {
            if (managedModel != null)
            {
                string input = Path.Combine(run, "launch input Ω.bpm"); File.Copy(managedModel, input);
                string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))).ToLowerInvariant();
                var opened = await Execute(client, "live_open", new() { ["path"] = input, ["expectedRevision"] = hash });
                sessionId = opened.GetProperty("Result").GetProperty("sessionId").GetString()!;
                File.WriteAllText(Path.Combine(run, "managed-open.json"), opened.GetRawText());
                var identities = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(liveRoot, sessionId, "owner-started.json")));
                foreach (string role in new[] { "Owner", "Editor" })
                {
                    var identity = identities.GetProperty(role);
                    using var process = Process.GetProcessById(identity.GetProperty("ProcessId").GetInt32()); _ = process.SafeHandle;
                    if (process.StartTime.ToUniversalTime() != identity.GetProperty("StartedAt").GetDateTimeOffset().UtcDateTime ||
                        process.PriorityClass != ProcessPriorityClass.Normal)
                        throw new InvalidOperationException("Managed interactive process identity or normal scheduling priority mismatch: " + role);
                }
                File.WriteAllText(Path.Combine(run, "managed-scheduling.json"), JsonSerializer.Serialize(new { normalPriorityVerified = true, identities }));
                var listed = await Call(client, "live_sessions_list", new());
                if (!listed.GetProperty("sessions").EnumerateArray().Any(s => s.GetProperty("SessionId").GetString() == sessionId))
                    throw new InvalidOperationException("The managed native launch was not recoverable from the registry.");
            }
            await Call(client, "live_read", new() { ["sessionId"] = "../not-a-session" }, true);
            await Call(client, "live_history", new() { ["sessionId"] = sessionId, ["expectedRevision"] = "not-dispatched", ["action"] = "execute" }, true);
            await Call(client, "live_checkpoint", new() { ["sessionId"] = sessionId, ["expectedRevision"] = "not-dispatched", ["expectedDiskRevision"] = "bad-hash" }, true);
            var before = Snapshot(await Execute(client, "live_read", new() { ["sessionId"] = sessionId }));
            // Create the disposable operator-original before any live edits. Production publication must adopt the checkpoint itself.
            File.Copy(before.GetProperty("Path").GetString()!, destination);
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
                ["changes"] = new[] { new { ElementId = elementId, Name = name, Documentation = documentation } } });
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
            if (managedModel != null)
            {
                var capability = await Call(client, "capabilities_get", new());
                using var host = Process.GetProcessById(capability.GetProperty("hostProcessId").GetInt32());
                _ = host.SafeHandle;
                if (host.Id == Environment.ProcessId || host.HasExited) throw new InvalidOperationException("Unexpected MCP test process identity.");
                // Kill the entire actual MCP tree while native work is unsaved. The scheduler owner must not be a descendant.
                host.Kill(entireProcessTree: true); await host.WaitForExitAsync();
                File.WriteAllText(Path.Combine(run, "mcp-tree-terminated.json"), JsonSerializer.Serialize(new { host.Id, host.ExitCode, nativeUnsavedRevision = editedRevision }));
            }
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
            checkpointHash = checkpoint.GetProperty("Revision").GetString()!;
            var laterUnsaved = Snapshot(await Execute(client, "live_apply", new() { ["sessionId"] = sessionId,
                ["expectedRevision"] = Revision(Snapshot(saved)), ["changes"] = new[] { new { ElementId = elementId, Name = name + " later unsaved" } } }));
            // A valid saved checkpoint is not permission to discard a newer dirty document.
            await Execute(client, "live_close", new() { ["sessionId"] = sessionId, ["expectedRevision"] = Revision(laterUnsaved),
                ["expectedDiskRevision"] = checkpointHash, ["checkpointOperationId"] = saved.GetProperty("OperationId").GetString() }, "failed");
            string artifact = checkpoint.GetProperty("ArtifactPath").GetString()!;
            // Workspace adoption is not publication: a private byte copy allows a
            // second real MCP tool to invoke its independent installed-engine worker.
            string copy = Path.Combine(run, "checkpoint.bpm"); File.Copy(artifact, copy);
            var readback = await Execute(client, "native_inspect", new() { ["path"] = copy });
            var reread = readback.GetProperty("Result").GetProperty("result");
            if (reread.GetProperty("Elements").EnumerateArray().Single(e => e.GetProperty("Id").GetString() == elementId).GetProperty("Name").GetString() != name)
                throw new InvalidOperationException("Fresh MCP native reader lost the live edit.");
            string previous = Path.Combine(run, "before-checkpoint.bpm"); File.Copy(checkpoint.GetProperty("PreviousArtifactPath").GetString()!, previous);
            checkpointId = saved.GetProperty("OperationId").GetString()!;
            var expectedChanges = new[] { new { ElementId = elementId, Name = name, Documentation = documentation } };
            // Simulate a real external replacement of this disposable original, then verify conflict preserves all evidence.
            byte[] originalBytes = File.ReadAllBytes(destination); File.Copy(copy, destination, true);
            await Call(client, "live_publish", new() { ["checkpointOperationId"] = checkpointId, ["destinationPath"] = destination,
                ["expectedDestinationRevision"] = diskRevision, ["expectedChanges"] = expectedChanges }, true);
            if (!File.ReadAllBytes(destination).SequenceEqual(File.ReadAllBytes(copy))) throw new InvalidOperationException("Conflict overwrote the external replacement.");
            File.WriteAllBytes(destination, originalBytes); // Restore only our disposable acceptance original.
            await Execute(client, "live_publish", new() { ["checkpointOperationId"] = checkpointId, ["destinationPath"] = destination,
                ["expectedDestinationRevision"] = diskRevision, ["expectedChanges"] = Array.Empty<object>() }, "failed");
            if (!File.ReadAllBytes(destination).SequenceEqual(originalBytes)) throw new InvalidOperationException("Fidelity rejection changed the destination.");
            var publication = await Execute(client, "live_publish", new() { ["checkpointOperationId"] = checkpointId, ["destinationPath"] = destination,
                ["expectedDestinationRevision"] = diskRevision, ["expectedChanges"] = expectedChanges });
            publicationId = publication.GetProperty("OperationId").GetString()!;
            var published = publication.GetProperty("Result");
            var fidelity = published.GetProperty("fidelity");
            string backup = published.GetProperty("commit").GetProperty("BackupPath").GetString()!;
            if (!fidelity.GetProperty("Preserved").GetBoolean() || !File.ReadAllBytes(backup).SequenceEqual(originalBytes) ||
                !File.ReadAllBytes(destination).SequenceEqual(File.ReadAllBytes(copy))) throw new InvalidOperationException("Live publication fidelity, backup or exact bytes failed.");
            if (reread.GetProperty("Elements").EnumerateArray().Single(e => e.GetProperty("Id").GetString() == elementId).GetProperty("Documentation").GetString() != documentation)
                throw new InvalidOperationException("Fresh native reader lost live documentation.");
            var stillUnsaved = Snapshot(await Execute(client, "live_read", new() { ["sessionId"] = sessionId }));
            if (Revision(stillUnsaved) != Revision(laterUnsaved) || Name(stillUnsaved, elementId) != name + " later unsaved" ||
                !stillUnsaved.GetProperty("Dirty").GetBoolean() || stillUnsaved.GetProperty("DiskRevision").GetString() != checkpointHash)
                throw new InvalidOperationException("Checkpoint publication altered later unsaved editor state.");
            // Undo only the acceptance edit after proving publication did not consume it, then retain a clean disposable working copy.
            var restored = Snapshot(await Execute(client, "live_history", new() { ["sessionId"] = sessionId, ["expectedRevision"] = Revision(stillUnsaved), ["action"] = "undo" }));
            var finalCheckpoint = await Execute(client, "live_checkpoint", new() { ["sessionId"] = sessionId, ["expectedRevision"] = Revision(restored), ["expectedDiskRevision"] = checkpointHash });
            closeRevision = Revision(Snapshot(finalCheckpoint)); closeDiskRevision = Snapshot(finalCheckpoint).GetProperty("DiskRevision").GetString()!;
            closeCheckpointId = finalCheckpoint.GetProperty("OperationId").GetString()!;
            File.WriteAllText(Path.Combine(run, "live-mcp-result.json"), JsonSerializer.Serialize(new { sessionId, elementId, name,
                documentation, saved, readback, fidelity, publication, laterUnsavedPreserved = true, stdioRestartPreservedUnsaved = true, originalDestinationPublished = true, managedLaunchAccredited = false }));
            Console.WriteLine("LIVE_STDIO_MCP_EDIT_HISTORY_RESTART_RECEIPT_CHECKPOINT_PUBLICATION_PASS");
        }
        // Simulate loss of only our MCP reply journal; the native checkpoint receipt remains authoritative.
        string missingReply = Path.Combine(run, "state", "runs", checkpointId, "live", "reply.json");
        File.Move(missingReply, missingReply + ".retained-test-copy");
        await using (var client = await McpClient.CreateAsync(Transport()))
        {
            var reconciliation = await Execute(client, "native_commit_reconcile", new() { ["operationId"] = publicationId });
            var result = reconciliation.GetProperty("Result");
            if (result.GetProperty("observedState").GetString() != "applied" || result.GetProperty("writeReplayed").GetBoolean() ||
                !result.GetProperty("nativeReadbackVerified").GetBoolean()) throw new InvalidOperationException("Restarted host did not reconcile live publication without replay.");
            File.WriteAllText(Path.Combine(run, "live-publication-reconciliation.json"), reconciliation.GetRawText());
            Console.WriteLine("LIVE_PUBLICATION_RESTART_RECONCILIATION_PASS");
            var recovered = await Execute(client, "live_publish", new() { ["checkpointOperationId"] = checkpointId, ["destinationPath"] = destination,
                ["expectedDestinationRevision"] = checkpointHash, ["expectedChanges"] = Array.Empty<object>() });
            if (!recovered.GetProperty("Result").GetProperty("destinationPublished").GetBoolean()) throw new InvalidOperationException("Native receipt fallback did not publish the retained checkpoint.");
            File.WriteAllText(Path.Combine(run, "live-publication-native-receipt.json"), recovered.GetRawText());
            Console.WriteLine("LIVE_PUBLICATION_NATIVE_RECEIPT_FALLBACK_PASS");
            await Execute(client, "live_close", new() { ["sessionId"] = sessionId, ["expectedRevision"] = closeRevision,
                ["expectedDiskRevision"] = closeDiskRevision, ["checkpointOperationId"] = updateId }, "failed");
            var closed = await Execute(client, "live_close", new() { ["sessionId"] = sessionId, ["expectedRevision"] = closeRevision,
                ["expectedDiskRevision"] = closeDiskRevision, ["checkpointOperationId"] = closeCheckpointId });
            closedOperationId = closed.GetProperty("OperationId").GetString()!;
            var exit = closed.GetProperty("Result").GetProperty("EditorExit");
            if (exit.GetProperty("ExitCode").GetInt32() != 0) throw new InvalidOperationException("Managed native close did not verify a normal editor exit.");
            if (managedModel != null && !exit.GetProperty("OwnedTreeVerified").GetBoolean())
                throw new InvalidOperationException("Managed Close did not verify independent-owner process-tree cleanup.");
            var closeReceipt = await Execute(client, "live_reconcile", new() { ["operationId"] = closed.GetProperty("OperationId").GetString() });
            if (closeReceipt.GetProperty("Result").GetProperty("receipt").GetProperty("Code").GetString() != "live_editor_exit_verified")
                throw new InvalidOperationException("Retained close reconciliation did not work after editor exit.");
            File.WriteAllText(Path.Combine(run, "live-close-result.json"), JsonSerializer.Serialize(new { closed, closeReceipt }));
            Console.WriteLine("LIVE_CLOSE_DIRTY_AND_UNRETAINED_REJECTED_NATIVE_EXIT_RECONCILIATION_PASS");
        }
        if (managedModel != null)
        {
            // Withhold only this disposable test's MCP observer file. The native
            // admission and independent-owner OS/job observations remain genuine.
            string observation = Path.Combine(liveRoot, sessionId, Guid.Parse(closedOperationId).ToString("D") + ".close-exit.json");
            File.Move(observation, observation + ".withheld-for-recovery-test");
        }
        // Neither the old MCP host nor the native process is alive: recovery must use durable exit observation only.
        await using (var client = await McpClient.CreateAsync(Transport()))
        {
            var recovered = await Execute(client, "live_reconcile", new() { ["operationId"] = closedOperationId });
            if (recovered.GetProperty("Result").GetProperty("receipt").GetProperty("EditorExit").GetProperty("ExitCode").GetInt32() != 0)
                throw new InvalidOperationException("Restarted MCP host lost retained native exit evidence.");
            File.WriteAllText(Path.Combine(run, "live-close-restart.json"), recovered.GetRawText());
            Console.WriteLine("LIVE_CLOSE_MCP_RESTART_RECONCILIATION_PASS");
            if (managedModel != null)
            {
                var listed = await Call(client, "live_sessions_list", new());
                File.WriteAllText(Path.Combine(run, "managed-final-registry.json"), listed.GetRawText());
                string root = Path.Combine(liveRoot, sessionId);
                // live_close already waited for the pinned owner to finish. A missing
                // journal now is a failure, not an unbounded polling opportunity.
                var ownerExit = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(root, "owner-exit.json")));
                if (!ownerExit.GetProperty("AllOwnedExited").GetBoolean() || ownerExit.GetProperty("ExitCode").GetInt32() != 0)
                    throw new InvalidOperationException("Production owner failed native process-tree cleanup.");
                File.WriteAllText(Path.Combine(run, "managed-owner-exit.json"), ownerExit.GetRawText());
                Console.WriteLine("MANAGED_LAUNCH_MCP_TREE_DEATH_UNSAVED_SURVIVAL_CLOSE_OWNER_CLEANUP_PASS");
            }
        }
    }
}
