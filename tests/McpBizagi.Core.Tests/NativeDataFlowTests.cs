using System.Xml.Linq;
using McpBizagi.Contracts;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Exact comparison/readback boundary tests; real native I/O acceptance uses the SDK client.</summary>
public sealed class NativeDataFlowTests
{
    private const string Owner = "11111111-1111-4111-8111-111111111111", Item = "22222222-2222-4222-8222-222222222222",
        Port = "33333333-3333-4333-8333-333333333333", Edge = "44444444-4444-4444-8444-444444444444", Visual = "55555555-5555-4555-8555-555555555555";
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static XDocument Document(bool connected, bool bound, bool input = true)
    {
        string direction = input ? "Input" : "Output";
        string ports = bound ? $"<Data{direction} Id='{Port}' IsCollection='false'><Documentation/></Data{direction}>" : "";
        string sets = bound ? $"<{direction}Sets><{direction}Set><{direction} ArtifactId='{Port}'/></{direction}Set></{direction}Sets>" : "";
        string source = input ? Item : Port, target = input ? Port : Item;
        string association = bound ? $"<DataAssociation Id='{Edge}' From='{source}' To='{target}'><Description/><ExtendedAttributes/></DataAssociation>" : "";
        string visual = connected ? $"<Association Id='{Visual}' Source='{(input ? Item : Owner)}' Target='{(input ? Owner : Item)}'/>" : "";
        return XDocument.Parse($"<Package xmlns='{Ns}' Id='diagram'><WorkflowProcesses><WorkflowProcess Id='process'><DataInputOutputs>{ports}</DataInputOutputs><Activities><Activity Id='{Owner}'><Implementation><Task/></Implementation>{sets}</Activity></Activities><DataObjects><DataObject Id='{Item}'/></DataObjects><Associations>{visual}</Associations><DataAssociations>{association}</DataAssociations></WorkflowProcess></WorkflowProcesses></Package>");
    }
    private static NativeElement[] Graph(bool input = true)
    {
        var port = new NativeElement { Id = Port, ParentId = Owner, Kind = input ? "DataInput" : "DataOutput", Data = new() { IsCollection = false } };
        var edge = new NativeElement { Id = Edge, ParentId = Owner, SourceId = input ? Item : Port, TargetId = input ? Port : Item };
        var data = new NativeDataFlowInfo { HasSpecification = true };
        if (input) { data.Inputs = [port]; data.InputAssociations = [edge]; data.InputSets = [[Port]]; }
        else { data.Outputs = [port]; data.OutputAssociations = [edge]; data.OutputSets = [[Port]]; }
        return [new() { Id = Owner, DataFlow = data }];
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void AddsOnlyVerifiedOwnedPortsAndMemberships(bool input)
    {
        var a = Document(false, false, input); var b = Document(true, true, input);
        NativeDataFlowPolicy.ProjectDerived(a, b, [], Graph(input));
        b.Descendants(Ns + "Association").Single().Remove();
        // XElement also distinguishes <Empty></Empty> from <Empty/> in memory. Normalize
        // only that fixture representation, not production XML content or comparison rules.
        foreach (var empty in a.Descendants().Concat(b.Descendants()).Where(e => !e.Nodes().Any())) empty.RemoveNodes();
        Assert.True(XNode.DeepEquals(a, b));
    }
    [Theory] [InlineData("endpoint")] [InlineData("owner")] [InlineData("membership")] [InlineData("state")]
    public void WrongIndependentBindingReadbackFails(string fault)
    {
        var graph = Graph(); var data = graph[0].DataFlow!;
        if (fault == "endpoint") data.InputAssociations[0].SourceId = "wrong";
        if (fault == "owner") data.Inputs[0].ParentId = "wrong";
        if (fault == "membership") data.InputSets = [[]];
        if (fault == "state") data.Inputs[0].Data!.State = "wrong";
        Assert.Throws<InvalidDataException>(() => NativeDataFlowPolicy.ProjectDerived(Document(false, false), Document(true, true), [], graph));
    }
    [Fact] public void AGraphicalAssociationAloneDoesNotProveADataBinding() => Assert.Throws<InvalidDataException>(() => NativeDataFlowPolicy.ProjectDerived(Document(false, false), Document(true, false), [], Graph()));
    [Fact] public void RemovingALiveBindingOrInventingUnrelatedIoFails()
    {
        Assert.Throws<InvalidDataException>(() => NativeDataFlowPolicy.ProjectDerived(Document(true, true), Document(true, false), [], Graph()));
        Assert.Throws<InvalidDataException>(() => NativeDataFlowPolicy.ProjectDerived(Document(false, false), Document(false, true), [], Graph()));
    }
    [Theory] [InlineData("name")] [InlineData("state")] [InlineData("collection")] [InlineData("unknown")] [InlineData("comment")] [InlineData("reference-attribute")]
    public void ImplicitCleanupCannotHideAuthoredOrUnknownPayload(string fault)
    {
        var before = Document(true, true); var port = before.Descendants(Ns + "DataInput").Single();
        if (fault == "name") port.SetAttributeValue("Name", "Authored");
        if (fault == "state") port.SetAttributeValue("State", "Authored");
        if (fault == "collection") port.SetAttributeValue("IsCollection", "true");
        if (fault == "unknown") port.Add(new XElement(Ns + "Unknown", "Keep"));
        if (fault == "comment") port.Add(new XComment("Keep"));
        if (fault == "reference-attribute") before.Descendants(Ns + "Input").Single().SetAttributeValue("Unknown", "Keep");
        Assert.Throws<InvalidDataException>(() => NativeDataFlowPolicy.ProjectDerived(before, Document(false, false), [], []));
    }
    [Theory] [InlineData("Input")] [InlineData("Output")]
    public void SetReferencesRequireExactNativeOwnerAndNamespace(string kind)
    {
        var doc = Document(true, true, kind == "Input"); var reference = doc.Descendants(Ns + kind).Single();
        Assert.True(NativeDataFlowPolicy.IsSetReference(reference));
        Assert.False(NativeDataFlowPolicy.IsSetReference(new XElement(reference)));
        reference.Name = XName.Get(kind, "urn:unknown"); Assert.False(NativeDataFlowPolicy.IsSetReference(reference));
    }
    [Theory] [InlineData("Association", "Source")] [InlineData("Association", "Target")] [InlineData("DataAssociation", "From")] [InlineData("DataAssociation", "To")]
    public void CloneRejectsReferencesToAnOriginalOrUnresolvedIdentity(string node, string field)
    {
        var doc = Document(true, true); NativeDataFlowPolicy.VerifyClonedData(doc, Graph());
        doc.Descendants(Ns + node).Single().SetAttributeValue(field, "old-source-identity");
        Assert.Throws<InvalidDataException>(() => NativeDataFlowPolicy.VerifyClonedData(doc, Graph()));
    }
    [Theory] [InlineData("preserved-space")] [InlineData("unknown-wrapper")] [InlineData("cdata")]
    public void ComparisonDoesNotProjectUnknownEmptyContainerContent(string fault)
    {
        var a = Document(false, false); var b = Document(true, true);
        var sets = b.Descendants(Ns + "InputSets").Single();
        if (fault == "preserved-space") { sets.SetAttributeValue(XNamespace.Xml + "space", "preserve"); sets.Add(new XText("  ")); }
        else if (fault == "cdata") sets.Add(new XCData("  "));
        else sets.Add(new XElement(Ns + "Unknown"));
        NativeDataFlowPolicy.ProjectDerived(a, b, [], Graph());
        Assert.NotNull(b.Descendants(Ns + "InputSets").SingleOrDefault());
    }
}
