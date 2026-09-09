using System.Text.Json;

/// <summary>Actual stdio MCP/native acceptance. Independently checks readback without importing planner contracts.</summary>
internal static class NativeSurfaceLayoutAcceptance
{
    private static string S(JsonElement e, string name) => e.GetProperty(name).GetString()!;
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> input, string expected = "completed")
        {
            string id = S(await call(tool, input), "OperationId"); var state = await wait(id, expected); exited(id);
            receipts.Add(new { tool, id, state });
            File.WriteAllText(Path.Combine(run, "native-surface-layout.json"), JsonSerializer.Serialize(receipts));
            return expected == "completed" ? state.GetProperty("Result") : state;
        }
        string Id() => Guid.NewGuid().ToString();
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Automatic surface Ω", "Untouched 日本語" } });
        var initial = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(initial.First(e => S(e, "Kind") == "Collaboration"), "Id");
        string pool = S(initial.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && e.GetProperty("IsMainParticipant").ValueKind == JsonValueKind.False), "Id");
        string process = S(initial.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string a = Id(), b = Id(), c = Id(), sub = Id(), na = Id(), nb = Id(), nc = Id(), boundary = Id();
        var bounds = new Dictionary<string, (double X, double Y, double W, double H)> { [boundary] = (1049, 269, 22, 22) };
        object Box(double x, double y, double w = 120, double h = 80) => new { X = x, Y = y, Width = w, Height = h };
        object Node(string id, string owner, string type, double x, double y)
        {
            bounds[id] = (x, y, 120, 80);
            return new { Operation = "create", ElementId = id, ParentId = owner, ElementType = type,
                Name = "Automatic Ω", Geometry = Box(x, y), Style = new { LabelBounds = new { X = x - 10, Y = y + 90, Width = 140, Height = 30 } } };
        }
        object Edge(string owner, string source, string target)
        {
            // Author genuinely docked original routes. Malformed arbitrary source points
            // are a separate rejected corpus, not a substitute for a valid editor model.
            var s = bounds[source]; var t = bounds[target];
            var start = source == boundary ? new { X = s.X + s.W / 2, Y = s.Y + s.H }
                : new { X = s.X + s.W, Y = s.Y + s.H / 2 };
            var end = new { X = t.X, Y = t.Y + t.H / 2 };
            var points = source == target
                ? new[] { start, new { X = start.X + 40, Y = start.Y }, new { X = start.X + 40, Y = s.Y - 40 },
                    new { X = s.X - 40, Y = s.Y - 40 }, new { X = s.X - 40, Y = end.Y }, end }
                : new[] { start, new { X = (start.X + end.X) / 2, Y = start.Y }, new { X = (start.X + end.X) / 2, Y = end.Y }, end };
            return new { Operation = "create", ElementId = Id(), ParentId = owner, ElementType = "SequenceFlow",
                SourceId = source, TargetId = target, SourcePort = source == boundary ? "2" : "4", TargetPort = "3", Points = points };
        }
        var seeded = await Op("native_mutate", new() { ["path"] = S(created, "outputArtifact"), ["expectedRevision"] = S(created, "outputRevision"), ["mutations"] = new object[]
        {
            new { Operation = "update", ElementId = pool, Geometry = Box(30, 30, 4000, 3000) },
            Node(a, process, "UserTask", 1000, 200), Node(b, process, "ManualTask", 200, 700), Node(c, process, "UserTask", 700, 400), Node(sub, process, "SubProcess", 1500, 300),
            Node(na, sub, "UserTask", 700, 80), Node(nb, sub, "ManualTask", 120, 350), Node(nc, sub, "UserTask", 400, 200),
            new { Operation = "create", ElementId = boundary, ParentId = process, ElementType = "TimerIntermediate", EventMode = "Boundary", Name = "Anchored Ω",
                Geometry = Box(1049, 269, 22, 22), EventProperties = new { AttachedToActivityId = a, IsInterrupting = false },
                EventPayloads = new[] { new { Kind = "Timer", Timer = new { Kind = "Cycle", Text = "R3/PT5M" } } } },
            Edge(process, a, b), Edge(process, b, c), Edge(process, c, a), Edge(process, a, b), Edge(process, c, c), Edge(process, boundary, sub),
            Edge(sub, na, nb), Edge(sub, nb, nc), Edge(sub, nc, na), Edge(sub, na, na)
        } });
        var rich = await Op("native_presentation_apply", new() { ["path"] = S(seeded, "outputArtifact"), ["expectedRevision"] = S(seeded, "outputRevision"),
            ["changes"] = new[] { new { Action = new { DiagramId = diagram, ElementId = a, Type = "File", Content = "action-file:Layout Ω.bin" }, DataBase64 = Convert.ToBase64String(new byte[] { 0, 255, 17, 0, 128, 39 }) } } });
        Dictionary<string, object?> Args(JsonElement model, string owner, string direction) => new()
        { ["path"] = S(model, "outputArtifact"), ["expectedRevision"] = S(model, "outputRevision"), ["layout"] = new { DiagramId = diagram, OwnerId = owner, Direction = direction } };
        // A real reader runs, then an invalid surface fails. The next write must recover normally.
        await Op("native_surface_layout", Args(rich, pool, "Right"), "failed");
        var root = await Op("native_surface_layout", Args(rich, process, "Right"));
        var nested = await Op("native_surface_layout", Args(root, sub, "Down"));
        foreach (var result in new[] { root, nested }) Verify(result);
        await Op("native_save_copy", new() { ["path"] = S(nested, "outputArtifact"), ["expectedRevision"] = S(nested, "outputRevision") });
        await Op("native_render_svg", new() { ["path"] = S(nested, "outputArtifact"), ["diagramId"] = diagram });
        await Op("native_render_svg", new() { ["path"] = S(nested, "outputArtifact"), ["diagramId"] = diagram, ["subProcessId"] = sub });
        var original = await Op("native_inspect", new() { ["path"] = S(rich, "outputArtifact") });
        if (S(original, "sourceRevision") != S(rich, "outputRevision")) throw new InvalidDataException("Automatic layout overwrote its source.");
        File.WriteAllText(Path.Combine(run, "surface-layout-identities.json"), JsonSerializer.Serialize(new { diagram, process, sub, a, boundary }));
    }

    private static void Verify(JsonElement result)
    {
        var before = result.GetProperty("before").GetProperty("Elements").EnumerateArray().ToDictionary(e => S(e, "Id"));
        var after = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToDictionary(e => S(e, "Id"));
        var placements = result.GetProperty("layoutPlan").GetProperty("Alignment").GetProperty("Placements").EnumerateArray().ToArray();
        int moved = 0; var envelopes = new List<(double X, double Y, double W, double H)>();
        foreach (var placement in placements)
        {
            string id = S(placement, "ElementId"); var old = before[id].GetProperty("Geometry"); var next = after[id].GetProperty("Geometry");
            double D(JsonElement e, string k) => e.GetProperty(k).GetDouble();
            if (D(next, "X") != D(placement, "X") || D(next, "Y") != D(placement, "Y") || D(old, "Width") != D(next, "Width") || D(old, "Height") != D(next, "Height"))
                throw new InvalidDataException("Actual native geometry differs from pre-editor calculated intent.");
            double dx = D(next, "X") - D(old, "X"), dy = D(next, "Y") - D(old, "Y");
            if (dx != 0 || dy != 0) moved++;
            var items = new[] { after[id] }.Concat(after.Values.Where(e => S(e, "Kind") == "BoundaryEvent" && S(e.GetProperty("Event"), "AttachedToActivityId") == id));
            var rectangles = new List<(double X, double Y, double W, double H)>();
            foreach (var item in items)
            {
                var g = item.GetProperty("Geometry"); var previous = before[S(item, "Id")].GetProperty("Geometry");
                if (D(g, "X") - D(previous, "X") != dx || D(g, "Y") - D(previous, "Y") != dy)
                    throw new InvalidDataException("Boundary attachment offset changed during automatic layout.");
                rectangles.Add((D(g, "X"), D(g, "Y"), D(g, "Width"), D(g, "Height")));
                if (item.GetProperty("Style").ValueKind == JsonValueKind.Object && item.GetProperty("Style").GetProperty("LabelBounds").ValueKind == JsonValueKind.Object)
                {
                    var label = item.GetProperty("Style").GetProperty("LabelBounds");
                    if (D(label, "Width") != 0 || D(label, "Height") != 0)
                    {
                        var oldLabel = before[S(item, "Id")].GetProperty("Style").GetProperty("LabelBounds");
                        if (D(label, "X") - D(oldLabel, "X") != dx || D(label, "Y") - D(oldLabel, "Y") != dy || D(label, "Width") != D(oldLabel, "Width") || D(label, "Height") != D(oldLabel, "Height"))
                            throw new InvalidDataException("Automatic layout changed a manual label's offset or size.");
                        rectangles.Add((D(label, "X"), D(label, "Y"), D(label, "Width"), D(label, "Height")));
                    }
                }
            }
            double x = rectangles.Min(r => r.X), y = rectangles.Min(r => r.Y);
            envelopes.Add((x, y, rectangles.Max(r => r.X + r.W) - x, rectangles.Max(r => r.Y + r.H) - y));
        }
        for (int i = 0; i < envelopes.Count; i++) for (int j = 0; j < i; j++)
        {
            var a = envelopes[i]; var b = envelopes[j];
            if (a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H)
                throw new InvalidDataException("Independent readback node/attachment/label envelopes overlap.");
        }
        if (moved < 2 || !result.GetProperty("fidelity").GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Automatic native layout lacks durable movement/fidelity evidence.");
    }
}
