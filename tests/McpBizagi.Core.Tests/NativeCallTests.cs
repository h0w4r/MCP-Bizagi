using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Policy-only fixtures; installed native calls are accredited separately through MCP.</summary>
public sealed class NativeCallTests
{
    private const string Call = "11111111-1111-4111-8111-111111111111", Process = "22222222-2222-4222-8222-222222222222", Other = "33333333-3333-4333-8333-333333333333";
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static XElement Activity(string? target, string unknown = "") => XDocument.Parse($"<Package xmlns='{Ns}'><WorkflowProcesses><WorkflowProcess Id='{Process}'><Activities><Activity Id='{Call}'><Implementation><SubFlow{(target == null ? "" : " Id='" + target + "'")}>{unknown}</SubFlow></Implementation></Activity></Activities></WorkflowProcess></WorkflowProcesses></Package>").Descendants(Ns + "Activity").Single();

    [Theory] [InlineData(Process)] [InlineData("")]
    public void ExplicitCallTargetsAreValidForCreationAndPropertyOnlyUpdates(string target)
    {
        NativeEditPlan.Validate([new() { Operation = "create", ElementId = Call, ParentId = Process, ElementType = "CallActivity", CallTarget = new() { ProcessId = target } }]);
        NativeEditPlan.Validate([new() { Operation = "update", ElementId = Call, CallTarget = new() { ProcessId = target } }]);
    }

    [Theory] [InlineData("delete")] [InlineData("reconnect")]
    public void DestructiveOrConnectorOperationsCannotCarryHiddenCallChanges(string operation) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([
        new() { Operation = operation, ElementId = Call, SourceId = operation == "reconnect" ? Process : "", TargetId = operation == "reconnect" ? Other : "",
            Points = operation == "reconnect" ? [new() { X = 0, Y = 0 }, new() { X = 1, Y = 1 }] : [], CallTarget = new() { ProcessId = Process } }]));

    [Fact] public void OrdinaryTaskCreationCannotAcceptACallTarget() => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([
        new() { Operation = "create", ElementId = Call, ParentId = Process, ElementType = "UserTask", CallTarget = new() { ProcessId = Process } }]));

    [Theory] [InlineData("", "Id_", "")] [InlineData(Process, Process, "urn:external-processes")]
    public void ClearedAndLocalTargetsCannotHideStaleQualifiedReferences(string target, string name, string ns)
    {
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([new() { Operation = "update", ElementId = Call, CallTarget = new() { ProcessId = target } }],
            [new() { Id = Call, Kind = "CallActivity", CallReference = new() { CatalogProcessId = target, BpmnName = name, BpmnNamespace = ns } }, new() { Id = Process, Kind = "Process" }]));
    }

    [Theory] [InlineData("not-guid")] [InlineData("00000000-0000-0000-0000-000000000000")]
    public void CallTargetRequiresACanonicalNonemptyGuidUnlessExplicitlyCleared(string target) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([
        new() { Operation = "update", ElementId = Call, CallTarget = new() { ProcessId = target } }]));

    [Theory] [InlineData(Process)] [InlineData("")]
    public void ReadbackRequiresExactNativeCatalogTargetAndNoExternalLink(string target)
    {
        NativeMutation[] changes = [new() { Operation = "update", ElementId = Call, CallTarget = new() { ProcessId = target } }];
        NativeElement[] elements = [new() { Id = Call, Kind = "CallActivity", CallReference = new() { CatalogProcessId = target } }, new() { Id = Process, Kind = "Process" }];
        NativeEditPlan.Verify(changes, elements);
        elements[0].CallReference!.External = new() { ProcessId = target };
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify(changes, elements));
        elements[0].CallReference!.External = null; elements[0].CallReference!.BpmnName = Other;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify(changes, elements));
    }

    [Theory] [InlineData(Process, Other)] [InlineData(Process, "")] [InlineData(null, Other)]
    public void ProjectsOnlyTheVerifiedNativeSubflowIdentity(string? oldId, string newId)
    {
        var before = Activity(oldId, "<Keep important='yes'/>"); var after = Activity(newId, "<Keep important='yes'/>");
        NativeCallFidelity.ProjectTarget(before, after, new() { ProcessId = newId });
        Assert.True(XNode.DeepEquals(before, after));
    }

    [Fact] public void UnknownCallMetadataCannotDisappearBehindProjection()
    {
        var before = Activity(Process, "<Keep important='yes'/>"); var after = Activity(Other);
        NativeCallFidelity.ProjectTarget(before, after, new() { ProcessId = Other });
        Assert.False(XNode.DeepEquals(before, after));
    }

    [Fact] public void WrongDurableReferenceIsRejectedInsteadOfProjected()
    { Assert.Throws<InvalidDataException>(() => NativeCallFidelity.ProjectTarget(Activity(Process), Activity(Process), new() { ProcessId = Other })); }

    [Fact] public void ExtensionLookalikesAndDuplicateImplementationsAreNotCalls()
    {
        var fake = XElement.Parse($"<Unknown xmlns='{Ns}'><Activity Id='{Call}'><Implementation><SubFlow Id='{Process}'/></Implementation></Activity></Unknown>");
        Assert.False(NativeCallFidelity.IsCallReference(fake.Descendants(Ns + "SubFlow").Single()));
        var activity = Activity(Process); activity.Add(new XElement(activity.Element(Ns + "Implementation")!));
        Assert.Throws<InvalidDataException>(() => NativeCallFidelity.ProjectTarget(activity, Activity(Other), new() { ProcessId = Other }));
    }
}
