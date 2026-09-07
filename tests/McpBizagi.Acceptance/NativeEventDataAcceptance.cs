using System.Text.Json;

/// <summary>Actual MCP event I/O lifecycle, with native restart, nested ownership and clone checks.</summary>
internal static class NativeEventDataAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = S(await call(tool, args), "OperationId"); var response = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-event-data.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Native event data Ω" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(graph.Single(e => S(e, "Kind") == "Collaboration"), "Id");
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string parent = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        async Task<JsonElement> Mutate(object[] mutations, string state = "completed")
        {
            var r = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = mutations }, state);
            if (state == "completed") { path = S(r, "outputArtifact"); revision = S(r, "outputRevision"); graph = r.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray(); }
            return r;
        }
        string Id() => Guid.NewGuid().ToString();
        string data = Id(), alternate = Id(), sub = Id(), deep = Id(), nestedData = Id(), deepData = Id(), task = Id(), nestedTask = Id();
        object Shape(string id, string type, string owner, double x, double y) => new { Operation = "create", ElementId = id, ElementType = type, ParentId = owner, Name = type + " Ω",
            Geometry = new { X = x, Y = y, Width = 90, Height = 60 } };
        var changes = new List<object> { Shape(data, "DataObject", parent, 80, 500), Shape(alternate, "DataObject", parent, 200, 500),
            Shape(sub, "SubProcess", parent, 750, 500), Shape(deep, "SubProcess", sub, 350, 250), Shape(nestedData, "DataObject", sub, 40, 250),
            Shape(deepData, "DataObject", deep, 30, 180), Shape(task, "UserTask", parent, 500, 500), Shape(nestedTask, "UserTask", sub, 180, 250) };
        var events = new List<(string Id, string Owner, string Data, bool Input, string Edge)>();
        void Event(string type, string owner, string item, string? mode = null, string? attachment = null)
        {
            int n = events.Count; string id = Id(), edge = Id(); bool input = type.EndsWith("End") || mode == "Throw";
            changes.Add(new { Operation = "create", ElementId = id, ElementType = type, ParentId = owner, Name = type + " " + (mode ?? "") + " 日本語", EventMode = mode,
                EventProperties = attachment == null ? null : new { AttachedToActivityId = attachment },
                Geometry = new { X = 70 + n % 6 * 125, Y = 70 + n / 6 * 100, Width = 32, Height = 32 } });
            events.Add((id, owner, item, input, edge));
        }
        Event("MessageStart", parent, data); Event("TimerStart", parent, data);
        Event("MessageEnd", parent, data); Event("NoneEnd", parent, data);
        Event("MessageIntermediate", parent, data, "Catch"); Event("MessageIntermediate", parent, data, "Throw");
        Event("SignalIntermediate", parent, data, "Catch"); Event("SignalIntermediate", parent, data, "Throw");
        Event("LinkIntermediate", parent, data, "Catch"); Event("LinkIntermediate", parent, data, "Throw");
        Event("MultipleIntermediate", parent, data, "Catch"); Event("MultipleIntermediate", parent, data, "Throw");
        Event("ParallelMultipleIntermediate", parent, data, "Catch"); Event("MessageIntermediate", parent, data, "Boundary", task);
        Event("MessageStart", sub, nestedData); Event("MessageEnd", sub, nestedData);
        Event("MessageIntermediate", sub, nestedData, "Catch"); Event("MessageIntermediate", sub, nestedData, "Throw");
        Event("TimerIntermediate", sub, nestedData, "Boundary", nestedTask);
        Event("MessageIntermediate", deep, deepData, "Catch"); Event("MessageIntermediate", deep, deepData, "Throw");
        object Connector(string id, string owner, string source, string target, string type = "Association") => new { Operation = "create", ElementId = id, ElementType = type, ParentId = owner,
            Name = "Event data Ω", SourceId = source, TargetId = target, Points = new[] { new { X = 100, Y = 90 }, new { X = 300, Y = 150 } } };
        foreach (var e in events) changes.Add(Connector(e.Edge, e.Owner, e.Input ? e.Data : e.Id, e.Input ? e.Id : e.Data));
        string duplicate = Id(), sequence = Id(), sequenceData = Id(), wrongCatch = Id(), wrongThrow = Id();
        changes.Add(Connector(duplicate, parent, data, events[5].Id));
        changes.Add(Connector(sequence, parent, events[4].Id, events[5].Id, "SequenceFlow"));
        changes.Add(Connector(sequenceData, parent, alternate, sequence));
        // Opposite directions remain ordinary graphical associations. They must not invent
        // input collections on catch events or output collections on throw events.
        changes.Add(Connector(wrongCatch, parent, data, events[0].Id));
        changes.Add(Connector(wrongThrow, parent, events[2].Id, data));
        await Mutate(changes.ToArray());
        JsonElement Flow(string id) => graph.Single(e => S(e, "Id") == id).GetProperty("DataFlow");
        JsonElement Binding(string owner, string item, bool input) => Flow(owner).GetProperty(input ? "InputAssociations" : "OutputAssociations").EnumerateArray().Single(a => S(a, input ? "SourceId" : "TargetId") == item);
        foreach (var e in events)
        {
            _ = Binding(e.Id, e.Data, e.Input); var flow = Flow(e.Id);
            if (flow.GetProperty("HasSpecification").GetBoolean() || flow.GetProperty(e.Input ? "Outputs" : "Inputs").GetArrayLength() != 0)
                throw new InvalidDataException("Native event I/O acquired an activity specification or opposite-direction ports.");
        }
        _ = Binding(events[4].Id, alternate, false); _ = Binding(events[5].Id, alternate, true);
        string retained = S(Binding(events[5].Id, data, true), "Id");
        await Mutate([new { Operation = "delete", ElementId = duplicate }]);
        if (S(Binding(events[5].Id, data, true), "Id") != retained) throw new InvalidDataException("Duplicate removal replaced a live event data identity.");
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        foreach (var attempt in new[] {
            (Changes: new object[] { new { Operation = "delete", ElementId = events[5].Id } }, Error: "incident connections"),
            (Changes: new object[] { new { Operation = "delete", ElementId = task } }, Error: "boundary events"),
            (Changes: new object[] { new { Operation = "reconnect", ElementId = events[5].Edge, SourceId = nestedData, TargetId = events[5].Id,
                Points = new[] { new { X = 100, Y = 100 }, new { X = 200, Y = 100 } } } }, Error: "same native container"),
            (Changes: new object[] { new { Operation = "reconnect", ElementId = events[5].Edge, SourceId = alternate, TargetId = events[5].Id,
                Points = new[] { new { X = 200, Y = 400 }, new { X = 350, Y = 200 } } } }, Error: "Artifact geometry would change native containment") })
        {
            var failure = await Mutate(attempt.Changes, "failed");
            if (!S(failure, "Error").Contains(attempt.Error)) throw new InvalidDataException("Event I/O rejection had an unrelated cause: " + S(failure, "Error"));
        }
        await Mutate([new { Operation = "reconnect", ElementId = sequence, SourceId = events[4].Id, TargetId = events[7].Id,
            Points = new[] { new { X = 100, Y = 100 }, new { X = 400, Y = 100 } } }]);
        _ = Binding(events[7].Id, alternate, true);
        await Mutate([new { Operation = "reconnect", ElementId = events[5].Edge, SourceId = alternate, TargetId = events[5].Id,
            Points = new[] { new { X = 200, Y = 120 }, new { X = 350, Y = 200 } } }, new { Operation = "update", ElementId = task, Name = "Unrelated change 日本語" }]);
        _ = Binding(events[5].Id, alternate, true);
        var original = events.ToDictionary(e => e.Id, e => Flow(e.Id));
        var clone = await Op("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision, ["patch"] = new { Changes = new[] { new { Operation = "clone", DiagramId = diagram, Name = "Cloned event data" } } } });
        path = S(clone, "outputArtifact"); revision = S(clone, "outputRevision"); graph = clone.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        var map = clone.GetProperty("edited").GetProperty("DiagramClones")[0].GetProperty("Identities").EnumerateArray().ToDictionary(e => S(e, "SourceId"), e => S(e, "TargetId"));
        foreach (var e in events)
        {
            if (!JsonElement.DeepEquals(original[e.Id], Flow(e.Id))) throw new InvalidDataException("Cloning changed original event I/O.");
            var before = original[e.Id]; var after = Flow(map[e.Id]);
            foreach (string field in new[] { "Inputs", "Outputs", "InputAssociations", "OutputAssociations" })
            {
                var a = before.GetProperty(field).EnumerateArray().ToArray(); var b = after.GetProperty(field).EnumerateArray().ToArray();
                if (a.Length != b.Length) throw new InvalidDataException("Clone lost event I/O nodes.");
                for (int n = 0; n < a.Length; n++)
                {
                    if (S(b[n], "Id") != map[S(a[n], "Id")]) throw new InvalidDataException("Clone lost an owned event I/O identity map.");
                    foreach (string side in new[] { "SourceId", "TargetId" })
                        if (S(a[n], side) is { Length: > 0 } id && S(b[n], side) != map[id]) throw new InvalidDataException("Clone retained an original event I/O endpoint.");
                }
            }
            foreach (string field in new[] { "InputSets", "OutputSets" })
            {
                var a = before.GetProperty(field).EnumerateArray().Select(s => s.EnumerateArray().Select(i => map[i.GetString()!]).ToArray()).ToArray();
                if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(a), after.GetProperty(field))) throw new InvalidDataException("Clone changed event set membership/order.");
            }
        }
        await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram });
        await Op("native_publish", new() { ["path"] = path, ["format"] = "word", ["title"] = "Native event data publication" });
        var cleanup = events.Select(e => (object)new { Operation = "delete", ElementId = e.Edge }).ToList();
        foreach (string edge in new[] { wrongCatch, wrongThrow, sequenceData, sequence }) cleanup.Add(new { Operation = "delete", ElementId = edge });
        await Mutate(cleanup.ToArray());
        foreach (var e in events)
            if (Flow(e.Id).GetProperty("Inputs").GetArrayLength() != 0 || Flow(e.Id).GetProperty("Outputs").GetArrayLength() != 0 ||
                Flow(e.Id).GetProperty("InputAssociations").GetArrayLength() != 0 || Flow(e.Id).GetProperty("OutputAssociations").GetArrayLength() != 0)
                throw new InvalidDataException("Explicit association deletion left an owned event I/O binding.");
        await Mutate(events.Select(e => (object)new { Operation = "delete", ElementId = e.Id }).Concat(new object[] {
            new { Operation = "delete", ElementId = task }, new { Operation = "delete", ElementId = nestedTask } }).ToArray());
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var inspect = await Op("native_inspect", new() { ["path"] = path });
        if (S(inspect, "sourceRevision") != revision) throw new InvalidDataException("Event data inspection changed the original bytes.");
    }
    private static string S(JsonElement e, string field) => e.GetProperty(field).GetString()!;
}
