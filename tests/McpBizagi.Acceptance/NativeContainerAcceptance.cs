using System.Text.Json;

/// <summary>Actual native container lifecycle through the public MCP transport and fresh-worker readback.</summary>
internal static class NativeContainerAcceptance
{
    public static async Task Run(string repo, string run, string? input, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        async Task<JsonElement> Operation(string name, Dictionary<string, object?> args, string status = "completed")
        {
            string id = (await call(name, args)).GetProperty("OperationId").GetString()!;
            var result = await wait(id, status); exited(id);
            return status == "completed" ? result.GetProperty("Result") : result;
        }
        string path;
        if (input == null)
        {
            File.Copy(Path.Combine(repo, "examples", "minimal.bpmn"), Path.Combine(run, "container source.bpmn"));
            var imported = await Operation("native_roundtrip", new() { ["path"] = "container source.bpmn", ["modelName"] = "Container acceptance" });
            path = imported.GetProperty("nativeArtifact").GetString()!;
        }
        else { path = Path.Combine(run, "container source.bpm"); File.Copy(Path.GetFullPath(input), path); }
        var baseline = await Operation("native_inspect", new() { ["path"] = path });
        string revision = baseline.GetProperty("sourceRevision").GetString()!;
        var original = baseline.GetProperty("result").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = original.First(e => e.GetProperty("Kind").GetString() == "Collaboration").GetProperty("Id").GetString()!;
        async Task<JsonElement> Apply(object[] mutations)
        {
            var result = await Operation("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = mutations });
            if (!result.GetProperty("fidelity").GetProperty("Preserved").GetBoolean()) throw new InvalidDataException("Container fidelity failed.");
            path = result.GetProperty("outputArtifact").GetString()!; revision = result.GetProperty("outputRevision").GetString()!;
            return result;
        }
        object Bounds(double x, double y, double width, double height) => new { X = x, Y = y, Width = width, Height = height };
        string pool = Guid.NewGuid().ToString(), process = Guid.NewGuid().ToString(), lane1 = Guid.NewGuid().ToString(), lane2 = Guid.NewGuid().ToString();
        string outer = Guid.NewGuid().ToString(), inner = Guid.NewGuid().ToString(), task = Guid.NewGuid().ToString(), task2 = Guid.NewGuid().ToString(), flow = Guid.NewGuid().ToString();
        string milestone1 = Guid.NewGuid().ToString(), milestone2 = Guid.NewGuid().ToString();
        object Create(string type, string id, string parent, string name, object geometry) => new { Operation = "create", ElementId = id, ParentId = parent, ElementType = type, Name = name, Geometry = geometry };
        var created = await Apply([
            new { Operation = "create", ElementId = pool, ProcessId = process, ParentId = diagram, ElementType = "Participant", Name = "Owned pool Ω",
                Geometry = new { X = 30, Y = 450, Width = 700, Height = 300, BackgroundArgb = -1, BorderArgb = -16777216 } },
            Create("Lane", lane1, process, "First lane Ω", Bounds(50, 0, 650, 150)),
            Create("Lane", lane2, process, "Second lane 日本語", Bounds(50, 150, 650, 150)),
            Create("Milestone", milestone1, process, "First phase Ω", Bounds(50, 0, 300, 300)),
            Create("Milestone", milestone2, process, "Second phase", Bounds(350, 0, 350, 300)),
            // Root process nodes use diagram coordinates, unlike children inside an embedded subprocess.
            Create("SubProcess", outer, process, "Outer subprocess Ω", Bounds(150, 520, 100, 70)),
            Create("SubProcess", inner, outer, "Nested subprocess", Bounds(40, 40, 100, 70)),
            Create("UserTask", task, inner, "Nested child", Bounds(30, 30, 100, 70)),
            Create("UserTask", task2, inner, "Second child", Bounds(200, 30, 100, 70)),
            new { Operation = "create", ElementId = flow, ParentId = inner, ElementType = "SequenceFlow", SourceId = task, TargetId = task2,
                Points = new[] { new { X = 130, Y = 65 }, new { X = 200, Y = 65 } } }
        ]);
        var opened = await Operation("native_inspect", new() { ["path"] = path });
        var reopened = opened.GetProperty("result").GetProperty("Elements").EnumerateArray().ToArray();
        if (reopened.Any(e => e.GetProperty("Kind").GetString() == "LaneSet")) throw new InvalidDataException("Runtime-only lane sets must not masquerade as durable identities.");
        foreach (string id in new[] { lane1, lane2 })
            if (reopened.Single(e => e.GetProperty("Id").GetString() == id).GetProperty("ParentId").GetString() != process)
                throw new InvalidDataException("Lane owner was not stable in a second independent read.");
        var rejected = await Operation("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["mutations"] = new[] { new { Operation = "delete", ElementId = pool } } }, "failed");
        await Operation("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["mutations"] = new[] { new { Operation = "update", ElementId = lane1, Geometry = Bounds(49, 0, 650, 150) } } }, "failed");
        var updated = await Apply([
            new { Operation = "update", ElementId = pool, Name = "Renamed pool Ω", Documentation = "Pool documentation",
                Geometry = new { X = 30, Y = 450, Width = 700, Height = 320, BackgroundArgb = -1, BorderArgb = -16777216 } },
            new { Operation = "update", ElementId = lane1, Name = "Renamed lane", Documentation = "Lane documentation", Geometry = Bounds(50, 0, 650, 120) },
            new { Operation = "update", ElementId = lane2, Geometry = Bounds(50, 120, 650, 200) },
            new { Operation = "update", ElementId = milestone1, Name = "Renamed phase", Documentation = "Phase documentation", Geometry = Bounds(50, 0, 250, 320) },
            new { Operation = "update", ElementId = milestone2, Geometry = Bounds(300, 0, 400, 320) },
            new { Operation = "update", ElementId = outer, Name = "Renamed outer", Documentation = "Subprocess documentation",
                Geometry = new { X = 100, Y = 485, Width = 100, Height = 70, Expanded = true }, ExpandedSize = new { Width = 550, Height = 250 } },
            new { Operation = "update", ElementId = inner, Name = "Renamed inner",
                Geometry = new { X = 40, Y = 40, Width = 100, Height = 70, Expanded = true }, ExpandedSize = new { Width = 400, Height = 160 } }
        ]);
        var rendered = await Operation("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram });
        var noop = await Operation("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        path = noop.GetProperty("outputArtifact").GetString()!; revision = noop.GetProperty("outputRevision").GetString()!;
        var cleared = await Apply(new[] { pool, lane1, milestone1, outer }.Select(id => (object)new { Operation = "update", ElementId = id, Documentation = "" }).ToArray());
        var collapsed = await Apply([
            new { Operation = "update", ElementId = outer, Geometry = Bounds(100, 485, 100, 70) },
            new { Operation = "update", ElementId = inner, Geometry = Bounds(40, 40, 100, 70) }
        ]);
        var collapsedElements = collapsed.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        if (collapsedElements.Single(e => e.GetProperty("Id").GetString() == outer).GetProperty("ExpandedGeometry").GetProperty("Width").GetDouble() != 550 ||
            collapsedElements.Single(e => e.GetProperty("Id").GetString() == inner).GetProperty("ExpandedGeometry").GetProperty("Width").GetDouble() != 400)
            throw new InvalidDataException("Collapsing must preserve the explicit latent expanded sizes.");
        var deleted = await Apply(new[] { flow, task2, task, inner, outer, milestone2, milestone1, lane2, lane1, pool }.Select(id => (object)new { Operation = "delete", ElementId = id }).ToArray());
        var final = deleted.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        if (!original.Select(e => e.GetProperty("Id").GetString()).Order().SequenceEqual(final.Select(e => e.GetProperty("Id").GetString()).Order()))
            throw new InvalidDataException("Container deletion changed the original durable identity set.");
        File.WriteAllText(Path.Combine(run, "container-acceptance.json"), JsonSerializer.Serialize(new { path, revision, baseline, created, opened, rejected, updated, rendered, noop, cleared, collapsed, deleted }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
