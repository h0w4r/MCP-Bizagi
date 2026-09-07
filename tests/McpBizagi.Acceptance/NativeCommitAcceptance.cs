using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Real MCP adoption, failures and host-death recovery. No server implementation calls or fault hooks.</summary>
internal static class NativeCommitAcceptance
{
    public static async Task Run(string run, string stateRoot, string? inputPath,
        Func<string, Dictionary<string, object?>, bool, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<Task> restart)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Operation(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = (await call(tool, args, false)).GetProperty("OperationId").GetString()!;
            var result = await wait(id, state); exited(id); receipts.Add(new { tool, id, result });
            return state == "completed" ? result.GetProperty("Result") : result;
        }
        string source;
        if (inputPath != null) { source = "source Ω.bpm"; File.Copy(Path.GetFullPath(inputPath), Path.Combine(run, source)); }
        else
        {
            var created = await Operation("native_model_create", new() { ["diagramNames"] = new[] { "Commit source Ω", "Other 日本語" } });
            source = created.GetProperty("outputArtifact").GetString()!;
        }
        var opened = await Operation("native_inspect", new() { ["path"] = source });
        string revision = opened.GetProperty("sourceRevision").GetString()!;
        var element = opened.GetProperty("result").GetProperty("Elements").EnumerateArray().First(e => e.GetProperty("Kind").GetString() == "Participant" && e.GetProperty("IsMainParticipant").ValueKind == JsonValueKind.False);
        var edited = await Operation("native_apply_changes", new() { ["path"] = source, ["expectedRevision"] = revision,
            ["changes"] = new[] { new { ElementId = element.GetProperty("Id").GetString(), Name = "Adopted native pool 日本語" } } });
        string artifact = edited.GetProperty("outputArtifact").GetString()!, editedRevision = edited.GetProperty("outputRevision").GetString()!;
        string target = "saved models/Target 日本語 Ω.bpm", fullTarget = Path.Combine(run, target);
        Dictionary<string, object?> CommitArgs(string from, string hash, string to, string? previous = null) =>
            new() { ["path"] = from, ["expectedRevision"] = hash, ["destinationPath"] = to, ["expectedDestinationRevision"] = previous };
        string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        void Require(bool ok, string message) { if (!ok) throw new InvalidDataException(message); }
        void CheckTarget(string expected) => Require(Hash(fullTarget) == expected, "Destination changed unexpectedly.");
        var first = await Operation("native_commit", CommitArgs(source, revision, target));
        CheckTarget(revision);
        Require(first.GetProperty("commit").GetProperty("BackupPath").ValueKind == JsonValueKind.Null, "Creation produced an unexpected backup.");
        var replacement = await Operation("native_commit", CommitArgs(artifact, editedRevision, target, revision));
        CheckTarget(editedRevision);
        string backup = replacement.GetProperty("commit").GetProperty("BackupPath").GetString()!;
        Require(Hash(backup) == revision, "Recoverable replacement backup is not byte-identical to the previous version.");
        var actual = await Operation("native_inspect", new() { ["path"] = target });
        Require(actual.GetProperty("sourceRevision").GetString() == editedRevision, "Independent MCP destination read returned another revision.");

        await call("native_commit", CommitArgs(artifact, revision, target, editedRevision), true); // Stale source, not a worker failure.
        await call("native_commit", CommitArgs(artifact, editedRevision, "../outside.bpm"), true);
        await call("native_commit", CommitArgs(artifact, editedRevision, "wrong-format.bpmn"), true);
        await Operation("native_commit", CommitArgs(source, revision, target, revision), "failed"); // Stale destination.
        await Operation("native_commit", CommitArgs(source, revision, target), "failed"); // No implicit overwrite.
        await Operation("native_commit", CommitArgs(source, revision, "missing.bpm", editedRevision), "failed");
        using (var locked = new FileStream(fullTarget, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Operation("native_commit", CommitArgs(source, revision, target, editedRevision), "failed");
        CheckTarget(editedRevision);
        File.WriteAllText(Path.Combine(run, "corrupt.bpm"), "This is not a native archive.");
        await call("native_commit", CommitArgs("corrupt.bpm", Hash(Path.Combine(run, "corrupt.bpm")), "must-not-exist.bpm"), true);
        Require(!File.Exists(Path.Combine(run, "must-not-exist.bpm")), "Invalid source reached destination publication.");

        async Task WaitReader(string id, string reader, bool requireNativeEntry = false)
        {
            string processFile = Path.Combine(stateRoot, "runs", id, reader, "worker-process.json");
            string settingsFile = Path.Combine(stateRoot, "runs", id, reader, "native-settings-paths.txt");
            while (!File.Exists(processFile) || (requireNativeEntry && !File.Exists(settingsFile)))
            {
                var journal = ReadJson(Path.Combine(stateRoot, "operations", id + ".json"));
                Require(journal.GetProperty("State").GetString() == "running", "Operation finished before the requested live-reader boundary.");
                await Task.Delay(20);
            }
        }
        // Change a real source while the engine is reading its private snapshot. Recheck the live
        // source before publication, retain prepared bytes, then reconcile without applying them.
        string changingSource = Path.Combine(run, "changing source.bpm"); File.Copy(fullTarget, changingSource);
        string sourceRaceId = (await call("native_commit", CommitArgs(changingSource, editedRevision, "source-race/never-published.bpm"), false)).GetProperty("OperationId").GetString()!;
        await WaitReader(sourceRaceId, "source-reader"); File.WriteAllBytes(changingSource, File.ReadAllBytes(backup));
        var sourceRace = await wait(sourceRaceId, "failed"); exited(sourceRaceId); receipts.Add(new { tool = "native_commit", id = sourceRaceId, result = sourceRace });
        Require(sourceRace.GetProperty("Error").GetString()!.Contains("native source changed before publication"), "Source race failed for an unrelated reason.");
        var notApplied = await Operation("native_commit_reconcile", new() { ["operationId"] = sourceRaceId });
        Require(notApplied.GetProperty("observedState").GetString() == "not_applied", "Retained preparation was not distinguished from an applied write.");
        Require(!File.Exists(Path.Combine(run, "source-race/never-published.bpm")), "Stale source was published.");

        // Cancellation after publication must retain the side-effect receipt even if the ordinary
        // operation result is cancelled. Reconciliation independently reads, but does not re-save.
        string cancelledId = (await call("native_commit", CommitArgs(source, revision, target, editedRevision), false)).GetProperty("OperationId").GetString()!;
        await WaitReader(cancelledId, "destination-reader", requireNativeEntry: true);
        await call("operation_cancel", new() { ["operationId"] = cancelledId }, false);
        var cancelled = await wait(cancelledId, "cancelled"); exited(cancelledId); receipts.Add(new { tool = "native_commit", id = cancelledId, result = cancelled });
        CheckTarget(revision);
        var cancelledRecovery = await Operation("native_commit_reconcile", new() { ["operationId"] = cancelledId });
        Require(cancelledRecovery.GetProperty("observedState").GetString() == "applied" && cancelledRecovery.GetProperty("nativeReadbackVerified").GetBoolean(), "Post-publication cancellation concealed its applied side effect.");
        await Operation("native_commit", CommitArgs(artifact, editedRevision, target, revision)); CheckTarget(editedRevision);

        // Kill the actual owned host after atomic publication, while its fresh destination reader exists.
        // This is observed via real receipts/process identities, not a test-only pause in production code.
        string interruptedId = (await call("native_commit", CommitArgs(source, revision, target, editedRevision), false)).GetProperty("OperationId").GetString()!;
        string operationRoot = Path.Combine(stateRoot, "runs", interruptedId), artifacts = Path.Combine(operationRoot, "artifacts");
        string childFile = Path.Combine(operationRoot, "destination-reader", "worker-process.json");
        var timer = Stopwatch.StartNew(); string lastPhase = "";
        while (!File.Exists(childFile))
        {
            // Reading the atomically replaced public journal avoids intrusive protocol polling at this boundary.
            var journal = ReadJson(Path.Combine(stateRoot, "operations", interruptedId + ".json"));
            string phase = journal.GetProperty("Phase").GetString()!;
            if (phase != lastPhase) { Console.WriteLine($"commit interruption phase={phase} elapsed={timer.Elapsed}"); lastPhase = phase; }
            Require(journal.GetProperty("State").GetString() is "running" or "cancelling", "Commit finished before the actual host interruption boundary; this run does not accredit interruption.");
            await Task.Delay(20);
        }
        var owned = ReadJson(childFile);
        JsonElement host;
        using (var lease = new FileStream(Path.Combine(stateRoot, ".host.lock"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            host = (await JsonDocument.ParseAsync(lease)).RootElement.Clone();
        using (var process = Process.GetProcessById(host.GetProperty("pid").GetInt32()))
        {
            Require(process.StartTime.ToUniversalTime() == host.GetProperty("startedAt").GetDateTime(), "Host PID identity changed.");
            process.Kill(entireProcessTree: false); await process.WaitForExitAsync();
        }
        bool childExited;
        try
        {
            using var child = Process.GetProcessById(owned.GetProperty("pid").GetInt32());
            childExited = child.StartTime.ToUniversalTime() != owned.GetProperty("startedAt").GetDateTime() || child.WaitForExit(10000);
        }
        catch (ArgumentException) { childExited = true; }
        Require(childExited, "Killed host left its owned native reader running.");
        Require(File.Exists(Path.Combine(artifacts, "commit-published.json")), "Host death was not after publication.");
        Require(!File.Exists(Path.Combine(artifacts, "commit-native-readback.json")), "Interruption was too late to test missing native readback recovery.");
        CheckTarget(revision);
        var intent = ReadJson(Path.Combine(artifacts, "commit-intent.json"));
        string crashBackup = intent.GetProperty("File").GetProperty("BackupPath").GetString()!;
        Require(Hash(crashBackup) == editedRevision, "Interrupted replacement lost its previous version.");
        var beforeRecovery = Directory.GetFiles(Path.GetDirectoryName(fullTarget)!).Select(p => new { path = p, hash = Hash(p), modified = File.GetLastWriteTimeUtc(p) }).OrderBy(p => p.path).ToArray();
        await restart();
        var interrupted = await call("operation_get", new() { ["operationId"] = interruptedId }, false);
        Require(interrupted.GetProperty("State").GetString() == "interrupted", "Restart replayed or falsely completed the original write.");
        for (int i = 0; i < 2; i++)
        {
            var reconciled = await Operation("native_commit_reconcile", new() { ["operationId"] = interruptedId });
            Require(reconciled.GetProperty("observedState").GetString() == "applied" && reconciled.GetProperty("nativeReadbackVerified").GetBoolean() &&
                !reconciled.GetProperty("writeReplayed").GetBoolean(), "Reconciliation did not independently observe/read the applied native file.");
        }
        var afterRecovery = Directory.GetFiles(Path.GetDirectoryName(fullTarget)!).Select(p => new { path = p, hash = Hash(p), modified = File.GetLastWriteTimeUtc(p) }).OrderBy(p => p.path).ToArray();
        Require(JsonSerializer.Serialize(beforeRecovery) == JsonSerializer.Serialize(afterRecovery), "Reconciliation wrote, deleted or duplicated a destination/stage/backup.");
        // A later operator write must be reported as a conflict, never silently rolled back.
        File.WriteAllBytes(fullTarget, File.ReadAllBytes(crashBackup));
        var conflict = await Operation("native_commit_reconcile", new() { ["operationId"] = interruptedId });
        Require(conflict.GetProperty("observedState").GetString() == "ambiguous", "Restored prior bytes plus a consumed stage must be ambiguous, not inferred as unapplied.");
        CheckTarget(editedRevision);
        File.WriteAllText(fullTarget, "External divergent bytes after the observed commit.");
        var divergent = await Operation("native_commit_reconcile", new() { ["operationId"] = interruptedId });
        Require(divergent.GetProperty("observedState").GetString() == "conflict" && File.ReadAllText(fullTarget).StartsWith("External divergent"), "Divergent external content was not retained as a conflict.");
        // Removal of private evidence must never be interpreted as proof that no write occurred.
        string intentFile = Path.Combine(artifacts, "commit-intent.json"), retainedIntent = intentFile + ".retained";
        File.Move(intentFile, retainedIntent);
        try
        {
            var missing = await Operation("native_commit_reconcile", new() { ["operationId"] = interruptedId });
            Require(missing.GetProperty("observedState").GetString() == "missing_intent", "Missing intent was treated as evidence of an unapplied operation.");
        }
        finally { File.Move(retainedIntent, intentFile); }
        var sourceAfter = await Operation("native_inspect", new() { ["path"] = source });
        Require(sourceAfter.GetProperty("sourceRevision").GetString() == revision, "Adoption or reconciliation modified its source.");
        File.WriteAllText(Path.Combine(run, "native-commit-acceptance.json"), JsonSerializer.Serialize(new { receipts, interruptedId, childExited, beforeRecovery, afterRecovery, intent, conflict }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static JsonElement ReadJson(string path)
    {
        // Observers allow atomic journal replacement and never deny a native/host writer access.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }
}
