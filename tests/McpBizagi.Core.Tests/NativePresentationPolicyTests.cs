using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Synthetic containers test policy only, never native operational acceptance.</summary>
public sealed class NativePresentationPolicyTests
{
    private const string D = "11111111-2222-4333-8444-555555555555";
    private const string A = "22222222-3333-4444-8555-666666666666";
    private const string B = "33333333-4444-4555-8666-777777777777";
    private const string X = "44444444-5555-4666-8777-888888888888";
    private static NativePresentationAction Action(string type = "Text", string value = "Normal", string content = "Literal", string owner = A) => new()
    { DiagramId = D, ElementId = owner, Type = type, TypeValue = value, Content = content };
    private static NativePresentationActionChange Change(NativePresentationAction? a = null, string data = "") => new() { Action = a ?? Action(), DataBase64 = data };
    private static string Xml(NativePresentationAction a) => new XElement("PresentationAction", new XAttribute("ElementId", a.ElementId), new XAttribute("Type", a.Type),
        new XAttribute("TypeValue", a.TypeValue), a.ExtendedAttributeId == "" ? null : new XAttribute("ExtendedAttributeId", a.ExtendedAttributeId),
        new XElement("DisplayName", a.DisplayName), new XElement("Content", a.Content)).ToString();
    private static byte[] Archive(string body = "", byte[]? file = null, string extra = "", string fileName = "own.xml")
    {
        using var result = new MemoryStream();
        using (var zip = new ZipArchive(result, ZipArchiveMode.Create, true))
        {
            void Write(ZipArchive z, string name, byte[] bytes) { using var s = z.CreateEntry(name).Open(); s.Write(bytes); }
            Write(zip, "ModelInfo.xml", Encoding.UTF8.GetBytes("<ModelInfo/>"));
            using var nested = new MemoryStream();
            using (var diag = new ZipArchive(nested, ZipArchiveMode.Create, true))
            {
                Write(diag, "Actions.xml", Encoding.UTF8.GetBytes("<DiagramActions>" + body + extra + "</DiagramActions>"));
                Write(diag, "Diagram.xml", Encoding.UTF8.GetBytes("<Unrelated exact='yes'/>"));
                if (file != null) Write(diag, "Actions/" + fileName, file);
            }
            Write(zip, D + ".diag", nested.ToArray());
        }
        return result.ToArray();
    }
    private static EngineReply Source(params NativePresentationAction[] actions) => new()
    {
        Elements = [new() { Id = D, Kind = "Collaboration", DiagramId = D }, new() { Id = A, DiagramId = D, Kind = "Task", Documentation = "Native description Ω" }, new() { Id = B, DiagramId = D, Kind = "Task" }],
        Presentation = new() { Actions = actions }
    };
    [Theory]
    [InlineData("None", "")]
    [InlineData("Text", "Unicode Ω\nC:\\not-a-file")]
    [InlineData("Link", "https://example.invalid/inert")]
    public void NormalScalarIntentIsLiteral(string type, string content)
    {
        var plan = NativePresentationPolicy.Prepare(Archive(), Source(), [Change(Action(type, content: content))]);
        Assert.Equal(content, Assert.Single(plan.Actions).Content);
    }
    [Theory]
    [InlineData("../outside")]
    [InlineData("C:\\outside")]
    [InlineData("x/y")]
    [InlineData("CON")]
    [InlineData("trailing.")]
    [InlineData("33333333-4444-4555-8666-777777777777.bin")]
    public void RejectsUnsafeOrForeignGuidFileNames(string name) => Assert.Throws<InvalidDataException>(() =>
        NativePresentationPolicy.Validate([Change(Action("File", content: "action-file:" + name))]));
    [Fact] public void RejectsRepeatedOwner() => Assert.Throws<InvalidDataException>(() => NativePresentationPolicy.Validate([Change(), Change()]));
    [Fact] public void RejectsMissingOwner() => Assert.Throws<InvalidDataException>(() => NativePresentationPolicy.Prepare(Archive(), Source(), [Change(Action(owner: X))]));
    [Fact] public void RejectsBinaryDataForText() => Assert.Throws<InvalidDataException>(() => NativePresentationPolicy.Validate([Change(data: "AQI=")]));
    [Fact] public void RejectsUnknownEnum() => Assert.Throws<InvalidDataException>(() => NativePresentationPolicy.Validate([Change(Action("Execute"))]));
    [Fact] public void RejectsCallerSuppliedReferenceCache() => Assert.Throws<InvalidDataException>(() => NativePresentationPolicy.Validate([Change(Action(value: "Description"))]));
    [Fact] public void DescriptionDerivesFromObservedNativeOwner() => Assert.Equal("Native description Ω",
        Assert.Single(NativePresentationPolicy.Prepare(Archive(), Source(), [Change(Action(value: "Description", content: ""))]).Actions).Content);
    [Fact] public void ReplacingUnknownActionFieldsIsRejected() => Assert.Throws<InvalidDataException>(() =>
        NativePresentationPolicy.Prepare(Archive(Xml(Action()).Replace("</PresentationAction>", "<Unknown/></PresentationAction>")), Source(Action()), [Change()]));
    [Fact] public void DuplicateStoredOwnersAreRejected() => Assert.Throws<ArgumentException>(() => NativePresentationPolicy.Prepare(Archive(Xml(Action()) + Xml(Action())), Source(Action()), [Change()]));
    [Fact] public void SourceObservationMismatchIsRejected() => Assert.Throws<InvalidDataException>(() => NativePresentationPolicy.Prepare(Archive(), Source(Action()), [Change()]));
    [Fact]
    public void BytePayloadIsNotParsedAsXmlOrNestedDiagram()
    {
        byte[] data = [0, 255, 1];
        Assert.Equal(data, NativeArchive.ReadEntries(Archive(file: data))[D + ".diag!/Actions/own.xml"]);
        Assert.Equal(data, NativeArchive.ReadEntries(Archive(file: data, fileName: "own.diag"))[D + ".diag!/Actions/own.diag"]);
        Assert.False(NativeFidelity.Compare(Archive(file: data), Archive(file: [1, 2])).Preserved);
    }
    [Fact]
    public void PayloadRelocationRequiresAnExistingArchiveOwnedFile()
    {
        var a = Action("File", content: "C:\\scratch\\" + D + "\\Actions\\own.xml");
        var bytes = Archive(Xml(a), [1, 2]); var entries = NativeArchive.ReadEntries(bytes);
        string normalized = NativePresentationPolicy.Normalize("<DiagramActions>" + Xml(a) + "</DiagramActions>", D, entries);
        Assert.Contains("action-file:own.xml", normalized);
        var other = Action("File", content: "D:\\other\\" + D + "\\Actions\\own.xml");
        Assert.True(NativeFidelity.Compare(bytes, Archive(Xml(other), [1, 2])).Preserved);
        Assert.Throws<InvalidDataException>(() => NativePresentationPolicy.Normalize("<DiagramActions>" + Xml(a) + "</DiagramActions>", D, new Dictionary<string, byte[]>()));
        a.Content = "C:\\wrong\\" + B + "\\Actions\\own.xml";
        Assert.Throws<InvalidDataException>(() => NativePresentationPolicy.Normalize("<DiagramActions>" + Xml(a) + "</DiagramActions>", D, entries));
    }
    [Fact]
    public void LiteralPathsAreNeverNormalized()
    {
        var a = Action(content: "C:\\scratch\\" + D + "\\Actions\\own.xml");
        var b = Action(content: "D:\\other\\" + D + "\\Actions\\own.xml");
        Assert.False(NativeFidelity.Compare(Archive(Xml(a)), Archive(Xml(b))).Preserved);
    }
    [Fact]
    public void SharedPayloadCannotBeOverwrittenButCanBeRetainedByAnotherOwner()
    {
        var a = Action("File", content: "action-file:own.xml"); var b = Action("File", content: a.Content, owner: B);
        var bytes = Archive(Xml(a) + Xml(b), [1, 2]);
        Assert.Throws<InvalidDataException>(() => NativePresentationPolicy.Prepare(bytes, Source(a, b), [Change(a, "AwQ=")]));
        var plan = NativePresentationPolicy.Prepare(bytes, Source(a, b), [new() { Operation = "delete", Action = Action("None", content: "") }]);
        Assert.Equal(B, Assert.Single(plan.Actions).ElementId);
        Assert.True(plan.ExpectedEntries.ContainsKey(D + ".diag!/Actions/own.xml"));
    }
    [Fact]
    public void FinalDeletionRemovesOnlyOwnedPayload()
    {
        var a = Action("File", content: "action-file:own.xml");
        var plan = NativePresentationPolicy.Prepare(Archive(Xml(a), [1, 2], "<Unknown exact='yes'/>"), Source(a), [new() { Operation = "delete", Action = Action("None", content: "") }]);
        Assert.Empty(plan.Actions); Assert.Empty(plan.Files);
        Assert.False(plan.ExpectedEntries.ContainsKey(D + ".diag!/Actions/own.xml"));
        Assert.Contains("<Unknown exact=\"yes\"", Encoding.UTF8.GetString(plan.ExpectedEntries[D + ".diag!/Actions.xml"]));
    }
    [Fact]
    public void UnownedPayloadOverwriteIsRejected() => Assert.Throws<InvalidDataException>(() =>
        NativePresentationPolicy.Prepare(Archive(file: [1, 2]), Source(), [Change(Action("File", content: "action-file:own.xml"), "AwQ=")]));

    [Fact]
    public void LastRecordDeletionDoesNotPromoteInterRecordFormattingToLiteralContent()
    {
        var plan = NativePresentationPolicy.Prepare(Archive("\n  " + Xml(Action()) + "\n"), Source(Action()),
            [new() { Operation = "delete", Action = Action("None", content: "") }]);
        Assert.True(NativePresentationPolicy.Compare(Archive(), plan, Source(), Source()).Preserved);
    }

    [Theory]
    [InlineData("Text", "Text")]
    [InlineData("Text", "LongText")]
    [InlineData("Text", "Number")]
    [InlineData("Text", "Date")]
    [InlineData("Link", "Link")]
    [InlineData("File", "FileLinked")]
    [InlineData("File", "FileEmbedded")]
    [InlineData("Image", "Image")]
    public void ReferenceContentDerivesOnlyFromMatchingOwnerAndDefinition(string actionType, string attributeType)
    {
        var a = Action(actionType, "ExtendedAttribute", ""); a.ExtendedAttributeId = X;
        var source = Source(); source.Documentation = new()
        {
            Definitions = [new() { Id = X, Xml = $"<ExtendedAttribute Id='{X}' Type='{attributeType}'/>" }],
            Values = [new() { DiagramId = D, ElementId = A, Xml = $"<ElementAttributeValues ElementId='{A}'><Values><ExtendedAttributeValue Id='{X}' Type='{attributeType}'><Content>Observed Ω</Content></ExtendedAttributeValue></Values></ElementAttributeValues>" }]
        };
        Assert.Equal("Observed Ω", Assert.Single(NativePresentationPolicy.Prepare(Archive(), source, [Change(a)]).Actions).Content);
        source.Documentation.Values[0].ElementId = B;
        Assert.Throws<InvalidDataException>(() => NativePresentationPolicy.Prepare(Archive(), source, [Change(a)]));
    }

    [Fact]
    public void ReferencedEmbeddedPathMustIdentifyActualOwnerAndPreservePayloadBytes()
    {
        var a = Action("Image", "ExtendedAttribute", "C:\\scratch\\Files\\" + A + "\\own.png"); a.ExtendedAttributeId = X;
        var entries = new Dictionary<string, byte[]> {
            ["Documentation/" + X + ".xml"] = Encoding.UTF8.GetBytes($"<ExtendedAttribute Id='{X}' Type='Image'/>"),
            [D + ".diag!/Files/" + A + "/own.png"] = [1, 2, 3]
        };
        Assert.Contains("attachment:own.png", NativePresentationPolicy.Normalize("<DiagramActions>" + Xml(a) + "</DiagramActions>", D, entries));
        a.Content = "C:\\scratch\\Files\\" + B + "\\own.png";
        Assert.Throws<InvalidDataException>(() => NativePresentationPolicy.Normalize("<DiagramActions>" + Xml(a) + "</DiagramActions>", D, entries));
    }
}
