using System.Security.Cryptography;
using System.Text.Json;
using System.Globalization;
using System.Xml.Linq;

/// <summary>Actual MCP stdio to installed native styling, durable readback, failures and rendering.</summary>
internal static class NativeStyleAcceptance
{
    public static async Task Run(string run, Func<string, Dictionary<string, object?>, Task<JsonElement>> call,
        Func<string, string, Task<JsonElement>> wait, Action<string> exited, Func<string, Dictionary<string, object?>, Task<JsonElement>> error)
    {
        var receipts = new List<object>();
        async Task<JsonElement> Op(string tool, Dictionary<string, object?> args, string state = "completed")
        {
            string id = S(await call(tool, args), "OperationId"); var response = await wait(id, state); exited(id);
            receipts.Add(new { tool, id, response });
            File.WriteAllText(Path.Combine(run, "native-styles.json"), JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
            return state == "completed" ? response.GetProperty("Result") : response;
        }
        var inventory = await Op("native_fonts_get", new());
        var fonts = inventory.GetProperty("Fonts").EnumerateArray().ToArray();
        string font = S(fonts.Where(e => e.GetProperty("Regular").GetBoolean() && e.GetProperty("BoldItalic").GetBoolean()).OrderBy(e => S(e, "Name") == "Arial" ? 0 : 1).ThenBy(e => S(e, "Name"), StringComparer.Ordinal).First(), "Name");
        var created = await Op("native_model_create", new() { ["diagramNames"] = new[] { "Native styles Ω" } });
        var graph = created.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        string diagram = S(graph.Single(e => S(e, "Kind") == "Collaboration"), "Id");
        string pool = S(graph.Single(e => S(e, "Kind") == "Participant" && !e.GetProperty("IsMainParticipant").GetBoolean()), "Id");
        string process = S(graph.Single(e => S(e, "Kind") == "Process" && S(e, "ParentId") == pool), "Id");
        string path = S(created, "outputArtifact"), revision = S(created, "outputRevision");
        async Task<JsonElement> Mutate(object[] changes, string state = "completed")
        {
            var result = await Op("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = changes }, state);
            if (state == "completed") { path = S(result, "outputArtifact"); revision = S(result, "outputRevision"); graph = result.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray(); }
            return result;
        }
        string Id() => Guid.NewGuid().ToString();
        string task = Id(), end = Id(), gateway = Id(), sub = Id(), nested = Id(), annotation = Id(), flow = Id(), association = Id();
        string styledPool = Id(), styledProcess = Id(), lane = Id(), milestone = Id(), data = Id();
        object Bounds(int x, int y, int w = 140, int h = 70) => new { X = x, Y = y, Width = w, Height = h };
        object Label(int x, int y) => new { X = x, Y = y, Width = 130, Height = 40 };
        object Node(string id, string parent, string kind, int x, int y, string? text = null) => new { Operation = "create", ElementId = id, ParentId = parent, ElementType = kind,
            Name = kind == "TextAnnotation" ? null : "STYLE-" + kind + "-Ω", Geometry = Bounds(x, y), ArtifactProperties = text == null ? null : new { Text = text },
            Style = new { FontName = font, FontSize = 12, Bold = true, Italic = true, Underline = true, Strikeout = true, Alignment = "Center", FontArgb = unchecked((int)0xff123456),
                BorderArgb = unchecked((int)0xff654321), BackgroundArgb = unchecked((int)0xffeecc99), BorderVisible = true, TextBackgroundArgb = unchecked((int)0xffaabbcc), TextDirection = "Horizontal" } };
        await Mutate([
            Node(task, process, "UserTask", 80, 100), Node(end, process, "NoneEnd", 430, 100), Node(gateway, process, "ExclusiveGateway", 280, 100),
            Node(sub, process, "SubProcess", 80, 320), Node(nested, sub, "UserTask", 100, 100), Node(annotation, process, "TextAnnotation", 280, 320, "STYLE-ANNOTATION-Ω"),
            Node(data, sub, "DataObject", 300, 100),
            new { Operation = "create", ElementId = flow, ParentId = process, ElementType = "SequenceFlow", SourceId = task, TargetId = gateway, Name = "STYLE-FLOW-Ω",
                Points = new[] { new { X = 220, Y = 135 }, new { X = 280, Y = 135 } }, Style = new { FontName = font, FontSize = 11, Bold = true, FontArgb = unchecked((int)0xff123456), BorderArgb = unchecked((int)0xff654321), LabelBounds = Label(230, 145) } },
            new { Operation = "create", ElementId = association, ParentId = process, ElementType = "Association", SourceId = task, TargetId = annotation,
                Points = new[] { new { X = 220, Y = 160 }, new { X = 280, Y = 340 } }, Style = new { FontName = font, FontSize = 11, Italic = true, BorderArgb = unchecked((int)0xff654321) } }
        ]);
        await Mutate([
            new { Operation = "update", ElementId = task, Style = new { Alignment = "Near", FontSize = 14, LabelBounds = Label(85, 110) } },
            new { Operation = "update", ElementId = pool, Style = new { FontName = font, FontSize = 13, Bold = true } },
            new { Operation = "update", ElementId = nested, Style = new { Alignment = "Far", TextDirection = "TopToBottom", LabelBounds = Label(110, 120) } },
            new { Operation = "update", ElementId = end, Style = new { TextDirection = "BottomToTop", LabelBounds = Label(400, 210) } }
        ]);
        await Mutate([
            new { Operation = "create", ElementId = styledPool, ParentId = diagram, ProcessId = styledProcess, ElementType = "Participant", Name = "STYLE-POOL-Ω", Geometry = Bounds(30, 500, 700, 300),
                Style = new { FontName = font, FontSize = 12, Bold = true, BackgroundArgb = unchecked((int)0xffeef1ff), BorderArgb = unchecked((int)0xff654321) } },
            new { Operation = "create", ElementId = lane, ParentId = styledProcess, ElementType = "Lane", Name = "STYLE-LANE-Ω", Geometry = Bounds(50, 0, 650, 300),
                Style = new { FontName = font, FontSize = 12, Italic = true, Alignment = "Near", TextDirection = "TopToBottom", BorderVisible = true, FontArgb = unchecked((int)0xff123456) } },
            new { Operation = "create", ElementId = milestone, ParentId = styledProcess, ElementType = "Milestone", Name = "STYLE-MILESTONE-Ω", Geometry = Bounds(50, 0, 650, 300),
                Style = new { FontName = font, FontSize = 12, Bold = true, Alignment = "Far", TextDirection = "Horizontal", BorderVisible = false, FontArgb = unchecked((int)0xff123456) } }
        ]);
        var expected = graph.ToDictionary(e => S(e, "Id"), e => e.GetProperty("Style"));
        string styledPath = path, styledRevision = revision;
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var clone = await Op("native_diagrams_apply", new() { ["path"] = path, ["expectedRevision"] = revision,
            ["patch"] = new { Changes = new[] { new { Operation = "clone", DiagramId = diagram, Name = "Cloned styles Ω" } } } });
        path = S(clone, "outputArtifact"); revision = S(clone, "outputRevision");
        var map = clone.GetProperty("edited").GetProperty("DiagramClones")[0].GetProperty("Identities").EnumerateArray().ToDictionary(e => S(e, "SourceId"), e => S(e, "TargetId"));
        graph = clone.GetProperty("reopened").GetProperty("Elements").EnumerateArray().ToArray();
        foreach (var pair in expected)
            if (!JsonElement.DeepEquals(pair.Value, graph.Single(e => S(e, "Id") == map[pair.Key]).GetProperty("Style"))) throw new InvalidDataException("Clone styling differs from durable source.");
        foreach (string surface in new[] { "", sub })
        {
            var rendered = await Op("native_render_svg", new() { ["path"] = path, ["diagramId"] = diagram, ["subProcessId"] = surface });
            VerifySvg(rendered.GetProperty("result"), surface == "" ? task : nested, font, surface == "" ? 14 : 12, surface == "" ? "left" : "right", surface == "" ? "0" : "90", surface == "", run);
        }
        await Mutate([new { Operation = "update", ElementId = task, Style = new { FontName = "MCP-Bizagi deliberately absent font " + Guid.NewGuid() } }], "failed");
        await Mutate([new { Operation = "update", ElementId = flow, Style = new { BackgroundArgb = -1 } }], "failed");
        await Mutate([new { Operation = "update", ElementId = process, Style = new { FontSize = 12 } }], "failed");
        await Mutate([new { Operation = "update", ElementId = pool, Style = new { TextDirection = "BottomToTop" } }], "failed");
        await error("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new[] { new { Operation = "update", ElementId = task, Style = new { LabelBounds = new { X = 1.5, Y = 0, Width = 30, Height = 30 } } } } });
        await error("native_mutate", new() { ["path"] = path, ["expectedRevision"] = revision, ["mutations"] = new[] { new { Operation = "update", ElementId = task, Style = new { LabelBounds = new { } } } } });
        await Mutate([new { Operation = "update", ElementId = task, Style = new { Bold = false, Italic = false, Underline = false, Strikeout = false,
            Alignment = "Far", BorderVisible = false, TextBackgroundArgb = 16777215, LabelBounds = new { X = 0, Y = 0, Width = 0, Height = 0 } } }]);
        if (!JsonElement.DeepEquals(expected[task], graph.Single(e => S(e, "Id") == map[task]).GetProperty("Style"))) throw new InvalidDataException("Updating the source changed the clone's formatting.");
        await Op("native_save_copy", new() { ["path"] = path, ["expectedRevision"] = revision });
        var intact = await Op("native_inspect", new() { ["path"] = styledPath });
        if (S(intact, "sourceRevision") != styledRevision) throw new InvalidDataException("Styling lifecycle modified its original source.");
    }
    private static void VerifySvg(JsonElement result, string task, string font, int size, string alignment, string direction, bool root, string run)
    {
        XNamespace ns = "http://www.w3.org/2000/svg";
        string file = result.GetProperty("Artifacts").EnumerateArray().Select(e => e.GetString()!).Single(p => p.EndsWith(".svg", StringComparison.Ordinal));
        var svg = XDocument.Load(file); var shape = svg.Descendants().Single(e => (string?)e.Attribute("data-element-id") == task);
        var text = shape.Descendants(ns + "text").Single();
        var css = ((string?)text.Attribute("style") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Split(':', 2)).ToDictionary(p => p[0].Trim(), p => p[1].Trim());
        // The installed 4.3 renderer uses this observed native-unit conversion, not an assumed 96/72 ratio.
        double pixels = double.Parse(css["font-size"].Replace("px", ""), CultureInfo.InvariantCulture);
        if (css["font-family"] != font || Math.Abs(pixels - size * 1.3281472327365) > 0.001 || css["font-weight"] != "bold" || css["font-style"] != "italic" ||
            css["fill"] != "rgb(18, 52, 86)" || css["text-decoration"] != "underline line-through" || (string?)text.Attribute("textAlign") != alignment || (string?)text.Attribute("textDirection") != direction)
            throw new InvalidDataException("Actual native SVG text styling differs from the requested measured subset.");
        if (!shape.Descendants(ns + "rect").Any(e => ((string?)e.Attribute("style") ?? "").Contains("fill: rgb(238, 204, 153); stroke: rgb(101, 67, 33);")))
            throw new InvalidDataException("Actual native SVG task colors differ from opaque native intent.");
        if (root)
        {
            // External label IDs are native-generated. Match this authored unique text, not a guessed ID suffix.
            var labelText = svg.Descendants(ns + "text").Single(e => string.Concat(e.Descendants(ns + "tspan").Select(t => t.Value)) == "STYLE-FLOW-Ω");
            var label = labelText.Ancestors().First(e => e.Attribute("data-element-id") != null);
            var rect = label.Descendants(ns + "rect").First();
            if ((string?)label.Attribute("transform") != "matrix(1 0 0 1 230 145)" || (string?)rect.Attribute("width") != "130" || (string?)rect.Attribute("height") != "40")
                throw new InvalidDataException("Native external sequence-flow label bounds differ from the durable request.");
        }
        File.WriteAllText(Path.Combine(run, root ? "style-svg-root.json" : "style-svg-nested.json"), JsonSerializer.Serialize(new {
            svgSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))), task, nativeFontSize = size, measuredCssPixels = pixels, css,
            alignment, nativeTextDirectionAttribute = direction, internalLabelViewport = text.Parent!.Attributes().ToDictionary(a => a.Name.ToString(), a => a.Value),
            externalSequenceLabelBoundsChecked = root, limitation = "Internal label bounds and text background can be ignored by the native renderer. Direction attributes are not a glyph/pixel rotation accreditation. No semitransparent-color or actual desktop GUI claim."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static string S(JsonElement value, string name) => value.GetProperty(name).GetString()!;
}
