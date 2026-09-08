using System.Text.Json;

/// <summary>Independent real MCP port lifecycle; no calls to production policy helpers.</summary>
internal static class NativeConnectorPortAcceptance
{
    // Independently declared protocol DTOs avoid referencing the implementation or its contracts.
    private sealed class Point { public double X { get; set; } public double Y { get; set; } }
    private sealed class BoundsDto { public double X { get; set; } public double Y { get; set; } public double Width { get; set; } public double Height { get; set; } }
    private sealed class Element
    {
        public string Id { get; set; } = ""; public string Kind { get; set; } = "";
        public string DiagramId { get; set; } = ""; public string ParentId { get; set; } = "";
        public bool? IsMainParticipant { get; set; }
        public string SourceId { get; set; } = ""; public string TargetId { get; set; } = "";
        public string? SourcePort { get; set; } public string? TargetPort { get; set; }
        public Point[] Points { get; set; } = [];
    }
    private sealed class Mutation
    {
        public string Operation { get; set; } = ""; public string ElementId { get; set; } = "";
        public string ParentId { get; set; } = ""; public string ElementType { get; set; } = "";
        public string ProcessId { get; set; } = ""; public string? Name { get; set; }
        public BoundsDto? Geometry { get; set; }
        public string SourceId { get; set; } = ""; public string TargetId { get; set; } = "";
        public string? SourcePort { get; set; } public string? TargetPort { get; set; }
        public Point[] Points { get; set; } = [];
    }
    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString()!;
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string expected = "completed")
        {
            string id = S(await call(tool, args), "OperationId"); var state = await wait(id, expected); exited(id);
            receipts.Add(new { tool, id, state });
            File.WriteAllText(Path.Combine(run, "native-connector-ports.json"), JsonSerializer.Serialize(receipts));
            return expected == "completed" ? state.GetProperty("Result") : state;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Native ports Ω", "Untouched 日本語" } });
        var initial = created.GetProperty("reopened").GetProperty("Elements").Deserialize<Element[]>()!;
        string diagram = initial.First(e => e.Kind == "Collaboration").Id;
        string pool = initial.Single(e => e.Kind == "Participant" && e.DiagramId == diagram && e.IsMainParticipant == false).Id;
        string process = initial.Single(e => e.Kind == "Process" && e.ParentId == pool).Id;
        string a = Guid.NewGuid().ToString(), b = Guid.NewGuid().ToString(), c = Guid.NewGuid().ToString();
        string secondPool = Guid.NewGuid().ToString(), secondProcess = Guid.NewGuid().ToString();
        string flow = Guid.NewGuid().ToString(), association = Guid.NewGuid().ToString(), message = Guid.NewGuid().ToString();
        BoundsDto Bounds(double x, double y, double w = 120, double h = 80) => new() { X = x, Y = y, Width = w, Height = h };
        Point[] Points(double y = 150) => [new() { X = 320, Y = y }, new() { X = 500, Y = y }];
        Mutation Node(string id, string owner, double x, double y) => new() { Operation = "create", ElementId = id,
            ParentId = owner, ElementType = "UserTask", Name = "Port task Ω", Geometry = Bounds(x, y) };
        Mutation Edge(string id, string kind, string owner, string target, Point[] points) => new()
        { Operation = "create", ElementId = id, ParentId = owner, ElementType = kind, SourceId = a, TargetId = target, Points = points, SourcePort = "4", TargetPort = "3" };
        var seeded = await Op("native_mutate", new() { ["path"] = S(created, "outputArtifact"), ["expectedRevision"] = S(created, "outputRevision"), ["mutations"] = new[]
        {
            new Mutation { Operation = "update", ElementId = pool, Geometry = Bounds(30, 30, 1000, 500) },
            new Mutation { Operation = "create", ElementId = secondPool, ParentId = diagram, ElementType = "Participant", ProcessId = secondProcess, Geometry = Bounds(30, 650, 1000, 400) },
            Node(a, process, 200, 110), Node(b, process, 500, 110), Node(c, secondProcess, 500, 730),
            Edge(flow, "SequenceFlow", process, b, Points()), Edge(association, "Association", process, b, Points()),
            Edge(message, "MessageFlow", diagram, c, [new() { X = 320, Y = 150 }, new() { X = 400, Y = 150 }, new() { X = 400, Y = 770 }, new() { X = 500, Y = 770 }])
        } });
        var ids = new[] { flow, association, message };
        void Check(JsonElement result, string? source, string? target)
        {
            var elements = result.GetProperty("reopened").GetProperty("Elements").Deserialize<Element[]>()!;
            foreach (string id in ids)
            {
                var e = elements.Single(e => e.Id == id);
                if ((e.SourcePort ?? "") != (source ?? "") || (e.TargetPort ?? "") != (target ?? "")) throw new InvalidDataException("Native port metadata did not survive a fresh process.");
            }
            if (!result.GetProperty("fidelity").GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Native port edit changed unrequested content.");
        }
        Check(seeded, "4", "3");
        string original = S(seeded, "outputArtifact"), originalRevision = S(seeded, "outputRevision");
        var current = seeded;
        Mutation[] Reconnect(string? source, string? target) => current.GetProperty("reopened").GetProperty("Elements").Deserialize<Element[]>()!
            .Where(e => ids.Contains(e.Id)).Select(e => new Mutation { Operation = "reconnect", ElementId = e.Id,
                SourceId = e.SourceId, TargetId = e.TargetId, Points = e.Points, SourcePort = source, TargetPort = target }).ToArray();
        Dictionary<string, object?> Args(Mutation[] changes) => new() { ["path"] = S(current, "outputArtifact"), ["expectedRevision"] = S(current, "outputRevision"), ["mutations"] = changes };
        // Bad identifiers fail at the actual protocol boundary, before any native write.
        await error("native_mutate", Args(Reconnect("75", null)));
        var invalid = Reconnect(null, null); invalid[0].TargetId = Guid.NewGuid().ToString();
        var failed = await Op("native_mutate", Args(invalid), "failed");
        if (!S(failed, "Error").Contains("Unknown native identity", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unexpected native failure reason.");
        current = await Op("native_mutate", Args(Reconnect(null, null))); Check(current, "4", "3");
        // Explicit replacement and clear are distinct from omission. Metadata alone does not reroute points.
        current = await Op("native_mutate", Args(Reconnect("1", null))); Check(current, "1", "3");
        current = await Op("native_mutate", Args(Reconnect("", ""))); Check(current, null, null);
        current = await Op("native_mutate", Args(Reconnect("4", "3"))); Check(current, "4", "3");
        await Op("native_save_copy", new() { ["path"] = S(current, "outputArtifact"), ["expectedRevision"] = S(current, "outputRevision") });
        await Op("native_render_svg", new() { ["path"] = S(current, "outputArtifact"), ["diagramId"] = diagram });
        var originalRead = await Op("native_inspect", new() { ["path"] = original });
        if (S(originalRead, "sourceRevision") != originalRevision) throw new InvalidDataException("Input artifact was modified.");
        File.WriteAllText(Path.Combine(run, "connector-port-result.json"), JsonSerializer.Serialize(new { output = S(current, "outputArtifact"), ids, diagram }));
    }
}
