using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Policy fixtures isolate validation; installed-engine acceptance is a separate gate.</summary>
public sealed class NativeEventTests
{
    private const string Id = "11111111-1111-4111-8111-111111111111", Parent = "22222222-2222-4222-8222-222222222222", Target = "33333333-3333-4333-8333-333333333333";
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static NativeMutation Create(string type, string? mode = null) => new() { Operation = "create", ElementId = Id, ParentId = Parent, ElementType = type, EventMode = mode };
    private static XElement Owner(string xml) => XDocument.Parse($"<Package xmlns='{Ns}'><WorkflowProcesses><WorkflowProcess Id='p'><Activities><Activity Id='a'><Event>{xml}</Event><Unknown keep='true'/></Activity></Activities></WorkflowProcess></WorkflowProcesses></Package>").Descendants(Ns + "Activity").Single();

    [Theory] [InlineData("NoneIntermediate", "Throw")] [InlineData("MessageIntermediate", "Catch")] [InlineData("TimerIntermediate", "Catch")]
    public void LegacyDefaultsRemainDefined(string type, string mode) { var c = Create(type); NativeEditPlan.Validate([c]); Assert.Equal(mode, NativeEventPolicy.Mode(c)); }
    [Theory] [InlineData("MessageIntermediate", "Throw")] [InlineData("SignalIntermediate", "Catch")] [InlineData("LinkIntermediate", "Throw")] [InlineData("CompensationIntermediate", "Throw")] [InlineData("ParallelMultipleIntermediate", "Catch")]
    public void ExplicitLegalModes(string type, string mode) => NativeEditPlan.Validate([Create(type, mode)]);
    [Theory] [InlineData("TimerIntermediate", "Throw")] [InlineData("NoneIntermediate", "Catch")] [InlineData("LinkIntermediate", "Boundary")] [InlineData("ErrorIntermediate", "Catch")] [InlineData("ParallelMultipleIntermediate", "Throw")] [InlineData("SignalIntermediate", null)]
    public void IllegalOrMissingModesFail(string type, string? mode) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Create(type, mode)]));
    [Theory] [InlineData("create")] [InlineData("update")] [InlineData("delete")]
    public void ModeCannotBeIgnoredOnNonIntermediate(string operation) { var c = Create("UserTask", "Catch"); c.Operation = operation; if (operation != "create") { c.ParentId = ""; c.ElementType = ""; } Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c])); }
    [Fact] public void BoundaryRequiresAttachment() => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Create("TimerIntermediate", "Boundary")]));
    [Theory] [InlineData("MessageIntermediate")] [InlineData("TimerIntermediate")] [InlineData("SignalIntermediate")] [InlineData("MultipleIntermediate")]
    public void NoninterruptingBoundaryIntent(string type) { var c = Create(type, "Boundary"); c.EventProperties = new() { AttachedToActivityId = Target, IsInterrupting = false }; NativeEditPlan.Validate([c]); }
    [Theory] [InlineData("ErrorIntermediate")] [InlineData("CompensationIntermediate")]
    public void NoninterruptingExceptionKindsAreRejected(string type) { var c = Create(type, "Boundary"); c.EventProperties = new() { AttachedToActivityId = Target, IsInterrupting = false }; Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c])); }
    [Theory] [InlineData("")] [InlineData("00000000-0000-0000-0000-000000000000")] [InlineData("not-an-id")]
    public void BoundaryTargetCannotBeEmptyOrMalformed(string id) { var c = Create("TimerIntermediate", "Boundary"); c.EventProperties = new() { AttachedToActivityId = id }; Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c])); }
    [Fact] public void PropertyOnlyUpdateIsMeaningful() => NativeEditPlan.Validate([new() { Operation = "update", ElementId = Id, EventProperties = new() { IsInterrupting = false } }]);
    [Fact] public void WrongReadbackModeFails() => Assert.Throws<InvalidDataException>(() => NativeEventPolicy.Verify(Create("MessageIntermediate", "Throw"), new() { Event = new() { Mode = "Catch" } }, []));
    [Fact] public void WrongContainerReadbackFails()
    {
        var c = Create("TimerIntermediate", "Boundary"); c.EventProperties = new() { AttachedToActivityId = Target };
        Assert.Throws<InvalidDataException>(() => NativeEventPolicy.Verify(c, new() { ParentId = Parent, Event = new() { Mode = "Boundary", AttachedToActivityId = Target } }, [new() { Id = Target, ParentId = "other", ActivityProperties = new() }]));
    }
    [Fact] public void ExactAttributePatchPreservesUnknownContent()
    {
        var a = Owner($"<IntermediateEvent Trigger='Timer' IsAttached='true' Target='{Target}' Interrupting='true'><TriggerTimer custom='retained'/><!--retained--></IntermediateEvent>");
        // Clone the complete document, preserving native ancestry without introducing
        // a new namespace declaration by serializing a detached descendant.
        var copy = new XDocument(a.Document!).Descendants(Ns + "Activity").Single();
        copy.Element(Ns + "Event")!.Element(Ns + "IntermediateEvent")!.SetAttributeValue("Interrupting", "false");
        NativeEventPolicy.Project(a, copy, new() { IsInterrupting = false }); Assert.True(XNode.DeepEquals(a, copy));
        copy.Element(Ns + "Unknown")!.Remove(); Assert.False(XNode.DeepEquals(a, copy));
    }
    [Fact] public void AttachmentProjectionDoesNotHideOtherAttributes()
    {
        var a = Owner($"<IntermediateEvent IsAttached='true' Target='{Target}' Marker='old'/>");
        var b = Owner($"<IntermediateEvent IsAttached='true' Target='{Id}' Marker='changed'/>");
        NativeEventPolicy.Project(a, b, new() { AttachedToActivityId = Id }); Assert.False(XNode.DeepEquals(a, b));
    }
    [Fact] public void WrongDurableValueFails() => Assert.Throws<InvalidDataException>(() => NativeEventPolicy.Project(Owner("<StartEvent Interrupting='true'/>"), Owner("<StartEvent Interrupting='true'/>"), new() { IsInterrupting = false }));
    [Fact] public void DuplicateEventWrapperFails() => Assert.Throws<InvalidDataException>(() => NativeEventPolicy.Project(Owner("<StartEvent/><StartEvent/>"), Owner("<StartEvent/>"), new() { IsInterrupting = true }));
    [Theory] [InlineData("EventBasedGatewayExclusive")] [InlineData("EventBasedGatewayParallel")]
    public void InstantiatingGatewayAcceptsDirection(string type) { var c = Create(type); c.GatewayDirection = "Diverging"; NativeEditPlan.Validate([c]); }
}
