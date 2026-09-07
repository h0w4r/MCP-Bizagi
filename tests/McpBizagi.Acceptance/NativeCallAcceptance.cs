using System.Text.Json;

/// <summary>Real installed-engine call links, nested references and independent durable MCP readback.</summary>
internal static class NativeCallAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Operation(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = (await call(tool, args)).GetProperty("OperationId").GetString()!;
            var result = await wait(id, state); exited(id); receipts.Add(new { tool, id, result });
            File.WriteAllText(Path.Combine(run, "native-calls-acceptance.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? result.GetProperty("Result") : result;
        }
        void Require(bool ok, string error) { if (!ok) throw new InvalidDataException(error); }
        var created = await Operation("native_model_create", new() { ["diagramNames"] = new[] { "Caller Ω", "Service 日本語", "Alternate" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string Diagram(string name) => graph.Single(e => e.GetProperty("Kind").GetString() == "Collaboration" && e.GetProperty("Name").GetString() == name).GetProperty("Id").GetString()!;
        string Process(string diagram, bool main = false)
        {
            string pool = graph.Single(e => e.GetProperty("Kind").GetString() == "Participant" && e.GetProperty("DiagramId").GetString() == diagram && e.GetProperty("IsMainParticipant").GetBoolean() == main).GetProperty("Id").GetString()!;
            return graph.Single(e => e.GetProperty("Kind").GetString() == "Process" && e.GetProperty("ParentId").GetString() == pool).GetProperty("Id").GetString()!;
        }
        string callerDiagram = Diagram("Caller Ω"), serviceDiagram = Diagram("Service 日本語"), alternateDiagram = Diagram("Alternate");
        string callerProcess = Process(callerDiagram), serviceProcess = Process(serviceDiagram), alternateProcess = Process(alternateDiagram), internalProcess = Process(callerDiagram, main: true);
        string rootCall = Guid.NewGuid().ToString(), nestedCall = Guid.NewGuid().ToString(), subprocess = Guid.NewGuid().ToString();
        string path = created.GetProperty("outputArtifact").GetString()!, revision = created.GetProperty("outputRevision").GetString()!;
        async Task<JsonElement> Mutate(object[] mutations, string state = "completed")
        {
            var result = await Operation("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = mutations }, state);
            if (state == "completed") { path = result.GetProperty("outputArtifact").GetString()!; revision = result.GetProperty("outputRevision").GetString()!; }
            return result;
        }
        var inserted = await Mutate([
            new { Operation = "create", ElementId = rootCall, ParentId = callerProcess, ElementType = "CallActivity", Name = "Call service Ω", CallTarget = new { ProcessId = serviceProcess }, Geometry = new { X = 160, Y = 100, Width = 110, Height = 65 } },
            new { Operation = "create", ElementId = subprocess, ParentId = callerProcess, ElementType = "SubProcess", Name = "Nested caller", Geometry = new { X = 380, Y = 100, Width = 110, Height = 65 }, ExpandedSize = new { Width = 500, Height = 300 } },
            new { Operation = "create", ElementId = nestedCall, ParentId = subprocess, ElementType = "CallActivity", Name = "Internal diagram call", CallTarget = new { ProcessId = internalProcess }, Geometry = new { X = 80, Y = 80, Width = 110, Height = 65 } }
        ]);
        JsonElement[] Elements(JsonElement result) => result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string Target(JsonElement[] elements, string id) => elements.Single(e => e.GetProperty("Id").GetString() == id).GetProperty("CallReference").GetProperty("CatalogProcessId").GetString()!;
        Require(Target(Elements(inserted), rootCall) == serviceProcess && Target(Elements(inserted), nestedCall) == internalProcess, "Created local calls lost native target identities.");
        var renamed = await Mutate([new { Operation = "update", ElementId = rootCall, Name = "Call service updated Ω", Documentation = "The unchanged target must survive an unrelated edit." }]);
        Require(Target(Elements(renamed), rootCall) == serviceProcess, "An omitted CallTarget did not preserve the native reference.");
        var wrongTarget = await Mutate([new { Operation = "update", ElementId = rootCall, CallTarget = new { ProcessId = serviceDiagram } }], "failed");
        Require(wrongTarget.GetProperty("Error").GetString()!.Contains("participant Process.Id"), "A diagram identity was accepted or rejected for an unrelated reason.");
        var wrongKind = await Mutate([new { Operation = "update", ElementId = subprocess, CallTarget = new { ProcessId = serviceProcess } }], "failed");
        Require(wrongKind.GetProperty("Error").GetString()!.Contains("only to a native call activity"), "An embedded subprocess was accepted as a reusable call.");
        await Operation("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        await Operation("native_render_svg", new() { ["path"] = path, ["diagramId"] = callerDiagram });
        var rejected = await Operation("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["patch"] = new { Changes = new[] { new { Operation = "delete", DiagramId = serviceDiagram } }, OpenedItems = Array.Empty<object>() } }, "failed");
        Require(rejected.GetProperty("Error").GetString()!.Contains("call activity references"), "Referenced-diagram deletion failed for the wrong reason.");
        string servicePool = graph.Single(e => e.GetProperty("Kind").GetString() == "Process" && e.GetProperty("Id").GetString() == serviceProcess).GetProperty("ParentId").GetString()!;
        var rejectedPool = await Mutate([new { Operation = "delete", ElementId = servicePool }], "failed");
        Require(rejectedPool.GetProperty("Error").GetString()!.Contains("call activity references"), "Referenced process was not protected from pool deletion.");
        await Mutate([new { Operation = "update", ElementId = nestedCall, CallTarget = new { ProcessId = alternateProcess } }]);
        var rejectedNested = await Operation("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["patch"] = new { Changes = new[] { new { Operation = "delete", DiagramId = alternateDiagram } }, OpenedItems = Array.Empty<object>() } }, "failed");
        Require(rejectedNested.GetProperty("Error").GetString()!.Contains("call activity references"), "Nested caller did not protect its separate target diagram.");
        await Mutate([new { Operation = "update", ElementId = nestedCall, CallTarget = new { ProcessId = internalProcess } }]);
        var redirected = await Mutate([new { Operation = "update", ElementId = rootCall, CallTarget = new { ProcessId = alternateProcess } }]);
        Require(Target(Elements(redirected), rootCall) == alternateProcess, "Redirected call did not survive restart.");
        var unlinked = await Mutate([new { Operation = "update", ElementId = rootCall, CallTarget = new { ProcessId = "" } }]);
        Require(Target(Elements(unlinked), rootCall) == "", "Unlinked call retained a native catalog target.");
        await Mutate([new { Operation = "update", ElementId = rootCall, CallTarget = new { ProcessId = serviceProcess } }]);
        var clone = await Operation("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["patch"] = new { Changes = new[] { new { Operation = "clone", DiagramId = callerDiagram, Name = "Cloned caller" } } } });
        var mapping = clone.GetProperty("edited").GetProperty("DiagramClones")[0];
        string Mapped(string id) => mapping.GetProperty("Identities").EnumerateArray().Single(i => i.GetProperty("SourceId").GetString() == id).GetProperty("TargetId").GetString()!;
        var clonedGraph = Elements(clone);
        Require(Target(clonedGraph, Mapped(rootCall)) == serviceProcess && Target(clonedGraph, Mapped(nestedCall)) == Mapped(internalProcess), "Native clone changed external-to-diagram links or failed to remap an internal link.");
        path = clone.GetProperty("outputArtifact").GetString()!; revision = clone.GetProperty("outputRevision").GetString()!;
        await Operation("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        await Operation("native_render_svg", new() { ["path"] = path, ["diagramId"] = mapping.GetProperty("TargetId").GetString() });
        // Unlink every surviving caller explicitly; deleting referenced targets must never rewrite other diagrams.
        await Mutate([new { Operation = "update", ElementId = rootCall, CallTarget = new { ProcessId = "" } },
            new { Operation = "update", ElementId = Mapped(rootCall), CallTarget = new { ProcessId = "" } }]);
        await Mutate([new { Operation = "delete", ElementId = rootCall }, new { Operation = "delete", ElementId = nestedCall }]);
        await Operation("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["patch"] = new { Changes = new[] { new { Operation = "delete", DiagramId = serviceDiagram } }, OpenedItems = Array.Empty<object>() } });
        await Operation("native_inspect", new() { ["path"] = created.GetProperty("outputArtifact").GetString() });
    }
}
