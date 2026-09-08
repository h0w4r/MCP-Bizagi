using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

/// <summary>Operator-facing MCP/native Visio interchange, including failures and worker cancellation.</summary>
internal static class NativeVisioAcceptance
{
    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString()!;

    public static async Task Run(string run, string stateRoot, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> input, string state = "completed")
        {
            string id = S(await call(tool, input), "OperationId"); var response = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-visio.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Visio 日本語 Ω", "Other Ω" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string[] diagrams = graph.Where(e => S(e, "Kind") == "Collaboration").Select(e => S(e, "Id")).ToArray();
        string pool = S(graph.First(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagrams[0] && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string process = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string a = Guid.NewGuid().ToString(), b = Guid.NewGuid().ToString(), sub = Guid.NewGuid().ToString();
        var seeded = await Op("native_mutate", new() { ["path"] = S(created, "outputArtifact"), ["expectedRevision"] = S(created, "outputRevision"), ["mutations"] = new object[] {
            new { Operation = "create", ElementId = a, ParentId = process, ElementType = "AbstractTask", Name = "Review 日本語 Ω", Geometry = new { X = 130, Y = 80, Width = 120, Height = 70 } },
            new { Operation = "create", ElementId = b, ParentId = process, ElementType = "UserTask", Name = "Approve Ω", Geometry = new { X = 300, Y = 80, Width = 120, Height = 70 } },
            new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = process, ElementType = "SequenceFlow", SourceId = a, TargetId = b, Points = new[] { new { X = 250, Y = 115 }, new { X = 300, Y = 115 } } },
            new { Operation = "create", ElementId = sub, ParentId = process, ElementType = "SubProcess", Name = "Nested Ω", Geometry = new { X = 440, Y = 70, Width = 240, Height = 170 } },
            new { Operation = "create", ElementId = Guid.NewGuid().ToString(), ParentId = sub, ElementType = "ManualTask", Name = "Inner 日本語", Geometry = new { X = 40, Y = 40, Width = 120, Height = 70 } }
        } });
        string path = S(seeded, "outputArtifact"), revision = S(seeded, "outputRevision");
        Dictionary<string, object?> Args(string[] selected) => new() { ["path"] = path, ["expectedRevision"] = revision, ["diagramIds"] = selected, ["acknowledgeFormatLimits"] = true };
        await error("native_visio_export", new(Args(diagrams)) { ["acknowledgeFormatLimits"] = false });
        await error("native_visio_export", new(Args(diagrams)) { ["expectedRevision"] = new string('0', 64) });
        await error("native_visio_export", Args([diagrams[0], diagrams[0]]));
        await Op("native_visio_export", Args([Guid.NewGuid().ToString()]), "failed");
        var exported = await Op("native_visio_export", Args(diagrams));
        if (exported.GetProperty("pages").GetArrayLength() != 3 || exported.GetProperty("graphDifferences").GetArrayLength() == 0 ||
            !exported.GetProperty("verification").GetProperty("Fidelity").GetProperty("Preserved").GetBoolean())
            throw new InvalidDataException("Visio multi-page native readback/loss evidence missing.");
        var restored = exported.GetProperty("verification").GetProperty("Verified").GetProperty("Elements").EnumerateArray().ToArray();
        if (!restored.Any(e => S(e, "Name") == "Review 日本語 Ω") || !restored.Any(e => S(e, "Name") == "Approve Ω"))
            throw new InvalidDataException("Tested root task Unicode labels did not survive native exchange.");
        var byId = restored.ToDictionary(e => S(e, "Id"));
        if (!restored.Any(e => S(e, "Kind") == "SequenceFlow" && byId.TryGetValue(S(e, "SourceId"), out var source) &&
            byId.TryGetValue(S(e, "TargetId"), out var target) && S(source, "Name") == "Review 日本語 Ω" && S(target, "Name") == "Approve Ω"))
            throw new InvalidDataException("Tested native Visio flow did not retain the observed source/target relationship.");
        var writtenGraph = exported.GetProperty("exported").GetProperty("Elements").EnumerateArray().ToArray();
        var pageNames = exported.GetProperty("pages").EnumerateArray().Select(p => S(p, "Name")).Take(diagrams.Length).ToArray();
        if (!pageNames.SequenceEqual(diagrams.Select(id => S(writtenGraph.Single(e => S(e, "Id") == id), "Name"))))
            throw new InvalidDataException("Native Visio page labels/order do not match the explicit selection.");
        // Document a real vendor limit, not a successful nested-content gate: an extra page is blank.
        if (exported.GetProperty("emptyPages").GetArrayLength() != 1 || !exported.GetProperty("projectionDifferences").EnumerateArray().Any(d =>
            S(d, "Name") == "Inner 日本語" && d.GetProperty("SourceCount").GetInt32() == 1 && d.GetProperty("ImportedCount").GetInt32() == 0))
            throw new InvalidDataException("Observed nested-content limitation changed; review the native route before updating its contract.");
        var vdx = new { Path = S(exported, "outputArtifact"), ExpectedRevision = S(exported, "outputRevision") };
        var imported = await Op("native_visio_import", new() { ["input"] = vdx, ["acknowledgeFormatLimits"] = true, ["modelName"] = "Imported 日本語 Ω" });
        if (imported.GetProperty("verification").GetProperty("Verified").GetProperty("Diagrams").GetArrayLength() != 3)
            throw new InvalidDataException("Visio import did not persist every page.");
        string localVdx = Path.Combine(run, "Visio input 日本語 Ω.vdx");
        string exportId = S(exported, "outputArtifact").Split(':')[1];
        File.Copy(Path.Combine(stateRoot, "runs", exportId, "artifacts", "export.vdx"), localVdx);
        var fileInput = new { Path = localVdx, ExpectedRevision = S(exported, "outputRevision") };
        await Op("native_visio_import", new() { ["input"] = fileInput, ["acknowledgeFormatLimits"] = true });
        if (Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(localVdx))) != fileInput.ExpectedRevision) throw new InvalidDataException("Visio import modified its local source.");
        await error("native_visio_import", new() { ["input"] = new { Path = localVdx, ExpectedRevision = new string('0', 64) }, ["acknowledgeFormatLimits"] = true });
        // Use a real exported document with deliberately unsupported stencil names, not a fake parser failure.
        string invalid = Path.Combine(run, "unsupported stencil.vdx");
        var unsupported = XDocument.Load(localVdx); XNamespace ns = "http://schemas.microsoft.com/visio/2003/core";
        foreach (var master in unsupported.Root!.Element(ns + "Masters")!.Elements(ns + "Master"))
        {
            string name = "MCP unsupported stencil " + (string?)master.Attribute("ID");
            master.SetAttributeValue("Name", name); master.SetAttributeValue("NameU", name);
        }
        unsupported.Save(invalid);
        await Op("native_visio_import", new() { ["input"] = new { Path = invalid, ExpectedRevision = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(invalid))) }, ["acknowledgeFormatLimits"] = true }, "failed");
        var subset = await Op("native_visio_export", Args([diagrams[1]]));
        if (subset.GetProperty("pages").GetArrayLength() != 1) throw new InvalidDataException("Visio selection included an unwanted diagram.");
        // Cancel while a real owned native exporter has registered, then repeat the same read-only export.
        string cancelId = S(await call("native_visio_export", Args([diagrams[1]])), "OperationId");
        while (true)
        {
            var state = await call("operation_get", new() { ["operationId"] = cancelId }); string phase = S(state, "Phase");
            if (phase == "native_registration" || phase.StartsWith("register:", StringComparison.Ordinal))
            { File.WriteAllText(Path.Combine(run, "visio-cancellation-intent.json"), JsonSerializer.Serialize(new { cancelId, phase })); break; }
            if (S(state, "State") is "completed" or "failed" or "cancelled" or "interrupted") throw new InvalidDataException("Active Visio cancellation boundary was not observed.");
            await Task.Delay(100);
        }
        await call("operation_cancel", new() { ["operationId"] = cancelId });
        var cancelled = await wait(cancelId, "cancelled"); exited(cancelId); receipts.Add(new { tool = "operation_cancel", id = cancelId, response = cancelled });
        await Op("native_visio_export", Args([diagrams[1]]));
        var original = await Op("native_inspect", new() { ["path"] = path });
        if (S(original, "sourceRevision") != revision) throw new InvalidDataException("Visio interchange changed the original native source.");
    }
}
