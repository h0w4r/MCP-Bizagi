using System.IO.Compression;
using System.Text;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class NativeMutationFidelityTests
{
    // Synthetic containers isolate the projection policy; they do not accredit any native-engine operation.
    private const string ElementId = "abcdef12-3456-4789-abcd-123456789abc";
    private const string ParentId = "11111111-2222-4333-8444-555555555555";
    private const string OtherId = "22222222-3333-4444-8555-666666666666";
    private const string TargetId = "33333333-4444-4555-8666-777777777777";
    private const string Ns = "http://www.wfmc.org/2009/XPDL2.2";

    private static byte[] Archive(string body, byte[]? attachment = null, string extra = "<Unknown keep='yes'/>", bool omitCollection = false,
        string collectionAttributes = "", string processAttributes = "")
    {
        using var stream = new MemoryStream();
        using (var outer = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(outer.CreateEntry("ModelInfo.xml").Open())) writer.Write("<ModelInfo/>");
            using (var file = outer.CreateEntry("attachments/preserved.bin").Open()) file.Write(attachment ?? [0, 1, 2, 255]);
            using var diagramBytes = new MemoryStream();
            using (var diagram = new ZipArchive(diagramBytes, ZipArchiveMode.Create, leaveOpen: true))
            {
                using var writer = new StreamWriter(diagram.CreateEntry("Diagram.xml").Open(), Encoding.UTF8);
                // Native transitions belong to Transitions, never an Activities extension lookalike.
                string collection = body.StartsWith("<Transition ", StringComparison.Ordinal) ? "Transitions" : "Activities";
                string list = omitCollection ? "" : $"<{collection} {collectionAttributes}>{body}</{collection}>";
                writer.Write($"<Package xmlns='{Ns}'><WorkflowProcesses><WorkflowProcess Id='{ParentId}' {processAttributes}>{list}{extra}</WorkflowProcess></WorkflowProcesses></Package>");
            }
            using var target = outer.CreateEntry("own-diagram.diag").Open(); target.Write(diagramBytes.ToArray());
        }
        return stream.ToArray();
    }

    private static string Activity(string name = "Original", string content = "", string id = ElementId, string attributes = "") =>
        $"<Activity Id='{id}' Name='{name}' {attributes}>{content}</Activity>";
    private static NativeMutation Update(string name = "Updated") => new() { Operation = "update", ElementId = ElementId, Name = name };
    private static NativeElement Readback(string name = "Updated") => new()
    { Id = ElementId, ParentId = ParentId, ElementType = "UserTask", Name = name };

    [Fact]
    public void ExplicitNameChangeIsProjectedAndReported()
    {
        var result = NativeMutationFidelity.Compare(Archive(Activity()), Archive(Activity("Updated")), [Update()], [Readback()]);
        Assert.True(result.Preserved);
        Assert.True(result.CheckedAtoms > 0);
        Assert.Contains(result.Differences, d => d.Classification == "requested_name_change" && d.ElementId == ElementId);
        Assert.Contains(result.Differences, d => d.Classification == "verified_requested_mutation" && d.Location == "update");
    }

    [Fact]
    public void RequestedMutationCannotExcuseUnrelatedNamesOrUnknownAttributes()
    {
        var before = Archive(Activity() + Activity("Other", id: OtherId));
        var after = Archive(Activity("Updated") + Activity("Changed without permission", id: OtherId));
        Assert.False(NativeMutationFidelity.Compare(before, after, [Update()], [Readback()]).Preserved);
        after = Archive(Activity("Updated", attributes: "unknown='new'") + Activity("Other", id: OtherId));
        var report = NativeMutationFidelity.Compare(before, after, [Update()], [Readback()]);
        Assert.False(report.Preserved);
        Assert.Contains(report.Differences, d => d.Classification == "unexpected_xml_change");
    }

    [Fact]
    public void BinaryAttachmentsMustRemainByteIdenticalDuringAnAuthorizedEdit()
    {
        var result = NativeMutationFidelity.Compare(Archive(Activity()), Archive(Activity("Updated"), [0, 1, 3, 255]), [Update()], [Readback()]);
        Assert.False(result.Preserved);
        Assert.Contains(result.Differences, d => d.Entry == "attachments/preserved.bin" && d.Classification == "binary_changed");
    }

    [Theory]
    [InlineData("<Unknown keep='no'/>")]
    [InlineData("")]
    [InlineData("<Unknown keep='yes'/><!--new unrequested content-->")]
    public void UnknownContentOutsideTheMutationMustBePreserved(string changed)
    {
        var result = NativeMutationFidelity.Compare(Archive(Activity()), Archive(Activity("Updated"), extra: changed), [Update()], [Readback()]);
        Assert.False(result.Preserved);
    }

    [Fact]
    public void NameProjectionRequiresBothIndependentReadbackAndRequestedNativeXmlValue()
    {
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(
            Archive(Activity()), Archive(Activity("Updated")), [Update()], [Readback("Wrong")]));
        var result = NativeMutationFidelity.Compare(Archive(Activity()), Archive(Activity("Wrong")), [Update()], [Readback()]);
        Assert.False(result.Preserved);
    }

    [Fact]
    public void CreationProjectsOnlyTheNewIdentityAndRetainsExistingObjects()
    {
        NativeMutation mutation = new() { Operation = "create", ElementId = ElementId, ParentId = ParentId, ElementType = "UserTask", Name = "Created" };
        string existing = Activity("Keep", id: OtherId);
        var result = NativeMutationFidelity.Compare(Archive(existing), Archive(existing + Activity("Created")), [mutation], [Readback("Created")]);
        Assert.True(result.Preserved);
        Assert.Contains(result.Differences, d => d.Classification == "verified_requested_mutation" && d.Location == "create");
        result = NativeMutationFidelity.Compare(Archive(existing), Archive(Activity("Lost name", id: OtherId) + Activity("Created")), [mutation], [Readback("Created")]);
        Assert.False(result.Preserved);
    }

    [Fact]
    public void CreationCannotReuseAnExistingNativeIdentity()
    {
        NativeMutation mutation = new() { Operation = "create", ElementId = ElementId, ParentId = ParentId, ElementType = "UserTask", Name = "Created" };
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(Archive(Activity()), Archive(Activity("Created")), [mutation], [Readback("Created")]));
    }

    [Fact]
    public void CreationRequiresTheIdentityToExistInTheDurableNativeXml()
    {
        NativeMutation mutation = new() { Operation = "create", ElementId = ElementId, ParentId = ParentId, ElementType = "UserTask", Name = "Created" };
        // A claimed DTO cannot replace evidence that the requested object was persisted.
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(Archive(""), Archive(""), [mutation], [Readback("Created")]));
    }

    [Fact]
    public void CreationRequiresExactlyOneDurableIdentityEvenWhenReadbackClaimsOne()
    {
        NativeMutation mutation = new() { Operation = "create", ElementId = ElementId, ParentId = ParentId, ElementType = "UserTask", Name = "Created" };
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(Archive(""),
            Archive(Activity("Created") + Activity("Created")), [mutation], [Readback("Created")]));
    }

    [Fact]
    public void DeletionRemovesOnlyTheRequestedIdentity()
    {
        NativeMutation mutation = new() { Operation = "delete", ElementId = ElementId };
        string existing = Activity("Keep", id: OtherId);
        var result = NativeMutationFidelity.Compare(Archive(Activity() + existing), Archive(existing), [mutation], []);
        Assert.True(result.Preserved);
        Assert.Contains(result.Differences, d => d.Classification == "verified_requested_mutation" && d.Location == "delete");
        Assert.False(NativeMutationFidelity.Compare(Archive(Activity() + existing), Archive(""), [mutation], []).Preserved);
    }

    [Fact]
    public void DeletionCannotBeAccreditedWhenTheElementSurvivesEitherReadback()
    {
        NativeMutation mutation = new() { Operation = "delete", ElementId = ElementId };
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(Archive(Activity()), Archive(""), [mutation], [Readback()]));
        Assert.False(NativeMutationFidelity.Compare(Archive(Activity()), Archive(Activity()), [mutation], []).Preserved);
    }

    [Fact]
    public void DeletionMustAddressAnIdentityThatExistedInTheNativeContainer()
    {
        NativeMutation mutation = new() { Operation = "delete", ElementId = ElementId };
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(Archive(""), Archive(""), [mutation], []));
    }

    [Theory]
    [InlineData("Description")]
    [InlineData("Documentation")]
    public void ExplicitDocumentationUpdateProjectsOnlySimpleText(string name)
    {
        NativeMutation mutation = new() { Operation = "update", ElementId = ElementId, Documentation = "New documentation" };
        var element = Readback("Original"); element.Documentation = "New documentation";
        var before = Archive(Activity(content: $"<{name}>Old documentation</{name}><Unknown keep='yes'/>"));
        var after = Archive(Activity(content: $"<{name}>New documentation</{name}><Unknown keep='yes'/>"));
        Assert.True(NativeMutationFidelity.Compare(before, after, [mutation], [element]).Preserved);
        after = Archive(Activity(content: $"<{name}>New documentation</{name}><Unknown keep='no'/>"));
        Assert.False(NativeMutationFidelity.Compare(before, after, [mutation], [element]).Preserved);
    }

    [Fact]
    public void DocumentationProjectionDoesNotEraseUnknownNestedMarkup()
    {
        NativeMutation mutation = new() { Operation = "update", ElementId = ElementId, Documentation = "New" };
        var element = Readback("Original"); element.Documentation = "New";
        var before = Archive(Activity(content: "<Description><Unknown>Old</Unknown></Description>"));
        var after = Archive(Activity(content: "<Description>New</Description>"));
        Assert.False(NativeMutationFidelity.Compare(before, after, [mutation], [element]).Preserved);
    }

    private static string Graphics(string x, string y, string width, string height, string fill = "-1", string border = "-16777216", string extra = "") =>
        $"<NodeGraphicsInfos><NodeGraphicsInfo Width='{width}' Height='{height}' FillColor='{fill}' BorderColor='{border}' {extra}><Coordinates XCoordinate='{x}' YCoordinate='{y}'/></NodeGraphicsInfo></NodeGraphicsInfos>";

    [Fact]
    public void GeometryAndColorsAreProjectedOnlyAfterNativeXmlAndDtoValuesMatch()
    {
        NativeMutation mutation = new()
        {
            Operation = "update",
            ElementId = ElementId,
            Geometry = new() { X = 30, Y = 40, Width = 120, Height = 70, BackgroundArgb = -65536, BorderArgb = -16711936 }
        };
        var element = Readback("Original"); element.Geometry = new()
        { X = 30, Y = 40, Width = 120, Height = 70, BackgroundArgb = -65536, BorderArgb = -16711936 };
        var before = Archive(Activity(content: Graphics("10", "20", "100", "60")));
        var after = Archive(Activity(content: Graphics("30", "40", "120", "70", "-65536", "-16711936")));
        Assert.True(NativeMutationFidelity.Compare(before, after, [mutation], [element]).Preserved);
        after = Archive(Activity(content: Graphics("31", "40", "120", "70", "-65536", "-16711936")));
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(before, after, [mutation], [element]));
        after = Archive(Activity(content: Graphics("30", "40", "120", "70", "-65536", "-16711936", "Unknown='changed'")));
        Assert.False(NativeMutationFidelity.Compare(before, after, [mutation], [element]).Preserved);
    }

    [Fact]
    public void UnrequestedColorChangesAreRejectedEvenDuringGeometryChanges()
    {
        NativeMutation mutation = new() { Operation = "update", ElementId = ElementId, Geometry = new() { X = 30, Y = 40, Width = 120, Height = 70 } };
        var element = Readback("Original"); element.Geometry = new() { X = 30, Y = 40, Width = 120, Height = 70 };
        var result = NativeMutationFidelity.Compare(Archive(Activity(content: Graphics("10", "20", "100", "60"))),
            Archive(Activity(content: Graphics("30", "40", "120", "70", "-65536"))), [mutation], [element]);
        Assert.False(result.Preserved);
    }

    private static string Transition(string source, string target, string points) =>
        $"<Transition Id='{ElementId}' From='{source}' To='{target}'><ConnectorGraphicsInfos><ConnectorGraphicsInfo>{points}</ConnectorGraphicsInfo></ConnectorGraphicsInfos></Transition>";
    private static string Point(int x, int y, string extra = "") => $"<Coordinates XCoordinate='{x}' YCoordinate='{y}' {extra}/>";

    [Fact]
    public void ReconnectionProjectsEndpointsAndExactPointSequenceWhileKeepingOtherContent()
    {
        NativeMutation mutation = new()
        {
            Operation = "reconnect",
            ElementId = ElementId,
            SourceId = OtherId,
            TargetId = TargetId,
            Points = [new() { X = 30, Y = 40 }, new() { X = 80, Y = 40 }, new() { X = 80, Y = 100 }]
        };
        NativeElement element = new()
        {
            Id = ElementId,
            SourceId = OtherId,
            TargetId = TargetId,
            Points = [new() { X = 30, Y = 40 }, new() { X = 80, Y = 40 }, new() { X = 80, Y = 100 }]
        };
        var before = Archive(Transition(ParentId, OtherId, Point(10, 10) + Point(20, 20)));
        string points = Point(30, 40) + Point(80, 40) + Point(80, 100);
        Assert.True(NativeMutationFidelity.Compare(before, Archive(Transition(OtherId, TargetId, points)), [mutation], [element]).Preserved);
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(before,
            Archive(Transition(OtherId, ParentId, points)), [mutation], [element]));
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(before,
            Archive(Transition(OtherId, TargetId, Point(31, 40) + Point(80, 40) + Point(80, 100))), [mutation], [element]));
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(before,
            Archive(Transition(OtherId, TargetId, Point(30, 40, "Unknown='keep'") + Point(80, 40) + Point(80, 100))), [mutation], [element]));
    }

    [Fact]
    public void AmbiguousUpdateIdentityInNativeXmlIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(Archive(Activity() + Activity()),
            Archive(Activity("Updated") + Activity("Updated")), [Update()], [Readback()]));
    }

    [Theory]
    [InlineData(false, "none")] [InlineData(true, "none")]
    [InlineData(false, "attribute")] [InlineData(true, "attribute")]
    [InlineData(false, "namespace")] [InlineData(true, "namespace")]
    [InlineData(false, "comment")] [InlineData(true, "comment")]
    [InlineData(false, "text")] [InlineData(true, "text")]
    [InlineData(false, "unknown")] [InlineData(true, "unknown")]
    [InlineData(false, "preserve")] [InlineData(true, "preserve")]
    [InlineData(false, "ancestor-preserve")] [InlineData(true, "ancestor-preserve")]
    [InlineData(false, "unrelated")] [InlineData(true, "unrelated")]
    public void FirstInsertionAndLastDeletionProjectOnlyEmptyNativeStructuralLists(bool delete, string fault)
    {
        string attributes = fault switch { "attribute" => "unknown='keep'", "namespace" => "xmlns:extra='urn:keep'", "preserve" => "xml:space='preserve'", _ => "" };
        string decoration = fault switch { "comment" => "<!--keep-->", "text" => "meaningful", "unknown" => "<Unknown/>", _ => "\n   " };
        string processAttributes = fault == "ancestor-preserve" ? "xml:space='preserve'" : "";
        var empty = Archive("", omitCollection: true, processAttributes: processAttributes);
        var populated = Archive(Activity("Created") + decoration, collectionAttributes: attributes, processAttributes: processAttributes,
            extra: fault == "unrelated" ? "<Unknown keep='lost'/>" : "<Unknown keep='yes'/>");
        NativeMutation change = delete ? new() { Operation = "delete", ElementId = ElementId }
            : new() { Operation = "create", ElementId = ElementId, ParentId = ParentId, ElementType = "UserTask", Name = "Created" };
        var report = NativeMutationFidelity.Compare(delete ? populated : empty, delete ? empty : populated, [change], delete ? [] : [Readback("Created")]);
        Assert.Equal(fault == "none", report.Preserved);
        if (fault == "none") Assert.Contains(report.Differences, d => d.Classification == "verified_empty_collection_projection");
    }

    [Fact] public void ExistingEmptyCollectionMetadataCannotDisappearDuringInsertion()
    {
        NativeMutation change = new() { Operation = "create", ElementId = ElementId, ParentId = ParentId, ElementType = "UserTask", Name = "Created" };
        Assert.False(NativeMutationFidelity.Compare(Archive("", collectionAttributes: "unknown='keep'"), Archive(Activity("Created")), [change], [Readback("Created")]).Preserved);
        Assert.False(NativeMutationFidelity.Compare(Archive("", collectionAttributes: "xmlns:q='urn:keep'"), Archive(Activity("Created")), [change], [Readback("Created")]).Preserved);
    }

    [Fact] public void AnUnknownExtensionCannotImpersonateAnOwnedNativeActivity()
    {
        NativeMutation change = new() { Operation = "create", ElementId = ElementId, ParentId = ParentId, ElementType = "UserTask", Name = "Created" };
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(Archive(""),
            Archive("", extra: $"<Unknown><Activities>{Activity("Created")}</Activities></Unknown>"), [change], [Readback("Created")]));
    }

    [Fact] public void UnrequestedEmptyListChangesAreNotAGlobalNoopWaiver()
    {
        Assert.False(NativeMutationFidelity.Compare(Archive(Activity()),
            Archive(Activity("Updated"), extra: "<Unknown keep='yes'/><Transitions/>"), [Update()], [Readback()]).Preserved);
    }
}
