using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Intent and fidelity fixtures are separate from installed-engine MCP acceptance.</summary>
public sealed class NativeSubProcessTests
{
    private const string Id = "11111111-1111-4111-8111-111111111111", Parent = "22222222-2222-4222-8222-222222222222";
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static NativeMutation Create(string? kind = null) => new() { Operation = "create", ElementId = Id, ParentId = Parent, ElementType = "SubProcess", SubProcessKind = kind };
    private static (XElement Activity, XElement Set) Owners(string attributes = "", string content = "")
    {
        // Retain real XPDL ancestry: extension lookalikes must never qualify for projection.
        var document = XDocument.Parse($"<Package xmlns='{Ns}'><WorkflowProcesses><WorkflowProcess Id='p'><ActivitySets><ActivitySet Id='s' {attributes}>{content}</ActivitySet></ActivitySets><Activities><Activity Id='s'><BlockActivity ActivitySetId='s'/><Unknown keep='true'/></Activity></Activities></WorkflowProcess></WorkflowProcesses></Package>", LoadOptions.PreserveWhitespace);
        return (document.Descendants(Ns + "Activity").Single(), document.Descendants(Ns + "ActivitySet").Single());
    }
    [Theory] [InlineData(null)] [InlineData("SubProcess")] [InlineData("Transaction")] [InlineData("AdHoc")]
    public void ExactKindsAreAccepted(string? kind) => NativeEditPlan.Validate([Create(kind)]);
    [Theory] [InlineData("")] [InlineData("transaction")] [InlineData("CallActivity")]
    public void UnknownKindsAreRejected(string kind) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Create(kind)]));
    [Theory] [InlineData("update")] [InlineData("delete")] [InlineData("reconnect")]
    public void KindCannotBeAnImplicitConversion(string operation) { var c = Create("Transaction"); c.Operation = operation; Assert.Throws<InvalidDataException>(() => NativeSubProcessPolicy.Validate(c)); }
    [Fact] public void TaskCannotCarrySubprocessOptions() { var c = Create("AdHoc"); c.ElementType = "UserTask"; Assert.Throws<InvalidDataException>(() => NativeSubProcessPolicy.Validate(c)); }
    [Fact] public void EmptyPatchIsRejected() { var c = Create(); c.SubProcessProperties = new(); Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c])); }
    [Fact] public void PropertyOnlyUpdateIsMeaningful() => NativeEditPlan.Validate([new() { Operation = "update", ElementId = Id, SubProcessProperties = new() { TriggeredByEvent = false } }]);
    [Theory] [InlineData("Transaction")] [InlineData("AdHoc")]
    public void MixedSubprocessModesAreRejected(string kind) { var c = Create(kind); c.SubProcessProperties = new() { TriggeredByEvent = true }; Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c])); }
    [Theory] [InlineData("Parallel")] [InlineData("Sequential")]
    public void AdHocOptionsAreExplicit(string ordering) { var c = Create("AdHoc"); c.SubProcessProperties = new() { AdHocOrdering = ordering, AdHocCompletionCondition = "complete > 2 & 日本語 Ω" }; NativeEditPlan.Validate([c]); }
    [Theory] [InlineData("")] [InlineData("parallel")]
    public void UnknownOrderingIsRejected(string ordering) { var c = Create("AdHoc"); c.SubProcessProperties = new() { AdHocOrdering = ordering }; Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c])); }
    [Fact] public void AdHocFieldsCannotBeIgnoredOnOtherKinds() { var c = Create(); c.SubProcessProperties = new() { AdHocCompletionCondition = "" }; Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c])); }
    [Fact] public void OversizedConditionIsRejected() { var c = Create("AdHoc"); c.SubProcessProperties = new() { AdHocCompletionCondition = new string('x', 1024 * 1024 + 1) }; Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c])); }
    [Fact] public void FreshReadbackRequiresActualKind() => Assert.Throws<InvalidDataException>(() => NativeSubProcessPolicy.Verify(Create("Transaction"), new() { SubProcess = new() { Kind = "SubProcess" } }));
    [Fact] public void FreshReadbackCannotOmitMetadata() => Assert.Throws<InvalidDataException>(() => NativeSubProcessPolicy.Verify(Create(), new()));
    [Fact] public void FreshReadbackChecksEveryRequestedField()
    {
        var c = Create("AdHoc"); c.SubProcessProperties = new() { TriggeredByEvent = false, AdHocOrdering = "Sequential", AdHocCompletionCondition = "" };
        var e = new NativeElement { SubProcess = new() { Kind = "AdHoc", AdHocOrdering = "Sequential", AdHocCompletionCondition = "" } };
        NativeSubProcessPolicy.Verify(c, e); e.SubProcess.AdHocCompletionCondition = "lost clear";
        Assert.Throws<InvalidDataException>(() => NativeSubProcessPolicy.Verify(c, e));
    }
    [Fact] public void ExactProjectionRetainsUnknownContent()
    {
        var a = Owners("AdHoc='true' AdHocOrdering='Sequential' AdHocCompletionCondition='old'", " <Unknown xmlns='urn:extension'>text<!--keep--></Unknown> ");
        var b = Owners("AdHoc='true' AdHocOrdering='Parallel' AdHocCompletionCondition='日本語 Ω'", " <Unknown xmlns='urn:extension'>text<!--keep--></Unknown> ");
        NativeSubProcessPolicy.Project(a.Activity, b.Activity, a.Set, b.Set, new() { AdHocOrdering = "Parallel", AdHocCompletionCondition = "日本語 Ω" });
        Assert.True(XNode.DeepEquals(a.Set, b.Set)); Assert.True(XNode.DeepEquals(a.Activity, b.Activity));
        b.Set.Element(XName.Get("Unknown", "urn:extension"))!.SetAttributeValue("other", "changed");
        Assert.False(XNode.DeepEquals(a.Set, b.Set));
    }
    [Fact] public void TriggerDefaultAndConditionClearCanBeOmittedBySerializer()
    {
        var a = Owners("TriggeredByEvent='true'"); var b = Owners();
        NativeSubProcessPolicy.Project(a.Activity, b.Activity, a.Set, b.Set, new() { TriggeredByEvent = false }); Assert.True(XNode.DeepEquals(a.Set, b.Set));
        a = Owners("AdHoc='true' AdHocCompletionCondition='old'"); b = Owners("AdHoc='true'");
        NativeSubProcessPolicy.Project(a.Activity, b.Activity, a.Set, b.Set, new() { AdHocCompletionCondition = "" }); Assert.True(XNode.DeepEquals(a.Set, b.Set));
        a = Owners("AdHoc='true' AdHocOrdering='Sequential'"); b = Owners("AdHoc='true'");
        NativeSubProcessPolicy.Project(a.Activity, b.Activity, a.Set, b.Set, new() { AdHocOrdering = "Parallel" }); Assert.True(XNode.DeepEquals(a.Set, b.Set));
    }
    [Fact] public void RequestedProjectionDoesNotHideOtherNativeChanges()
    {
        var a = Owners("AdHoc='true' AdHocOrdering='Sequential' TriggeredByEvent='false'"); var b = Owners("AdHoc='true' TriggeredByEvent='true'");
        NativeSubProcessPolicy.Project(a.Activity, b.Activity, a.Set, b.Set, new() { AdHocOrdering = "Parallel" }); Assert.False(XNode.DeepEquals(a.Set, b.Set));
    }
    [Fact] public void WrongDurableValueFails() { var a = Owners(); var b = Owners(); Assert.Throws<InvalidDataException>(() => NativeSubProcessPolicy.Project(a.Activity, b.Activity, a.Set, b.Set, new() { TriggeredByEvent = true })); }
    [Fact] public void WrongCompanionFails() { var a = Owners(); var b = Owners(); b.Activity.Element(Ns + "BlockActivity")!.SetAttributeValue("ActivitySetId", "other"); Assert.Throws<InvalidDataException>(() => NativeSubProcessPolicy.Project(a.Activity, b.Activity, a.Set, b.Set, new() { TriggeredByEvent = false })); }
    [Fact] public void DetachedLookalikeFails() { var a = Owners(); var b = Owners(); Assert.Throws<InvalidDataException>(() => NativeSubProcessPolicy.Project(a.Activity, b.Activity, a.Set, new XElement(b.Set), new() { TriggeredByEvent = false })); }
    [Fact] public void NonAdHocCompanionFails() { var a = Owners(); var b = Owners(); Assert.Throws<InvalidDataException>(() => NativeSubProcessPolicy.Project(a.Activity, b.Activity, a.Set, b.Set, new() { AdHocOrdering = "Parallel" })); }
    [Fact] public void DuplicateBlockFails() { var a = Owners(); var b = Owners(); b.Activity.Add(new XElement(b.Activity.Element(Ns + "BlockActivity")!)); Assert.Throws<InvalidDataException>(() => NativeSubProcessPolicy.Project(a.Activity, b.Activity, a.Set, b.Set, new() { TriggeredByEvent = false })); }
    [Fact] public void CompanionCannotComeFromAnotherModel() { var a = Owners(); var b = Owners(); var other = Owners(); Assert.Throws<InvalidDataException>(() => NativeSubProcessPolicy.Project(a.Activity, b.Activity, a.Set, other.Set, new() { TriggeredByEvent = false })); }
    [Fact] public void ActivityIdentityMustMatchNativeCompanion() { var a = Owners(); var b = Owners(); a.Activity.SetAttributeValue("Id", "other"); Assert.Throws<InvalidDataException>(() => NativeSubProcessPolicy.Project(a.Activity, b.Activity, a.Set, b.Set, new() { TriggeredByEvent = false })); }
}
