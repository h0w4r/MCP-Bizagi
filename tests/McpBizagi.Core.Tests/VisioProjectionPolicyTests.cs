using System.Text;
using System.Xml.Linq;
using McpBizagi.Contracts;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Adversarial projection policy units; native/MCP acceptance remains a separate gate.</summary>
public sealed class VisioProjectionPolicyTests
{
    private const string Reservation = "<Page ID='7' NameU='Page NameU'><PageSheet><PageProps><PageWidth>8</PageWidth><PageHeight>11</PageHeight><PageScale>0.04</PageScale><DrawingScale>0.04</DrawingScale></PageProps></PageSheet></Page>";
    private const string Root = "<Page ID='2' Name='Root Ω'><Shapes><Shape ID='1'><Text>Keep me</Text></Shape></Shapes></Page>";
    private const string Sub = "<Page ID='5' Name='Nested 日本語'><Shapes><Shape ID='1'><Text>Body</Text></Shape></Shapes></Page>";
    private static NativeElement[] Source() => [
        new() { Id = "root", Kind = "Collaboration", Name = "Root Ω", DiagramId = "root" },
        new() { Id = "sub", Kind = "SubProcess", Name = "Nested 日本語", DiagramId = "root", SubProcess = new() },
        new() { Id = "task", Kind = "Task", ParentId = "sub", DiagramId = "root", Geometry = new() }
    ];
    private static NativeVisioPageReceipt[] Receipts() => [
        new() { SourceDiagramId = "root", PageId = "2", PageName = "Root Ω" },
        new() { SourceDiagramId = "root", SourceSubProcessId = "sub", PageId = "5", PageName = "Nested 日本語" }
    ];
    private static byte[] Document(string pages) => Encoding.UTF8.GetBytes("<VisioDocument xmlns='http://schemas.microsoft.com/visio/2003/core'><Masters><Master ID='9'><Text>Untouched resource</Text></Master></Masters><Pages>" + pages + "</Pages></VisioDocument>");

    [Fact] public void RemovesOnlyRecognizedReservationsAndPreservesSourceDocumentNodes()
    {
        var original = Document(Root + Sub + Reservation);
        var result = VisioDocument.PrepareExport(original, Source(), ["root"], Receipts());
        Assert.Equal(["7"], result.RemovedReservedPages);
        var before = XDocument.Parse(Encoding.UTF8.GetString(original));
        var after = XDocument.Parse(Encoding.UTF8.GetString(result.Bytes));
        XNamespace ns = VisioDocument.Namespace;
        Assert.True(XNode.DeepEquals(before.Root!.Element(ns + "Masters"), after.Root!.Element(ns + "Masters")));
        Assert.True(XNode.DeepEquals(before.Root.Element(ns + "Pages")!.Elements().First(), after.Root.Element(ns + "Pages")!.Elements().First()));
        Assert.Equal(2, VisioDocument.Inspect(result.Bytes).Length);
    }

    [Theory]
    [InlineData("shape")][InlineData("comment")][InlineData("attribute")][InlineData("namespace")]
    [InlineData("unknown")][InlineData("nonfinite")][InlineData("zero")][InlineData("negative")]
    [InlineData("sheetAttribute")][InlineData("propertyAttribute")][InlineData("fieldComment")]
    [InlineData("fieldAttribute")][InlineData("label")][InlineData("id")][InlineData("background")]
    public void UnexpectedReservationPayloadRejectsWithoutErasure(string change)
    {
        string tail = Reservation, root = Root;
        tail = change switch {
            "shape" => tail.Replace("</Page>", "<Shapes><Shape ID='3'/></Shapes></Page>"),
            "comment" => tail.Replace("</Page>", "<!--retain--></Page>"),
            "attribute" => tail.Replace("NameU=", "Custom='retain' NameU="),
            "namespace" => tail.Replace("<PageSheet>", "<PageSheet xmlns='urn:other'>"),
            "unknown" => tail.Replace("</PageProps>", "<Other>retain</Other></PageProps>"),
            "nonfinite" => tail.Replace(">8<", ">NaN<"),
            "zero" => tail.Replace(">8<", ">0<"),
            "negative" => tail.Replace(">8<", ">-1<"),
            "sheetAttribute" => tail.Replace("<PageSheet>", "<PageSheet Custom='retain'>"),
            "propertyAttribute" => tail.Replace("<PageProps>", "<PageProps Custom='retain'>"),
            "fieldComment" => tail.Replace(">8<", ">8<!--retain--><"),
            "fieldAttribute" => tail.Replace("<PageWidth>", "<PageWidth F='retain'>"),
            "label" => tail.Replace("Page NameU", "User page"),
            "id" => tail.Replace("ID='7'", "ID='5'"),
            _ => tail
        };
        if (change == "background") root = root.Replace("ID='2'", "ID='2' BackPage='7'");
        Assert.Throws<InvalidDataException>(() => VisioDocument.PrepareExport(Document(root + Sub + tail), Source(), ["root"], Receipts()));
    }

    [Theory]
    [InlineData("missing")][InlineData("duplicate")][InlineData("source")][InlineData("name")]
    [InlineData("order")][InlineData("blank")][InlineData("selectedDuplicate")][InlineData("selectedMissing")]
    public void SourceToPageCoverageIsRequired(string change)
    {
        var receipts = Receipts(); string pages = Root + Sub, selected = "root";
        if (change == "missing") receipts = receipts.Take(1).ToArray();
        if (change == "duplicate") receipts[1].PageId = "2";
        if (change == "source") receipts[1].SourceSubProcessId = "wrong";
        if (change == "name") receipts[1].PageName = "wrong";
        if (change == "order") pages = Sub + Root;
        if (change == "blank") pages = Root + "<Page ID='5' Name='Nested 日本語'/>";
        if (change == "selectedMissing") selected = "missing";
        Assert.Throws<InvalidDataException>(() => VisioDocument.PrepareExport(Document(pages), Source(),
            change == "selectedDuplicate" ? ["root", "root"] : [selected], receipts));
    }

    [Fact] public void EmptySubprocessDoesNotInventAnExportPage()
    {
        var source = Source().Take(2).ToArray();
        var result = VisioDocument.PrepareExport(Document(Root), source, ["root"], Receipts().Take(1).ToArray());
        Assert.Single(VisioDocument.Inspect(result.Bytes));
        Assert.Empty(result.RemovedReservedPages);
    }
}
