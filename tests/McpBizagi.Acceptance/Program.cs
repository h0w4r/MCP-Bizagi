using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// Independent protocol client: never invokes the server's implementation classes.
string repo = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? ".");
bool native = args.Contains("--native");
string run = Path.Combine(repo, ".local", "acceptance", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
Directory.CreateDirectory(run);
var env = new Dictionary<string, string?>
{
    ["MCP_BIZAGI_ROOT"] = run,
    ["MCP_BIZAGI_STATE"] = Path.Combine(run, "state"),
    ["MCP_BIZAGI_WORKER"] = Path.Combine(repo, "src", "McpBizagi.Worker", "bin", "Release", "net48", "McpBizagi.Worker.exe"),
    ["MCP_BIZAGI_EXPERIMENTAL_NATIVE"] = native ? "1" : "0"
};
var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "MCP-Bizagi acceptance",
    Command = "dotnet",
    Arguments = [Path.Combine(repo, "src", "McpBizagi.Server", "bin", "Release", "net10.0-windows", "McpBizagi.Server.dll")],
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
    if (!readback.GetRawText().Contains("Revisi", StringComparison.Ordinal)) throw new InvalidDataException("Readback was not observed.");
    await Call("bpmn_apply_changes", new() { ["path"] = "Unicode path/Request.bpmn", ["expectedRevision"] = revision,
        ["changes"] = new[] { new { elementId = "Task_Review", property = "name", value = "Must not overwrite" } } }, expectError: true);
    await Call("bpmn_inspect", new() { ["path"] = "../outside.bpmn" }, expectError: true);
    await Call("bpmn_validate", new() { ["path"] = "Unicode path/Request.bpmn" });
    Console.WriteLine("MCP_XML_E2E_PASS evidence=" + run);
    if (native)
    {
        var operation = await Call("native_roundtrip", new() { ["path"] = "Unicode path/Request.bpmn", ["modelName"] = "Acceptance model" });
        string id = operation.GetProperty("OperationId").GetString()!;
        var timer = Stopwatch.StartNew();
        string lastPhase = "";
        while (true)
        {
            var state = await Call("operation_get", new() { ["operationId"] = id });
            string phase = state.GetProperty("Phase").GetString()!;
            if (phase != lastPhase) { Console.WriteLine($"native phase={phase} elapsed={timer.Elapsed}"); lastPhase = phase; }
            string status = state.GetProperty("State").GetString()!;
            if (status == "completed") { Console.WriteLine("NATIVE_ROUNDTRIP_DIAGNOSTIC_PASS"); break; }
            if (status is "failed" or "cancelled" or "interrupted") throw new InvalidOperationException(state.GetRawText());
            await Task.Delay(3000);
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
