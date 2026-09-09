using System.Text.Json;

/// <summary>Independent actual MCP acceptance for host-attached events; never calls production policy helpers.</summary>
internal static class NativeAnchoredAlignmentAcceptance
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
            File.WriteAllText(Path.Combine(run, "native-anchored-alignment.json"), JsonSerializer.Serialize(receipts));
            return expected == "completed" ? state.GetProperty("Result") : state;
        }
        string Id() => Guid.NewGuid().ToString();
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Anchored alignment Ω", "Untouched 日本語" } });
        var initial = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(initial.First(e => S(e, "Kind") == "Collaboration"), "Id");
        string pool = S(initial.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && e.GetProperty("IsMainParticipant").ValueKind == JsonValueKind.False), "Id");
        string process = S(initial.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string a = Id(), b = Id(), sub = Id(), nestedA = Id(), nestedB = Id(), first = Id(), second = Id(), nestedEvent = Id();
        object Bounds(double x, double y, double w = 120, double h = 80) => new { X = x, Y = y, Width = w, Height = h };
        object Node(string id, string owner, string type, double x, double y) => new { Operation = "create", ElementId = id, ParentId = owner, ElementType = type, Name = "Host Ω", Geometry = Bounds(x, y) };
        object Event(string id, string owner, string host, string type, double x, double y, bool interrupting) => new
        {
            Operation = "create", ElementId = id, ParentId = owner, ElementType = type, EventMode = "Boundary", Name = "Attached Ω",
            Geometry = Bounds(x, y, 22, 22), EventProperties = new { AttachedToActivityId = host, IsInterrupting = interrupting },
            Style = new { LabelBounds = new { X = x - 30, Y = y + 30, Width = 100, Height = 30 } },
            EventPayloads = type == "TimerIntermediate"
                ? new object[] { new { Kind = "Timer", Timer = new { Kind = "Cycle", Text = "R3/PT5M" } } }
                : new object[] { new { Kind = "Signal", Name = "Preserve definition Ω" } }
        };
        object Edge(string owner, string source, string target, double x, double y, double endX, double endY, string sourcePort) => new
        { Operation = "create", ElementId = Id(), ParentId = owner, ElementType = "SequenceFlow", SourceId = source, TargetId = target,
            SourcePort = sourcePort, TargetPort = "3", Points = new[] { new { X = x, Y = y }, new { X = endX, Y = endY } } };
        var seeded = await Op("native_mutate", new() { ["path"] = S(created, "outputArtifact"), ["expectedRevision"] = S(created, "outputRevision"), ["mutations"] = new object[]
        {
            new { Operation = "update", ElementId = pool, Geometry = Bounds(30, 30, 1500, 1000) },
            Node(a, process, "UserTask", 200, 100), Node(b, process, "ManualTask", 500, 300), Node(sub, process, "SubProcess", 1000, 120),
            Event(first, process, a, "TimerIntermediate", 249, 169, false), Event(second, process, a, "SignalIntermediate", 309, 129, true),
            Edge(process, first, b, 260, 191, 500, 340, "2"), Edge(process, second, b, 331, 140, 500, 340, "4"),
            Node(nestedA, sub, "UserTask", 100, 60), Node(nestedB, sub, "ManualTask", 350, 200),
            Event(nestedEvent, sub, nestedA, "TimerIntermediate", 149, 129, true), Edge(sub, nestedEvent, nestedB, 160, 151, 350, 240, "2")
        } });
        var rich = await Op("native_presentation_apply", new() { ["path"] = S(seeded, "outputArtifact"), ["expectedRevision"] = S(seeded, "outputRevision"),
            ["changes"] = new[] { new { Action = new { DiagramId = diagram, ElementId = first, Type = "File", Content = "action-file:Boundary Ω.bin" }, DataBase64 = Convert.ToBase64String(new byte[] { 0, 255, 7, 0, 19, 128 }) } } });
        string original = S(rich, "outputArtifact"), revision = S(rich, "outputRevision");
        Dictionary<string, object?> Args(JsonElement model, string mode, string surface, params string[] ids) => new()
        { ["path"] = S(model, "outputArtifact"), ["expectedRevision"] = S(model, "outputRevision"), ["alignment"] = new { DiagramId = diagram, SubProcessId = surface, Mode = mode, ElementIds = ids } };
        var rejected = await Op("native_elements_align", Args(rich, "Bottom", "", first, b), "failed");
        if (!S(rejected, "Error").Contains("unsupported native layout shape", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Incorrect direct-boundary rejection.");
        var root = await Op("native_elements_align", Args(rich, "Bottom", "", a, b));
        var nested = await Op("native_elements_align", Args(root, "Right", sub, nestedA, nestedB));
        void Check(JsonElement before, JsonElement after, string host, string[] boundaryIds, double dx, double dy)
        {
            var left = before.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToDictionary(e => S(e, "Id"));
            var right = after.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToDictionary(e => S(e, "Id"));
            foreach (string id in boundaryIds.Append(host))
            {
                var old = left[id]; var current = right[id]; var g = old.GetProperty("Geometry"); var h = current.GetProperty("Geometry");
                if (Math.Abs(h.GetProperty("X").GetDouble() - g.GetProperty("X").GetDouble() - dx) > .001 ||
                    Math.Abs(h.GetProperty("Y").GetDouble() - g.GetProperty("Y").GetDouble() - dy) > .001 ||
                    h.GetProperty("Width").GetDouble() != g.GetProperty("Width").GetDouble() || h.GetProperty("Height").GetDouble() != g.GetProperty("Height").GetDouble() ||
                    S(old, "ParentId") != S(current, "ParentId") || old.GetProperty("Event").GetRawText() != current.GetProperty("Event").GetRawText())
                    throw new InvalidDataException("Native anchored geometry, ownership or event payload changed unexpectedly.");
                if (boundaryIds.Contains(id))
                {
                    var label = old.GetProperty("Style").GetProperty("LabelBounds"); var moved = current.GetProperty("Style").GetProperty("LabelBounds");
                    if (moved.GetProperty("X").GetDouble() != label.GetProperty("X").GetDouble() + dx || moved.GetProperty("Y").GetDouble() != label.GetProperty("Y").GetDouble() + dy ||
                        moved.GetProperty("Width").GetDouble() != label.GetProperty("Width").GetDouble() || moved.GetProperty("Height").GetDouble() != label.GetProperty("Height").GetDouble())
                        throw new InvalidDataException("Native anchored manual label lost its offset or dimensions.");
                }
            }
            if (!after.GetProperty("fidelity").GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Native anchored alignment lost archive content.");
        }
        Check(rich, root, a, [first, second], 0, 200); Check(root, nested, nestedA, [nestedEvent], 250, 0);
        await Op("native_elements_align", Args(nested, "Right", sub, nestedA, nestedB));
        await Op("native_save_copy", new() { ["path"] = S(nested, "outputArtifact"), ["expectedRevision"] = S(nested, "outputRevision") });
        await Op("native_render_svg", new() { ["path"] = S(nested, "outputArtifact"), ["diagramId"] = diagram });
        await Op("native_render_svg", new() { ["path"] = S(nested, "outputArtifact"), ["diagramId"] = diagram, ["subProcessId"] = sub });
        var inspected = await Op("native_inspect", new() { ["path"] = original });
        if (S(inspected, "sourceRevision") != revision) throw new InvalidDataException("Original anchored input was overwritten.");
        File.WriteAllText(Path.Combine(run, "anchored-identities.json"), JsonSerializer.Serialize(new { diagram, a, b, sub, nestedA, nestedB, first, second, nestedEvent }));
    }
}
