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
if (args.Contains("--discover-worker"))
{
    if (package == null || args.Contains("--connection-failure"))
        throw new ArgumentException("Worker discovery acceptance requires an intact --package distribution.");
    // Exercise the operator's default sibling-worker lookup, not an explicit test override.
    // Clear only this acceptance process's environment; never change the user's saved settings.
    env.Remove("MCP_BIZAGI_WORKER");
    Environment.SetEnvironmentVariable("MCP_BIZAGI_WORKER", null, EnvironmentVariableTarget.Process);
    Console.WriteLine("PACKAGED_WORKER_AUTODISCOVERY_ENABLED");
}
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
    JsonElement output;
    try { output = JsonSerializer.Deserialize<JsonElement>(text); }
    catch (JsonException) when (result.IsError == true)
    {
        // Official SDK binding errors occur before our tool method and can be plain text.
        // Retain the actual error, rather than failing the evidence recorder's JSON parser.
        output = JsonSerializer.SerializeToElement(new { sdkError = text });
    }
    evidence.Add(new { tool, result.IsError, output });
    File.WriteAllText(Path.Combine(run, "mcp-transcript.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    if (result.IsError == true != expectError) throw new InvalidOperationException(tool + ": " + text);
    return output;
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
        // Check the actual native provider paths recorded inside the separate worker, not host assumptions.
        string[] settings = File.ReadAllLines(Path.Combine(Path.GetDirectoryName(file)!, "native-settings-paths.txt"));
        string[] expectedSettings = [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "h0w4r", "McpBizagi.Worker"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "h0w4r", "McpBizagi.Worker")];
        if (!settings.SequenceEqual(expectedSettings, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Native settings application namespace was not isolated from Modeler.");
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
    var advertised = await Call("capabilities_get");
    // A real protocol response must expose concrete creation spellings, not an inferred
    // internal enum. This verifies schema discovery only, never native execution.
    if (package == null)
    {
        var types = advertised.GetProperty("nativeMutationTypes").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var modes = advertised.GetProperty("intermediateCreationModes").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var subprocesses = advertised.GetProperty("nativeSubProcessKinds").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var payloads = advertised.GetProperty("nativeEventPayloadKinds").EnumerateArray().Select(e => e.GetString()!).ToArray();
        if (types.Length == 0 || types.Distinct(StringComparer.Ordinal).Count() != types.Length ||
            !types.Contains("TimerIntermediate") || !types.Contains("CancelEnd") || !types.Contains("EventBasedGatewayParallel") ||
            !modes.SequenceEqual(new[] { "Catch", "Throw", "Boundary" }) || !subprocesses.SequenceEqual(new[] { "SubProcess", "Transaction", "AdHoc" }) ||
            !payloads.SequenceEqual(new[] { "Message", "Timer", "Conditional", "Link", "Signal", "Error", "Escalation", "Compensation" }))
            throw new InvalidDataException("Native mutation schema inventory is absent, ambiguous or incomplete.");
    }
    if (args.Contains("--connection-failure"))
    {
        if (!native) throw new ArgumentException("Connection failure acceptance requires --native.");
        // Damage only a newly copied, private worker dependency set. The installed vendor,
        // source binaries and supplied release candidate remain byte-for-byte untouched.
        string goodWorker = env["MCP_BIZAGI_WORKER"]!, damaged = Path.Combine(run, "missing-worker-dependency");
        Directory.CreateDirectory(damaged);
        foreach (string file in Directory.EnumerateFiles(Path.GetDirectoryName(goodWorker)!))
            if (Path.GetFileName(file) != "StreamJsonRpc.dll") File.Copy(file, Path.Combine(damaged, Path.GetFileName(file)));
        await activeClient.DisposeAsync();
        env["MCP_BIZAGI_WORKER"] = Path.Combine(damaged, Path.GetFileName(goodWorker));
        env["MCP_BIZAGI_CONNECTION_SECONDS"] = "3";
        await using (var broken = await McpClient.CreateAsync(NewTransport()))
        {
            activeClient = broken;
            string id = (await Call("native_probe")).GetProperty("OperationId").GetString()!;
            var failure = await WaitOperation(id, "failed");
            if (!failure.GetProperty("Error").GetString()!.Contains("before any engine request was dispatched"))
                throw new InvalidDataException("The initial connection failure lost its pre-dispatch diagnosis.");
            string root = Path.Combine(stateRoot, "runs", id);
            var connection = JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(root, "worker-connection-error.json", SearchOption.AllDirectories).Single())).RootElement;
            if (connection.GetProperty("requestDispatched").GetBoolean() || connection.GetProperty("operationCancellationRequested").GetBoolean() ||
                connection.GetProperty("exceptionType").GetString() != "TimeoutException") throw new InvalidDataException("An initial deadline was misreported as operator cancellation.");
            foreach (string exit in Directory.GetFiles(root, "worker-exit.json", SearchOption.AllDirectories))
                if (!JsonDocument.Parse(File.ReadAllText(exit)).RootElement.GetProperty("exited").GetBoolean()) throw new InvalidDataException("Faulted worker did not exit.");
            string stderr = File.ReadAllText(Directory.GetFiles(root, "worker.stderr.log", SearchOption.AllDirectories).Single());
            if (!stderr.Contains("StreamJsonRpc")) throw new InvalidDataException("Fault injection did not expose the actual missing worker dependency.");
        }
        env["MCP_BIZAGI_WORKER"] = goodWorker; env.Remove("MCP_BIZAGI_CONNECTION_SECONDS");
        await using var recovered = await McpClient.CreateAsync(NewTransport()); activeClient = recovered;
        string recoveredId = (await Call("native_probe")).GetProperty("OperationId").GetString()!;
        await WaitOperation(recoveredId); VerifyWorkerExit(recoveredId);
        Console.WriteLine("NATIVE_CONNECTION_FAILURE_CLASSIFICATION_AND_RECOVERY_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--artifacts-only"))
    {
        if (!native) throw new ArgumentException("Artifact acceptance requires --native.");
        await NativeArtifactAcceptance.Run(run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        Console.WriteLine("NATIVE_ARTIFACT_LIFECYCLE_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--styles-only"))
    {
        if (!native) throw new ArgumentException("Style acceptance requires --native.");
        await NativeStyleAcceptance.Run(run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit, (name, input) => Call(name, input, true));
        Console.WriteLine("NATIVE_STYLE_LIFECYCLE_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--xpdl-only"))
    {
        if (!native) throw new ArgumentException("XPDL acceptance requires --native.");
        await NativeXpdlAcceptance.Run(run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit, (name, input) => Call(name, input, true));
        Console.WriteLine("NATIVE_XPDL_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--refactoring-only"))
    {
        if (!native) throw new ArgumentException("Refactoring acceptance requires --native.");
        int extractionInput = Array.IndexOf(args, "--input");
        if (extractionInput >= 0)
        {
            if (extractionInput + 1 >= args.Length) throw new ArgumentException("--input requires an existing native model path.");
            await NativeRefactoringAcceptance.RunExisting(run, args[extractionInput + 1], (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        }
        else await NativeRefactoringAcceptance.Run(repo, run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit, (name, input) => Call(name, input, true));
        Console.WriteLine("NATIVE_REFACTORING_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--conversions-only"))
    {
        if (!native) throw new ArgumentException("Conversion acceptance requires --native.");
        await NativeConversionAcceptance.Run(run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit, (name, input) => Call(name, input, true));
        Console.WriteLine("NATIVE_CONVERSION_LIFECYCLE_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--custom-artifacts-only"))
    {
        if (!native) throw new ArgumentException("Custom artifact acceptance requires --native.");
        await NativeCustomArtifactAcceptance.Run(repo, run, stateRoot, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit, (name, input) => Call(name, input, true));
        Console.WriteLine("NATIVE_CUSTOM_ARTIFACT_LIFECYCLE_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--images-only"))
    {
        if (!native) throw new ArgumentException("Image acceptance requires --native.");
        await NativeImageAcceptance.Run(repo, run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit, (name, input) => Call(name, input, true));
        Console.WriteLine("NATIVE_IMAGE_LIFECYCLE_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--event-data-only"))
    {
        if (!native) throw new ArgumentException("Event data acceptance requires --native.");
        await NativeEventDataAcceptance.Run(run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        Console.WriteLine("NATIVE_EVENT_DATA_LIFECYCLE_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--data-only"))
    {
        if (!native) throw new ArgumentException("Data acceptance requires --native.");
        await NativeDataAcceptance.Run(run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        Console.WriteLine("NATIVE_DATA_LIFECYCLE_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--event-payloads-only"))
    {
        if (!native) throw new ArgumentException("Event payload acceptance requires --native.");
        await NativeEventPayloadAcceptance.Run(run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        Console.WriteLine("NATIVE_EVENT_PAYLOAD_LIFECYCLE_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--subprocesses-only"))
    {
        if (!native) throw new ArgumentException("Subprocess acceptance requires --native.");
        await NativeSubProcessAcceptance.Run(run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        Console.WriteLine("NATIVE_SUBPROCESS_LIFECYCLE_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--events-only"))
    {
        if (!native) throw new ArgumentException("Event acceptance requires --native.");
        await NativeEventAcceptance.Run(run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        Console.WriteLine("NATIVE_EVENT_LIFECYCLE_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--loops-only"))
    {
        if (!native) throw new ArgumentException("Loop acceptance requires --native.");
        await NativeLoopAcceptance.Run(run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        Console.WriteLine("NATIVE_LOOP_EDITING_AND_DIAGNOSTICS_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--semantics-only"))
    {
        if (!native) throw new ArgumentException("Semantic acceptance requires --native.");
        await NativeSemanticAcceptance.Run(run, (name, input, error) => Call(name, input, error), WaitOperation, VerifyWorkerExit, args.Contains("--require-token-semantics"));
        Console.WriteLine("NATIVE_SEMANTIC_EDITING_AND_SIMULATION_DIAGNOSTICS_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--calls-behavior-only"))
    {
        if (!native) throw new ArgumentException("Call behavior acceptance requires --native.");
        await NativeCallBehaviorAcceptance.Run(run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        Console.WriteLine("NATIVE_CALL_BEHAVIOR_SIMULATION_PUBLICATION_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--calls-only"))
    {
        if (!native) throw new ArgumentException("Call-activity acceptance requires --native.");
        await NativeCallAcceptance.Run(run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        Console.WriteLine("NATIVE_CALL_ACTIVITY_LIFECYCLE_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--commit-only"))
    {
        if (!native) throw new ArgumentException("Native commit acceptance requires --native.");
        int inputArgument = Array.IndexOf(args, "--input");
        McpClient? restartedCommitClient = null;
        try
        {
            await NativeCommitAcceptance.Run(run, stateRoot, inputArgument >= 0 ? args[inputArgument + 1] : null,
                (name, input, error) => Call(name, input, error), WaitOperation, VerifyWorkerExit, async () =>
                {
                    restartedCommitClient = await McpClient.CreateAsync(NewTransport());
                    activeClient = restartedCommitClient;
                });
        }
        finally { if (restartedCommitClient != null) await restartedCommitClient.DisposeAsync(); }
        Console.WriteLine("NATIVE_COMMIT_REPLACEMENT_HOST_INTERRUPTION_RECONCILIATION_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--model-create-only"))
    {
        if (!native) throw new ArgumentException("Native model creation acceptance requires --native.");
        await Call("native_model_create", new() { ["diagramNames"] = new[] { "Collision", "collision" } }, expectError: true);
        await NativeModelCreationAcceptance.Run(run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        Console.WriteLine("NATIVE_MODEL_CREATION_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--diagrams-only"))
    {
        if (!native) throw new ArgumentException("Diagram acceptance requires --native.");
        int inputArgument = Array.IndexOf(args, "--input");
        await NativeDiagramAcceptance.Run(repo, run, inputArgument >= 0 ? args[inputArgument + 1] : null,
            (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit, args.Contains("--configured-clone-simulation"));
        Console.WriteLine("NATIVE_DIAGRAM_LIFECYCLE_PASS evidence=" + run); return 0;
    }
    if (args.Contains("--containers-only"))
    {
        if (!native) throw new ArgumentException("Container acceptance requires --native.");
        int inputArgument = Array.IndexOf(args, "--input");
        await NativeContainerAcceptance.Run(repo, run, inputArgument >= 0 ? args[inputArgument + 1] : null,
            (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        Console.WriteLine("NATIVE_CONTAINER_LIFECYCLE_PASS evidence=" + run);
        return 0;
    }
    if (args.Contains("--attributes-only"))
    {
        if (!native) throw new ArgumentException("Attribute acceptance requires --native.");
        await NativeDocumentationAcceptance.Run(repo, run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        Console.WriteLine("NATIVE_ATTRIBUTES_ATTACHMENTS_PASS evidence=" + run);
        return 0;
    }
    if (args.Contains("--metadata-only"))
    {
        if (!native) throw new ArgumentException("Metadata acceptance requires --native.");
        await NativeMetadataAcceptance.Run(repo, run, (name, input) => Call(name, input), WaitOperation, VerifyWorkerExit);
        Console.WriteLine("NATIVE_METADATA_SCENARIOS_WHAT_IF_PASS evidence=" + run);
        return 0;
    }
    if (args.Contains("--expanded-render-only"))
    {
        if (!native) throw new ArgumentException("Expanded rendering acceptance requires --native.");
        File.Copy(Path.Combine(repo, "examples", "collaboration-nested.bpmn"), Path.Combine(run, "nested.bpmn"));
        var imported = await Call("native_roundtrip", new() { ["path"] = "nested.bpmn", ["modelName"] = "Expanded subprocess acceptance" });
        string importId = imported.GetProperty("OperationId").GetString()!;
        var result = (await WaitOperation(importId)).GetProperty("Result"); VerifyWorkerExit(importId);
        var elements = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        var expanded = elements.Where(e => e.GetProperty("Kind").GetString() == "SubProcess").ToArray();
        if (expanded.Length != 2 || result.GetProperty("saved").GetProperty("IntegrationAdjustments").GetArrayLength() != 2)
            throw new InvalidDataException("Expanded import corrections were not recorded for both subprocess levels.");
        foreach (var element in expanded)
        {
            var original = element.GetProperty("Geometry"); var bounds = element.GetProperty("ExpandedGeometry");
            if (!bounds.GetProperty("Expanded").GetBoolean() || bounds.GetProperty("Width").GetDouble() != original.GetProperty("Width").GetDouble() ||
                bounds.GetProperty("Height").GetDouble() != original.GetProperty("Height").GetDouble())
                throw new InvalidDataException("Fresh native readback lost supplied expanded BPMN bounds.");
        }
        string nativePath = result.GetProperty("saved").GetProperty("Artifacts")[0].GetString()!;
        byte[] nativeBytes = File.ReadAllBytes(nativePath);
        string diagramId = elements.Single(e => e.GetProperty("Kind").GetString() == "Collaboration").GetProperty("Id").GetString()!;
        var rendered = await Call("native_render_svg", new() { ["path"] = nativePath, ["diagramId"] = diagramId });
        string renderId = rendered.GetProperty("OperationId").GetString()!;
        var output = (await WaitOperation(renderId)).GetProperty("Result").GetProperty("result"); VerifyWorkerExit(renderId);
        var svg = System.Xml.Linq.XDocument.Load(output.GetProperty("Artifacts")[0].GetString()!);
        var graphical = svg.Descendants().Where(e => e.Attribute("data-element-id") != null).ToLookup(e => e.Attribute("data-element-id")!.Value);
        // Root-level ID presence alone missed the asynchronous nested-content loss. Require every
        // graphical corpus element at both expanded levels, and verify the actual rendered rectangles.
        foreach (var element in elements.Where(e => e.GetProperty("Geometry").ValueKind != JsonValueKind.Null &&
            e.GetProperty("Kind").GetString() is not "Collaboration" and not "Process" and not "LaneSet" and not "Resource"))
            if (!graphical.Contains(element.GetProperty("Id").GetString()!)) throw new InvalidDataException("Expanded SVG omitted a native graphical element.");
        foreach (var element in expanded)
        {
            var rect = graphical[element.GetProperty("Id").GetString()!].Single().Descendants().First(e => e.Name.LocalName == "rect");
            var bounds = element.GetProperty("ExpandedGeometry");
            if ((double)rect.Attribute("width")! != bounds.GetProperty("Width").GetDouble() || (double)rect.Attribute("height")! != bounds.GetProperty("Height").GetDouble())
                throw new InvalidDataException("Expanded native renderer changed durable bounds.");
        }
        var publication = await Call("native_publish", new() { ["path"] = nativePath, ["format"] = "pdf", ["title"] = "Expanded subprocess acceptance" });
        string publicationId = publication.GetProperty("OperationId").GetString()!;
        await WaitOperation(publicationId); VerifyWorkerExit(publicationId);
        if (!nativeBytes.SequenceEqual(File.ReadAllBytes(nativePath))) throw new InvalidDataException("Rendering or publication changed saved native geometry.");
        Console.WriteLine("NATIVE_EXPANDED_IMPORT_BOUNDS_NESTED_RENDER_PDF_PASS evidence=" + run);
        return 0;
    }
    if (args.Contains("--palette-only"))
    {
        int inputArgument = Array.IndexOf(args, "--input");
        if (inputArgument < 0 || !native) throw new ArgumentException("--palette-only requires --native and --input <native file>.");
        string input = Path.Combine(run, "palette-source.bpm"); File.Copy(Path.GetFullPath(args[inputArgument + 1]), input);
        byte[] original = File.ReadAllBytes(input);
        var inspection = await Call("native_inspect", new() { ["path"] = input });
        string inspectId = inspection.GetProperty("OperationId").GetString()!;
        var opened = (await WaitOperation(inspectId)).GetProperty("Result"); VerifyWorkerExit(inspectId);
        string parent = opened.GetProperty("result").GetProperty("Elements").EnumerateArray().First(e => e.GetProperty("Kind").GetString() == "Process").GetProperty("Id").GetString()!;
        // A disconnected palette is an editing/persistence corpus, not a behaviorally valid process claim.
        string[] types = ["AbstractTask", "UserTask", "ManualTask", "ServiceTask", "ScriptTask", "SendTask", "ReceiveTask", "BusinessRuleTask",
            "NoneStart", "MessageStart", "TimerStart", "NoneEnd", "MessageEnd", "TerminateEnd", "NoneIntermediate", "MessageIntermediate", "TimerIntermediate",
            "ExclusiveGateway", "InclusiveGateway", "ParallelGateway", "EventBasedGateway", "ComplexGateway"];
        var mutations = types.Select((type, index) => new
        {
            Operation = "create",
            ElementId = Guid.NewGuid().ToString(),
            ParentId = parent,
            ElementType = type,
            Name = "Palette " + type,
            Geometry = new { X = 100 + index % 6 * 100, Y = 220 + index / 6 * 100, Width = 50, Height = 50 }
        }).ToArray();
        var created = await Call("native_mutate", new() { ["path"] = input, ["expectedRevision"] = opened.GetProperty("sourceRevision").GetString(), ["mutations"] = mutations });
        string createId = created.GetProperty("OperationId").GetString()!;
        var result = (await WaitOperation(createId)).GetProperty("Result"); VerifyWorkerExit(createId);
        var deleted = await Call("native_mutate", new()
        {
            ["path"] = result.GetProperty("outputArtifact").GetString(),
            ["expectedRevision"] = result.GetProperty("outputRevision").GetString(),
            ["mutations"] = mutations.Select(m => new { Operation = "delete", m.ElementId }).ToArray()
        });
        string deleteId = deleted.GetProperty("OperationId").GetString()!;
        await WaitOperation(deleteId); VerifyWorkerExit(deleteId);
        if (!original.SequenceEqual(File.ReadAllBytes(input))) throw new InvalidDataException("Palette editing changed the original.");
        Console.WriteLine("NATIVE_PALETTE_CREATE_DELETE_PASS types=" + types.Length + " evidence=" + run);
        return 0;
    }
    if (args.Contains("--publication-only"))
    {
        int inputArgument = Array.IndexOf(args, "--input");
        if (inputArgument < 0 || !native) throw new ArgumentException("--publication-only requires --native and --input <native file>.");
        string input = Path.Combine(run, "publication-source.bpm"); File.Copy(Path.GetFullPath(args[inputArgument + 1]), input);
        byte[] original = File.ReadAllBytes(input);
        int formatArgument = Array.IndexOf(args, "--format");
        string[] formats = formatArgument >= 0 ? [args[formatArgument + 1]] : ["excel", "word", "pdf"];
        await Call("native_publish", new() { ["path"] = input, ["format"] = "unsupported" }, expectError: true);
        foreach (string format in formats)
        {
            var publication = await Call("native_publish", new()
            {
                ["path"] = input,
                ["format"] = format,
                ["title"] = "Verified process documentation",
                ["allowImageResampling"] = args.Contains("--allow-image-resampling")
            });
            string publicationId = publication.GetProperty("OperationId").GetString()!;
            var result = (await WaitOperation(publicationId)).GetProperty("Result"); VerifyWorkerExit(publicationId);
            if (!original.SequenceEqual(File.ReadAllBytes(input)) || result.GetProperty("verifiedNames").GetInt32() == 0)
                throw new InvalidDataException("Publication did not verify durable names and source preservation.");
            Console.WriteLine("NATIVE_PUBLICATION_" + format.ToUpperInvariant() + "_PASS evidence=" + run);
        }
        return 0;
    }
    if (args.Contains("--mutations-only"))
    {
        // A real existing archive is copied into a fresh operator workspace; every edit traverses MCP and two workers.
        int inputArgument = Array.IndexOf(args, "--input");
        if (inputArgument < 0 || !native) throw new ArgumentException("--mutations-only requires --native and --input <native file>.");
        string input = Path.Combine(run, "mutation-source.bpm"); File.Copy(Path.GetFullPath(args[inputArgument + 1]), input);
        byte[] original = File.ReadAllBytes(input);
        var request = await Call("native_inspect", new() { ["path"] = input });
        var opened = (await WaitOperation(request.GetProperty("OperationId").GetString()!)).GetProperty("Result");
        var elements = opened.GetProperty("result").GetProperty("Elements").EnumerateArray().ToArray();
        var mutationTask = elements.First(e => e.GetProperty("Kind").GetString()!.EndsWith("Task"));
        string taskId = mutationTask.GetProperty("Id").GetString()!, parentId = mutationTask.GetProperty("ParentId").GetString()!;
        var incoming = elements.First(e => e.GetProperty("Kind").GetString() == "SequenceFlow" && e.GetProperty("TargetId").GetString() == taskId);
        // Reject a real dangling-connection edit and a stale revision, then recover with a valid native batch.
        var invalid = await Call("native_mutate", new()
        {
            ["path"] = input,
            ["expectedRevision"] = opened.GetProperty("sourceRevision").GetString(),
            ["mutations"] = new[] { new { Operation = "delete", ElementId = taskId } }
        });
        string invalidId = invalid.GetProperty("OperationId").GetString()!;
        await WaitOperation(invalidId, "failed"); VerifyWorkerExit(invalidId);
        await Call("native_mutate", new()
        {
            ["path"] = input,
            ["expectedRevision"] = new string('0', 64),
            ["mutations"] = new[] { new { Operation = "update", ElementId = taskId, Name = "Must not overwrite" } }
        }, expectError: true);
        string addedTask = Guid.NewGuid().ToString(), addedFlow = Guid.NewGuid().ToString();
        object[] mutations = [
            new { Operation="create", ElementId=addedTask, ParentId=parentId, ElementType="UserTask", Name="Independent review — 東京", Documentation="New review step",
                Geometry=new { X=170, Y=160, Width=90, Height=50, BackgroundArgb=-1249281, BorderArgb=-16777216 } },
            new { Operation="update", ElementId=taskId, Name="Reviewed task", Documentation="Policy — 東京", Geometry=new { X=320, Y=90, Width=140, Height=70, BackgroundArgb=-1638505, BorderArgb=-10311914 } },
            new { Operation="reconnect", ElementId=incoming.GetProperty("Id").GetString(), SourceId=incoming.GetProperty("SourceId").GetString(), TargetId=addedTask,
                Points=new[] {new {X=150,Y=125},new {X=170,Y=185}} },
            new { Operation="create", ElementId=addedFlow, ParentId=parentId, ElementType="SequenceFlow", SourceId=addedTask, TargetId=taskId,
                Points=new[] {new {X=260,Y=185},new {X=320,Y=125}} }
        ];
        var edited = await Call("native_mutate", new() { ["path"] = input, ["expectedRevision"] = opened.GetProperty("sourceRevision").GetString(), ["mutations"] = mutations });
        string editId = edited.GetProperty("OperationId").GetString()!;
        var outcome = (await WaitOperation(editId)).GetProperty("Result"); VerifyWorkerExit(editId);
        if (!outcome.GetProperty("fidelity").GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Native structural fidelity failed.");
        // Undo structural insertion explicitly: detach/delete the connector before deleting the node.
        object[] undo = [ new { Operation="delete", ElementId=addedFlow },
            new {Operation="reconnect", ElementId=incoming.GetProperty("Id").GetString(), SourceId=incoming.GetProperty("SourceId").GetString(), TargetId=taskId,
                Points=new[] {new {X=150,Y=125},new {X=320,Y=125}} }, new {Operation="delete", ElementId=addedTask} ];
        var removed = await Call("native_mutate", new()
        {
            ["path"] = outcome.GetProperty("outputArtifact").GetString(),
            ["expectedRevision"] = outcome.GetProperty("outputRevision").GetString(),
            ["mutations"] = undo
        });
        string removeId = removed.GetProperty("OperationId").GetString()!;
        var removedResult = (await WaitOperation(removeId)).GetProperty("Result"); VerifyWorkerExit(removeId);
        if (!removedResult.GetProperty("fidelity").GetProperty("Preserved").GetBoolean() || !original.SequenceEqual(File.ReadAllBytes(input)))
            throw new InvalidDataException("Native deletion fidelity or source preservation failed.");
        Console.WriteLine("NATIVE_CREATE_GEOMETRY_STYLE_DOCUMENTATION_RECONNECT_DELETE_PASS evidence=" + run);
        return 0;
    }
    if (args.Contains("--settings-contention"))
    {
        if (!native) throw new ArgumentException("--settings-contention requires --native.");
        // Exercise an actual Windows file-sharing conflict, then a fresh real native worker after release.
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "h0w4r", "McpBizagi.Worker");
        Directory.CreateDirectory(directory);
        using (var lease = new FileStream(Path.Combine(directory, ".native-worker.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            var request = await Call("native_probe");
            string operationId = request.GetProperty("OperationId").GetString()!;
            var failed = await WaitOperation(operationId, "failed");
            if (!failed.GetProperty("Error").GetString()!.Contains(".native-worker.lock"))
                throw new InvalidDataException("Native settings contention did not produce the expected sharing failure.");
            // A swallowed native initializer exception must remain diagnosable, without tracing personal settings.
            var diagnostics = Directory.EnumerateFiles(Path.Combine(stateRoot, "runs", operationId), "native-settings-io-*.txt", SearchOption.AllDirectories).ToArray();
            if (diagnostics.Length == 0 || !diagnostics.Any(file => File.ReadAllText(file).Contains(".native-worker.lock")))
                throw new InvalidDataException("The actual settings sharing failure has no scoped I/O diagnostic.");
            VerifyWorkerExit(operationId);
        }
        var retry = await Call("native_probe");
        string retryId = retry.GetProperty("OperationId").GetString()!;
        await WaitOperation(retryId); VerifyWorkerExit(retryId);
        Console.WriteLine("NATIVE_SETTINGS_CONTENTION_AND_RECOVERY_PASS");
    }
    if (args.Contains("--render-only"))
    {
        // Focused real-protocol diagnostics shorten renderer investigation without substituting an engine double.
        int inputArgument = Array.IndexOf(args, "--input");
        if (inputArgument < 0 || !native) throw new ArgumentException("--render-only requires --native and --input <native file>.");
        string inputPath = Path.Combine(run, "render-input.bpm"); File.Copy(Path.GetFullPath(args[inputArgument + 1]), inputPath);
        var opened = await Call("native_inspect", new() { ["path"] = inputPath });
        string openId = opened.GetProperty("OperationId").GetString()!;
        var diagrams = (await WaitOperation(openId)).GetProperty("Result").GetProperty("result").GetProperty("Elements").EnumerateArray()
            .Where(e => e.GetProperty("Kind").GetString() == "Collaboration").Select(e => e.GetProperty("Id").GetString()!).ToArray();
        VerifyWorkerExit(openId);
        int diagramArgument = Array.IndexOf(args, "--diagram-id"), repeatArgument = Array.IndexOf(args, "--render-repetitions");
        string diagramId = diagramArgument >= 0 ? args[diagramArgument + 1] : diagrams.First();
        if (!diagrams.Contains(diagramId)) throw new ArgumentException("The requested diagram is not in the inspected native model.");
        int repetitions = repeatArgument >= 0 ? int.Parse(args[repeatArgument + 1], System.Globalization.CultureInfo.InvariantCulture) : 1;
        if (repetitions is < 1 or > 20) throw new ArgumentException("Render repetitions must be between 1 and 20.");
        // Each MCP request starts a different real engine worker; repeats never mask or skip a failed attempt.
        for (int attempt = 1; attempt <= repetitions; attempt++)
        {
            var render = await Call("native_render_svg", new() { ["path"] = inputPath, ["diagramId"] = diagramId });
            string renderId = render.GetProperty("OperationId").GetString()!;
            var output = await WaitOperation(renderId);
            string artifact = output.GetProperty("Result").GetProperty("result").GetProperty("Artifacts")[0].GetString()!;
            if (System.Xml.Linq.XDocument.Load(artifact).Root?.Name.LocalName != "svg") throw new InvalidDataException("Invalid native SVG.");
            VerifyWorkerExit(renderId);
            Console.WriteLine($"NATIVE_RENDER_ATTEMPT_PASS attempt={attempt}/{repetitions} diagram={diagramId} operation={renderId} artifact={artifact}");
        }
        Console.WriteLine("NATIVE_RENDER_DIAGNOSTIC_PASS evidence=" + run);
        return 0;
    }
    string xml = File.ReadAllText(Path.Combine(repo, "examples", "minimal.bpmn"));
    await Call("bpmn_create", new() { ["path"] = "Unicode path/Request.bpmn", ["xml"] = xml });
    var inspected = await Call("bpmn_inspect", new() { ["path"] = "Unicode path/Request.bpmn" });
    string revision = inspected.GetProperty("result").GetProperty("Revision").GetString()!;
    await Call("bpmn_apply_changes", new()
    {
        ["path"] = "Unicode path/Request.bpmn",
        ["expectedRevision"] = revision,
        ["changes"] = new[] { new { elementId = "Task_Review", property = "name", value = "Revisión — 東京" } }
    });
    var readback = await Call("bpmn_inspect", new() { ["path"] = "Unicode path/Request.bpmn" });
    var task = readback.GetProperty("result").GetProperty("Elements").EnumerateArray().Single(e => e.GetProperty("Id").GetString() == "Task_Review");
    if (task.GetProperty("Name").GetString() != "Revisión — 東京") throw new InvalidDataException("Exact Unicode readback was not observed.");
    await Call("bpmn_apply_changes", new()
    {
        ["path"] = "Unicode path/Request.bpmn",
        ["expectedRevision"] = revision,
        ["changes"] = new[] { new { elementId = "Task_Review", property = "name", value = "Must not overwrite" } }
    }, expectError: true);
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
            var multiEdit = await Call("native_apply_changes", new()
            {
                ["path"] = multiFile,
                ["expectedRevision"] = multiRevision,
                ["changes"] = new[] { new { elementId = Id(pack), name = "Nested durable edit — 東京" } }
            });
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

        var invalidEdit = await Call("native_apply_changes", new()
        {
            ["path"] = "Unicode path/Existing model.bpm",
            ["expectedRevision"] = nativeRevision,
            ["changes"] = new[] { new { elementId = "missing-element", name = "Must fail" } }
        });
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
            // Require an actual native registration event. Host startup/connection phases
            // do not prove the engine initialized its isolated settings namespace.
            if (phase == "native_registration" || phase.StartsWith("register:", StringComparison.Ordinal))
            {
                File.WriteAllText(Path.Combine(run, "native-cancellation-intent.json"), JsonSerializer.Serialize(new
                { operationId = cancelId, observedPhase = phase, observedState = "running", observedAtUtc = DateTime.UtcNow }));
                break;
            }
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
