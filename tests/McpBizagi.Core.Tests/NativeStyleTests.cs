using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Deterministic policy checks; installed-engine accreditation lives in the independent SDK client.</summary>
public sealed class NativeStyleTests
{
    private const string Id = "11111111-1111-4111-8111-111111111111";
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static NativeMutation Change(NativeStylePatch p) => new() { Operation = "update", ElementId = Id, Style = p };
    private static XElement Owner(string data, string attributes = "") => XDocument.Parse($"<Package xmlns='{Ns}'><WorkflowProcesses><WorkflowProcess Id='p'><Activities><Activity Id='{Id}'><NodeGraphicsInfos><NodeGraphicsInfo {attributes}>{data}</NodeGraphicsInfo></NodeGraphicsInfos></Activity></Activities></WorkflowProcess></WorkflowProcesses></Package>").Descendants(Ns + "Activity").Single();

    [Fact] public void EmptyAndUnusedStyleFail()
    {
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new())]));
        foreach (string op in new[] { "delete", "reconnect" })
        { var c = Change(new() { Bold = false }); c.Operation = op; Assert.Throws<InvalidDataException>(() => NativeStylePolicy.Validate(c)); }
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(513)]
    public void FontSizeMustBeInNativeRange(int size) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new() { FontSize = size })]));
    [Theory] [InlineData("center")] [InlineData("Justified")] [InlineData("")]
    public void AlignmentMustBeExact(string value) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new() { Alignment = value })]));
    [Theory] [InlineData("horizontal")] [InlineData("90")] [InlineData("")]
    public void DirectionMustBeExact(string value) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new() { TextDirection = value })]));
    [Theory] [InlineData("")] [InlineData("  ")] [InlineData("Arial ")]
    public void FontNameCannotBeBlankOrTrimmedSilently(string value) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new() { FontName = value })]));
    [Theory] [InlineData(-1, 10)] [InlineData(1.5, 10)] [InlineData(double.NaN, 10)] [InlineData(1000001, 10)] [InlineData(1, 0)]
    public void LabelBoundsCannotBeRoundedOrDropped(double x, double width) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new() { LabelBounds = new() { X = x, Y = 1, Width = width, Height = 10 } })]));
    [Fact] public void ExplicitFalseAndZeroLabelResetAreIntent()
    {
        NativeEditPlan.Validate([Change(new() { Bold = false, Italic = false, LabelBounds = new() { X = 0, Y = 0, Width = 0, Height = 0 }, BorderVisible = false })]);
        NativeStylePolicy.Verify(new() { Bold = false, LabelBounds = new() { X = 0, Y = 0, Width = 0, Height = 0 } }, new() { Style = new() });
    }
    [Fact] public void EmptyOrPartialRectangleCannotSilentlyClearLabelPlacement()
    {
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new() { LabelBounds = new() })]));
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new() { LabelBounds = new() { X = 0, Y = 0, Width = 0 } })]));
        Assert.Throws<InvalidDataException>(() => NativeStylePolicy.Verify(new() { LabelBounds = new() }, new() { Style = new() }));
    }
    [Fact] public void DuplicateColorsAndUnsupportedConnectorIntentFail()
    {
        var c = Change(new() { BackgroundArgb = -1 }); c.Geometry = new() { Width = 100, Height = 60, BackgroundArgb = -1 };
        Assert.Throws<InvalidDataException>(() => NativeStylePolicy.Validate(c));
        c.Geometry = null; c.Operation = "create"; c.ElementType = "SequenceFlow";
        Assert.Throws<InvalidDataException>(() => NativeStylePolicy.Validate(c));
    }
    [Theory] [InlineData("Participant")] [InlineData("DataStore")]
    public void UnpersistedPoolLabelOrSharedDefinitionIntentFails(string kind)
    {
        var c = Change(new() { TextDirection = "Horizontal" }); c.Operation = "create"; c.ElementType = kind;
        Assert.Throws<InvalidDataException>(() => NativeStylePolicy.Validate(c));
    }
    [Fact] public void ExactRequestedLeavesAreProjectedButUnknownFieldsRemain()
    {
        var left = Owner("<Formatting><Bold>false</Bold><FontName>Arial</FontName><Unknown>original</Unknown></Formatting>", "FillColor='-1'");
        var right = Owner("<Formatting><Bold>true</Bold><FontName>Arial</FontName><Unknown>changed</Unknown></Formatting>", "FillColor='-1'");
        NativeStylePolicy.Project(left, right, new() { Bold = true });
        Assert.Equal("false", right.Descendants(Ns + "Bold").Single().Value);
        Assert.False(XNode.DeepEquals(left, right));
        Assert.Throws<InvalidDataException>(() => NativeStylePolicy.Project(left, right, new() { Bold = true }));
    }
    [Fact] public void NilDirectionAndClearedLabelBoundsRestoreExactKnownFields()
    {
        var left = Owner("<TextDirection xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:nil='true'/>", "TextX='10' TextY='20' TextWidth='30' TextHeight='40'");
        var right = Owner("<TextDirection xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'>Horizontal</TextDirection>");
        NativeStylePolicy.Project(left, right, new() { TextDirection = "Horizontal", LabelBounds = new() { X = 0, Y = 0, Width = 0, Height = 0 } });
        Assert.True(XNode.DeepEquals(left, right));
    }
    [Fact] public void MalformedNestedOrAmbiguousFormattingCannotBeWaived()
    {
        var left = Owner("<Formatting><Bold><Unknown/></Bold></Formatting>");
        var right = Owner("<Formatting><Bold>true</Bold></Formatting>");
        Assert.Throws<InvalidDataException>(() => NativeStylePolicy.Project(left, right, new() { Bold = true }));
        left = Owner("<Formatting/><Formatting/>");
        Assert.Throws<InvalidDataException>(() => NativeStylePolicy.Project(left, right, new() { Bold = true }));
    }
    [Fact] public void ReadbackRejectsMissingStyleOrDiscardedIntent()
    {
        Assert.Throws<InvalidDataException>(() => NativeStylePolicy.Verify(new() { FontSize = 12 }, new()));
        Assert.Throws<InvalidDataException>(() => NativeStylePolicy.Verify(new() { FontSize = 12 }, new() { Style = new() { FontSize = 8 } }));
        Assert.False(NativeStylePolicy.Same(new() { Bold = true }, new()));
        NativeStylePolicy.Verify(new() { FontSize = 12, BorderVisible = false }, new() { Style = new() { FontSize = 12 } });
    }
}
