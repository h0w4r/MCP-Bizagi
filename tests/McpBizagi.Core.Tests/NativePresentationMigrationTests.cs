using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Pure migration intent tests; these synthetic archives never accredit native execution.</summary>
public sealed class NativePresentationMigrationTests
{
    private const string A = "11111111-2222-4333-8444-555555555555", B = "22222222-3333-4444-8555-666666666666";
    private const string M = "33333333-4444-4555-8666-777777777777", K = "44444444-5555-4666-8777-888888888888", T = "55555555-6666-4777-8888-999999999999";
    private static NativePresentationAction Action(string owner = M, string diagram = A, string type = "Text", string content = "Original cache Ω", string value = "Normal") =>
        new() { DiagramId = diagram, ElementId = owner, Type = type, Content = content, TypeValue = value, DisplayName = "Original label" };
    private static XElement Xml(NativePresentationAction a) => new("PresentationAction", new XAttribute("ElementId", a.ElementId), new XAttribute("Type", a.Type),
        new XAttribute("TypeValue", a.TypeValue), new XElement("DisplayName", a.DisplayName), new XElement("Content", a.Content));
    private static byte[] Archive(NativePresentationAction[] actions, Dictionary<string, byte[]>? files = null, string extra = "", string attributes = "")
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            void Write(ZipArchive z, string name, byte[] bytes) { using var stream = z.CreateEntry(name).Open(); stream.Write(bytes); }
            Write(zip, "ModelInfo.xml", Encoding.UTF8.GetBytes("<ModelInfo/>"));
            foreach (string d in new[] { A, B })
            {
                using var nested = new MemoryStream();
                using (var diag = new ZipArchive(nested, ZipArchiveMode.Create, true))
                {
                    Write(diag, "Actions.xml", Encoding.UTF8.GetBytes("<DiagramActions " + (d == A ? attributes : "") + ">" + string.Join("\n", actions.Where(a => a.DiagramId == d).Select(Xml)) + (d == A ? extra : "") + "</DiagramActions>"));
                    Write(diag, "Diagram.xml", Encoding.UTF8.GetBytes("<Preserve exact='yes'/>"));
                    foreach (var file in files ?? []) if (file.Key.StartsWith(d + "/", StringComparison.Ordinal)) Write(diag, "Actions/" + file.Key[(d.Length + 1)..], file.Value);
                }
                Write(zip, d + ".diag", nested.ToArray());
            }
        }
        return buffer.ToArray();
    }
    private static NativeElement[] Graph(bool moved = false) => [new() { Id = A, Kind = "Collaboration", DiagramId = A }, new() { Id = B, Kind = "Collaboration", DiagramId = B },
        new() { Id = M, Kind = "Task", DiagramId = moved ? B : A, Documentation = "New owner description" }, new() { Id = K, Kind = "Task", DiagramId = A }, new() { Id = T, Kind = "Task", DiagramId = B }];
    private static EngineReply Source(byte[] bytes, params NativePresentationAction[] actions)
    {
        var archive = NativeArchive.ReadEntries(bytes);
        return new() { Elements = Graph(), Presentation = new() { Actions = actions,
            Files = actions.Where(a => a.TypeValue == "Normal" && a.Type is "File" or "Image").Select(a =>
            {
                byte[] payload = archive[a.DiagramId + ".diag!/Actions/" + a.Content[12..]];
                return new NativeAttachmentInfo { DiagramId = a.DiagramId, ElementId = a.ElementId, FileName = a.Content[12..], Length = payload.Length, Sha256 = BpmnDocument.Revision(payload) };
            }).ToArray() } };
    }
    private static EngineReply Observed(NativePresentationMigrationPlan plan) => new() { Presentation = new() { Actions = plan.ExpectedActions, Files = plan.ExpectedFiles } };
    [Theory]
    [InlineData("Normal", "Text", "Literal Ω")]
    [InlineData("Normal", "Link", "https://example.invalid/inert")]
    [InlineData("Description", "Text", "Deliberately stale cache Ω")]
    public void MovesSelectedOwnerWithoutRefreshingContent(string value, string type, string content)
    {
        var a = Action(type: type, value: value, content: content); var bytes = Archive([a]);
        var plan = NativePresentationMigrationPolicy.Prepare(bytes, Source(bytes, a), Graph(true));
        Assert.Equal(A, Assert.Single(plan.Transfers).SourceAction.DiagramId);
        Assert.Equal(B, Assert.Single(plan.ExpectedActions).DiagramId); Assert.Equal(content, plan.ExpectedActions[0].Content);
    }
    [Fact]
    public void CopiesSharedPayloadAndRetainsUnmovedSourceReference()
    {
        var a = Action(type: "File", content: "action-file:own.xml"); var k = Action(K, type: "File", content: a.Content);
        var bytes = Archive([a, k], new() { [A + "/own.xml"] = [1, 2] });
        var plan = NativePresentationMigrationPolicy.Prepare(bytes, Source(bytes, a, k), Graph(true));
        Assert.Equal(new byte[] { 1, 2 }, plan.ExpectedEntries[A + ".diag!/Actions/own.xml"]);
        Assert.Equal(new byte[] { 1, 2 }, plan.ExpectedEntries[B + ".diag!/Actions/own.xml"]);
        Assert.Equal(2, plan.ExpectedFiles.Length);
    }
    [Fact]
    public void RetiresSourceFileOnlyAfterItsLastReferenceMoves()
    {
        var a = Action(type: "File", content: "action-file:own.xml"); var bytes = Archive([a], new() { [A + "/own.xml"] = [1, 2] });
        var plan = NativePresentationMigrationPolicy.Prepare(bytes, Source(bytes, a), Graph(true));
        Assert.False(plan.ExpectedEntries.ContainsKey(A + ".diag!/Actions/own.xml"));
        Assert.Equal(new byte[] { 1, 2 }, plan.ExpectedEntries[B + ".diag!/Actions/own.xml"]);
    }
    [Fact]
    public void PreventsKnownNativeGuidFilenamePruningOfSharedSourceContent()
    {
        var a = Action(type: "File", content: "action-file:" + M + ".bin"); var k = Action(K, type: "File", content: a.Content);
        var bytes = Archive([a, k], new() { [A + "/" + M + ".bin"] = [1, 2] });
        Assert.Throws<InvalidDataException>(() => NativePresentationMigrationPolicy.Prepare(bytes, Source(bytes, a, k), Graph(true)));
    }
    [Fact]
    public void RejectsDifferentDestinationBytesIncludingUnownedFiles()
    {
        var a = Action(type: "File", content: "action-file:own.xml"); var bytes = Archive([a], new() { [A + "/own.xml"] = [1], [B + "/own.xml"] = [2] });
        Assert.Throws<InvalidDataException>(() => NativePresentationMigrationPolicy.Prepare(bytes, Source(bytes, a), Graph(true)));
    }
    [Fact]
    public void RetainsTargetOrderThenAppendsIncomingRecords()
    {
        var a = Action(); var t = Action(T, B); var bytes = Archive([a, t]); var plan = NativePresentationMigrationPolicy.Prepare(bytes, Source(bytes, a, t), Graph(true));
        var doc = XDocument.Parse(Encoding.UTF8.GetString(plan.ExpectedEntries[B + ".diag!/Actions.xml"]));
        Assert.Equal(new[] { T, M }, doc.Root!.Elements().Select(e => (string)e.Attribute("ElementId")!));
    }
    [Theory]
    [InlineData("<Unknown/>", "")]
    [InlineData("<!--Retain comment-->", "")]
    [InlineData("significant", "")]
    [InlineData("", "xml:space='preserve'")]
    [InlineData("", "extra='unknown'")]
    public void RejectsUnrepresentedMigratedCollectionContent(string extra, string attributes)
    {
        var a = Action(); var bytes = Archive([a], extra: extra, attributes: attributes);
        Assert.Throws<InvalidDataException>(() => NativePresentationMigrationPolicy.Prepare(bytes, Source(bytes, a), Graph(true)));
    }
    [Fact]
    public void DuplicateOrOrphanedMovedOwnersAreRejected()
    {
        var a = Action(); var duplicate = Action(diagram: B); var bytes = Archive([a, duplicate]);
        Assert.Throws<InvalidDataException>(() => NativePresentationMigrationPolicy.Prepare(bytes, Source(bytes, a, duplicate), Graph(true)));
        bytes = Archive([duplicate]); Assert.Throws<InvalidDataException>(() => NativePresentationMigrationPolicy.Prepare(bytes, Source(bytes, duplicate), Graph(true)));
    }
    [Fact]
    public void FinalArchiveMustMatchIntentBeforeComparisonLocationsAreRestored()
    {
        var a = Action(); var t = Action(T, B); var bytes = Archive([a, t]); var plan = NativePresentationMigrationPolicy.Prepare(bytes, Source(bytes, a, t), Graph(true));
        var actual = NativeArchive.ReadEntries(Archive([t, Action(diagram: B)])).ToDictionary(p => p.Key, p => p.Value);
        var original = NativeArchive.ReadEntries(bytes).ToDictionary(p => p.Key, p => p.Value);
        NativePresentationMigrationPolicy.Project(original, actual, plan, Observed(plan), Observed(plan));
        Assert.True(plan.Verification!.Preserved);
        Assert.Equal(original.Keys.Order(), actual.Keys.Order());
        foreach (var pair in original) Assert.Equal(pair.Value, actual[pair.Key]);
        actual = NativeArchive.ReadEntries(Archive([t, Action(diagram: B, content: "Unrequested change")])).ToDictionary(p => p.Key, p => p.Value);
        Assert.Throws<InvalidDataException>(() => NativePresentationMigrationPolicy.Project(original, actual, plan, Observed(plan), Observed(plan)));
    }
    [Fact]
    public void ReorderedOrExtraPayloadsCannotHideBehindCorrectSnapshots()
    {
        var a = Action(); var t = Action(T, B); var bytes = Archive([a, t]); var plan = NativePresentationMigrationPolicy.Prepare(bytes, Source(bytes, a, t), Graph(true));
        var original = NativeArchive.ReadEntries(bytes).ToDictionary(p => p.Key, p => p.Value);
        var reordered = NativeArchive.ReadEntries(Archive([Action(diagram: B), t])).ToDictionary(p => p.Key, p => p.Value);
        Assert.Throws<InvalidDataException>(() => NativePresentationMigrationPolicy.Project(original, reordered, plan, Observed(plan), Observed(plan)));
        var extra = NativeArchive.ReadEntries(Archive([t, Action(diagram: B)], new() { [B + "/extra.bin"] = [1] })).ToDictionary(p => p.Key, p => p.Value);
        Assert.Throws<InvalidDataException>(() => NativePresentationMigrationPolicy.Project(original, extra, plan, Observed(plan), Observed(plan)));
    }
}
