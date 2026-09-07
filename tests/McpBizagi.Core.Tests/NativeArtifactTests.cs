using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Pure intent/fidelity cases, separate from installed-engine artifact lifecycle acceptance.</summary>
public sealed class NativeArtifactTests
{
    private const string Id = "11111111-1111-4111-8111-111111111111", Parent = "22222222-2222-4222-8222-222222222222";
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static NativeMutation Patch(string? text) => new() { Operation = "update", ElementId = Id, ArtifactProperties = new() { Text = text } };
    private static XElement Artifact(string contents, string type = "ArtifactType='Annotation'") =>
        XDocument.Parse($"<Package xmlns='{Ns}'><Artifacts><Artifact Id='{Id}' {type}>{contents}</Artifact></Artifacts></Package>").Descendants(Ns + "Artifact").Single();

    [Theory] [InlineData("")] [InlineData("Unicode Ω 日本語")] [InlineData("<p><b>Native rich text</b></p>")]
    public void ExplicitContentIncludingEmptyIsValid(string text) => NativeEditPlan.Validate([Patch(text)]);
    [Fact] public void NullOversizedAndIllegalXmlTextFail()
    {
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Patch(null)]));
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Patch(new string('x', 1024 * 1024 + 1))]));
        Assert.Throws<XmlException>(() => NativeEditPlan.Validate([Patch("invalid\0") ]));
    }
    [Theory] [InlineData("delete")] [InlineData("reconnect")]
    public void DestructiveOperationsCannotSmuggleText(string operation)
    {
        var c = Patch("new"); c.Operation = operation;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c]));
    }
    [Theory] [InlineData("TextAnnotation")] [InlineData("FormattedTextArtifact")] [InlineData("HeaderArtifact")]
    public void NonpersistentDisplayNamesFailExplicitly(string kind)
    {
        var c = new NativeMutation { Operation = "create", ElementId = Id, ParentId = Parent, ElementType = kind, Name = "discarded" };
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c]));
        Assert.Throws<InvalidDataException>(() => NativeArtifactPolicy.Verify(c, new() { Kind = kind }, []));
    }
    [Theory] [InlineData("UserTask")] [InlineData("Group")] [InlineData("HeaderArtifact")]
    public void ArtifactTextCannotTargetAnotherType(string kind)
    {
        var c = Patch("text"); c.Operation = "create"; c.ParentId = Parent; c.ElementType = kind;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c]));
        Assert.Throws<InvalidDataException>(() => NativeArtifactPolicy.Verify(c, new() { Kind = kind, Artifact = new() { Text = "text" } }, []));
    }
    [Fact] public void GroupOwnershipAndHeaderContextAreRealPostconditions()
    {
        var c = new NativeMutation { Operation = "update", ElementId = Id, Name = "group" };
        Assert.Throws<InvalidDataException>(() => NativeArtifactPolicy.Verify(c, new() { Kind = "Group", ParentId = Parent }, []));
        NativeArtifactPolicy.Verify(c, new() { Kind = "Group", ParentId = Parent }, [new() { Id = Parent, Kind = "Collaboration" }]);
        c.Name = null;
        Assert.Throws<InvalidDataException>(() => NativeArtifactPolicy.Verify(c, new() { Kind = "HeaderArtifact", DiagramId = Parent, Artifact = new() { HeaderDiagramId = Id } }, []));
        NativeArtifactPolicy.Verify(c, new() { Kind = "HeaderArtifact", DiagramId = Parent, Artifact = new() { HeaderDiagramId = Parent } }, []);
    }
    [Fact] public void IntrinsicGroupViewIsNotAnImplicitSubprocessExpansion()
    {
        var c = new NativeMutation { Operation = "create", ElementId = Id, ParentId = Parent, ElementType = "Group", Geometry = new() { Width = 250, Height = 200, Expanded = true } };
        NativeEditPlan.Validate([c]); c.Geometry.Expanded = false;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c]));
        c.Operation = "update"; c.ParentId = ""; c.ElementType = ""; c.Geometry.Expanded = true;
        NativeEditPlan.Validate([c]);
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([c], [new() { Id = Id, Kind = "SubProcess" }]));
    }
    [Theory] [InlineData("TextAnnotation")] [InlineData("FormattedTextArtifact")]
    public void TextMustActuallySurviveReadback(string kind)
    {
        var e = new NativeElement { Kind = kind, Artifact = new() { Text = "new" } };
        NativeArtifactPolicy.Verify(Patch("new"), e, []); e.Artifact.Text = "old";
        Assert.Throws<InvalidDataException>(() => NativeArtifactPolicy.Verify(Patch("new"), e, []));
    }
    [Theory] [InlineData("ArtifactType='Annotation'")] [InlineData("BizAgiArtifactType='FormattedText'")]
    public void ExactContentProjectionRetainsEveryOtherField(string type)
    {
        var a = Artifact("<Unknown keep='yes'/>", type + " TextAnnotation='old'");
        var b = Artifact("<Unknown keep='yes'/>", type + " TextAnnotation='new'");
        NativeArtifactPolicy.Project(a, b, Patch("new")); Assert.True(XNode.DeepEquals(a, b));
        b.Element(Ns + "Unknown")!.Remove(); Assert.False(XNode.DeepEquals(a, b));
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void EmptyTextCarrierLifecycleIsNarrow(bool clearing)
    {
        var a = Artifact("<Unknown/>", "ArtifactType='Annotation'" + (clearing ? " TextAnnotation='old'" : ""));
        var b = Artifact("<Unknown/>", "ArtifactType='Annotation'" + (clearing ? "" : " TextAnnotation='new'"));
        NativeArtifactPolicy.Project(a, b, Patch(clearing ? "" : "new")); Assert.True(XNode.DeepEquals(a, b));
    }
    [Theory]
    [InlineData("TextAnnotation='wrong'")]
    [InlineData("xmlns:f='urn:foreign' f:TextAnnotation='new'")]
    [InlineData("")]
    public void WrongOrForeignCarriersCannotImpersonateTextIntent(string attributes) =>
        Assert.Throws<InvalidDataException>(() => NativeArtifactPolicy.Project(Artifact("", "ArtifactType='Annotation' TextAnnotation='old'"),
            Artifact("<TextAnnotation>new</TextAnnotation>", "ArtifactType='Annotation' " + attributes), Patch("new")));
    [Fact] public void ForeignAttributesAndUnknownChildrenRemainOutsideIntent()
    {
        var a = Artifact("<!--preserve--><TextAnnotation custom='keep'>unknown</TextAnnotation>", "ArtifactType='Annotation' TextAnnotation='old' xmlns:f='urn:foreign' f:TextAnnotation='retain'");
        var b = Artifact("<!--preserve--><TextAnnotation custom='keep'>unknown</TextAnnotation>", "ArtifactType='Annotation' TextAnnotation='new' xmlns:f='urn:foreign' f:TextAnnotation='retain'");
        NativeArtifactPolicy.Project(a, b, Patch("new")); Assert.True(XNode.DeepEquals(a, b));
        b.SetAttributeValue(XName.Get("TextAnnotation", "urn:foreign"), "changed"); Assert.False(XNode.DeepEquals(a, b));
    }
    [Fact] public void DetachedOwnersAndImageTypesAreNotTextArtifacts()
    {
        Assert.Throws<InvalidDataException>(() => NativeArtifactPolicy.Project(new XElement(Artifact("")), Artifact("", "ArtifactType='Annotation' TextAnnotation='new'"), Patch("new")));
        Assert.Throws<InvalidDataException>(() => NativeArtifactPolicy.Project(Artifact(""), Artifact("", "BizAgiArtifactType='Image' TextAnnotation='new'"), Patch("new")));
    }
    [Fact] public void GroupDerivedNameDoesNotAuthorizeIdentityOrUnknownEdits()
    {
        var c = new NativeMutation { Operation = "update", ElementId = Id, Name = "new" };
        var a = Artifact($"<Group Id='{Id}' Name='old' extra='retain'/>", "ArtifactType='Group'");
        var b = Artifact($"<Group Id='{Id}' Name='new' extra='retain'/>", "ArtifactType='Group'");
        NativeArtifactPolicy.Project(a, b, c); Assert.True(XNode.DeepEquals(a, b));
        b.Element(Ns + "Group")!.SetAttributeValue("Id", Parent);
        Assert.Throws<InvalidDataException>(() => NativeArtifactPolicy.Project(a, b, c));
    }
    [Fact] public void OnlyTheExactNativeGroupIdentityIsACloneReference()
    {
        var owner = Artifact($"<Group Id='{Id}'/><Unknown><Group Id='{Id}'/></Unknown>", "ArtifactType='Group'");
        var group = owner.Element(Ns + "Group")!;
        Assert.True(NativeArtifactPolicy.IsGroupIdentityReference(group));
        Assert.False(NativeArtifactPolicy.IsGroupIdentityReference(owner.Element(Ns + "Unknown")!.Element(Ns + "Group")!));
        Assert.False(NativeArtifactPolicy.IsGroupIdentityReference(new XElement(group)));
        owner.Add(new XElement(group)); Assert.False(NativeArtifactPolicy.IsGroupIdentityReference(group));
    }
}
