using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Real MCP inlining, original identity checks, revision conflict and owned cancellation/recovery.</summary>
internal static class NativeInliningAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error,
        string? inputPath, string? requestPath)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> input, string state = "completed")
        {
            string id = S(await call(tool, input), "OperationId"); var result = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, result });
            File.WriteAllText(Path.Combine(run, "native-inlining.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? result.GetProperty("Result") : result;
        }
        string path, revision; JsonElement intent;
        if (inputPath != null)
        {
            if (requestPath == null) throw new ArgumentException("Existing native input requires --inlining-request.");
            path = Path.Combine(run, "inline source Ω.bpm"); File.Copy(inputPath, path);
            revision = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            intent = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(requestPath));
        }
        else
        {
            var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Caller Ω", "Shared body 日本語" } });
            var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
            var diagrams = graph.Where(e => S(e, "Kind") == "Collaboration").Select(e => S(e, "Id")).ToArray();
            string Process(string diagram)
            {
                string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
                return S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
            }
            string caller = Process(diagrams[0]), callee = Process(diagrams[1]);
            string id = Guid.NewGuid().ToString(), other = Guid.NewGuid().ToString(), a = Guid.NewGuid().ToString(), b = Guid.NewGuid().ToString();
            var seeded = await Op("native_mutate", new() { ["path"] = S(created, "outputArtifact"), ["expectedRevision"] = S(created, "outputRevision"), ["mutations"] = new object[]
            {
                new { Operation = "create", ElementId = id, ParentId = caller, ElementType = "CallActivity", Name = "Inline Ω", CallTarget = new { ProcessId = callee }, Geometry = new { X = 260, Y = 85, Width = 120, Height = 80 } },
                new { Operation = "create", ElementId = other, ParentId = caller, ElementType = "CallActivity", Name = "Keep shared call", CallTarget = new { ProcessId = callee }, Geometry = new { X = 530, Y = 85, Width = 120, Height = 80 } },
                new { Operation = "create", ElementId = a, ParentId = callee, ElementType = "UserTask", Name = "Review 日本語", Geometry = new { X = 100, Y = 100, Width = 120, Height = 80 } },
                new { Operation = "create", ElementId = b, ParentId = callee, ElementType = "ManualTask", Name = "Approve Ω", Geometry = new { X = 330, Y = 100, Width = 120, Height = 80 } },
                new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = callee, ElementType = "SequenceFlow", SourceId = a, TargetId = b, Points = new[] { new { X = 220, Y = 140 }, new { X = 330, Y = 140 } } }
            } });
            path = S(seeded, "outputArtifact"); revision = S(seeded, "outputRevision");
            intent = JsonSerializer.SerializeToElement(new { ElementId = id, ExpectedProcessId = callee, Position = new { X = 60, Y = 60 } });
        }
        Dictionary<string, object?> Args(JsonElement request, string hash) => new() { ["path"] = path, ["expectedRevision"] = hash, ["inlining"] = request };
        var completed = await Op("native_subprocess_inline", Args(intent, revision));
        Verify(completed, intent);
        await error("native_subprocess_inline", Args(intent, new string('0', 64)));
        var missing = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(intent)!;
        missing["ExpectedProcessId"] = JsonSerializer.SerializeToElement(Guid.NewGuid().ToString());
        await Op("native_subprocess_inline", Args(JsonSerializer.SerializeToElement(missing), revision), "failed");
        string cancelId = S(await call("native_subprocess_inline", Args(intent, revision)), "OperationId");
        while (true)
        {
            var state = await call("operation_get", new() { ["operationId"] = cancelId }); string phase = S(state, "Phase");
            if (phase.StartsWith("register:", StringComparison.Ordinal) || phase == "native_registration")
            { File.WriteAllText(Path.Combine(run, "inlining-cancellation-intent.json"), JsonSerializer.Serialize(new { cancelId, phase, observedAtUtc = DateTime.UtcNow })); break; }
            if (S(state, "State") is "completed" or "failed" or "cancelled" or "interrupted") throw new InvalidDataException("No live native cancellation boundary observed.");
            await Task.Delay(100);
        }
        await call("operation_cancel", new() { ["operationId"] = cancelId });
        var cancelled = await wait(cancelId, "cancelled"); exited(cancelId);
        receipts.Add(new { tool = "operation_cancel", id = cancelId, result = cancelled });
        Verify(await Op("native_subprocess_inline", Args(intent, revision)), intent);
        if (inputPath != null && Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) != revision) throw new InvalidDataException("Inlining modified its source.");
    }

    private static void Verify(JsonElement result, JsonElement intent)
    {
        if (!result.GetProperty("conversionFidelity").GetProperty("Preserved").GetBoolean() || !result.GetProperty("copyFidelity").GetProperty("Preserved").GetBoolean())
            throw new InvalidDataException("Inlining did not pass both whole-archive stages.");
        var before = result.GetProperty("before").GetProperty("Elements").EnumerateArray().ToArray();
        var after = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToDictionary(e => S(e, "Id"));
        string callId = S(intent, "ElementId");
        foreach (var old in before)
        {
            var current = after[S(old, "Id")];
            foreach (var field in old.EnumerateObject())
            {
                if (S(old, "Id") == callId && field.Name is "Kind" or "ElementType" or "CallReference" or "SubProcess") continue;
                if (!JsonElement.DeepEquals(field.Value, current.GetProperty(field.Name))) throw new InvalidDataException("Inlining changed an original graph field: " + field.Name);
            }
        }
        if (S(after[callId], "Kind") != "SubProcess" || S(after[callId], "ElementType") != "SubProcess" || after[callId].GetProperty("CallReference").ValueKind != JsonValueKind.Null)
            throw new InvalidDataException("Inlining did not produce the expected native type.");
        var identities = result.GetProperty("receipt").GetProperty("Identities").EnumerateArray().ToArray();
        var map = identities.ToDictionary(e => S(e, "SourceId"), e => S(e, "TargetId"));
        if (map.Count == 0 || after.Count != before.Length + map.Count || map.Values.Any(id => before.Any(e => S(e, "Id") == id)))
            throw new InvalidDataException("Inlining did not add exactly its reported body identities.");
        foreach (var old in before.Where(e => map.ContainsKey(S(e, "Id"))))
        {
            var current = after[map[S(old, "Id")]];
            if (S(current, "ParentId") != map.GetValueOrDefault(S(old, "ParentId"), callId) || S(current, "Kind") != S(old, "Kind") || S(current, "Name") != S(old, "Name"))
                throw new InvalidDataException("Copied body ownership, type or name differs.");
        }
    }
    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString()!;
}
