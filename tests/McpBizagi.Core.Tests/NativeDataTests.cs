using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Policy fixtures stay distinct from installed-engine data lifecycle acceptance.</summary>
public sealed class NativeDataTests
{
    private const string Id = "11111111-1111-4111-8111-111111111111", Store = "22222222-2222-4222-8222-222222222222";
    private static NativeMutation Patch(NativeDataProperties p) => new() { Operation = "update", ElementId = Id, DataProperties = p };
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static XElement Data(string content) => XDocument.Parse($"<Package xmlns='{Ns}'><WorkflowProcesses><WorkflowProcess Id='p'><DataObjects><DataObject Id='{Id}' State='old'>{content}</DataObject></DataObjects></WorkflowProcess></WorkflowProcesses></Package>").Descendants(Ns + "DataObject").Single();
    [Theory] [InlineData("")] [InlineData("0")] [InlineData("123456789012345678901234567890")]
    public void CapacityIsDurableCanonicalText(string capacity) => NativeEditPlan.Validate([Patch(new() { Capacity = capacity })]);
    [Theory] [InlineData("01")] [InlineData("-1")] [InlineData("1.0")] [InlineData("+2")] [InlineData("１２")]
    public void InvalidCapacityCannotBeSilentlyNormalized(string capacity) => Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Patch(new() { Capacity = capacity })]));
    [Fact] public void EmptyPatchAndMissingStoreIdentityFail()
    {
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Patch(new())]));
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([new() { Operation = "create", ElementId = Id, ParentId = Store, ElementType = "DataStoreReference" }]));
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Patch(new() { StoreId = "" })]));
    }
    [Theory] [InlineData("DataStore")] [InlineData("DataStoreReference")] [InlineData("UserTask")]
    public void CollectionRequiresActualObject(string kind) => Assert.Throws<InvalidDataException>(() => NativeDataPolicy.Verify(Patch(new() { IsCollection = true }), new() { Kind = kind, Data = new() { IsCollection = true } }, []));
    [Fact] public void UnresolvedStoreAndMissingSharedStateCarriersFail()
    {
        Assert.Throws<InvalidDataException>(() => NativeDataPolicy.Verify(Patch(new() { StoreId = Store }), new() { Kind = "DataStoreReference", Data = new() { StoreBpmnName = Store } }, []));
        Assert.Throws<InvalidDataException>(() => NativeDataPolicy.Verify(Patch(new() { State = "ready" }), new() { Id = Store, Kind = "DataStore", Data = new() { State = "ready" } }, []));
    }
    [Fact] public void DataCollectionProjectionDoesNotDropUnknownContent()
    {
        var a = Data("<DataField IsArray='false' Custom='retain'><Unknown/></DataField>"); var b = Data("<DataField IsArray='true' Custom='retain'><Unknown/></DataField>");
        NativeDataPolicy.Project(a, b, Patch(new() { IsCollection = true }), []); Assert.True(XNode.DeepEquals(a, b));
        b.Descendants(Ns + "Unknown").Single().Remove(); Assert.False(XNode.DeepEquals(a, b));
    }
    [Fact] public void DuplicateDataFieldsAndUnknownOwnerCannotBeProjected()
    {
        var a = Data("<DataField/><DataField/>"); var b = Data("<DataField IsArray='true'/>");
        Assert.Throws<InvalidDataException>(() => NativeDataPolicy.Project(a, b, Patch(new() { IsCollection = true }), []));
        Assert.Throws<InvalidDataException>(() => NativeDataPolicy.Project(new XElement(a), b, Patch(new() { State = "new" }), []));
    }
    [Theory] [InlineData("SequenceFlow")] [InlineData("MessageFlow")] [InlineData("Association")]
    public void CreatedConnectorRequiresActualEndpointAndPathReadback(string kind)
    {
        var c = new NativeMutation { Operation = "create", ElementId = Id, ParentId = Store, ElementType = kind, SourceId = "source", TargetId = "target", Points = [new() { X = 1, Y = 2 }, new() { X = 3, Y = 4 }] };
        var e = new NativeElement { Id = Id, ParentId = Store, Kind = kind, ElementType = kind, SourceId = "source", TargetId = "target", Points = [new() { X = 1, Y = 2 }, new() { X = 3, Y = 4 }] };
        NativeEditPlan.Verify([c], [e]); e.TargetId = "wrong"; Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([c], [e]));
        e.TargetId = "target"; e.Points[1].X = 99; Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([c], [e]));
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void StoreUnlimitedUsesTheDurableXpdlDefaultNotTheBpmnConstructor(bool value)
    {
        XElement StoreNode(string attribute) => XDocument.Parse($"<Package xmlns='{Ns}'><DataStores><DataStore Id='{Id}' {attribute}/></DataStores></Package>").Descendants(Ns + "DataStore").Single();
        var a = StoreNode("IsUnlimited='true'"); var b = StoreNode(value ? "IsUnlimited='true'" : "");
        NativeDataPolicy.Project(a, b, Patch(new() { IsUnlimited = value }), []); Assert.True(XNode.DeepEquals(a, b));
        Assert.Throws<InvalidDataException>(() => NativeDataPolicy.Project(a, StoreNode(""), Patch(new() { IsUnlimited = true }), []));
    }
}
