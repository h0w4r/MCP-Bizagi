using System.Text;
using System.Xml;
using McpBizagi.Contracts;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Parser and difference-report units do not accredit the installed XPDL engine.</summary>
public sealed class XpdlDocumentTests
{
    private static byte[] Document(string body = "", string version = "2.2") => Encoding.UTF8.GetBytes(
        $"<Package xmlns='http://www.wfmc.org/2009/XPDL2.2'><PackageHeader><XPDLVersion>{version}</XPDLVersion></PackageHeader>{body}</Package>");

    [Fact] public void UnicodeAndUnknownContentAreNotNormalized()
    {
        byte[] input = Document("<Unknown Name='日本語 Ω'>COMPLEX &amp; Ω</Unknown>");
        Assert.Contains("日本語 Ω", XpdlDocument.Parse(input).ToString());
        Assert.Empty(XpdlDocument.CompareXml(input, input));
        Assert.NotEmpty(XpdlDocument.CompareXml(input, Document()));
    }

    [Theory]
    [InlineData("2.1")]
    [InlineData("1.0")]
    [InlineData("")]
    public void UnsupportedVersionsReject(string version) => Assert.Throws<InvalidDataException>(() => XpdlDocument.Parse(Document(version: version)));

    [Theory]
    [InlineData("<ExternalPackage href='file:///unused'/>")]
    [InlineData("<?document unused?>")]
    [InlineData("<xi:include xmlns:xi='http://www.w3.org/2001/XInclude' href='unused'/>")]
    public void ExternalResolversReject(string body) => Assert.Throws<InvalidDataException>(() => XpdlDocument.Parse(Document(body)));

    [Fact] public void DtdRejectsWithoutResolution() => Assert.Throws<XmlException>(() => XpdlDocument.Parse(
        Encoding.UTF8.GetBytes("<!DOCTYPE Package [<!ENTITY text 'unused'>]>" + Encoding.UTF8.GetString(Document()))));

    [Theory]
    [InlineData("<x>a</x>", "<x>b</x>")]
    [InlineData("<x a='1'/>", "<x a='2'/>")]
    [InlineData("<!--preserve-->", "")]
    [InlineData("<x xml:space='preserve'> <y/> </x>", "<x xml:space='preserve'><y/></x>")]
    [InlineData("<x>before <y/> after</x>", "<x>before<y/>after</x>")]
    public void ChangesAreExplicit(string before, string after) => Assert.NotEmpty(XpdlDocument.CompareXml(Document(before), Document(after)));

    [Fact] public void SerializerIndentationIsNotUserContent() => Assert.Empty(XpdlDocument.CompareXml(Document("<x>\n <y/>\n</x>"), Document("<x><y/></x>")));

    [Fact] public void GraphChecksAllObservedFieldsNotJustCounts()
    {
        NativeElement[] before = [new() { Id = "same", Name = "日本語", Geometry = new() { Width = 10 } }];
        NativeElement[] after = [new() { Id = "same", Name = "日本語", Geometry = new() { Width = 20 } }];
        Assert.Single(XpdlDocument.CompareGraph(before, after));
    }

    [Fact] public void ExcessiveXmlDepthRejectsBeforeRecursion() => Assert.Throws<InvalidDataException>(() => XpdlDocument.Parse(
        Document(string.Concat(Enumerable.Repeat("<nested>", 140)) + string.Concat(Enumerable.Repeat("</nested>", 140)))));

    [Fact] public void TopLevelCommentsAreReported()
    {
        var before = Encoding.UTF8.GetBytes("<!--user note-->" + Encoding.UTF8.GetString(Document()));
        Assert.Single(XpdlDocument.CompareXml(before, Document()));
    }

    [Fact] public void MissingMetadataIsNeverEvidence() => Assert.Throws<InvalidDataException>(() => XpdlDocument.CompareMetadata(new(), new()));

    [Fact] public void CatalogOrderIsNotDefinitionIdentity()
    {
        var one = new NativeAttributeDefinition { Id = "one", Xml = "<definition/>" };
        var two = new NativeAttributeDefinition { Id = "two", Xml = "<definition/>" };
        var before = new EngineReply { Metadata = new(), Documentation = new() { Definitions = [one, two] } };
        var after = new EngineReply { Metadata = new(), Documentation = new() { Definitions = [two, one] } };
        Assert.Empty(XpdlDocument.CompareMetadata(before, after));
        after.Documentation.Values = [new() { DiagramId = "diagram", ElementId = "one", Xml = "<value>must be reported</value>" }];
        Assert.Single(XpdlDocument.CompareMetadata(before, after));
    }
}
