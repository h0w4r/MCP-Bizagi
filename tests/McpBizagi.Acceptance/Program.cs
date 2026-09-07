using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// Independent protocol client: never invokes the server's implementation classes.
string repo = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? ".");
bool native = args.Contains("--native");
int packageArgument = Array.IndexOf(args, "--package");
string? package = packageArgument >= 0 ? Path.GetFullPath(args[packageArgument + 1]) : null;
string run = Path.Combine(repo, ".local", "acceptance", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
Directory.CreateDirectory(run);
string stateRoot = args.Contains("--external-state") ? Path.Combine(repo, ".local", "acceptance-state", Path.GetFileName(run)) : Path.Combine(run, "state");
var env = new Dictionary<string, string?>
{
    ["MCP_BIZAGI_ROOT"] = run,
    ["MCP_BIZAGI_STATE"] = stateRoot,
    ["MCP_BIZAGI_WORKER"] = package == null
        ? Path.Combine(repo, "src", "McpBizagi.Worker", "bin", "Release", "net48", "McpBizagi.Worker.exe")
        : Path.Combine(package, "worker", "McpBizagi.Worker.exe"),
    ["MCP_BIZAGI_EXPERIMENTAL_NATIVE"] = native ? "1" : "0"
};
StdioClientTransport NewTransport(Action<string>? stderr = null) => new(new StdioClientTransportOptions
{
    Name = "MCP-Bizagi acceptance",
    Command = "dotnet",
    Arguments = [package == null
        ? Path.Combine(repo, "src", "McpBizagi.Server", "bin", "Release", "net10.0-windows", "McpBizagi.Server.dll")
        : Path.Combine(package, "McpBizagi.Server.dll")],
    EnvironmentVariables = env,
    StandardErrorLines = stderr
});
await using var client = await McpClient.CreateAsync(NewTransport());
McpClient activeClient = client;
var evidence = new List<object>();
async Task<JsonElement> Call(string tool, Dictionary<string, object?>? input = null, bool expectError = false)
{
    var result = await activeClient.CallToolAsync(tool, input ?? new());
    string text = result.Content.OfType<TextContentBlock>().First().Text;
    evidence.Add(new { tool, result.IsError, output = JsonSerializer.Deserialize<JsonElement>(text) });
    File.WriteAllText(Path.Combine(run, "mcp-transcript.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    if (result.IsError == true != expectError) throw new InvalidOperationException(tool + ": " + text);
    return JsonSerializer.Deserialize<JsonElement>(text);
}

// Observe the operator-facing journal rather than calling implementation methods.
async Task<JsonElement> WaitOperation(string id, string expectedState = "completed")
{
    var timer = Stopwatch.StartNew();
    string lastPhase = "";
    while (true)
    {
        var state = await Call("operation_get", new() { ["operationId"] = id });
        string phase = state.GetProperty("Phase").GetString()!;
        if (phase != lastPhase) { Console.WriteLine($"native phase={phase} elapsed={timer.Elapsed}"); lastPhase = phase; }
        string status = state.GetProperty("State").GetString()!;
        if (status == expectedState) return state;
        if (status is "completed" or "failed" or "cancelled" or "interrupted") throw new InvalidOperationException(state.GetRawText());
        await Task.Delay(1000);
    }
}

void VerifyWorkerExit(string operationId)
{
    // PIDs alone are not durable identities; compare the process start time if Windows reused one.
    foreach (string file in Directory.EnumerateFiles(Path.Combine(stateRoot, "runs", operationId), "worker-process.json", SearchOption.AllDirectories))
    {
        var process = JsonDocument.Parse(File.ReadAllText(file)).RootElement;
        string exitFile = Path.Combine(Path.GetDirectoryName(file)!, "worker-exit.json");
        if (!File.Exists(exitFile) || !JsonDocument.Parse(File.ReadAllText(exitFile)).RootElement.GetProperty("exited").GetBoolean())
            throw new InvalidDataException("Worker exit evidence is missing.");
        var desktop = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(file)!, "desktop-observation.json"))).RootElement;
        if (desktop.GetProperty("visibleWindowObserved").GetBoolean() || desktop.GetProperty("workerForegroundObserved").GetBoolean())
            throw new InvalidDataException("Worker desktop independence observation failed.");
        try
        {
            using var live = Process.GetProcessById(process.GetProperty("pid").GetInt32());
            if (!live.HasExited && live.StartTime.ToUniversalTime() == process.GetProperty("startedAt").GetDateTime())
                throw new InvalidOperationException("Owned worker is still running.");
        }
        catch (ArgumentException) { /* An absent PID is the expected terminal state. */ }
    }
}
try
{
    var tools = await client.ListToolsAsync();
    Console.WriteLine("tools=" + tools.Count);
    await Call("capabilities_get");
    if (args.Contains("--render-only"))
    {
        // Focused real-protocol diagnostics shorten renderer investigation without substituting an engine double.
        int inputArgument = Array.IndexOf(args, "--input");
        if (inputArgument < 0 || !native) throw new ArgumentException("--render-only requires --native and --input <native file>.");
        string inputPath = Path.Combine(run, "render-input.bpm"); File.Copy(Path.GetFullPath(args[inputArgument + 1]), inputPath);
        var opened = await Call("native_inspect", new() { ["path"] = inputPath });
        string openId = opened.GetProperty("OperationId").GetString()!;
        var graph = (await WaitOperation(openId)).GetProperty("Result").GetProperty("result").GetProperty("Elements").EnumerateArray();
        string diagramId = graph.First(e => e.GetProperty("Kind").GetString() == "Collaboration").GetProperty("Id").GetString()!;
        var render = await Call("native_render_svg", new() { ["path"] = inputPath, ["diagramId"] = diagramId });
        string renderId = render.GetProperty("OperationId").GetString()!;
        var output = await WaitOperation(renderId);
        string artifact = output.GetProperty("Result").GetProperty("result").GetProperty("Artifacts")[0].GetString()!;
        if (System.Xml.Linq.XDocument.Load(artifact).Root?.Name.LocalName != "svg") throw new InvalidDataException("Invalid native SVG.");
        VerifyWorkerExit(openId); VerifyWorkerExit(renderId);
        Console.WriteLine("NATIVE_RENDER_DIAGNOSTIC_PASS artifact=" + artifact + " evidence=" + run);
        return 0;
    }
    string xml = File.ReadAllText(Path.Combine(repo, "examples", "minimal.bpmn"));
    await Call("bpmn_create", new() { ["path"] = "Unicode path/Request.bpmn", ["xml"] = xml });
    var inspected = await Call("bpmn_inspect", new() { ["path"] = "Unicode path/Request.bpmn" });
    string revision = inspected.GetProperty("result").GetProperty("Revision").GetString()!;
    await Call("bpmn_apply_changes", new() { ["path"] = "Unicode path/Request.bpmn", ["expectedRevision"] = revision,
        ["changes"] = new[] { new { elementId = "Task_Review", property = "name", value = "Revisión — 東京" } } });
    var readback = await Call("bpmn_inspect", new() { ["path"] = "Unicode path/Request.bpmn" });
    var task = readback.GetProperty("result").GetProperty("Elements").EnumerateArray().Single(e => e.GetProperty("Id").GetString() == "Task_Review");
    if (task.GetProperty("Name").GetString() != "Revisión — 東京") throw new InvalidDataException("Exact Unicode readback was not observed.");
    await Call("bpmn_apply_changes", new() { ["path"] = "Unicode path/Request.bpmn", ["expectedRevision"] = revision,
        ["changes"] = new[] { new { elementId = "Task_Review", property = "name", value = "Must not overwrite" } } }, expectError: true);
    await Call("bpmn_inspect", new() { ["path"] = "../outside.bpmn" }, expectError: true);
    await Call("bpmn_validate", new() { ["path"] = "Unicode path/Request.bpmn" });
    Console.WriteLine("MCP_XML_E2E_PASS evidence=" + run);
    if (native)
    {
        var operation = await Call("native_roundtrip", new() { ["path"] = "Unicode path/Request.bpmn", ["modelName"] = "Acceptance model" });
        string id = operation.GetProperty("OperationId").GetString()!;
        var completed = await WaitOperation(id);
        VerifyWorkerExit(id);
        Console.WriteLine("NATIVE_ROUNDTRIP_PASS");

        // Exercise opening an existing native file through the public tool, preserving its bytes.
        string nativeArtifact = completed.GetProperty("Result").GetProperty("saved").GetProperty("Artifacts")[0].GetString()!;
        string nativeInput = Path.Combine(run, "Unicode path", "Existing model.bpm");
        File.Copy(nativeArtifact, nativeInput);
        byte[] nativeBefore = File.ReadAllBytes(nativeInput);
        var inspectOperation = await Call("native_inspect", new() { ["path"] = "Unicode path/Existing model.bpm" });
        string inspectId = inspectOperation.GetProperty("OperationId").GetString()!;
        var inspectedNative = await WaitOperation(inspectId);
        if (!inspectedNative.GetProperty("Result").GetProperty("result").GetProperty("Elements").EnumerateArray()
            .Any(e => e.GetProperty("Name").GetString() == "Revisión — 東京"))
            throw new InvalidDataException("Native inspect failed to return the persisted name.");
        if (!nativeBefore.SequenceEqual(File.ReadAllBytes(nativeInput))) throw new InvalidDataException("Native inspection changed its source.");
        VerifyWorkerExit(inspectId);

        // Native IDs come from the actual reopened model, not the source BPMN IDs (which Bizagi may regenerate).
        var nativeElements = inspectedNative.GetProperty("Result").GetProperty("result").GetProperty("Elements").EnumerateArray().ToArray();
        string taskId = nativeElements.Single(e => e.GetProperty("Kind").GetString()!.EndsWith("Task", StringComparison.Ordinal)).GetProperty("Id").GetString()!;
        string startId = nativeElements.Single(e => e.GetProperty("Kind").GetString() == "StartEvent").GetProperty("Id").GetString()!;
        string nativeRevision = inspectedNative.GetProperty("Result").GetProperty("sourceRevision").GetString()!;
        object[] batch = [new { elementId = taskId, name = "Native edit verified — 東京" }, new { elementId = startId, name = "Inicio verificado" }];
        await Call("native_apply_changes", new() { ["path"] = "Unicode path/Existing model.bpm", ["expectedRevision"] = "stale", ["changes"] = batch }, expectError: true);
        var editedOperation = await Call("native_apply_changes", new() { ["path"] = "Unicode path/Existing model.bpm", ["expectedRevision"] = nativeRevision, ["changes"] = batch });
        string editId = editedOperation.GetProperty("OperationId").GetString()!;
        var editedNative = await WaitOperation(editId);
        if (editedNative.GetProperty("Result").GetProperty("requestedChangesVerified").GetInt32() != 2)
            throw new InvalidDataException("Native batch was not fully verified.");
        if (!nativeBefore.SequenceEqual(File.ReadAllBytes(nativeInput))) throw new InvalidDataException("Native copy-only edit changed the source.");
        VerifyWorkerExit(editId);
        Console.WriteLine("NATIVE_BATCH_EDIT_FRESH_READER_AND_STALE_REVISION_PASS");

        // Exercise whole-container comparison through MCP, not an implementation call.
        string editedFile = editedNative.GetProperty("Result").GetProperty("outputArtifact").GetString()!;
        var comparison = await Call("native_compare", new() { ["path"] = nativeInput, ["otherPath"] = editedFile, ["expectedNames"] = batch });
        if (!comparison.GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Native MCP comparison rejected the verified edit.");
        var unexpectedComparison = await Call("native_compare", new() { ["path"] = nativeInput, ["otherPath"] = editedFile });
        if (unexpectedComparison.GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Native comparison hid an unrequested name change.");
        var savedCopy = await Call("native_save_copy", new() { ["path"] = nativeInput, ["expectedRevision"] = nativeRevision });
        string savedCopyId = savedCopy.GetProperty("OperationId").GetString()!;
        var savedCopyResult = await WaitOperation(savedCopyId);
        if (!savedCopyResult.GetProperty("Result").GetProperty("fidelity").GetProperty("Preserved").GetBoolean())
            throw new InvalidDataException("No-op native save did not preserve content.");
        VerifyWorkerExit(savedCopyId);
        Console.WriteLine("NATIVE_NOOP_SAVE_AND_CONTENT_COMPARISON_PASS");

        var validated = await Call("native_validate", new() { ["path"] = nativeInput });
        string validationId = validated.GetProperty("OperationId").GetString()!;
        var validationResult = await WaitOperation(validationId);
        if (!validationResult.GetProperty("Result").GetProperty("result").TryGetProperty("Validation", out _))
            throw new InvalidDataException("Native validation findings were not returned.");
        VerifyWorkerExit(validationId);
        Console.WriteLine("NATIVE_VALIDATION_EXECUTION_PASS");

        if (args.Contains("--extended"))
        {
            await Call("bpmn_create", new() { ["path"] = "Unicode path/Nested.bpmn", ["xml"] = File.ReadAllText(Path.Combine(repo, "examples", "collaboration-nested.bpmn")) });
            var multi = await Call("native_roundtrip", new() { ["path"] = "Unicode path/Nested.bpmn", ["additionalPaths"] = new[] { "Unicode path/Request.bpmn" }, ["modelName"] = "Two diagrams" });
            string multiId = multi.GetProperty("OperationId").GetString()!;
            var multiResult = (await WaitOperation(multiId)).GetProperty("Result");
            var graph = multiResult.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
            string Kind(JsonElement e) => e.GetProperty("Kind").GetString()!;
            string Name(JsonElement e) => e.GetProperty("Name").GetString()!;
            string Id(JsonElement e) => e.GetProperty("Id").GetString()!;
            if (graph.Count(e => Kind(e) == "Collaboration") != 2 || graph.Count(e => Kind(e) == "Lane") != 2 ||
                graph.Count(e => Kind(e) == "SubProcess") != 2 || !graph.Any(e => Kind(e) == "MessageFlow") || !graph.Any(e => Kind(e) == "ExclusiveGateway"))
                throw new InvalidDataException("Native graph lost required collaborations, lanes, subprocesses, messages or gateways.");
            var pack = graph.Single(e => Name(e) == "Pack parcel — 東京");
            var parent = graph.Single(e => Id(e) == pack.GetProperty("ParentId").GetString());
            var grandparent = graph.Single(e => Id(e) == parent.GetProperty("ParentId").GetString());
            if (Kind(parent) != "SubProcess" || Kind(grandparent) != "SubProcess" || pack.GetProperty("Geometry").GetProperty("Width").GetDouble() <= 0)
                throw new InvalidDataException("Nested containment or native geometry was not observed.");
            string multiFile = multiResult.GetProperty("nativeArtifact").GetString()!;
            var multiInspect = await Call("native_inspect", new() { ["path"] = multiFile });
            string multiInspectId = multiInspect.GetProperty("OperationId").GetString()!;
            string multiRevision = (await WaitOperation(multiInspectId)).GetProperty("Result").GetProperty("sourceRevision").GetString()!;
            var multiEdit = await Call("native_apply_changes", new() { ["path"] = multiFile, ["expectedRevision"] = multiRevision,
                ["changes"] = new[] { new { elementId = Id(pack), name = "Nested durable edit — 東京" } } });
            string multiEditId = multiEdit.GetProperty("OperationId").GetString()!;
            var multiEditResult = (await WaitOperation(multiEditId)).GetProperty("Result");
            if (!multiEditResult.GetProperty("fidelity").GetProperty("Preserved").GetBoolean() || multiEditResult.GetProperty("reopened").GetProperty("Diagrams").GetArrayLength() != 2)
                throw new InvalidDataException("Native multi-diagram edit did not preserve the full container.");
            foreach (string completedId in new[] { multiId, multiInspectId, multiEditId }) VerifyWorkerExit(completedId);
            Console.WriteLine("NATIVE_MULTI_DIAGRAM_NESTED_GRAPH_AND_EDIT_PASS");
        }

        if (args.Contains("--simulation"))
        {
            string diagramId = nativeElements.Single(e => e.GetProperty("Kind").GetString() == "Collaboration").GetProperty("Id").GetString()!;
            var simulation = await Call("native_simulate", new() { ["path"] = nativeInput, ["diagramId"] = diagramId });
            string simulationId = simulation.GetProperty("OperationId").GetString()!;
            var simulated = await WaitOperation(simulationId);
            string resultsFile = simulated.GetProperty("Result").GetProperty("result").GetProperty("Artifacts").EnumerateArray()
                .Select(a => a.GetString()!).Single(a => Path.GetFileName(a) == "Results.xml");
            var results = System.Xml.Linq.XDocument.Load(resultsFile);
            if (results.Root == null || !results.Root.HasElements) throw new InvalidDataException("Native simulation results are empty.");
            if (!results.Descendants("process").Any(p => (string?)p.Attribute("numberOfProcessesStarted") == "1000" &&
                (string?)p.Attribute("numberOfProcessesCompleted") == "1000" && (string?)p.Attribute("numberOfProcessesFailed") == "0"))
                throw new InvalidDataException("The default native scenario did not complete its 1000 requested instances.");
            VerifyWorkerExit(simulationId);
            Console.WriteLine("NATIVE_SIMULATION_REAL_RESULTS_PASS");
        }

        if (args.Contains("--render"))
        {
            string diagramId = nativeElements.Single(e => e.GetProperty("Kind").GetString() == "Collaboration").GetProperty("Id").GetString()!;
            var render = await Call("native_render_svg", new() { ["path"] = nativeInput, ["diagramId"] = diagramId });
            string renderId = render.GetProperty("OperationId").GetString()!;
            var rendered = await WaitOperation(renderId);
            string svgFile = rendered.GetProperty("Result").GetProperty("result").GetProperty("Artifacts")[0].GetString()!;
            var svg = System.Xml.Linq.XDocument.Load(svgFile);
            if (svg.Root?.Name.LocalName != "svg" || !svg.DescendantNodes().OfType<System.Xml.Linq.XText>().Any(t => t.Value.Contains("Revisión")))
                throw new InvalidDataException("The native renderer did not include the persisted diagram text.");
            VerifyWorkerExit(renderId);
            Console.WriteLine("NATIVE_OFFSCREEN_SVG_PASS");
        }

        var invalidEdit = await Call("native_apply_changes", new() { ["path"] = "Unicode path/Existing model.bpm", ["expectedRevision"] = nativeRevision,
            ["changes"] = new[] { new { elementId = "missing-element", name = "Must fail" } } });
        string failureId = invalidEdit.GetProperty("OperationId").GetString()!;
        var failed = await WaitOperation(failureId, "failed");
        if (string.IsNullOrWhiteSpace(failed.GetProperty("Error").GetString())) throw new InvalidDataException("Native error was not reported.");
        VerifyWorkerExit(failureId);
        Console.WriteLine("NATIVE_ENGINE_FAILURE_JOURNALED_PASS");
        File.WriteAllText(Path.Combine(run, "corrupt.bpm"), "not a native archive");
        await Call("native_inspect", new() { ["path"] = "corrupt.bpm" }, expectError: true);
        Console.WriteLine("NATIVE_INSPECT_AND_CORRUPT_INPUT_PASS");

        var cancelOperation = await Call("native_probe");
        string cancelId = cancelOperation.GetProperty("OperationId").GetString()!;
        while (true)
        {
            var state = await Call("operation_get", new() { ["operationId"] = cancelId });
            if (state.GetProperty("State").GetString() != "running")
                throw new InvalidOperationException("Probe finished before active cancellation could be tested.");
            string phase = state.GetProperty("Phase").GetString()!;
            if (phase is not "queued" and not "worker_started") break;
            await Task.Delay(100);
        }
        await Call("operation_cancel", new() { ["operationId"] = cancelId });
        await WaitOperation(cancelId, "cancelled");
        VerifyWorkerExit(cancelId);
        var recovered = await Call("native_probe");
        string recoveredId = recovered.GetProperty("OperationId").GetString()!;
        await WaitOperation(recoveredId);
        VerifyWorkerExit(recoveredId);
        Console.WriteLine("NATIVE_ACTIVE_CANCELLATION_AND_RECOVERY_PASS");

        if (args.Contains("--recovery"))
        {
            // A second real host must fail its lease before touching the first host's journals.
            var duplicateErrors = new System.Text.StringBuilder();
            bool rejected = false;
            using (var handshake = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                try { await using var duplicate = await McpClient.CreateAsync(NewTransport(line => duplicateErrors.AppendLine(line)), cancellationToken: handshake.Token); }
                catch (Exception) { rejected = true; }
            }
            if (!rejected || !duplicateErrors.ToString().Contains("Another MCP-Bizagi host owns this state directory"))
                throw new InvalidDataException("The second host did not explicitly reject the occupied state lease.");
            File.WriteAllText(Path.Combine(run, "duplicate-host.stderr.log"), duplicateErrors.ToString());

            var interrupted = await Call("native_probe");
            string interruptedId = interrupted.GetProperty("OperationId").GetString()!;
            string processFile = Path.Combine(stateRoot, "runs", interruptedId, "probe", "worker-process.json");
            while (!File.Exists(processFile)) await Task.Delay(50);
            var owned = JsonDocument.Parse(File.ReadAllText(processFile)).RootElement;
            JsonElement host;
            using (var lease = new FileStream(Path.Combine(stateRoot, ".host.lock"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                host = (await JsonDocument.ParseAsync(lease)).RootElement.Clone();
            using (var process = Process.GetProcessById(host.GetProperty("pid").GetInt32()))
            {
                if (process.StartTime.ToUniversalTime() != host.GetProperty("startedAt").GetDateTime()) throw new InvalidDataException("Host PID identity changed.");
                // Kill only the test-created host. Job Object cleanup, not Kill(entireProcessTree), must stop its worker.
                process.Kill(entireProcessTree: false); await process.WaitForExitAsync();
            }
            bool childExited;
            try
            {
                using var child = Process.GetProcessById(owned.GetProperty("pid").GetInt32());
                childExited = child.StartTime.ToUniversalTime() != owned.GetProperty("startedAt").GetDateTime() || child.WaitForExit(10000);
            }
            catch (ArgumentException) { childExited = true; }
            if (!childExited) throw new InvalidDataException("Host death left its native worker running.");
            await using var restarted = await McpClient.CreateAsync(NewTransport());
            activeClient = restarted;
            var recoveredJournal = await Call("operation_get", new() { ["operationId"] = interruptedId });
            if (recoveredJournal.GetProperty("State").GetString() != "interrupted") throw new InvalidDataException("Interrupted work was replayed or incorrectly marked complete.");
            var probeAfterRestart = await Call("native_probe");
            string restartId = probeAfterRestart.GetProperty("OperationId").GetString()!;
            await WaitOperation(restartId); VerifyWorkerExit(restartId);
            File.WriteAllText(Path.Combine(run, "host-recovery.json"), JsonSerializer.Serialize(new { interruptedId, childExited, restartId, duplicateHostRejected = rejected }));
            Console.WriteLine("NATIVE_HOST_DEATH_JOBCLEANUP_JOURNAL_RESTART_AND_STATE_LEASE_PASS");
        }
    }
    else Console.WriteLine("NATIVE_NOT_RUN (not an operational pass)");
    return 0;
}
catch (Exception error)
{
    File.WriteAllText(Path.Combine(run, "acceptance-error.txt"), error.ToString());
    Console.Error.WriteLine(error.Message + " evidence=" + run);
    return 1;
}
