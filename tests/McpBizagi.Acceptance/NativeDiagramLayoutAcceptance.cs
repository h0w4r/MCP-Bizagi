using System.Text.Json;
// Independent protocol DTOs below; no references to planner or implementation contracts.

// Real native corpus: two-dimensional partitions, two pools, nested expanded bodies and opaque action bytes.
internal static class NativeDiagramLayoutAcceptance
{
    private static string S(JsonElement e, string n) => e.GetProperty(n).GetString()!;
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, bool includeGroups = false, bool includeExpandedAnchors = false)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> input, string expected = "completed")
        {
            string id = S(await call(tool, input), "OperationId"); var state = await wait(id, expected); exited(id);
            receipts.Add(new { tool, id, state }); File.WriteAllText(Path.Combine(run, "native-diagram-layout.json"), JsonSerializer.Serialize(receipts));
            return expected == "completed" ? state.GetProperty("Result") : state;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Partitioned layout Ω", "Untouched 日本語" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").Deserialize<NativeElement[]>()!;
        string diagram = graph.First(e => e.Kind == "Collaboration").Id;
        string firstPool = graph.Single(e => e.DiagramId == diagram && e.Kind == "Participant" && e.IsMainParticipant == false).Id;
        string firstProcess = graph.Single(e => e.Kind == "Process" && e.ParentId == firstPool).Id;
        // Exercise zero- and one-node diagrams through the real new tool as well.
        await Op("native_diagram_layout", new() { ["path"] = S(created, "outputArtifact"), ["expectedRevision"] = S(created, "outputRevision"),
            ["layout"] = new { DiagramId = diagram, Direction = "Right" } });
        var single = await Op("native_mutate", new() { ["path"] = S(created, "outputArtifact"), ["expectedRevision"] = S(created, "outputRevision"),
            ["mutations"] = new[] { new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = firstProcess, ElementType = "UserTask", Name = "Single Ω",
                Geometry = new { X = 160, Y = 80, Width = 120, Height = 70 } } } });
        await Op("native_diagram_layout", new() { ["path"] = S(single, "outputArtifact"), ["expectedRevision"] = S(single, "outputRevision"),
            ["layout"] = new { DiagramId = diagram, Direction = "Down" } });
        string secondPool = Guid.NewGuid().ToString(), secondProcess = Guid.NewGuid().ToString();
        string outer = Guid.NewGuid().ToString(), inner = Guid.NewGuid().ToString();
        var seed = new List<NativeMutation>(); var roots = new List<string>();
        NativeGeometry Bounds(double x, double y, double w, double h, bool expanded = false) => new() { X = x, Y = y, Width = w, Height = h, Expanded = expanded };
        string Node(string type, string owner, double x, double y, double w = 120, double h = 70, string? id = null)
        {
            id ??= Guid.NewGuid().ToString();
            seed.Add(new() { Operation = "create", ElementId = id, ParentId = owner, ElementType = type, Name = type + " Ω", Geometry = Bounds(x, y, w, h) }); return id;
        }
        NativeGeometry Visible(string id)
        {
            var item = seed.Single(m => m.ElementId == id); var g = item.Geometry!;
            return Bounds(g.X, g.Y, g.Expanded ? item.ExpandedSize!.Width : g.Width, g.Expanded ? item.ExpandedSize!.Height : g.Height);
        }
        void Edge(string owner, string a, string b, string sourcePort = "4", string targetPort = "3")
        {
            var s = Visible(a); var t = Visible(b);
            NativePoint Point(NativeGeometry g, string port) => port switch
            {
                "1" => new() { X = (float)(g.X + g.Width / 2), Y = (float)g.Y },
                "2" => new() { X = (float)(g.X + g.Width / 2), Y = (float)(g.Y + g.Height) },
                "3" => new() { X = (float)g.X, Y = (float)(g.Y + g.Height / 2) },
                "4" => new() { X = (float)(g.X + g.Width), Y = (float)(g.Y + g.Height / 2) },
                _ => throw new InvalidDataException("Unknown authored port")
            };
            var first = Point(s, sourcePort); var last = Point(t, targetPort);
            NativePoint[] points = a == b
                ? [first, new() { X = first.X + 40, Y = first.Y }, new() { X = first.X + 40, Y = (float)s.Y - 40 },
                    new() { X = (float)s.X - 40, Y = (float)s.Y - 40 }, new() { X = (float)s.X - 40, Y = last.Y }, last]
                : [first, new() { X = (first.X + last.X) / 2, Y = first.Y }, new() { X = (first.X + last.X) / 2, Y = last.Y }, last];
            seed.Add(new() { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = owner,
                ElementType = "SequenceFlow", SourceId = a, TargetId = b, SourcePort = sourcePort, TargetPort = targetPort, Points = points });
        }
        void Boundary(string owner, string host, string target)
        {
            var h = Visible(host); string id = Guid.NewGuid().ToString();
            seed.Add(new() { Operation = "create", ElementId = id, ParentId = owner, ElementType = "TimerIntermediate", EventMode = "Boundary",
                Name = "Anchored timer Ω", Geometry = Bounds(h.X + h.Width / 2 - 11, h.Y + h.Height - 11, 22, 22),
                EventProperties = new() { AttachedToActivityId = host, IsInterrupting = false },
                EventPayloads = [new() { Kind = "Timer", Timer = new() { Kind = "Cycle", Text = "R3/PT5M" } }],
                Style = new() { LabelBounds = new() { X = h.X + h.Width + 25, Y = h.Y + h.Height + 25, Width = 130, Height = 30 } } });
            Edge(owner, id, target, "2", "1");
        }
        foreach (var (pool, process, y) in new[] { (firstPool, firstProcess, 30d), (secondPool, secondProcess, 2030d) })
        {
            seed.Add(new() { Operation = pool == firstPool ? "update" : "create", ElementId = pool, ParentId = pool == firstPool ? "" : diagram,
                ElementType = pool == firstPool ? "" : "Participant", ProcessId = pool == firstPool ? "" : process, Geometry = Bounds(30, y, 2550, 1800) });
            Node("Lane", process, 50, 0, 2500, 900); Node("Lane", process, 50, 900, 2500, 900);
            Node("Milestone", process, 50, 0, 1250, 1800); Node("Milestone", process, 1300, 0, 1250, 1800);
            string a = Node("UserTask", process, 250, y + 80), b = Node("ManualTask", process, 1450, y + 100);
            string c = Node("ServiceTask", process, 300, y + 1100), d = Node("UserTask", process, 1600, y + 1100), e = Node("ManualTask", process, 1780, y + 1250);
            roots.Add(a); Edge(process, a, b); Edge(process, a, c, "2", "1"); Edge(process, b, d); Edge(process, c, d); Edge(process, d, e); Edge(process, e, a); Edge(process, d, d);
            Boundary(process, d, b);
        }
        Node("SubProcess", firstProcess, 160, 230, 120, 80, outer);
        seed[^1].Geometry!.Expanded = true; seed[^1].ExpandedSize = new() { Width = 900, Height = 600 };
        Node("SubProcess", outer, 50, 80, 110, 70, inner);
        seed[^1].Geometry!.Expanded = true; seed[^1].ExpandedSize = new() { Width = 650, Height = 350 };
        string t1 = Node("UserTask", inner, 50, 50), t2 = Node("ManualTask", inner, 250, 130), t3 = Node("ServiceTask", inner, 450, 60);
        Edge(inner, t1, t2); Edge(inner, t1, t3); Edge(inner, t2, t3);
        string sibling = Node("UserTask", outer, 50, 470); Edge(outer, inner, sibling);
        Edge(firstProcess, roots[0], outer);
        Boundary(inner, t2, t3);
        if (includeExpandedAnchors)
        {
            // Exercise real native anchor resolution at root and embedded levels.
            // Both hosts are resized by the actual bottom-up layout, not a fixture resolver.
            Boundary(firstProcess, outer, roots[0]);
            Boundary(outer, inner, sibling);
        }
        // Actual diagram-owned message flows connect separate native pools in both directions.
        Edge(diagram, roots[0], roots[1], "2", "1"); seed[^1].ElementType = "MessageFlow";
        Edge(diagram, roots[1], roots[0], "4", "4"); seed[^1].ElementType = "MessageFlow";
        foreach (var task in seed.Where(m => m.ElementType is "UserTask" or "ManualTask" or "ServiceTask"))
            task.Style = new() { LabelBounds = new() { X = task.Geometry!.X + 5, Y = task.Geometry.Y + 10, Width = 95, Height = 35 } };
        if (includeGroups)
        {
            // Groups retain graphical enclosure relationships; they never own the
            // enclosed BPMN nodes. Include nested, per-pool, task-subset and empty enclosures.
            foreach (var g in new[] { Bounds(20, 20, 2570, 1820, true), Bounds(20, 2020, 2570, 1820, true),
                         Bounds(10, 10, 2590, 3840, true), Bounds(5000, 5000, 100, 100, true),
                         Bounds(240, 100, 140, 90, true), Bounds(1590, 1120, 320, 240, true) })
                seed.Add(new() { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = diagram,
                    ElementType = "Group", Name = "Graphical enclosure Ω", Geometry = g });
        }
        var seeded = await Op("native_mutate", new() { ["path"] = S(created, "outputArtifact"), ["expectedRevision"] = S(created, "outputRevision"), ["mutations"] = seed });
        var rich = await Op("native_presentation_apply", new() { ["path"] = S(seeded, "outputArtifact"), ["expectedRevision"] = S(seeded, "outputRevision"),
            ["changes"] = new[] { new { Action = new { DiagramId = diagram, ElementId = t1, Type = "File", Content = "action-file:Keep exact Ω.bin" }, DataBase64 = Convert.ToBase64String(new byte[] { 0, 255, 19, 28, 0, 13 }) } } });
        string path = S(rich, "outputArtifact"), revision = S(rich, "outputRevision");
        graph = rich.GetProperty("reopened").GetProperty("Elements").Deserialize<NativeElement[]>()!;
        File.WriteAllText(Path.Combine(run, "partitioned-before.json"), rich.GetProperty("reopened").GetProperty("Elements").GetRawText());
        if (includeGroups)
        {
            // Native persistence permits a graphical boundary through a task.
            // Layout must reject that ambiguous enclosure before writing a result.
            string partialId = seed.Single(m => m.ElementType == "Group" && m.Geometry!.X == 240).ElementId;
            var partial = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision,
                ["mutations"] = new[] { new NativeMutation { Operation = "update", ElementId = partialId, Geometry = Bounds(260, 100, 120, 90, true) } } });
            var denied = await Op("native_diagram_layout", new() { ["path"] = S(partial, "outputArtifact"), ["expectedRevision"] = S(partial, "outputRevision"),
                ["layout"] = new { DiagramId = diagram, Direction = "Right" } }, "failed");
            if (!S(denied, "Error").Contains("Group boundary cuts", StringComparison.Ordinal)) throw new InvalidDataException("Unexpected partial-group rejection.");
        }
        var rejected = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["mutations"] = new[] { new NativeMutation { Operation = "update", ElementId = firstPool, Geometry = Bounds(30, 30, 3000, 1800) } } }, "failed");
        if (!S(rejected, "Error").Contains("Lane partitions", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Incorrect rejection reason.");
        await Op("native_diagram_layout", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["layout"] = new { DiagramId = Guid.NewGuid().ToString(), Direction = "Right" } }, "failed");
        var right = await Op("native_diagram_layout", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["layout"] = new { DiagramId = diagram, Direction = "Right" } });
        var applied = await Op("native_diagram_layout", new() { ["path"] = S(right, "outputArtifact"), ["expectedRevision"] = S(right, "outputRevision"),
            ["layout"] = new { DiagramId = diagram, Direction = "Down" } });
        if (!applied.GetProperty("fidelity").GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Native partitioned layout lost unrequested archive content.");
        var after = applied.GetProperty("reopened").GetProperty("Elements").Deserialize<NativeElement[]>()!;
        File.WriteAllText(Path.Combine(run, "partitioned-after.json"), applied.GetProperty("reopened").GetProperty("Elements").GetRawText());
        var beforeMembership = Membership(graph, diagram); var afterMembership = Membership(after, diagram);
        if (!beforeMembership.OrderBy(p => p.Key).SequenceEqual(afterMembership.OrderBy(p => p.Key))) throw new InvalidDataException("Persisted native lane/milestone assignment changed.");
        foreach (var sub in after.Where(e => e.SubProcess.ValueKind == JsonValueKind.Object && e.DiagramId == diagram))
            foreach (var child in after.Where(e => e.ParentId == sub.Id && e.Geometry != null && e.SourceId == ""))
            {
                var visible = child.Geometry!.Expanded ? child.ExpandedGeometry! : child.Geometry;
                if (visible.X < 0 || visible.Y < 0 || visible.X + visible.Width > sub.ExpandedGeometry!.Width || visible.Y + visible.Height > sub.ExpandedGeometry.Height)
                    throw new InvalidDataException("Expanded descendant escapes the independently read native container.");
            }
        string output = S(applied, "outputArtifact"), hash = S(applied, "outputRevision");
        await Op("native_save_copy", new() { ["path"] = output, ["expectedRevision"] = hash });
        await Op("native_render_svg", new() { ["path"] = output, ["diagramId"] = diagram });
        await Op("native_render_svg", new() { ["path"] = output, ["diagramId"] = diagram, ["subProcessId"] = inner });
        var original = await Op("native_inspect", new() { ["path"] = path });
        if (S(original, "sourceRevision") != revision) throw new InvalidDataException("Original input changed.");
        File.WriteAllText(Path.Combine(run, "partition-membership.json"), JsonSerializer.Serialize(new { beforeMembership, afterMembership }));
        // The anchor corpus cancels the actual installed editor preview; the
        // base/group corpus retains its independent solver/writer cancellation.
        string cancelId = S(await call("native_diagram_layout", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["layout"] = new { DiagramId = diagram, Direction = "Right" } }), "OperationId");
        string lastPhase = "";
        while (true)
        {
            var view = await call("operation_get", new() { ["operationId"] = cancelId }); string phase = S(view, "Phase");
            if (phase != lastPhase) { Console.WriteLine("diagram cancellation phase=" + phase); lastPhase = phase; }
            bool observed = includeExpandedAnchors ? phase.StartsWith("native_editor_", StringComparison.Ordinal)
                : phase.StartsWith("native_diagram_layout_", StringComparison.Ordinal) || phase.StartsWith("native_mutation:", StringComparison.Ordinal) || phase == "native_persist_edited_bpm";
            if (observed) break;
            if (S(view, "State") is "completed" or "failed" or "cancelled" or "interrupted") throw new InvalidDataException("No live diagram planning/writing cancellation phase was observed.");
            await Task.Delay(100);
        }
        await call("operation_cancel", new() { ["operationId"] = cancelId });
        var cancelled = await wait(cancelId, "cancelled"); exited(cancelId); receipts.Add(new { tool = "operation_cancel", id = cancelId, state = cancelled, observedPhase = lastPhase });
        await Op("native_diagram_layout", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["layout"] = new { DiagramId = diagram, Direction = "Right" } });
    }

    private static Dictionary<string, string> Membership(NativeElement[] graph, string diagram)
    {
        var result = new Dictionary<string, string>();
        foreach (var pool in graph.Where(e => e.Kind == "Participant" && e.IsMainParticipant == false && e.DiagramId == diagram))
        {
            var process = graph.Single(e => e.ParentId == pool.Id && e.Kind == "Process"); var origin = pool.Geometry!;
            var lanes = graph.Where(e => e.ParentId == process.Id && e.Kind == "Lane").ToArray(); var stages = graph.Where(e => e.ParentId == process.Id && e.Kind == "Milestone").ToArray();
            foreach (var node in graph.Where(e => e.ParentId == process.Id && e.SourceId == "" && e.Kind is not "Lane" and not "Milestone"))
            {
                var box = node.Geometry!.Expanded ? node.ExpandedGeometry! : node.Geometry; double x = box.X - origin.X, y = box.Y - origin.Y;
                string lane = lanes.Single(l => y >= l.Geometry!.Y && y + box.Height <= l.Geometry.Y + l.Geometry.Height).Id;
                string stage = stages.Single(s => x >= s.Geometry!.X && x + box.Width <= s.Geometry.X + s.Geometry.Width).Id;
                result.Add(node.Id, pool.Id + "/" + lane + "/" + stage);
            }
        }
        return result;
    }
    // Deliberately independent, minimal wire shapes. The client never calculates layout.
    private sealed class NativeGeometry { public double X { get; set; } public double Y { get; set; } public double Width { get; set; } public double Height { get; set; } public bool Expanded { get; set; } }
    private sealed class NativeSize { public double Width { get; set; } public double Height { get; set; } }
    private sealed class NativePoint { public float X { get; set; } public float Y { get; set; } }
    private sealed class NativeLabel { public double X { get; set; } public double Y { get; set; } public double Width { get; set; } public double Height { get; set; } }
    private sealed class NativeStyle { public NativeLabel? LabelBounds { get; set; } }
    private sealed class NativeEventProperties { public string? AttachedToActivityId { get; set; } public bool? IsInterrupting { get; set; } }
    private sealed class NativeTimer { public string Kind { get; set; } = ""; public string Text { get; set; } = ""; }
    private sealed class NativePayload { public string Kind { get; set; } = ""; public NativeTimer? Timer { get; set; } }
    private sealed class NativeElement
    {
        public string Id { get; set; } = ""; public string Kind { get; set; } = ""; public string DiagramId { get; set; } = "";
        public string ParentId { get; set; } = ""; public string SourceId { get; set; } = "";
        public NativeGeometry? Geometry { get; set; } public NativeGeometry? ExpandedGeometry { get; set; }
        public bool? IsMainParticipant { get; set; } public JsonElement SubProcess { get; set; }
    }
    private sealed class NativeMutation
    {
        public string Operation { get; set; } = ""; public string ElementId { get; set; } = ""; public string ParentId { get; set; } = "";
        public string ElementType { get; set; } = ""; public string ProcessId { get; set; } = ""; public string? Name { get; set; }
        public NativeGeometry? Geometry { get; set; } public NativeSize? ExpandedSize { get; set; }
        public string SourceId { get; set; } = ""; public string TargetId { get; set; } = "";
        public string? SourcePort { get; set; } public string? TargetPort { get; set; } public NativePoint[] Points { get; set; } = [];
        public string? EventMode { get; set; } public NativeEventProperties? EventProperties { get; set; }
        public NativePayload[]? EventPayloads { get; set; } public NativeStyle? Style { get; set; }
    }
}
