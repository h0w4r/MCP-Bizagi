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
var env = new Dictionary<string, string?>
{
    ["MCP_BIZAGI_ROOT"] = run,
    ["MCP_BIZAGI_STATE"] = Path.Combine(run, "state"),
    ["MCP_BIZAGI_WORKER"] = package == null
        ? Path.Combine(repo, "src", "McpBizagi.Worker", "bin", "Release", "net48", "McpBizagi.Worker.exe")
        : Path.Combine(package, "worker", "McpBizagi.Worker.exe"),
    ["MCP_BIZAGI_EXPERIMENTAL_NATIVE"] = native ? "1" : "0"
};
var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "MCP-Bizagi acceptance",
    Command = "dotnet",
    Arguments = [package == null
        ? Path.Combine(repo, "src", "McpBizagi.Server", "bin", "Release", "net10.0-windows", "McpBizagi.Server.dll")
        : Path.Combine(package, "McpBizagi.Server.dll")],
    EnvironmentVariables = env
});
await using var client = await McpClient.CreateAsync(transport);
var evidence = new List<object>();
async Task<JsonElement> Call(string tool, Dictionary<string, object?>? input = null, bool expectError = false)
{
    var result = await client.CallToolAsync(tool, input ?? new());
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
    foreach (string file in Directory.EnumerateFiles(Path.Combine(run, "state", "runs", operationId), "worker-process.json", SearchOption.AllDirectories))
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
