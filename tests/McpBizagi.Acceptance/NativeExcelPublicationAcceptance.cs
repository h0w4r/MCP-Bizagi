using System.Text.Json;

/// <summary>Real MCP/native Excel projection for empty/populated visible and implicit pools.</summary>
internal static class NativeExcelPublicationAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string expected = "completed")
        {
            string id = S(await call(tool, args), "OperationId");
            var response = await wait(id, expected); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-excel-publication.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return expected == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Excel first Ω", "Excel second 日本語" } });
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        var roots = graph.Where(e => S(e, "Kind") == "Collaboration").Select(e => S(e, "Id")).ToArray();
        var pools = roots.SelectMany((root, index) => graph.Where(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == root)
            .Select(e => new { Id = S(e, "Id"), Root = root, Main = e.GetProperty("IsMainParticipant").GetBoolean(), Index = index,
                Process = S(graph.Single(p => S(p, "Kind") == "Process" && S(p, "ParentId") == S(e, "Id")), "Id") })).ToArray();
        async Task<JsonElement> Publish(string[] omitted, string[]? selected = null)
        {
            var result = await Op("native_publish", new() { ["path"] = path, ["format"] = "excel", ["diagramIds"] = selected ?? [] });
            var losses = result.GetProperty("publicationOmissions").EnumerateArray().ToArray();
            if (!losses.Select(o => S(o, "ElementId")).ToHashSet().SetEquals(omitted) ||
                losses.Any(o => S(o, "Code") != "native_excel_empty_pool_sheet_omitted") || !result.GetProperty("nativeSourceUnmodified").GetBoolean())
                throw new InvalidDataException("Excel omission identity/reason or source integrity did not match actual input.");
            return result;
        }
        // A completely empty model has no visible worksheet in the native generator.
        // Preserve that actual rejection rather than injecting a fabricated sheet.
        var blank = await Op("native_publish", new() { ["path"] = path, ["format"] = "excel" }, "failed");
        if (!S(blank, "Error").Contains("A workbook must contain at least a visible worksheet", StringComparison.Ordinal))
            throw new InvalidDataException("Blank native workbook failed for an unrelated reason.");
        var active = pools.Where(p => p.Main == (p.Index == 0)).ToArray();
        var empty = pools.Except(active).ToArray();
        async Task Apply(object[] mutations)
        {
            var result = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = mutations });
            path = S(result, "outputArtifact"); revision = S(result, "outputRevision");
        }
        object Task(string parent, string label) => new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = parent,
            ElementType = "UserTask", Name = label, Documentation = "Actual Excel task description 日本語", Geometry = new { X = 160, Y = 120, Width = 120, Height = 70 } };
        // Names and documentation on omitted pools make the loss explicit, not an empty-metadata shortcut.
        await Apply(pools.Select(p => (object)new { Operation = "update", ElementId = p.Id, Name = $"Pool {p.Index} {(p.Main ? "implicit" : "visible")} Ω",
            Documentation = "Pool documentation whose omission must be disclosed" }).Concat(active.Select(p => Task(p.Process, "Populated " + p.Id))).ToArray());
        var mixed = await Publish(empty.Select(p => p.Id).ToArray());
        string text = S(mixed.GetProperty("reopened").GetProperty("Publication"), "Text");
        foreach (var pool in active) if (!text.Contains("Populated " + pool.Id, StringComparison.Ordinal)) throw new InvalidDataException("Populated pool task did not survive workbook readback.");
        await Publish(empty.Where(p => p.Root == roots[0]).Select(p => p.Id).ToArray(), [roots[0]]);
        await Apply(empty.Select(p => Task(p.Process, "Newly populated " + p.Id)).ToArray());
        await Publish([]);
        var failed = await Op("native_publish", new() { ["path"] = path, ["format"] = "excel", ["diagramIds"] = new[] { Guid.NewGuid().ToString() } }, "failed");
        if (!S(failed, "Error").Contains("selection", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Excel selection failed for an unrelated reason.");
        await Publish([]);
        var inspected = await Op("native_inspect", new() { ["path"] = path });
        if (S(inspected, "sourceRevision") != revision) throw new InvalidDataException("Excel publication changed native source bytes.");
    }

    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString()!;
}
