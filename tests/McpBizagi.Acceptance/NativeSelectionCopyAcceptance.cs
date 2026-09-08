using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Real MCP native copying; no adapter calls or replacement engine.</summary>
internal static class NativeSelectionCopyAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error,
        string? inputPath = null, string? requestPath = null)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> input, string state = "completed")
        {
            string id = S(await call(tool, input), "OperationId"); var result = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, result });
            File.WriteAllText(Path.Combine(run, "native-selection-copy.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? result.GetProperty("Result") : result;
        }
        string path, revision; object selection; string[] roots;
        if (inputPath != null)
        {
            // A caller-supplied native corpus is copied inside the acceptance workspace.
            if (requestPath == null) throw new ArgumentException("Existing copy input requires --selection-request.");
            path = Path.Combine(run, "copy source Ω.bpm"); File.Copy(inputPath, path);
            revision = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            var intent = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(requestPath));
            selection = intent;
            roots = intent.GetProperty("ElementIds").EnumerateArray().Select(e => e.GetString()!).ToArray();
        }
        else
        {
            var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Copy Ω", "Untouched 日本語" } });
            var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
            string diagram = S(graph.First(e => S(e, "Kind") == "Collaboration"), "Id");
            string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
            string process = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
            string a = Guid.NewGuid().ToString(), b = Guid.NewGuid().ToString(), flow = Guid.NewGuid().ToString();
            var seeded = await Op("native_mutate", new() { ["path"] = S(created, "outputArtifact"), ["expectedRevision"] = S(created, "outputRevision"), ["mutations"] = new object[]
            {
                new { Operation = "create", ElementId = a, ParentId = process, ElementType = "AbstractTask", Name = "Review 日本語 Ω", Geometry = new { X = 260, Y = 85, Width = 120, Height = 80 } },
                new { Operation = "create", ElementId = b, ParentId = process, ElementType = "UserTask", Name = "Approve Ω", Geometry = new { X = 430, Y = 130, Width = 120, Height = 70 } },
                new { Operation = "create", ElementId = flow, ParentId = process, ElementType = "SequenceFlow", SourceId = a, TargetId = b,
                    Points = new[] { new { X = 380, Y = 125 }, new { X = 430, Y = 165 } } }
            } });
            path = S(seeded, "outputArtifact"); revision = S(seeded, "outputRevision"); roots = [a, b, flow];
            selection = new { SourceDiagramId = diagram, TargetParentId = process, ElementIds = roots, Position = new { X = 280, Y = 105 } };
        }
        Dictionary<string, object?> Args(object selected, string hash) => new() { ["path"] = path, ["expectedRevision"] = hash, ["selection"] = selected };
        var copied = await Op("native_elements_copy", Args(selection, revision));
        if (!copied.GetProperty("fidelity").GetProperty("Preserved").GetBoolean() || copied.GetProperty("receipt").GetProperty("Identities").GetArrayLength() < roots.Length)
            throw new InvalidDataException("Copy did not prove a complete preserved native selection.");
        await error("native_elements_copy", Args(selection, new string('0', 64)));
        // A valid request with an absent root must fail after a real source-reader worker.
        var rejectedIntent = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(selection))!;
        rejectedIntent["ElementIds"] = JsonSerializer.SerializeToElement(new[] { Guid.NewGuid().ToString() });
        var rejected = await Op("native_elements_copy", Args(rejectedIntent, revision), "failed");
        if (!rejected.GetRawText().Contains("absent", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Expected missing-selection failure.");
        // Cancel an actually running native source-reader, not a queued synthetic task.
        string cancelId = S(await call("native_elements_copy", Args(selection, revision)), "OperationId");
        while (true)
        {
            var state = await call("operation_get", new() { ["operationId"] = cancelId });
            string phase = S(state, "Phase");
            if (phase.StartsWith("register:", StringComparison.Ordinal) || phase == "native_registration")
            {
                File.WriteAllText(Path.Combine(run, "copy-cancellation-intent.json"), JsonSerializer.Serialize(new { cancelId, phase, observedAtUtc = DateTime.UtcNow }));
                break;
            }
            if (S(state, "State") is "completed" or "failed" or "cancelled" or "interrupted") throw new InvalidDataException("No active native cancellation boundary observed.");
            await Task.Delay(100);
        }
        await call("operation_cancel", new() { ["operationId"] = cancelId });
        var cancelled = await wait(cancelId, "cancelled"); exited(cancelId);
        receipts.Add(new { tool = "operation_cancel", id = cancelId, result = cancelled });
        await Op("native_elements_copy", Args(selection, revision));
        if (inputPath != null && Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) != revision)
            throw new InvalidDataException("Selection copying modified the original file.");
    }
    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString()!;
}
