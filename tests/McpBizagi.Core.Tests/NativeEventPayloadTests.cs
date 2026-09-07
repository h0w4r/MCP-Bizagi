using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Policy fixtures do not accredit the native event engine or simulator.</summary>
public sealed class NativeEventPayloadTests
{
    private const string Id = "11111111-1111-4111-8111-111111111111";
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static NativeMutation Change(params NativeEventPayloadPatch[] p) => new() { Operation = "update", ElementId = Id, EventPayloads = p };
    private static XElement Owner(string body) => XDocument.Parse($"<Package xmlns='{Ns}'><WorkflowProcesses><WorkflowProcess Id='p'><Activities><Activity Id='a'><Event>{body}</Event></Activity></Activities></WorkflowProcess></WorkflowProcesses></Package>").Descendants(Ns + "Activity").Single();
    [Theory] [InlineData("Message")] [InlineData("Conditional")] [InlineData("Link")] [InlineData("Signal")]
    public void NamedPayloadAllowsExplicitClear(string kind) => NativeEditPlan.Validate([Change(new NativeEventPayloadPatch() { Kind = kind, Name = "" })]);
    [Theory] [InlineData("Error")] [InlineData("Timer")] [InlineData("Escalation")] [InlineData("Compensation")] [InlineData("Unknown")]
    public void WrongKindFieldIsNotIgnored(string kind) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new NativeEventPayloadPatch() { Kind = kind, Name = "name" })]));
    [Fact] public void EmptyPatchAndDuplicateSelectionFail()
    {
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change()]));
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new NativeEventPayloadPatch() { Kind = "Message" })]));
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new NativeEventPayloadPatch() { Kind = "Message", Name = "a" }, new() { Kind = "Message", Name = "b" })]));
    }
    [Theory] [InlineData("delete")] [InlineData("reconnect")]
    public void PayloadRequiresPropertyOperation(string operation) { var c = Change(new NativeEventPayloadPatch() { Kind = "Signal", Name = "a" }); c.Operation = operation; Assert.Throws<InvalidDataException>(() => NativeEventPayloadPolicy.Validate(c)); }
    [Theory] [InlineData("None", "")] [InlineData("Cycle", "R3/PT5M")] [InlineData("Cycle", "R/2026-09-07T10:00:00/PT2H")] [InlineData("Date", "2026-09-07T10:00:00")]
    public void CanonicalTimerIntent(string kind, string text) => NativeEditPlan.Validate([Change(new NativeEventPayloadPatch() { Kind = "Timer", Timer = new() { Kind = kind, Text = text } })]);
    [Theory] [InlineData("None", "R1/PT1M")] [InlineData("Duration", "PT1M")] [InlineData("Cycle", "2 MI")] [InlineData("Date", "2026-09-07T10:00:00Z")] [InlineData("Date", "2026-02-30T10:00:00")]
    public void UnsupportedOrAmbiguousTimerIntentFails(string kind, string text) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new NativeEventPayloadPatch() { Kind = "Timer", Timer = new() { Kind = kind, Text = text } })]));
    [Fact] public void OversizedTextFails() => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new NativeEventPayloadPatch() { Kind = "Conditional", Condition = new string('x', 1024 * 1024 + 1) })]));
    [Fact] public void CompensationHasExplicitClearAndIdentityRules()
    {
        NativeEditPlan.Validate([Change(new NativeEventPayloadPatch() { Kind = "Compensation", Compensation = new() { ActivityId = "" } })]);
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new NativeEventPayloadPatch() { Kind = "Compensation", Compensation = new() })]));
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(new NativeEventPayloadPatch() { Kind = "Compensation", Compensation = new() { ActivityId = "wrong" } })]));
    }
    [Fact] public void FreshReadbackRequiresActualPayload()
    {
        var c = Change(new NativeEventPayloadPatch() { Kind = "Error", ErrorCode = "E1" });
        Assert.Throws<InvalidDataException>(() => NativeEventPayloadPolicy.Verify(c, new() { Event = new() { DefinitionKinds = ["Error"] } }, []));
        NativeEventPayloadPolicy.Verify(c, new() { Event = new() { Definitions = [new() { Kind = "Error", ErrorCode = "E1" }] } }, []);
    }
    [Fact] public void CompensationClearRequiresEveryAliasCleared() => Assert.Throws<InvalidDataException>(() => NativeEventPayloadPolicy.Verify(Change(new NativeEventPayloadPatch() { Kind = "Compensation", Compensation = new() { ActivityId = "" } }),
        new() { Event = new() { Definitions = [new() { Kind = "Compensation", Compensation = new() { BpmnName = Id } }] } }, []));
    [Theory] [InlineData("StartEvent")] [InlineData("IntermediateEvent")] [InlineData("EndEvent")]
    public void MessageNameProjectionPreservesIdentityAndUnknownContent(string mode)
    {
        var a = Owner($"<{mode}><TriggerResultMessage><Message Id='id' Name='old'/><Unknown keep='yes'/></TriggerResultMessage></{mode}>");
        var b = Owner($"<{mode}><TriggerResultMessage><Message Id='id' Name='new'/><Unknown keep='yes'/></TriggerResultMessage></{mode}>");
        NativeEventPayloadPolicy.Project(a, b, [new() { Kind = "Message", Name = "new" }]); Assert.True(XNode.DeepEquals(a, b));
        b.Descendants(Ns + "Message").Single().SetAttributeValue("Id", "other"); Assert.False(XNode.DeepEquals(a, b));
    }
    [Theory] [InlineData("TriggerMultiple")] [InlineData("ResultMultiple")] [InlineData("TriggerIntermediateMultiple")]
    public void MultipleProjectionPreservesUnrequestedDefinition(string wrapper)
    {
        string mode = wrapper == "TriggerMultiple" ? "StartEvent" : wrapper == "ResultMultiple" ? "EndEvent" : "IntermediateEvent";
        var a = Owner($"<{mode}><{wrapper}><ResultError ErrorCode='old'/><TriggerResultSignal Name='keep'/></{wrapper}></{mode}>");
        var b = Owner($"<{mode}><{wrapper}><ResultError ErrorCode='new'/><TriggerResultSignal Name='keep'/></{wrapper}></{mode}>");
        NativeEventPayloadPolicy.Project(a, b, [new() { Kind = "Error", ErrorCode = "new" }]); Assert.True(XNode.DeepEquals(a, b));
    }
    [Theory] [InlineData("StartEvent", "ResultMultiple")] [InlineData("EndEvent", "TriggerMultiple")] [InlineData("StartEvent", "TriggerIntermediateMultiple")]
    public void KnownWrapperInWrongModeRemainsUnknown(string mode, string wrapper)
    {
        var a = Owner($"<{mode}><{wrapper}><ResultError ErrorCode='old'/></{wrapper}></{mode}>");
        var b = Owner($"<{mode}><{wrapper}><ResultError ErrorCode='new'/></{wrapper}></{mode}>");
        Assert.Throws<InvalidDataException>(() => NativeEventPayloadPolicy.Project(a, b, [new() { Kind = "Error", ErrorCode = "new" }]));
    }
    [Fact] public void TimerSwitchProjectsOnlyTwoKnownAttributes()
    {
        var a = Owner("<StartEvent><TriggerTimer TimeCycle='R1/PT2M'><TimeCycle>unknown expression variant</TimeCycle></TriggerTimer></StartEvent>");
        var b = Owner("<StartEvent><TriggerTimer TimeDate='2026-09-07T10:00:00'><TimeCycle>unknown expression variant</TimeCycle></TriggerTimer></StartEvent>");
        NativeEventPayloadPolicy.Project(a, b, [new() { Kind = "Timer", Timer = new() { Kind = "Date", Text = "2026-09-07T10:00:00" } }]); Assert.True(XNode.DeepEquals(a, b));
    }
    [Theory] [InlineData("")] [InlineData("<Expression>old</Expression>")]
    public void ConditionInsertionAndClearRetainOtherPayload(string old)
    {
        var a = Owner($"<StartEvent><TriggerConditional>{old}<Unknown/></TriggerConditional></StartEvent>");
        var b = Owner("<StartEvent><TriggerConditional><Expression>new Ω</Expression><Unknown/></TriggerConditional></StartEvent>");
        NativeEventPayloadPolicy.Project(a, b, [new() { Kind = "Conditional", Condition = "new Ω" }]); Assert.True(XNode.DeepEquals(a, b));
    }
    [Fact] public void UnknownExpressionAttributesCannotDisappear()
    {
        var a = Owner("<StartEvent><TriggerConditional><Expression custom='retain'>old</Expression></TriggerConditional></StartEvent>"); var b = Owner("<StartEvent><TriggerConditional/></StartEvent>");
        Assert.Throws<InvalidDataException>(() => NativeEventPayloadPolicy.Project(a, b, [new() { Kind = "Conditional", Condition = "" }]));
    }
    [Theory] [InlineData("Changed 日本語 Ω", "Changed_x0020_日本語_x0020_Ω")]
    [InlineData("_x0020_ Ω", "_x005F_x0020__x0020_Ω")]
    public void LinkNamesRequireExactNativeNmTokenEncoding(string requested, string encoded)
    {
        var a = Owner("<IntermediateEvent><TriggerResultLink Name='old' custom='keep'/></IntermediateEvent>");
        var b = Owner($"<IntermediateEvent><TriggerResultLink Name='{encoded}' custom='keep'/></IntermediateEvent>");
        NativeEventPayloadPolicy.Project(a, b, [new() { Kind = "Link", Name = requested }]); Assert.True(XNode.DeepEquals(a, b));
        b.Descendants(Ns + "TriggerResultLink").Single().SetAttributeValue("Name", requested);
        Assert.Throws<InvalidDataException>(() => NativeEventPayloadPolicy.Project(a, b, [new() { Kind = "Link", Name = requested }]));
    }
    [Fact] public void DuplicateDefinitionAndExtensionLookalikeFail()
    {
        var a = Owner("<StartEvent><ResultError/><ResultError/></StartEvent>"); var b = Owner("<StartEvent><ResultError ErrorCode='E'/></StartEvent>");
        Assert.Throws<InvalidDataException>(() => NativeEventPayloadPolicy.Project(a, b, [new() { Kind = "Error", ErrorCode = "E" }]));
        Assert.Throws<InvalidDataException>(() => NativeEventPayloadPolicy.Project(new XElement(b), b, [new() { Kind = "Error", ErrorCode = "E" }]));
    }
}
