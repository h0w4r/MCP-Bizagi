using System.Text.Json;

/// <summary>Independent MCP/native data and association lifecycle, including original-preserving clone verification.</summary>
internal static class NativeDataAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = S(await call(tool, args), "OperationId"); var response = await wait(id, state); exited(id); receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-data.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Native data Ω", "Foreign store" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(graph.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Name") == "Native data Ω"), "Id");
        string foreignDiagram = S(graph.Single(e => S(e, "Kind") == "Collaboration" && S(e, "Id") != diagram), "Id");
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && S(e, "DiagramId") == diagram && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string parent = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        async Task<JsonElement> Mutate(object[] mutations, string state = "completed")
        {
            var r = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = mutations }, state);
            if (state == "completed") { path = S(r, "outputArtifact"); revision = S(r, "outputRevision"); graph = r.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray(); }
            return r;
        }
        string Id() => Guid.NewGuid().ToString();
        string store = Id(), replacement = Id(), foreign = Id(), task = Id(), task2 = Id(), data = Id(), data2 = Id(), sub = Id(), nestedData = Id(), reference = Id(), ref2 = Id(), nestedRef = Id();
        string inputAssociation = Id(), outputAssociation = Id(); int index = 0;
        string duplicateAssociation = Id(), sequence = Id(), flowAssociation = Id(), nestedAssociation = Id(), nestedTask = Id(), alternateTask = Id();
        object Node(string owner, string id, string type, object? properties = null)
        {
            int n = index++;
            return new { Operation = "create", ParentId = owner, ElementId = id, ElementType = type, DataProperties = properties,
                Name = type == "DataStoreReference" ? null : type + " 日本語 " + n,
                Geometry = type == "DataStore" ? null : new { X = 60 + n % 5 * 140, Y = 70 + n / 5 * 120, Width = type == "DataObject" ? 60 : 90, Height = 60 } };
        }
        object Association(string id, string source, string target, string? container = null, string type = "Association") => new { Operation = "create", ParentId = container ?? parent, ElementId = id, ElementType = type, Name = "Data association Ω",
            SourceId = source, TargetId = target, Points = new[] { new { X = 100, Y = 100 }, new { X = 250, Y = 100 }, new { X = 250, Y = 220 } } };
        await Mutate([
            Node(diagram, store, "DataStore", new { Capacity = "1200", IsUnlimited = false, State = "Ready 日本語" }), Node(diagram, replacement, "DataStore", new { Capacity = "0", IsUnlimited = true }),
            Node(foreignDiagram, foreign, "DataStore"), Node(parent, task, "UserTask"), Node(parent, task2, "ManualTask"), Node(parent, sub, "SubProcess"),
            Node(parent, data, "DataObject", new { State = "Received Ω", IsCollection = true }), Node(parent, data2, "DataObject", new { State = "Pending", IsCollection = false }),
            Node(sub, nestedData, "DataObject", new { State = "Nested", IsCollection = true }), Node(sub, nestedTask, "UserTask"), Node(parent, alternateTask, "UserTask"),
            Node(parent, reference, "DataStoreReference", new { StoreId = store }), Node(parent, ref2, "DataStoreReference", new { StoreId = store }), Node(sub, nestedRef, "DataStoreReference", new { StoreId = store }),
            Association(inputAssociation, data, task), Association(outputAssociation, task2, reference), Association(duplicateAssociation, data, task),
            Association(sequence, task, task2, type: "SequenceFlow"), Association(flowAssociation, data2, sequence), Association(nestedAssociation, nestedData, nestedTask, sub)]);
        JsonElement Binding(string owner, string item, bool input) => graph.Single(e => S(e, "Id") == owner).GetProperty("DataFlow")
            .GetProperty(input ? "InputAssociations" : "OutputAssociations").EnumerateArray().Single(a => S(a, input ? "SourceId" : "TargetId") == item);
        string retainedBinding = S(Binding(task, data, true), "Id");
        _ = Binding(task, data2, false); _ = Binding(task2, data2, true); _ = Binding(nestedTask, nestedData, true);
        // Duplicate graphical edges share one binding. Removing one must not remove or recreate that I/O identity.
        await Mutate([new { Operation = "delete", ElementId = duplicateAssociation }]);
        if (S(Binding(task, data, true), "Id") != retainedBinding) throw new InvalidDataException("Parallel association cleanup replaced a live native I/O binding.");
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        await Mutate([new { Operation = "update", ElementId = data, DataProperties = new { State = "Processed", IsCollection = false }, Documentation = "Data documentation 日本語" },
            new { Operation = "update", ElementId = store, DataProperties = new { State = "Archived Ω", Capacity = "999999", IsUnlimited = true }, Documentation = "Shared catalog documentation" }]);
        await Mutate([new { Operation = "update", ElementId = task, Name = "Unrelated edit preserves data" }]);
        foreach (var attempt in new[] {
            (Changes: new object[] { new { Operation = "delete", ElementId = store } }, Error: "store references"),
            (Changes: new object[] { new { Operation = "delete", ElementId = data } }, Error: "incident connections"),
            (Changes: new object[] { new { Operation = "update", ElementId = task, DataProperties = new { State = "wrong" } } }, Error: "DataProperties requires"),
            (Changes: new object[] { new { Operation = "update", ElementId = reference, DataProperties = new { StoreId = foreign } } }, Error: "same diagram") })
        {
            var failure = await Mutate(attempt.Changes, "failed");
            if (!S(failure, "Error").Contains(attempt.Error)) throw new InvalidDataException("Data rejection had an unrelated cause: " + S(failure, "Error"));
        }
        await Mutate([new { Operation = "update", ElementId = reference, DataProperties = new { StoreId = replacement } }]);
        await Mutate([new { Operation = "reconnect", ElementId = inputAssociation, SourceId = data2, TargetId = task,
            Points = new[] { new { X = 220, Y = 100 }, new { X = 340, Y = 240 } } }]);
        _ = Binding(task, data2, true);
        await Mutate([new { Operation = "reconnect", ElementId = sequence, SourceId = task, TargetId = alternateTask,
            Points = new[] { new { X = 200, Y = 120 }, new { X = 360, Y = 240 } } }]);
        _ = Binding(alternateTask, data2, true);
        var originalData = graph.Where(e => S(e, "DiagramId") == diagram && e.GetProperty("Data").ValueKind == JsonValueKind.Object).ToDictionary(e => S(e, "Id"), e => e.GetProperty("Data"));
        var originalIo = graph.Where(e => S(e, "DiagramId") == diagram && e.GetProperty("DataFlow").ValueKind == JsonValueKind.Object).ToDictionary(e => S(e, "Id"), e => e.GetProperty("DataFlow"));
        var clone = await Op("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new { Changes = new[] { new { Operation = "clone", DiagramId = diagram, Name = "Cloned data" } } } });
        path = S(clone, "outputArtifact"); revision = S(clone, "outputRevision"); graph = clone.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        var map = clone.GetProperty("edited").GetProperty("DiagramClones")[0].GetProperty("Identities").EnumerateArray().ToDictionary(e => S(e, "SourceId"), e => S(e, "TargetId"));
        foreach (var item in originalData)
        {
            var prior = graph.Single(e => S(e, "Id") == item.Key).GetProperty("Data"); if (!JsonElement.DeepEquals(prior, item.Value)) throw new InvalidDataException("Cloning mutated original native data.");
            var copy = graph.Single(e => S(e, "Id") == map[item.Key]).GetProperty("Data");
            foreach (string field in new[] { "State", "IsCollection", "Capacity", "IsUnlimited" })
                if (!JsonElement.DeepEquals(copy.GetProperty(field), item.Value.GetProperty(field))) throw new InvalidDataException("Clone changed native data values.");
            string id = S(item.Value, "StoreId"); if (S(copy, "StoreId") != (id == "" ? "" : map[id])) throw new InvalidDataException("Clone store reference was not remapped.");
        }
        foreach (var element in graph.Where(e => S(e, "DiagramId") == diagram && e.GetProperty("DataFlow").ValueKind == JsonValueKind.Object))
        {
            var flow = element.GetProperty("DataFlow"); var copied = graph.Single(e => S(e, "Id") == map[S(element, "Id")]).GetProperty("DataFlow");
            if (!JsonElement.DeepEquals(originalIo[S(element, "Id")], flow)) throw new InvalidDataException("Cloning mutated original native I/O state.");
            foreach (string property in new[] { "Inputs", "Outputs", "InputAssociations", "OutputAssociations" })
            {
                var left = flow.GetProperty(property).EnumerateArray().ToArray(); var right = copied.GetProperty(property).EnumerateArray().ToArray();
                if (left.Length != right.Length) throw new InvalidDataException("Clone changed native I/O counts.");
                for (int n = 0; n < left.Length; n++)
                {
                    if (S(right[n], "Id") != map[S(left[n], "Id")]) throw new InvalidDataException("Clone did not map an owned I/O identity.");
                    foreach (string side in new[] { "SourceId", "TargetId" })
                        if (S(left[n], side) is { Length: > 0 } id && S(right[n], side) != map[id]) throw new InvalidDataException("Clone I/O endpoint was not remapped.");
                }
            }
        }
        await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram });
        await Op("native_publish", new() { ["path"] = path, ["format"] = "word", ["title"] = "Native data publication" });
        await Mutate([new { Operation = "update", ElementId = data, DataProperties = new { State = "", IsCollection = true }, Documentation = "" },
            new { Operation = "update", ElementId = store, DataProperties = new { State = "", Capacity = "", IsUnlimited = false }, Documentation = "" }]);
        await Mutate([new { Operation = "delete", ElementId = inputAssociation }, new { Operation = "delete", ElementId = outputAssociation },
            new { Operation = "delete", ElementId = flowAssociation }, new { Operation = "delete", ElementId = sequence },
            new { Operation = "delete", ElementId = nestedAssociation }, new { Operation = "delete", ElementId = nestedData }, new { Operation = "delete", ElementId = nestedTask },
            new { Operation = "delete", ElementId = data }, new { Operation = "delete", ElementId = reference }, new { Operation = "delete", ElementId = ref2 }, new { Operation = "delete", ElementId = nestedRef }, new { Operation = "delete", ElementId = store }]);
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var inspect = await Op("native_inspect", new() { ["path"] = path }); if (S(inspect, "sourceRevision") != revision) throw new InvalidDataException("Native data inspection altered source bytes.");
    }
    private static string S(JsonElement e, string field) => e.GetProperty(field).GetString()!;
}
