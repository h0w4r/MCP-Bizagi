using System.IO.Compression;
using System.Xml.Linq;
using McpBizagi.Contracts;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Projection-policy tests only. The installed engine has a separate actual MCP acceptance suite.</summary>
public sealed class NativeContainerFidelityTests
{
    private const string Diagram = "10000000-0000-4000-8000-000000000001";
    private const string Pool = "20000000-0000-4000-8000-000000000002";
    private const string Process = "30000000-0000-4000-8000-000000000003";
    private const string Sub = "40000000-0000-4000-8000-000000000004";
    private const string Child = "50000000-0000-4000-8000-000000000005";
    private const string Lane = "60000000-0000-4000-8000-000000000006";
    private const string Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static byte[] Archive(string pools = "", string processes = "", string extra = "")
    {
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("ModelInfo.xml").Open())) writer.Write("<ModelInfo/>");
            using var innerBytes = new MemoryStream();
            using (var inner = new ZipArchive(innerBytes, ZipArchiveMode.Create, true))
            {
                using var writer = new StreamWriter(inner.CreateEntry("Diagram.xml").Open());
                writer.Write($"<Package xmlns='{Ns}'><Pools>{pools}</Pools><WorkflowProcesses>{processes}</WorkflowProcesses>{extra}</Package>");
            }
            using var target = zip.CreateEntry(Diagram + ".diag").Open(); target.Write(innerBytes.ToArray());
        }
        return bytes.ToArray();
    }
    private static string PoolXml(string name = "Pool", string content = "") => $"<Pool Id='{Pool}' Name='{name}' Process='{Process}'>{content}</Pool>";
    private static string ProcessXml(string content = "", string name = "Pool") => $"<WorkflowProcess Id='{Process}' Name='{name}'>{content}</WorkflowProcess>";
    private static string SubXml(string name = "Sub") => $"<Activity Id='{Sub}' Name='{name}'><BlockActivity ActivitySetId='{Sub}' View='COLLAPSED'/></Activity>";
    private static string SetXml(string name = "Sub", string content = "") => $"<ActivitySet Id='{Sub}' Name='{name}'><Activities>{content}</Activities></ActivitySet>";
    private static NativeMutation Create(string id, string parent, string type, string name) => new()
    { Operation = "create", ElementId = id, ParentId = parent, ElementType = type, Name = name, ProcessId = type == "Participant" ? Process : "" };
    private static NativeElement Element(string id, string parent, string type, string name) => new()
    { Id = id, ParentId = parent, Kind = type, ElementType = type, Name = name, DiagramId = Diagram };
    private static NativeMutation[] Creates() => [Create(Pool, Diagram, "Participant", "Pool"), Create(Lane, Process, "Lane", "Lane"),
        Create(Sub, Process, "SubProcess", "Sub"), Create(Child, Sub, "UserTask", "Child")];
    private static NativeElement[] Readback() => [Element(Pool, Diagram, "Participant", "Pool"), Element(Process, Pool, "Process", "Pool"),
        Element(Lane, Process, "Lane", "Lane"), Element(Sub, Process, "SubProcess", "Sub"), Element(Child, Sub, "UserTask", "Child")];
    private static byte[] Populated() => Archive(PoolXml(content: $"<Lanes><Lane Id='{Lane}' Name='Lane'/></Lanes>"),
        ProcessXml($"<Activities>{SubXml()}</Activities><ActivitySets>{SetXml(content: $"<Activity Id='{Child}' Name='Child'/>")}</ActivitySets>"));

    [Fact] public void ParentFirstCreationAndChildFirstDeletionCoverLinkedStructuresWithoutMaskingMembers()
    {
        var report = NativeMutationFidelity.Compare(Archive(), Populated(), Creates(), Readback());
        Assert.True(report.Preserved); Assert.Equal(4, report.Differences.Count(d => d.Classification == "verified_requested_mutation"));
        var deletes = Creates().Reverse().Select(c => new NativeMutation { Operation = "delete", ElementId = c.ElementId }).ToArray();
        report = NativeMutationFidelity.Compare(Populated(), Archive(), deletes, []);
        Assert.True(report.Preserved); Assert.Equal(4, report.Differences.Count(d => d.Location == "delete"));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ContainerProjectionCannotHideAnUnrequestedChild(bool create)
    {
        var changes = Creates().Where(c => c.ElementId != Child).ToArray();
        if (!create) changes = changes.Reverse().Select(c => new NativeMutation { Operation = "delete", ElementId = c.ElementId }).ToArray();
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(create ? Archive() : Populated(), create ? Populated() : Archive(), changes, create ? Readback() : []));
    }
    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("shared")] [InlineData("reused")]
    public void PoolProcessCompanionRequiresANewUnambiguousIdentityAndOwner(string fault)
    {
        var before = fault == "reused" ? Archive(processes: ProcessXml()) : Archive();
        string pools = PoolXml(); string processes = ProcessXml();
        if (fault == "missing") processes = "";
        if (fault == "duplicate") processes += ProcessXml();
        if (fault == "shared") pools += $"<Pool Id='{Child}' Process='{Process}'/>";
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(before, Archive(pools, processes), [Creates()[0]], Readback()));
    }
    [Fact] public void ProcessIdentityMustMatchBothDurableReferenceAndReadbackOwnership()
    {
        var change = Creates()[0]; change.ProcessId = Child;
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(Archive(), Archive(PoolXml(), ProcessXml()), [change], Readback()));
        var readback = Readback(); readback[1].ParentId = Sub;
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(Archive(), Archive(PoolXml(), ProcessXml()), [Creates()[0]], readback));
    }
    [Fact] public void SubprocessRenameKeepsChildPayloadAndRequiresItsLinkedActivitySet()
    {
        byte[] Model(string name, string child) => Archive(processes: ProcessXml($"<Activities>{SubXml(name)}</Activities><ActivitySets>{SetXml(name, child)}</ActivitySets>"));
        var change = new NativeMutation { Operation = "update", ElementId = Sub, Name = "New" };
        var element = Element(Sub, Process, "SubProcess", "New");
        var a = Model("Sub", $"<Activity Id='{Child}' Name='Keep'/>");
        Assert.True(NativeMutationFidelity.Compare(a, Model("New", $"<Activity Id='{Child}' Name='Keep'/>"), [change], [element]).Preserved);
        Assert.False(NativeMutationFidelity.Compare(a, Model("New", $"<Activity Id='{Child}' Name='Lost'/>"), [change], [element]).Preserved);
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(a, Archive(processes: ProcessXml($"<Activities>{SubXml("New")}</Activities>")), [change], [element]));
    }
    private static string Runtime(string json) => new XElement(XName.Get("ExtendedAttributes", Ns),
        new XElement(XName.Get("ExtendedAttribute", Ns), new XAttribute("Name", "RuntimeProperties"), new XAttribute("Value", json))).ToString();
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PoolDocumentationProjectsOnlyTheRequestedRuntimeLeaf(bool mutateOther)
    {
        string old = "{\"processClassProperties\":{\"keep\":7},\"other\":[1,2]}";
        string updated = "{\"processClassProperties\":{\"keep\":" + (mutateOther ? "8" : "7") + ",\"description\":\"New documentation\"},\"other\":[1,2]}";
        var before = Archive(PoolXml(), ProcessXml("<ProcessHeader><Description>Old</Description></ProcessHeader>" + Runtime(old)));
        var after = Archive(PoolXml("New"), ProcessXml("<ProcessHeader><Description>New documentation</Description></ProcessHeader>" + Runtime(updated), "New"));
        NativeMutation change = new() { Operation = "update", ElementId = Pool, Name = "New", Documentation = "New documentation" };
        var element = Element(Pool, Diagram, "Participant", "New"); element.Documentation = change.Documentation;
        if (mutateOther) Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(before, after, [change], [element]));
        else Assert.True(NativeMutationFidelity.Compare(before, after, [change], [element]).Preserved);
    }
    [Theory]
    [InlineData("<Unknown Id='{0}' Name='Old'/>", "<Unknown Id='{0}' Name='New'/>")]
    [InlineData("<Unknown><Activities><Activity Id='{0}' Name='Old'/></Activities></Unknown>", "<Unknown><Activities><Activity Id='{0}' Name='New'/></Activities></Unknown>")]
    [InlineData("<ExtendedAttributes><Activities><Activity Id='{0}' Name='Old'/></Activities></ExtendedAttributes>", "<ExtendedAttributes><Activities><Activity Id='{0}' Name='New'/></Activities></ExtendedAttributes>")]
    public void UnknownNameCollisionsCannotBorrowAnAuthorizedElementIdentity(string a, string b)
    {
        var report = NativeFidelity.Compare(Archive(extra: string.Format(a, Sub)), Archive(extra: string.Format(b, Sub)), [new(Sub, "New")]);
        Assert.False(report.Preserved); Assert.DoesNotContain(report.Differences, d => d.Classification == "requested_name_change");
    }
    [Fact] public void ParticipantCreationRequiresDistinctExplicitProcessIdentity()
    {
        var c = Creates()[0]; c.ProcessId = "";
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c]));
        c.ProcessId = Pool; Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c]));
        c.ProcessId = Process; NativeEditPlan.Validate([c]);
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c, new() { Operation = "update", ElementId = Process, Name = "Other" }]));
        c.Operation = "update"; c.ElementType = ""; c.ParentId = "";
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c]));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ExpandedSizeAndViewAreExplicitAndDoNotExcuseOtherGraphics(bool expand)
    {
        byte[] Model(bool expanded, int width, string extra = "") => Archive(processes: ProcessXml($"<Activities><Activity Id='{Sub}' Name='Sub'>" +
            $"<BlockActivity ActivitySetId='{Sub}' {(expanded ? "View='EXPANDED'" : "")}/><NodeGraphicsInfos><NodeGraphicsInfo Width='100' Height='70' " +
            $"Expanded='{expanded.ToString().ToLowerInvariant()}' ExpandedWidth='{width}' ExpandedHeight='250' {extra}><Coordinates XCoordinate='30' YCoordinate='40'/></NodeGraphicsInfo>" +
            $"</NodeGraphicsInfos></Activity></Activities><ActivitySets>{SetXml()}</ActivitySets>"));
        NativeMutation change = new() { Operation = "update", ElementId = Sub,
            Geometry = new() { X = 30, Y = 40, Width = 100, Height = 70, Expanded = expand }, ExpandedSize = new() { Width = 550, Height = 250 } };
        var element = Element(Sub, Process, "SubProcess", "Sub"); element.Geometry = change.Geometry;
        element.ExpandedGeometry = new() { Width = 550, Height = 250, Expanded = expand };
        Assert.True(NativeMutationFidelity.Compare(Model(!expand, 300), Model(expand, 550), [change], [element]).Preserved);
        Assert.False(NativeMutationFidelity.Compare(Model(!expand, 300), Model(expand, 550, "Unknown='new'"), [change], [element]).Preserved);
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(Model(!expand, 300), Model(expand, 549), [change], [element]));
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(Model(!expand, 300), Model(!expand, 550), [change], [element]));
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(1000001)]
    public void ExpandedDimensionsRejectInvalidBounds(double size)
    {
        NativeMutation c = new() { Operation = "update", ElementId = Sub, Geometry = new() { Width = 100, Height = 70, Expanded = true }, ExpandedSize = new() { Width = size, Height = 300 } };
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c]));
    }
    [Fact] public void ExpandedFieldsCannotBeIgnoredOnOtherOperationsOrNodeKinds()
    {
        var c = Create(Child, Sub, "UserTask", "Child"); c.Geometry = new() { Width = 100, Height = 70 }; c.ExpandedSize = new() { Width = 300, Height = 200 };
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c]));
        c.Operation = "delete"; c.ElementType = ""; c.ParentId = ""; c.Name = null; c.Geometry = null;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([c]));
    }
    [Fact] public void MilestoneDeletionCannotBeHiddenInsideAPoolDeletion()
    {
        var before = Archive(PoolXml(content: $"<Milestones><Milestone Id='{Lane}' Name='Phase'/></Milestones>"), ProcessXml());
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(before, Archive(), [new() { Operation = "delete", ElementId = Pool }], []));
        var report = NativeMutationFidelity.Compare(before, Archive(), [new() { Operation = "delete", ElementId = Lane }, new() { Operation = "delete", ElementId = Pool }], []);
        Assert.True(report.Preserved); Assert.Equal(2, report.Differences.Count(d => d.Classification == "verified_requested_mutation"));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ClearingPoolDocumentationAllowsNativeAbsenceButNotAnotherRuntimeChange(bool mutateOther)
    {
        var before = Archive(PoolXml(), ProcessXml("<ProcessHeader><Description>Old</Description></ProcessHeader>" +
            Runtime("{\"processClassProperties\":{\"description\":\"Old\",\"keep\":7},\"other\":[1,2]}")));
        var after = Archive(PoolXml(), ProcessXml("<ProcessHeader><Description/></ProcessHeader>" +
            Runtime("{\"processClassProperties\":{\"keep\":7},\"other\":[1," + (mutateOther ? "3" : "2") + "]}")));
        NativeMutation change = new() { Operation = "update", ElementId = Pool, Documentation = "" };
        var element = Element(Pool, Diagram, "Participant", "Pool");
        if (mutateOther) Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(before, after, [change], [element]));
        else Assert.True(NativeMutationFidelity.Compare(before, after, [change], [element]).Preserved);
    }
    [Theory]
    [InlineData("{\"processClassProperties\":{\"description\":\"New\",\"keep\":7e0}}")]
    [InlineData("{\"processClassProperties\":{\"description\":\"New\",\"keep\":7,\"unknown\":true}}")]
    [InlineData("{\"processClassProperties\":{\"description\":\"New\",\"keep\":7,\"keep\":8}}")]
    public void RuntimeProjectionPreservesNumericEncodingAndUnknownKeysAndRejectsDuplicates(string changed)
    {
        var before = Archive(PoolXml(), ProcessXml("<ProcessHeader><Description>Old</Description></ProcessHeader>" + Runtime("{\"processClassProperties\":{\"description\":\"Old\",\"keep\":7}}")));
        var after = Archive(PoolXml(), ProcessXml("<ProcessHeader><Description>New</Description></ProcessHeader>" + Runtime(changed)));
        NativeMutation c = new() { Operation = "update", ElementId = Pool, Documentation = "New" };
        var e = Element(Pool, Diagram, "Participant", "Pool"); e.Documentation = "New";
        // Parsing errors and semantic differences are both terminal: neither can accredit preservation.
        Assert.NotNull(Record.Exception(() => NativeMutationFidelity.Compare(before, after, [c], [e])));
    }
}
