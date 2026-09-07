using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Pure contract/projection tests; no native capability is accredited by these XML fixtures.</summary>
public sealed class NativeSemanticTests
{
    private const string Id = "11111111-1111-4111-8111-111111111111";
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static XElement Owner(string kind, string attributes = "", string content = "") => XDocument.Parse($"<Package xmlns='{Ns}'><WorkflowProcesses><WorkflowProcess Id='process'><{(kind == "Activity" ? "Activities" : "Transitions")}><{kind} Id='{Id}' {attributes}>{content}</{kind}></{(kind == "Activity" ? "Activities" : "Transitions")}></WorkflowProcess></WorkflowProcesses></Package>").Descendants(Ns + kind).Single();

    [Theory] [InlineData("None")] [InlineData("Ready")] [InlineData("Active")] [InlineData("Completing")] [InlineData("Completed")] [InlineData("Aborted")] [InlineData("Aborting")]
    public void ExactNativeActivityStateAllowlist(string state) => NativeEditPlan.Validate([new() { Operation = "update", ElementId = Id, ActivityProperties = new() { State = state } }]);
    [Theory] [InlineData(0)] [InlineData(-1)]
    public void TokenQuantitiesMustBePositive(int count) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([new() { Operation = "update", ElementId = Id, ActivityProperties = new() { CompletionQuantity = count } }]));
    [Theory] [InlineData("delete")] [InlineData("reconnect")]
    public void HiddenSemanticChangesOnOtherOperationsAreRejected(string operation) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([new() { Operation = operation, ElementId = Id, ActivityProperties = new() { State = "Active" } }]));
    [Theory] [InlineData("Unknown")] [InlineData("active")] [InlineData("")]
    public void StateDoesNotFallBackSilentlyToNone(string state) => Assert.Throws<InvalidDataException>(() => NativeSemanticPolicy.Validate(new() { ActivityProperties = new() { State = state } }));
    [Fact] public void EmptyPatchIsRejected() => Assert.Throws<InvalidDataException>(() => NativeSemanticPolicy.Validate(new() { ActivityProperties = new() }));

    [Theory] [InlineData("None", "")] [InlineData("Expression", "x < 3 & y > 2 Ω")] [InlineData("Default", "")]
    public void ConditionsAreExplicitAndComplete(string kind, string text) => NativeEditPlan.Validate([new() { Operation = "update", ElementId = Id, FlowCondition = new() { Kind = kind, Text = text } }]);
    [Theory] [InlineData("None")] [InlineData("Default")]
    public void NonexpressionConditionCannotSilentlyDiscardText(string kind) => Assert.Throws<InvalidDataException>(() => NativeSemanticPolicy.Validate(new() { FlowCondition = new() { Kind = kind, Text = "Keep this" } }));
    [Theory] [InlineData("Unspecified")] [InlineData("Converging")] [InlineData("Diverging")] [InlineData("Mixed")]
    public void GatewayDirectionsAreExplicit(string direction) => NativeEditPlan.Validate([new() { Operation = "update", ElementId = Id, GatewayDirection = direction }]);

    [Fact] public void ScalarProjectionPreservesUnknownProperties()
    {
        var a = Owner("Activity", "StartQuantity='1' CompletionQuantity='1' Unknown='keep'", "<Unknown child='keep'/>");
        var b = Owner("Activity", "StartQuantity='2' CompletionQuantity='3' IsForCompensation='true' Status='Active' Unknown='keep'", "<Unknown child='keep'/>");
        NativeSemanticPolicy.Project(a, b, new() { ActivityProperties = new() { StartQuantity = 2, CompletionQuantity = 3, IsForCompensation = true, State = "Active" } });
        Assert.True(XNode.DeepEquals(a, b)); b.Element(Ns + "Unknown")!.Remove(); Assert.False(XNode.DeepEquals(a, b));
    }
    [Fact] public void WrongDurableScalarIsNotNormalizedAway() => Assert.Throws<InvalidDataException>(() => NativeSemanticPolicy.Project(Owner("Activity"), Owner("Activity"), new() { ActivityProperties = new() { StartQuantity = 2 } }));
    [Fact] public void DefaultScalarOmissionRetainsTheOriginalShape()
    {
        var a = Owner("Activity", "IsForCompensation='true' Status='Active'"); var b = Owner("Activity");
        NativeSemanticPolicy.Project(a, b, new() { ActivityProperties = new() { IsForCompensation = false, State = "None" } }); Assert.True(XNode.DeepEquals(a, b));
    }
    [Fact] public void GatewayProjectionDoesNotRemoveUnknownRouteMetadata()
    {
        var a = Owner("Activity", content: "<Route Unknown='keep'/>"); var b = Owner("Activity", content: "<Route GatewayDirection='Diverging' Unknown='keep'/>");
        NativeSemanticPolicy.Project(a, b, new() { GatewayDirection = "Diverging" }); Assert.True(XNode.DeepEquals(a, b));
    }
    [Theory] [InlineData("<Condition/>", "<Condition Type='CONDITION'><Expression>x &lt; 3</Expression></Condition>", "Expression", "x < 3")]
    [InlineData("<Condition Type='CONDITION'><Expression>old</Expression></Condition>", "<Condition Type='OTHERWISE'/>", "Default", "")]
    public void ConditionProjectionRestoresOnlyKnownTextAndType(string old, string updated, string kind, string value)
    {
        var a = Owner("Transition", content: old); var b = Owner("Transition", content: updated);
        NativeSemanticPolicy.Project(a, b, new() { FlowCondition = new() { Kind = kind, Text = value } }); Assert.True(XNode.DeepEquals(a, b));
    }
    [Theory] [InlineData("language='custom'", "old")] [InlineData("", "<!--keep-->old")] [InlineData("", "<Unknown/>old")]
    public void UnknownExpressionContentCannotBeDiscarded(string attributes, string content) => Assert.Throws<InvalidDataException>(() => NativeSemanticPolicy.Project(
        Owner("Transition", content: $"<Condition Type='CONDITION'><Expression {attributes}>{content}</Expression></Condition>"), Owner("Transition", content: "<Condition Type='OTHERWISE'/>"), new() { FlowCondition = new() { Kind = "Default" } }));
    [Fact] public void DuplicateConditionsAreRejected() => Assert.Throws<InvalidDataException>(() => NativeSemanticPolicy.Project(Owner("Transition", content: "<Condition/><Condition/>"), Owner("Transition", content: "<Condition/>"), new() { FlowCondition = new() }));
    [Fact] public void NewExpressionScaffoldDoesNotBecomeInventedConditionText()
    {
        var a = Owner("Transition", content: "<Condition/>"); var b = Owner("Transition", content: "<Condition Type='CONDITION'/>");
        b.Element(Ns + "Condition")!.Add(new XText("\n  "), new XElement(Ns + "Expression", "x"), new XText("\n"));
        NativeSemanticPolicy.Project(a, b, new() { FlowCondition = new() { Kind = "Expression", Text = "x" } }); Assert.True(XNode.DeepEquals(a, b));
    }
    [Theory] [InlineData("preserve", "\n  ")] [InlineData("default", "important")]
    public void SignificantConditionTextIsNotWaived(string space, string text)
    {
        var a = Owner("Transition", content: "<Condition/>"); var b = Owner("Transition", content: "<Condition Type='CONDITION'/>");
        a.SetAttributeValue(XNamespace.Xml + "space", space); b.SetAttributeValue(XNamespace.Xml + "space", space);
        b.Element(Ns + "Condition")!.Add(new XText(text), new XElement(Ns + "Expression", "x"));
        NativeSemanticPolicy.Project(a, b, new() { FlowCondition = new() { Kind = "Expression", Text = "x" } }); Assert.False(XNode.DeepEquals(a, b));
    }
    [Fact] public void ReadbackRequiresActualProperties() => Assert.Throws<InvalidDataException>(() => NativeSemanticPolicy.Verify(new() { ActivityProperties = new() { StartQuantity = 2 } }, new() { Kind = "StartEvent" }, []));
    [Fact] public void ConditionCdataIsNotSerializerIndentation()
    {
        var a = Owner("Transition", content: "<Condition/>"); var b = Owner("Transition", content: "<Condition Type='CONDITION'/>");
        b.Element(Ns + "Condition")!.Add(new XCData("\n  "), new XElement(Ns + "Expression", "x"));
        NativeSemanticPolicy.Project(a, b, new() { FlowCondition = new() { Kind = "Expression", Text = "x" } }); Assert.False(XNode.DeepEquals(a, b));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void DerivedTransitionQuantityRequiresVerifiedActivityOrReconnection(bool reconnect)
    {
        const string source = "22222222-2222-4222-8222-222222222222";
        var a = Owner("Transition", $"From='{source}' Unknown='keep'"); var b = Owner("Transition", $"From='{source}' Quantity='3' Unknown='keep'");
        NativeMutation[] changes = reconnect ? [new() { Operation = "reconnect", ElementId = Id }] : [new() { Operation = "update", ElementId = source, ActivityProperties = new() { CompletionQuantity = 3 } }];
        NativeSemanticPolicy.ProjectDerivedQuantities(a.Document!, b.Document!, changes, [new() { Id = source, Kind = "UserTask", ActivityProperties = new() { CompletionQuantity = 3 } }]);
        Assert.True(XNode.DeepEquals(a, b));
    }
    [Fact] public void UnrequestedTransitionQuantityAndUnknownFieldsRemainCompared()
    {
        var a = Owner("Transition", "From='source' Unknown='keep'"); var b = Owner("Transition", "From='source' Quantity='3'");
        NativeSemanticPolicy.ProjectDerivedQuantities(a.Document!, b.Document!, [], []); Assert.False(XNode.DeepEquals(a, b));
    }
    [Fact] public void MissingSourceDoesNotTurnIntoADefaultQuantity() => Assert.Throws<InvalidDataException>(() => NativeSemanticPolicy.ProjectDerivedQuantities(
        Owner("Transition", "From='source'").Document!, Owner("Transition", "From='source' Quantity='3'").Document!, [new() { ElementId = Id, Operation = "reconnect" }], []));
}
