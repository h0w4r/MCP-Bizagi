using System.IO.Compression;
using System.Text;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class NativeDocumentationPolicyTests
{
    private const string D = "11111111-1111-4111-8111-111111111111", E = "22222222-2222-4222-8222-222222222222", A = "33333333-3333-4333-8333-333333333333";
    private static string Definition(string name = "Own attribute", string type = "Text") => $"<ExtendedAttribute Id='{A}' Type='{type}' ExportAsTable='false' Visible='false'><Name>{name}</Name><Description/><Options/><TableColumns/><ElementTypes><AttributeElementType Type='Task'/></ElementTypes></ExtendedAttribute>";
    private static string Values(string content = "Value") => $"<ElementAttributeValues ElementId='{E}'><Values><ExtendedAttributeValue Id='{A}' Type='Text'><Content>{content}</Content><DisplayValue>{content}</DisplayValue><TableValues/></ExtendedAttributeValue></Values></ElementAttributeValues>";
    private static NativeDocumentationPatch DefinitionPatch(string xml) => new() { Definitions = [new() { Id = A, Xml = xml }] };
    private static NativeDocumentationSnapshot Readback(string xml) => new() { Definitions = [new() { Id = A, Xml = xml }] };
    private static byte[] Archive(string? definition = null, string? values = null, byte[]? attachment = null, string unknown = "<Unknown keep='true'/>", string? order = null, string attachmentName = "file.xml")
    {
        // Small synthetic archives exercise comparison policy only, never native-engine accreditation.
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            void Write(string name, string text) { using var s = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); s.Write(text); }
            Write("ModelInfo.xml", "<ModelInfo/>"); Write("unknown.xml", unknown);
            if (definition != null) Write("Documentation/" + A + ".xml", definition);
            if (order != null) Write("Documentation/Task.order", order);
            using var diagram = new MemoryStream();
            using (var nested = new ZipArchive(diagram, ZipArchiveMode.Create, true))
            {
                if (values != null) { using var s = new StreamWriter(nested.CreateEntry("ExtendedAttributeValues.xml").Open(), new UTF8Encoding(false)); s.Write("<DiagramAttributeValues>" + values + "</DiagramAttributeValues>"); }
                if (attachment != null) { using var s = nested.CreateEntry("Files/" + E + "/" + attachmentName).Open(); s.Write(attachment); }
            }
            using var destination = zip.CreateEntry(D + ".diag").Open(); destination.Write(diagram.ToArray());
        }
        return buffer.ToArray();
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("LongText")]
    [InlineData("Number")]
    [InlineData("Date")]
    [InlineData("Combo")]
    [InlineData("Radio")]
    [InlineData("Check")]
    [InlineData("Link")]
    [InlineData("FileLinked")]
    [InlineData("FileEmbedded")]
    [InlineData("Image")]
    [InlineData("Table")]
    public void AllKnownDefinitionKindsHaveExactDurableReadbackGate(string type)
    {
        string xml = Definition(type: type);
        var report = NativeDocumentationPolicy.Compare(Archive(), Archive(xml), DefinitionPatch(xml), Readback(xml));
        Assert.True(report.Preserved); Assert.True(report.CheckedAtoms > 0);
        Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.Compare(Archive(), Archive(Definition("Changed", type)), DefinitionPatch(xml), Readback(xml)));
    }

    [Theory]
    [InlineData("../file")]
    [InlineData("C:\\file")]
    [InlineData("a/b")]
    [InlineData("file:stream")]
    [InlineData("NUL.txt")]
    [InlineData("COM1")]
    [InlineData("LPT9.txt")]
    [InlineData("file.")]
    [InlineData("file ")]
    [InlineData("a\nb")]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    public void AttachmentNamesCannotBecomePathsOrDeviceNames(string name) => Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.RequireFileName(name));

    [Theory]
    [InlineData("Evidence Ω.xml")]
    [InlineData("日本語.png")]
    [InlineData("file name.txt")]
    [InlineData("COM10.txt")]
    public void OrdinaryUnicodeAttachmentNamesAreAccepted(string name) => NativeDocumentationPolicy.RequireFileName(name);

    [Fact]
    public void DefinitionReplacementCannotHideUnrelatedUnknownChange()
    {
        string xml = Definition("New");
        Assert.False(NativeDocumentationPolicy.Compare(Archive(Definition()), Archive(xml, unknown: "<Unknown keep='false'/>"), DefinitionPatch(xml), Readback(xml)).Preserved);
    }

    [Fact]
    public void DefinitionDeletionRequiresActualRemovalAndAbsentReadback()
    {
        var patch = new NativeDocumentationPatch { Definitions = [new() { Id = A, Operation = "delete" }] };
        Assert.True(NativeDocumentationPolicy.Compare(Archive(Definition()), Archive(), patch, new()).Preserved);
        Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.Compare(Archive(Definition()), Archive(Definition()), patch, new()));
        Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.Compare(Archive(Definition()), Archive(), patch, Readback(Definition())));
    }

    [Fact]
    public void ExplicitElementReplacementPreservesUnknownNeighbors()
    {
        string replacement = Values("Updated");
        var patch = new NativeDocumentationPatch { Values = [new() { DiagramId = D, ElementId = E, Xml = replacement }] };
        var snapshot = new NativeDocumentationSnapshot { Values = patch.Values };
        string neighbor = "<ElementAttributeValues ElementId='44444444-4444-4444-8444-444444444444'><Values/><Unknown>retain</Unknown></ElementAttributeValues>";
        Assert.True(NativeDocumentationPolicy.Compare(Archive(values: Values() + neighbor), Archive(values: replacement + neighbor), patch, snapshot).Preserved);
        Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.Compare(Archive(values: Values() + neighbor), Archive(values: replacement + neighbor.Replace("retain", "lost")), patch, snapshot));
    }

    [Theory]
    [InlineData("file.xml")][InlineData("file.diag")][InlineData("file.bpm")][InlineData("file.txt")]
    public void EmbeddedAttachmentNamesDoNotChangeOpaqueByteTreatment(string name)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("Not XML at all");
        Assert.True(NativeFidelity.Compare(Archive(attachment: bytes, attachmentName: name), Archive(attachment: bytes, attachmentName: name)).Preserved);
        Assert.False(NativeFidelity.Compare(Archive(attachment: bytes, attachmentName: name), Archive(attachment: [1, 2, 3], attachmentName: name)).Preserved);
    }

    [Fact]
    public void AttachmentProjectionRequiresBytesLengthAndFreshHash()
    {
        byte[] payload = [1, 2, 3, 255];
        var patch = new NativeDocumentationPatch { Attachments = [new() { DiagramId = D, ElementId = E, FileName = "file.xml", DataBase64 = Convert.ToBase64String(payload) }] };
        var readback = new NativeDocumentationSnapshot { Attachments = [new() { DiagramId = D, ElementId = E, FileName = "file.xml", Length = 4, Sha256 = BpmnDocument.Revision(payload) }] };
        Assert.True(NativeDocumentationPolicy.Compare(Archive(), Archive(attachment: payload), patch, readback).Preserved);
        readback.Attachments[0].Sha256 = "invalid";
        Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.Compare(Archive(), Archive(attachment: payload), patch, readback));
    }

    [Fact]
    public void AttachmentNormalizationRequiresOwningElementAndExistingArchiveLeaf()
    {
        string xml = Values("C:\\scratch\\" + E + "\\file.xml").Replace("Type='Text'", "Type='FileEmbedded'");
        var entries = NativeArchive.ReadEntries(Archive(attachment: [1]));
        Assert.Contains("attachment:file.xml", NativeDocumentationPolicy.ValuesContent(xml, D, entries));
        Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.ValuesContent(xml, D));
        Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.ValuesContent(xml.Replace("scratch\\" + E, "scratch\\" + A), D, entries));
    }

    [Fact]
    public void DefinitionOrderMustMatchExpectedMembershipAndPosition()
    {
        string order = $"<ExtendedAttributeOrder><ExtendedAttributes><Id>{A}</Id></ExtendedAttributes><ElementType Type='Task'/></ExtendedAttributeOrder>";
        Assert.True(NativeDocumentationPolicy.Compare(Archive(), Archive(Definition(), order: order), DefinitionPatch(Definition()), Readback(Definition())).Preserved);
        Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.Compare(Archive(), Archive(Definition(), order: order.Replace(A, E)), DefinitionPatch(Definition()), Readback(Definition())));
    }

    [Fact]
    public void DefinitionsRejectDtdNullDuplicateAndMissingScope()
    {
        Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.Validate(new()));
        Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.Validate(new() { Definitions = [null!] }));
        var patch = DefinitionPatch(Definition()); patch.Definitions = [patch.Definitions[0], patch.Definitions[0]];
        Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.Validate(patch));
        Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.Validate(DefinitionPatch(Definition().Replace("<AttributeElementType Type='Task'/>", ""))));
        Assert.Throws<System.Xml.XmlException>(() => NativeDocumentationPolicy.Validate(DefinitionPatch("<!DOCTYPE x [<!ENTITY y 'text'>]>" + Definition())));
    }

    [Fact]
    public void PassiveDefinitionAuditTimestampIsClassifiedButUnknownFieldsRemainExact()
    {
        string before = Definition().Replace("ExportAsTable", "ModificationDate='2026-09-07T01:00:00Z' ExportAsTable");
        string after = before.Replace("01:00:00Z", "02:00:00Z");
        var report = NativeFidelity.Compare(Archive(before), Archive(after));
        Assert.True(report.Preserved);
        Assert.Contains(report.Differences, d => d.Classification == "modification_timestamp" && d.Entry == "Documentation/" + A + ".xml");
        Assert.False(NativeFidelity.Compare(Archive(before), Archive(after.Replace("Own attribute", "Unrequested"))).Preserved);
        Assert.False(NativeFidelity.Compare(Archive(before), Archive(after.Replace("2026-09-07T02:00:00Z", "not a timestamp"))).Preserved);
    }

    [Fact]
    public void PassiveAttachmentRelocationIsClassifiedWithoutIgnoringByteChanges()
    {
        string old = Values("C:\\old\\" + E + "\\file.xml").Replace("Type='Text'", "Type='FileEmbedded'");
        // DisplayValue is user-visible data, not a runtime path. Only change Content in the second model.
        string updated = old.Replace("<Content>C:\\old\\", "<Content>C:\\new\\");
        var report = NativeFidelity.Compare(Archive(values: old, attachment: [1]), Archive(values: updated, attachment: [1]));
        Assert.True(report.Preserved); Assert.Contains(report.Differences, d => d.Classification == "attachment_runtime_path_relocated");
        Assert.False(NativeFidelity.Compare(Archive(values: old, attachment: [1]), Archive(values: updated, attachment: [2])).Preserved);
    }

    [Fact]
    public void RuntimePathNormalizationCannotEraseCommentsOrUnknownChildren()
    {
        string xml = Values("C:\\scratch\\" + E + "\\file.xml").Replace("Type='Text'", "Type='FileEmbedded'").Replace("</Content>", "<!--retain--></Content>");
        Assert.Throws<InvalidDataException>(() => NativeDocumentationPolicy.ValuesContent(xml, D, NativeArchive.ReadEntries(Archive(attachment: [1]))));
    }
}
