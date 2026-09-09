using System.IO.Compression;
using System.Text;
using System.Text.Json;
using McpBizagi.Contracts;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Adversarial comparison policy tests; these synthetic archives are not native-engine evidence.</summary>
public sealed class NativeInliningTests
{
    private static string Id(int n) => $"00000000-0000-4000-8000-{n:000000000000}";
    private static NativeSubProcessInlining Request() => new() { ElementId = Id(4), ExpectedProcessId = Id(5), Position = new() { X = 60, Y = 60 } };
    private static NativeElement[] Graph() =>
    [
        new() { Id = Id(1), Kind = "Collaboration", DiagramId = Id(1) },
        new() { Id = Id(2), Kind = "Participant", DiagramId = Id(1), ParentId = Id(1) },
        new() { Id = Id(3), Kind = "Process", DiagramId = Id(1), ParentId = Id(2) },
        new() { Id = Id(4), Kind = "CallActivity", ElementType = "CallActivity", Name = "Call Ω", DiagramId = Id(1), ParentId = Id(3),
            CallReference = new() { CatalogProcessId = Id(5), BpmnName = Id(5) }, Geometry = new() { X = 100, Y = 100, Width = 120, Height = 80 } },
        new() { Id = Id(5), Kind = "Process", DiagramId = Id(1), ParentId = Id(2) },
        new() { Id = Id(6), Kind = "UserTask", ElementType = "UserTask", DiagramId = Id(1), ParentId = Id(5), Geometry = new() { X = 20, Y = 20, Width = 120, Height = 80 } }
    ];
    private static byte[] Archive(bool converted, string change = "")
    {
        string flowId = change == "binding" ? Id(9) : Id(5);
        string selector = converted ? $"<BlockActivity ActivitySetId='{Id(4)}' {(change == "block-attribute" ? "unknown='lost'" : "")}/>" :
            $"<Implementation {(change == "implementation-attribute" ? "unknown='lost'" : "")}><SubFlow Id='{flowId}'{(change == "subflow-attribute" ? " unknown='lost'" : "")}/>{(change == "implementation-comment" ? "<!--keep-->" : "")}</Implementation>";
        string call = $"<Activity Id='{Id(4)}' Name='Call Ω'><Description>{(change == "description" ? "lost" : "retained")}</Description>{selector}</Activity>";
        string set = converted ? $"<ActivitySets><ActivitySet Id='{Id(4)}' Name='{(change == "set-name" ? "changed" : "Call Ω")}' {(change == "set-attribute" ? "unknown='lost'" : "")}><Associations/><Artifacts/><Activities>{(change == "body" ? "<Unknown/>" : "")}</Activities><Transitions/>{(change == "set-comment" ? "<!--keep-->" : "")}</ActivitySet></ActivitySets>" : "";
        string xml = $"<Package xmlns='http://www.wfmc.org/2009/XPDL2.2' Id='{Id(1)}'><WorkflowProcesses><WorkflowProcess Id='{Id(3)}'>{set}<Activities>{call}</Activities></WorkflowProcess><WorkflowProcess Id='{Id(5)}'><Activities><Activity Id='{Id(6)}' Name='{(change == "callee" ? "changed" : "Keep")}'/></Activities></WorkflowProcess></WorkflowProcesses></Package>";
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            using (var w = new StreamWriter(zip.CreateEntry("ModelInfo.xml").Open())) w.Write("<ModelInfo/>");
            using (var w = new StreamWriter(zip.CreateEntry("unknown.bin").Open())) w.Write(change == "binary" ? "changed" : "preserved");
            using var inner = new MemoryStream();
            using (var diagram = new ZipArchive(inner, ZipArchiveMode.Create, true))
            { using var w = new StreamWriter(diagram.CreateEntry("Diagram.xml").Open()); w.Write(xml); }
            using var stream = zip.CreateEntry(Id(1) + ".diag").Open(); stream.Write(inner.ToArray());
        }
        return memory.ToArray();
    }
    private static NativeFidelityReport Compare(string before = "", string after = "") => NativeInliningPolicy.CompareConversion(
        Archive(false, before), Archive(true, after), Graph(), NativeInliningPolicy.ExpectedConvertedGraph(Graph(), Request()), Request());

    [Fact] public void OnlyRequestedSelectorAndEmptyContainerAreProjected() => Assert.True(Compare().Preserved);

    [Theory]
    [InlineData("binding")][InlineData("implementation-attribute")][InlineData("subflow-attribute")][InlineData("implementation-comment")]
    public void UnknownOrWrongSourceImplementationCannotBeRetired(string change) => Assert.Throws<InvalidDataException>(() => Compare(before: change));

    [Theory]
    [InlineData("block-attribute")][InlineData("set-name")][InlineData("set-attribute")][InlineData("set-comment")][InlineData("body")]
    public void UnrequestedContainerContentCannotBeHidden(string change) => Assert.Throws<InvalidDataException>(() => Compare(after: change));

    [Theory]
    [InlineData("description")][InlineData("callee")][InlineData("binary")]
    public void UnrelatedContentRemainsCompared(string change) => Assert.False(Compare(after: change).Preserved);

    [Theory]
    [InlineData("name")][InlineData("owner")][InlineData("id")][InlineData("trigger")]
    public void ReaderCannotRedefineRequestedGraph(string change)
    {
        var graph = NativeInliningPolicy.ExpectedConvertedGraph(Graph(), Request());
        switch (change)
        {
            case "name": graph[3].Name = "changed"; break;
            case "owner": graph[3].ParentId = Id(5); break;
            case "id": graph[3].Id = Id(9); break;
            case "trigger": graph[3].SubProcess!.TriggeredByEvent = true; break;
        }
        Assert.Throws<InvalidDataException>(() => NativeInliningPolicy.CompareConversion(Archive(false), Archive(true), Graph(), graph, Request()));
    }

    [Theory]
    [InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(-1)][InlineData(1000001)]
    public void InvalidCoordinatesReject(double n)
    { var request = Request(); request.Position!.X = n; Assert.Throws<InvalidDataException>(() => NativeInliningPolicy.Validate(request)); }

    [Fact] public void PreflightUsesCompleteSourceBodyAndDoesNotModifyInput()
    {
        var graph = Graph(); string original = JsonSerializer.Serialize(graph);
        var selection = NativeInliningPolicy.Preflight(Archive(false), new() { Elements = graph, Metadata = new(), Documentation = new() }, Request());
        Assert.Equal(new[] { Id(6) }, selection.ElementIds); Assert.Equal(Id(4), selection.TargetParentId);
        Assert.Equal(original, JsonSerializer.Serialize(graph));
    }

    [Fact] public void RecursiveBodyRejectsBeforeConversion()
    {
        var graph = Graph(); graph[3].ParentId = Id(5);
        Assert.Throws<NotSupportedException>(() => NativeInliningPolicy.Preflight(Archive(false), new() { Elements = graph, Metadata = new(), Documentation = new() }, Request()));
    }

    [Fact] public void BindingMismatchRejectsBeforeConversion()
    {
        var request = Request(); request.ExpectedProcessId = Id(3);
        Assert.Throws<InvalidDataException>(() => NativeInliningPolicy.Preflight(Archive(false), new() { Elements = Graph(), Metadata = new(), Documentation = new() }, request));
    }
}
