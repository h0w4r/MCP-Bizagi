using System.IO.Compression;
using System.Text;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class NativeFidelityTests
{
    // Small containers isolate the comparer; real-engine accreditation remains in the independent MCP suite.
    private static byte[] Archive(string xml, string attachment = "original attachment", bool nested = true)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("ModelInfo.xml").Open())) writer.Write("<ModelInfo/>");
            using (var writer = new StreamWriter(zip.CreateEntry("attachment.txt").Open())) writer.Write(attachment);
            if (nested)
            {
                using var inner = new MemoryStream();
                using (var diagram = new ZipArchive(inner, ZipArchiveMode.Create, leaveOpen: true))
                { using var writer = new StreamWriter(diagram.CreateEntry("Diagram.xml").Open()); writer.Write(xml); }
                using var target = zip.CreateEntry("diagram.diag").Open(); target.Write(inner.ToArray());
            }
            else { using var writer = new StreamWriter(zip.CreateEntry("Diagram.xml").Open()); writer.Write(xml); }
        }
        return stream.ToArray();
    }
    [Fact] public void ExactSemanticArchiveIsPreserved()
    { var a = Archive("<Diagram><Task Id='a' Name='Old'/><Unknown flag='keep'/></Diagram>"); Assert.True(NativeFidelity.Compare(a, a).Preserved); }
    [Fact] public void RequestedNameDoesNotExcuseUnrelatedChanges()
    {
        var a = Archive("<Diagram><Task Id='a' Name='Old'/><Unknown flag='keep'/></Diagram>");
        var b = Archive("<Diagram><Task Id='a' Name='New'/><Unknown flag='lost'/></Diagram>");
        var report = NativeFidelity.Compare(a, b, [new("a", "New")]);
        Assert.False(report.Preserved);
        Assert.Contains(report.Differences, d => d.Classification == "requested_name_change");
        Assert.Contains(report.Differences, d => d.Classification == "unexpected_xml_change");
    }
    [Fact] public void BinaryAttachmentChangesAreNeverIgnored()
    { Assert.Contains(NativeFidelity.Compare(Archive("<D/>"), Archive("<D/>", "changed")).Differences, d => d.Classification == "binary_changed"); }
    [Fact] public void XmlAttributeOrderingAndFormattingAreNotDataLoss()
    {
        var report = NativeFidelity.Compare(Archive("<D a='1' b='2'><C /></D>"), Archive("<D b='2' a='1'>\n <C/>\n</D>"));
        Assert.True(report.Preserved, System.Text.Json.JsonSerializer.Serialize(report));
    }
    [Fact] public void RemovedEntriesAndNestedContentAreDetected()
    { Assert.False(NativeFidelity.Compare(Archive("<D/>"), Archive("<D/>", nested: false)).Preserved); }

    private const string Ns = "http://www.wfmc.org/2009/XPDL2.2";
    [Fact] public void KnownTimestampRewriteIsExplicitlyReported()
    {
        string A(string date) => $"<Package xmlns='{Ns}'><WorkflowProcesses><WorkflowProcess Id='p'><ProcessHeader><Created>{date}</Created></ProcessHeader></WorkflowProcess></WorkflowProcesses></Package>";
        var result = NativeFidelity.Compare(Archive(A("2026-09-06T00:00:00Z")), Archive(A("2026-09-07T00:00:00Z")));
        Assert.True(result.Preserved);
        Assert.Contains(result.Differences, d => d.Classification == "engine_process_timestamp");
    }
    [Theory]
    [InlineData("<Unknown Created='2026-09-06'/>", "<Unknown Created='2026-09-07'/>")]
    [InlineData("<D xml:space='preserve'> </D>", "<D xml:space='preserve'>  </D>")]
    [InlineData("<!--retain--><D/>", "<!--changed--><D/>")]
    [InlineData("<D xmlns:p='urn:one' reference='p:Item'/>", "<D xmlns:p='urn:two' reference='p:Item'/>")]
    public void UnknownMetadataWhitespaceCommentsAndNamespaceMeaningArePreserved(string before, string after)
    { Assert.False(NativeFidelity.Compare(Archive(before), Archive(after)).Preserved); }
    [Fact] public void DefaultTransparentTextBackgroundIsReportedNotSilentlyLost()
    {
        var a = Archive($"<Package xmlns='{Ns}'><NodeGraphicsInfo><Coordinates XCoordinate='1'/></NodeGraphicsInfo></Package>");
        var b = Archive($"<Package xmlns='{Ns}'><NodeGraphicsInfo><Coordinates XCoordinate='1'/><TextBackgroundColor>16777215</TextBackgroundColor></NodeGraphicsInfo></Package>");
        var result = NativeFidelity.Compare(a, b);
        Assert.True(result.Preserved);
        Assert.Contains(result.Differences, d => d.Classification == "transparent_text_background_materialized");
    }
    [Fact] public void AuditAppendIsAllowedButRemovalOrReattributionIsNot()
    {
        string Wrap(string modifications) => $"<Package xmlns='{Ns}'><PackageHeader><Modifications>{modifications}</Modifications></PackageHeader></Package>";
        string first = "<Modification Date='2026-09-06T00:00:00Z' UserName='original'/>";
        string second = "<Modification Date='2026-09-07T00:00:00Z' UserName='new'/>";
        var a = Archive(Wrap(first)); var b = Archive(Wrap(first + second));
        Assert.True(NativeFidelity.Compare(a, b).Preserved);
        Assert.False(NativeFidelity.Compare(b, a).Preserved);
        Assert.False(NativeFidelity.Compare(a, Archive(Wrap(first.Replace("original", "different")))).Preserved);
    }
}
