using System.Text;
using System.Xml;
using McpBizagi.Contracts;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>VDX policy units; these fixtures never stand in for native acceptance.</summary>
public sealed class VisioDocumentTests
{
    private static byte[] Document(string pages) => Encoding.UTF8.GetBytes("<VisioDocument xmlns='http://schemas.microsoft.com/visio/2003/core'><Pages>" + pages + "</Pages></VisioDocument>");

    [Fact] public void InventoryPreservesUnicodeAndGroupOwnership()
    {
        var pages = VisioDocument.Inspect(Document("<Page ID='0' Name='日本語 Ω'><Shapes><Shape ID='2' Master='8'><Text>Hello <cp IX='0'/>Ω</Text><Shapes><Shape ID='3'/></Shapes></Shape></Shapes></Page>"));
        Assert.Equal("日本語 Ω", pages[0].Name); Assert.Equal(2, pages[0].Shapes.Length);
        Assert.Equal("Hello Ω", pages[0].Shapes[0].Text); Assert.Equal("2", pages[0].Shapes[1].ParentId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<Page ID='1'/><Page ID='1'/>")]
    [InlineData("<Page ID='-1'/>")]
    [InlineData("<Page ID='01'/>")]
    [InlineData("<Page/>")]
    [InlineData("<Page ID='1'><Shapes><Shape ID='2'/><Shape ID='2'/></Shapes></Page>")]
    [InlineData("<Page ID='1'><Shapes><Shape/></Shapes></Page>")]
    [InlineData("<?process ignored?><Page ID='1'/>")]
    [InlineData("<Page ID='1' xml:base='file:///unused'/>")]
    [InlineData("<Page ID='1'><include xmlns='http://www.w3.org/2001/XInclude'/></Page>")]
    public void InvalidDocumentsReject(string pages) => Assert.Throws<InvalidDataException>(() => VisioDocument.Inspect(Document(pages)));

    [Fact] public void WrongFormatRejects() => Assert.Throws<InvalidDataException>(() => VisioDocument.Inspect(Encoding.UTF8.GetBytes("<Document/>")));
    [Fact] public void DtdRejects() => Assert.Throws<XmlException>(() => VisioDocument.Inspect(Encoding.UTF8.GetBytes("<!DOCTYPE VisioDocument [<!ELEMENT VisioDocument ANY>]><VisioDocument/>")));
    [Fact] public void ExcessNestingRejects() => Assert.Throws<InvalidDataException>(() => VisioDocument.Inspect(Document("<Page ID='1'>" + string.Concat(Enumerable.Repeat("<x>", 130)) + string.Concat(Enumerable.Repeat("</x>", 130)) + "</Page>")));
    [Fact] public void PageIdsDoNotGloballyConstrainShapeIds() => Assert.Equal(2, VisioDocument.Inspect(Document("<Page ID='1'><Shapes><Shape ID='1'/></Shapes></Page><Page ID='2'><Shapes><Shape ID='1'/></Shapes></Page>")).Length);

    private static NativeElement Element() => new() { Id = "id", Kind = "Task", Name = "Review Ω", BpmnId = "bpmn", DiagramId = "diagram" };
    [Fact] public void FirstImportGraphicsNormalizationIsExplicit()
    {
        var before = Element(); var after = Element(); after.Geometry = new NativeGeometry { Width = 100 };
        Assert.Single(VisioDocument.ImportNormalization([before], [after]));
        Assert.Null(before.Geometry);
    }
    [Theory]
    [InlineData("name")]
    [InlineData("kind")]
    [InlineData("documentation")]
    [InlineData("bpmn")]
    [InlineData("reference")]
    public void SemanticChangesAreNotHiddenAsImportNormalization(string field)
    {
        var before = Element(); var after = Element();
        if (field == "name") after.Name = "lost";
        if (field == "kind") after.Kind = "UserTask";
        if (field == "documentation") after.Documentation = "changed";
        if (field == "bpmn") after.BpmnId = "other";
        if (field == "reference") after.SourceRef = "missing";
        Assert.Throws<InvalidDataException>(() => VisioDocument.ImportNormalization([before], [after]));
    }
    [Fact] public void LostNativeIdentityRejects() => Assert.Throws<InvalidDataException>(() => VisioDocument.ImportNormalization([Element()], []));

    private static EngineReply Reply(string? xml = null) => new() {
        Elements = [Element()], Metadata = new NativeMetadataSnapshot(), Documentation = new NativeDocumentationSnapshot {
            Values = xml == null ? [] : [new NativeAttributeValues { ElementId = "id", DiagramId = "diagram", Xml = xml }] } };

    [Fact] public void OnlyEmptyKnownValueInitializationIsAllowed() => Assert.Single(VisioDocument.ImportMetadataNormalization(Reply(),
        Reply("<ElementAttributeValues ElementId='id'><Values /></ElementAttributeValues>")));

    [Theory]
    [InlineData("<ElementAttributeValues ElementId='id'><Values><Attribute /></Values></ElementAttributeValues>")]
    [InlineData("<ElementAttributeValues ElementId='id'><Values><!--unknown--></Values></ElementAttributeValues>")]
    [InlineData("<ElementAttributeValues ElementId='id' xml:space='preserve'><Values /></ElementAttributeValues>")]
    [InlineData("<ElementAttributeValues ElementId='id' xmlns='urn:unknown'><Values /></ElementAttributeValues>")]
    [InlineData("<ElementAttributeValues ElementId='id'><Values />private text</ElementAttributeValues>")]
    public void UnknownMetadataCannotBecomeAnInitializationException(string xml) => Assert.Throws<InvalidDataException>(() => VisioDocument.ImportMetadataNormalization(Reply(), Reply(xml)));

    [Fact] public void RemovedMetadataCannotBecomeAnInitializationException() => Assert.Throws<InvalidDataException>(() =>
        VisioDocument.ImportMetadataNormalization(Reply("<ElementAttributeValues ElementId='id'><Values /></ElementAttributeValues>"), Reply()));

    [Fact] public void ProjectionReportsDuplicateLabelLossDespiteReassignedIds()
    {
        var second = Element(); second.Id = "second";
        var remapped = Element(); remapped.Id = "third";
        var difference = Assert.Single(VisioDocument.CompareProjection([Element(), second], [remapped]));
        Assert.Equal(2, difference.SourceCount); Assert.Equal(1, difference.ImportedCount);
    }
}
