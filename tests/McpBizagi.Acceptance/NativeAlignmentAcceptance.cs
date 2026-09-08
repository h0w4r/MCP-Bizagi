using System.Text.Json;

/// <summary>Real stdio MCP alignment corpus. No server classes or substitute engine are invoked.</summary>
internal static class NativeAlignmentAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> input, string state = "completed")
        {
            string id = S(await call(tool, input), "OperationId"); var result = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, result });
            File.WriteAllText(Path.Combine(run, "native-alignment.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? result.GetProperty("Result") : result;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Alignment Ω", "Untouched 日本語" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(graph.First(e => S(e, "Kind") == "Collaboration"), "Id");
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string process = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string a = Guid.NewGuid().ToString(), b = Guid.NewGuid().ToString(), c = Guid.NewGuid().ToString(), flow = Guid.NewGuid().ToString();
        var seeded = await Op("native_mutate", new() { ["path"] = S(created, "outputArtifact"), ["expectedRevision"] = S(created, "outputRevision"), ["mutations"] = new object[]
        {
            new { Operation = "create", ElementId = a, ParentId = process, ElementType = "AbstractTask", Name = "Review 日本語 Ω", Geometry = new { X = 260, Y = 85, Width = 120, Height = 80 } },
            new { Operation = "create", ElementId = b, ParentId = process, ElementType = "UserTask", Name = "Approve Ω", Geometry = new { X = 430, Y = 130, Width = 120, Height = 70 } },
            new { Operation = "create", ElementId = c, ParentId = process, ElementType = "ManualTask", Name = "Record Ω", Geometry = new { X = 350, Y = 155, Width = 60, Height = 40 } },
            new { Operation = "create", ElementId = flow, ParentId = process, ElementType = "SequenceFlow", SourceId = a, TargetId = b,
                Points = new[] { new { X = 380, Y = 125 }, new { X = 430, Y = 165 } } }
        } });
        string path = S(seeded, "outputArtifact"), revision = S(seeded, "outputRevision");
        Dictionary<string, object?> Args(string mode, string source, string hash) => new() { ["path"] = source, ["expectedRevision"] = hash,
            ["alignment"] = new { DiagramId = diagram, ElementIds = new[] { a, b, c }, Mode = mode } };
        foreach (string mode in new[] { "Top", "Bottom", "Left", "Right", "Horizontal", "Vertical", "HorizontalEvenly", "VerticalEvenly" })
        {
            var result = await Op("native_elements_align", Args(mode, path, revision));
            if (!result.GetProperty("fidelity").GetProperty("Preserved").GetBoolean() || result.GetProperty("receipt").GetProperty("NoOp").GetBoolean())
                throw new InvalidDataException("Actual native layout acceptance did not prove a nonempty preserved change.");
            if (mode == "Bottom")
            {
                var noOp = await Op("native_elements_align", Args(mode, S(result, "outputArtifact"), S(result, "outputRevision")));
                if (!noOp.GetProperty("receipt").GetProperty("NoOp").GetBoolean()) throw new InvalidDataException("Repeated alignment was not a verified no-op.");
            }
        }
        await error("native_elements_align", Args("Bottom", path, new string('0', 64)));
        await error("native_elements_align", Args("executeScript", path, revision));
        // A semantically dangerous frontend move is rejected through the real MCP operation.
        var unsafeSeed = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new object[]
        {
            new { Operation = "update", ElementId = b, Geometry = new { X = 430, Y = 620, Width = 120, Height = 70 } }
        } });
        var rejected = await Op("native_elements_align", Args("Bottom", S(unsafeSeed, "outputArtifact"), S(unsafeSeed, "outputRevision")), "failed");
        if (!rejected.GetRawText().Contains("semantic", StringComparison.OrdinalIgnoreCase) && !rejected.GetRawText().Contains("participant", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Expected explicit semantic/containment rejection, not an unrelated failure.");
        // Follow the actual failure with another successful operation against the unchanged fixture.
        await Op("native_elements_align", Args("Top", path, revision));
    }
    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString()!;
}
