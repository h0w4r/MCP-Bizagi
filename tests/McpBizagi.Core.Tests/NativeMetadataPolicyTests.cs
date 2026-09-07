using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class NativeMetadataPolicyTests
{
    [Fact]
    public void XmlReaderAcceptsOneDecodedBomButRejectsRepeatedMarks()
    {
        // Native result leaves contain UTF-8 BOM bytes, which remain after explicit UTF-8 decoding.
        Assert.True(NativeMetadataPolicy.XmlEquivalent("\uFEFF<ScenarioResults/>", "<ScenarioResults/>"));
        Assert.Throws<XmlException>(() => NativeMetadataPolicy.Read("\uFEFF\uFEFF<ScenarioResults/>"));
    }
    // Synthetic, in-memory containers exercise policy only; they do not accredit the installed engine.
    private const string ResourceId = "abcdef12-3456-4789-abcd-123456789abc";
    private const string OtherResourceId = "22222222-3333-4444-8555-666666666666";
    private const string DiagramId = "11111111-2222-4333-8444-555555555555";
    private const string OtherDiagramId = "33333333-4444-4555-8666-777777777777";
    private const string ActivityId = "44444444-5555-4666-8777-888888888888";
    private const string OtherActivityId = "55555555-6666-4777-8888-999999999999";
    private const string XpdlNamespace = "http://www.wfmc.org/2009/XPDL2.2";
    private const string Bp = NativeMetadataPolicy.BpsimNamespace;

    private static NativeResourceChange Upsert(string name = "Updated", string type = "Role", string description = "New documentation") =>
        new() { Id = ResourceId, Name = name, Type = type, Description = description };
    private static NativeResourceChange Delete() => new() { Id = ResourceId, Operation = "delete" };
    private static NativeMetadataPatch ResourcePatch(NativeResourceChange change) => new() { Resources = [change] };
    private static NativeMetadataSnapshot ResourceReadback(NativeResourceChange change) => new()
    {
        Resources = [new() { Id = change.Id, BpmnId = "Resource_One", Name = change.Name, Description = change.Description, Type = change.Type }]
    };
    private static string Scenario(string id = "Scenario_One", string parameters = "baseTimeUnit='minute' replication='2'", string content = "") =>
        $"<Scenario id='{id}' name='Own scenario' author='h0w4r' version='1.0'><ScenarioParameters {parameters}/>{content}</Scenario>";
    private static string Simulation(string scenarios = "", string level = "LevelTwo") => $"<BPSimData xmlns='{Bp}' simulationLevel='{level}'>{scenarios}</BPSimData>";
    private static NativeMetadataPatch SimulationPatch(string xml, bool discard = false) => new()
    { Simulations = [new() { DiagramId = DiagramId, Xml = xml }], DiscardSimulationResults = discard };
    private static NativeMetadataSnapshot SimulationReadback(string xml) => new()
    { Simulations = [new() { DiagramId = DiagramId, Xml = xml }] };
    private static string Participant(string id = ResourceId, string name = "Original", string type = "ROLE", string description = "Old documentation", string extra = "") =>
        $"<Participant Id='{id}' Name='{name}'><Description>{description}</Description><ParticipantType Type='{type}'/><ExtendedAttributes><ExtendedAttribute Name='Resource_One'/></ExtendedAttributes>{extra}</Participant>";

    private static byte[] Archive(string resources = "", string? simulation = null, string? results = "<ScenarioResults/>",
        byte[]? attachment = null, string otherDiagram = "<Untargeted keep='yes'/>", string extra = "<Unknown keep='yes'/>",
        string? diagramResources = null, string? otherResources = null, string diagramBody = "<Diagram keep='yes'/>")
    {
        using var bytes = new MemoryStream();
        using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Write(string name, string xml)
            { using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(xml); }
            Write("Participants.xml", $"<Participants xmlns='{XpdlNamespace}'>{resources}</Participants>");
            Write("ModelInfo.xml", "<ModelInfo/>");
            Write("unknown.xml", extra);
            using (var binary = archive.CreateEntry("attachments/keep.bin").Open()) binary.Write(attachment ?? [0, 1, 2, 255]);
            void Diagram(string id, string settings, string? output, string model, string catalog)
            {
                using var diagramBytes = new MemoryStream();
                using (var diagram = new ZipArchive(diagramBytes, ZipArchiveMode.Create, leaveOpen: true))
                {
                    void Leaf(string name, string xml)
                    { using var writer = new StreamWriter(diagram.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(xml); }
                    // V5 repeats the resource catalog in the top-level leaf and each diagram package.
                    Leaf("Diagram.xml", $"<Package xmlns='{XpdlNamespace}'><Participants>{catalog}</Participants>{model}</Package>");
                    Leaf("BPSimData.xml", settings);
                    if (output != null) Leaf("BPSimDataResult.xml", output);
                }
                using var target = archive.CreateEntry(id + ".diag").Open(); target.Write(diagramBytes.ToArray());
            }
            Diagram(DiagramId, simulation ?? Simulation(), results, diagramBody, diagramResources ?? resources);
            Diagram(OtherDiagramId, Simulation(Scenario("Scenario_Other")), "<ScenarioResults/>", otherDiagram, otherResources ?? resources);
        }
        return bytes.ToArray();
    }

    [Fact]
    public void ValidateAcceptsMixedExplicitChangesWithoutMutatingTheirValues()
    {
        var patch = SimulationPatch(Simulation(Scenario(content: "<Calendar id='Working' name='Shifts'>BEGIN:VCALENDAR</Calendar><ElementParameters elementRef='Activity_One'><TimeParameters><ProcessingTime><NumericParameter value='5' validFor='Working'/></ProcessingTime></TimeParameters></ElementParameters>")), discard: true);
        patch.Resources = [Upsert("Revisión – 東京", "Entity", "Unicode documentation"), new() { Id = OtherResourceId, Operation = "delete" }];
        string expected = patch.Simulations[0].Xml;
        NativeMetadataPolicy.Validate(patch);
        Assert.Equal(expected, patch.Simulations[0].Xml);
        Assert.Equal("Entity", patch.Resources[0].Type);
        Assert.True(patch.DiscardSimulationResults);
    }

    [Fact]
    public void ValidateEnforcesTransactionCountBoundariesAndNonNullCollections()
    {
        Assert.Throws<ArgumentNullException>(() => NativeMetadataPolicy.Validate(null!));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(new()));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(new() { Resources = null! }));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(new() { Resources = [Upsert()], Simulations = null! }));
        var changes = Enumerable.Range(1, 1000).Select(i => new NativeResourceChange
        { Id = new Guid(i, 1, 1, new byte[8]).ToString(), Name = "Resource " + i }).ToArray();
        NativeMetadataPolicy.Validate(new() { Resources = changes });
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(new() { Resources = [.. changes, Upsert()] }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("ABCDEF12-3456-4789-ABCD-123456789ABC")]
    [InlineData("abcdef1234564789abcd123456789abc")]
    [InlineData("{abcdef12-3456-4789-abcd-123456789abc}")]
    public void RequireIdRejectsNonCanonicalOrEmptyNativeIdentity(string id)
    {
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.RequireId(id));
        var resource = Upsert(); resource.Id = id;
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(ResourcePatch(resource)));
        var simulation = SimulationPatch(Simulation()); simulation.Simulations[0].DiagramId = id;
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(simulation));
    }

    [Theory]
    [InlineData("upsert", "", "Role", "")]
    [InlineData("upsert", " ", "Role", "")]
    [InlineData("upsert", "Name", "role", "")]
    [InlineData("upsert", "Name", "Unknown", "")]
    [InlineData("replace", "Name", "Role", "")]
    [InlineData("delete", "Ignored name", "Role", "")]
    [InlineData("delete", "", "Entity", "")]
    [InlineData("delete", "", "Role", "Ignored description")]
    public void ValidateRejectsInvalidOrIgnoredResourceValues(string operation, string name, string type, string description) =>
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(ResourcePatch(new()
        { Id = ResourceId, Operation = operation, Name = name, Type = type, Description = description })));

    [Fact]
    public void ValidateRejectsNullsDuplicateTargetsAndUnscopedResultDiscard()
    {
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(new() { Resources = [null!] }));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(new() { Simulations = [null!] }));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(new() { Resources = [Upsert(), Delete()] }));
        var patch = SimulationPatch(Simulation()); patch.Simulations = [patch.Simulations[0], patch.Simulations[0]];
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(patch));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(new() { Resources = [Upsert()], DiscardSimulationResults = true }));
        var change = Upsert(); change.Name = null!;
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(ResourcePatch(change)));
        change = Upsert(); change.Description = null!;
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(ResourcePatch(change)));
        change = Upsert(new string('a', 10001));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(ResourcePatch(change)));
        change = Upsert(description: new string('a', 1024 * 1024 + 1));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(ResourcePatch(change)));
    }

    [Theory]
    [InlineData("LevelOne")]
    [InlineData("LevelTwo")]
    [InlineData("LevelThree")]
    [InlineData("LevelFour")]
    public void ValidateSimulationAcceptsNativeLevelsAndScenarioLocalIdentities(string level)
    {
        // Element and calendar identities are unique within a scenario, not globally across scenarios.
        string content = "<ElementParameters elementRef='Activity_One'><TimeParameters><ProcessingTime><NumericParameter value='2' validFor='Working'/></ProcessingTime></TimeParameters></ElementParameters><Calendar id='Working'>BEGIN:VCALENDAR</Calendar>";
        NativeMetadataPolicy.ValidateSimulation(Simulation(Scenario(content: content) + Scenario("Scenario_Two", "replication='10000'", content), level));
        Assert.Equal(2, NativeMetadataPolicy.Read(Simulation(Scenario() + Scenario("Scenario_Two"), level)).Root!.Elements().Count());
    }

    [Theory]
    [InlineData("<BPSimData/>")]
    [InlineData("<BPSimData xmlns='http://www.bpsim.org/schemas/1.0'/>")]
    [InlineData("<BPSimData xmlns='http://www.bpsim.org/schemas/1.0' simulationLevel='LevelFive'/>")]
    [InlineData("<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'/>")]
    public void ValidateSimulationRejectsWrongRootNamespaceAndLevel(string xml) =>
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.ValidateSimulation(xml));

    [Theory]
    [InlineData("replication='0'")]
    [InlineData("replication='10001'")]
    [InlineData("replication='-1'")]
    [InlineData("replication='1.5'")]
    [InlineData("replication='not-a-number'")]
    public void ValidateSimulationRejectsInvalidReplicationCount(string parameters) =>
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.ValidateSimulation(Simulation(Scenario(parameters: parameters))));

    [Theory]
    [InlineData("<ElementParameters/>")]
    [InlineData("<ElementParameters elementRef=' '/>")]
    [InlineData("<ElementParameters elementRef='a'/><ElementParameters elementRef='a'/>")]
    [InlineData("<Calendar/>")]
    [InlineData("<Calendar id=' '/>")]
    [InlineData("<Calendar id='Working'/><Calendar id='Working'/>")]
    [InlineData("<ElementParameters elementRef='a'><NumericParameter value='1' validFor='Missing'/></ElementParameters>")]
    public void ValidateSimulationRejectsAmbiguousElementsCalendarsAndDanglingValidity(string content) =>
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.ValidateSimulation(Simulation(Scenario(content: content))));

    [Fact]
    public void ValidateSimulationRejectsMissingDuplicateAndMalformedScenarioIdsOrParameters()
    {
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.ValidateSimulation(Simulation(Scenario() + Scenario())));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.ValidateSimulation(Simulation(Scenario(""))));
        Assert.Throws<XmlException>(() => NativeMetadataPolicy.ValidateSimulation(Simulation(Scenario("prefix:Scenario"))));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.ValidateSimulation(Simulation("<Scenario id='Scenario_One'/>")));
        Assert.Throws<NotSupportedException>(() => NativeMetadataPolicy.ValidateSimulation(Simulation(Scenario(parameters: "baseTimeUnit='year'"))));
        string many = string.Concat(Enumerable.Range(0, 1001).Select(i => Scenario("Scenario_" + i)));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.ValidateSimulation(Simulation(many)));
    }

    [Fact]
    public void XmlReaderRejectsMalformedDocumentsAndExternalEntityDeclarations()
    {
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Read(null!));
        Assert.Throws<XmlException>(() => NativeMetadataPolicy.Read("<D>"));
        Assert.Throws<XmlException>(() => NativeMetadataPolicy.Read("<!DOCTYPE D [<!ENTITY local SYSTEM 'file:///not-read.txt'>]><D>&local;</D>"));
        Assert.Equal(XName.Get("D"), NativeMetadataPolicy.Read("<?xml version='1.0'?><D/>").Root!.Name);
    }

    [Fact]
    public void XmlEquivalentIgnoresKnownPrefixesDeclarationAndFormattingOnly()
    {
        string left = Simulation(Scenario());
        string right = $"<?xml version='1.0'?><bp:BPSimData xmlns:bp='{Bp}' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' simulationLevel='LevelTwo'>\n  <bp:Scenario version='1.0' author='h0w4r' name='Own scenario' id='Scenario_One'>\n    <bp:ScenarioParameters replication='2' baseTimeUnit='minute'/>\n  </bp:Scenario>\n</bp:BPSimData>";
        Assert.True(NativeMetadataPolicy.XmlEquivalent(left, right));
        Assert.False(NativeMetadataPolicy.XmlEquivalent(left, right.Replace("replication='2'", "replication='3'")));
    }

    [Theory]
    [InlineData("<D><!--keep--><C/></D>", "<D><!--lost--><C/></D>")]
    [InlineData("<D><A/><B/></D>", "<D><B/><A/></D>")]
    [InlineData("<D unknown='keep'/>", "<D unknown='lost'/>")]
    [InlineData("<D> </D>", "<D>  </D>")]
    [InlineData("<D xml:space='preserve'> <C/> </D>", "<D xml:space='preserve'>  <C/> </D>")]
    [InlineData("<D xml:space='preserve'><P> <C/> </P></D>", "<D xml:space='preserve'><P>  <C/> </P></D>")]
    [InlineData("<D xmlns:p='urn:one' reference='p:Item'/>", "<D xmlns:p='urn:two' reference='p:Item'/>")]
    [InlineData("<D><?keep original?></D>", "<D><?keep changed?></D>")]
    public void XmlEquivalentPreservesUnknownDataOrderWhitespaceAndQNameMeaning(string left, string right) =>
        Assert.False(NativeMetadataPolicy.XmlEquivalent(left, right));

    [Theory]
    [InlineData("Role", "ROLE")]
    [InlineData("Entity", "RESOURCE")]
    public void ResourceCreationRequiresMatchingDurableAndIndependentReadback(string type, string nativeType)
    {
        var change = Upsert("Created", type);
        string keep = Participant(OtherResourceId, "Keep");
        var before = Archive(keep);
        var after = Archive(keep + Participant(name: "Created", type: nativeType, description: change.Description));
        var report = NativeMetadataPolicy.Compare(before, after, ResourcePatch(change), ResourceReadback(change));
        Assert.True(report.Preserved);
        Assert.True(report.CheckedAtoms > 0);
        Assert.Equal(report.OriginalEntries, report.ResultingEntries);
        Assert.False(NativeMetadataPolicy.Compare(before, Archive(Participant(OtherResourceId, "Unexpected") + Participant(name: "Created", type: nativeType, description: change.Description)), ResourcePatch(change), ResourceReadback(change)).Preserved);
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(before, Archive(keep), ResourcePatch(change), ResourceReadback(change)));
    }

    [Fact]
    public void ResourceUpdateProjectsOnlyThreeKnownScalarsAndKeepsUnknownContent()
    {
        var change = Upsert("Updated", "Entity");
        var before = Archive(Participant(extra: "<Unknown keep='yes'/>") + Participant(OtherResourceId, "Keep"));
        var after = Archive(Participant(name: change.Name, type: "RESOURCE", description: change.Description, extra: "<Unknown keep='yes'/>") + Participant(OtherResourceId, "Keep"));
        Assert.True(NativeMetadataPolicy.Compare(before, after, ResourcePatch(change), ResourceReadback(change)).Preserved);
        after = Archive(Participant(name: change.Name, type: "RESOURCE", description: change.Description, extra: "<Unknown keep='no'/>") + Participant(OtherResourceId, "Keep"));
        var report = NativeMetadataPolicy.Compare(before, after, ResourcePatch(change), ResourceReadback(change));
        Assert.False(report.Preserved);
        Assert.Contains(report.Differences, d => d.Entry == "Participants.xml" && d.Classification == "unexpected_xml_change");
    }

    [Theory]
    [InlineData("name")]
    [InlineData("description")]
    [InlineData("type")]
    public void ResourceUpdateRejectsReadbackAndDurableValuesThatDisagreeWithIntent(string field)
    {
        var change = Upsert(); var readback = ResourceReadback(change);
        var before = Archive(Participant());
        var valid = Archive(Participant(name: change.Name, description: change.Description));
        if (field == "name") readback.Resources[0].Name = "Wrong";
        if (field == "description") readback.Resources[0].Description = "Wrong";
        if (field == "type") readback.Resources[0].Type = "Entity";
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(before, valid, ResourcePatch(change), readback));
        string invalid = Participant(name: field == "name" ? "Wrong" : change.Name,
            description: field == "description" ? "Wrong" : change.Description, type: field == "type" ? "RESOURCE" : "ROLE");
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(before, Archive(invalid), ResourcePatch(change), ResourceReadback(change)));
    }

    [Theory]
    [InlineData("<Unknown>Old documentation</Unknown>", "New documentation")]
    [InlineData("Old documentation", "<Unknown>New documentation</Unknown>")]
    [InlineData("Old documentation<!--keep-->", "New documentation")]
    [InlineData("Old documentation", "New documentation<!--added-->")]
    [InlineData("Old documentation<?keep original?>", "New documentation")]
    public void ResourceDescriptionProjectionCannotEraseUnknownMarkup(string oldDescription, string newDescription)
    {
        var change = Upsert();
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(Archive(Participant(description: oldDescription)),
            Archive(Participant(name: change.Name, description: newDescription)), ResourcePatch(change), ResourceReadback(change)));
    }

    [Fact]
    public void ResourceDeletionRequiresPriorExistenceAndAbsenceFromBothReadbacks()
    {
        string keep = Participant(OtherResourceId, "Keep");
        var before = Archive(Participant() + keep); var after = Archive(keep); var patch = ResourcePatch(Delete());
        Assert.True(NativeMetadataPolicy.Compare(before, after, patch, new()).Preserved);
        Assert.False(NativeMetadataPolicy.Compare(before, Archive(""), patch, new()).Preserved);
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(before, before, patch, new()));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(before, after, patch, ResourceReadback(Upsert())));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(after, after, patch, new()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResourceUpdateRejectsDuplicatePersistedIdentityOnEitherSide(bool duplicateBefore)
    {
        var change = Upsert(); string updated = Participant(name: change.Name, description: change.Description);
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(Archive(Participant() + (duplicateBefore ? Participant() : "")),
            Archive(updated + (duplicateBefore ? "" : updated)), ResourcePatch(change), ResourceReadback(change)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResourceUpdateRequiresEveryDiagramCatalogToAgreeWithTheModel(bool otherDiagram)
    {
        var change = Upsert(); string updated = Participant(name: change.Name, description: change.Description);
        var after = otherDiagram ? Archive(updated, otherResources: Participant()) : Archive(updated, diagramResources: Participant());
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(Archive(Participant()), after,
            ResourcePatch(change), ResourceReadback(change)));
    }

    [Fact]
    public void SimulationReplacementProjectsOnlyRequestedDiagramAndPreservesPreviousResults()
    {
        string xml = Simulation(Scenario()); string oldResults = "<ScenarioResults><Result scenarioId='Scenario_One'>Stored result</Result></ScenarioResults>";
        var before = Archive(simulation: Simulation(), results: oldResults);
        var after = Archive(simulation: xml, results: oldResults);
        var report = NativeMetadataPolicy.Compare(before, after, SimulationPatch(xml), SimulationReadback(xml));
        Assert.True(report.Preserved);
        Assert.True(report.CheckedAtoms > 0);
        Assert.False(NativeMetadataPolicy.Compare(before, Archive(simulation: xml), SimulationPatch(xml), SimulationReadback(xml)).Preserved);
        Assert.False(NativeMetadataPolicy.Compare(before, Archive(simulation: xml, results: oldResults, otherDiagram: "<Untargeted keep='no'/>"), SimulationPatch(xml), SimulationReadback(xml)).Preserved);
    }

    [Fact]
    public void SimulationReplacementRequiresExactDurableAndIndependentReadback()
    {
        string xml = Simulation(Scenario()); string different = Simulation(Scenario(parameters: "replication='3'"));
        var before = Archive(); var after = Archive(simulation: xml); var patch = SimulationPatch(xml);
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(before, after, patch, SimulationReadback(different)));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(before, Archive(simulation: different), patch, SimulationReadback(xml)));
        Assert.Throws<InvalidOperationException>(() => NativeMetadataPolicy.Compare(before, after, patch, new()));
        var duplicate = SimulationReadback(xml); duplicate.Simulations = [duplicate.Simulations[0], duplicate.Simulations[0]];
        Assert.Throws<InvalidOperationException>(() => NativeMetadataPolicy.Compare(before, after, patch, duplicate));
    }

    [Theory]
    [InlineData("<ScenarioResults><Result scenarioId='Scenario_One'>Old</Result></ScenarioResults>")]
    [InlineData(null)]
    public void ExplicitDiscardAcceptsOnlyAnEmptyResultSetAndOnlyForTargetedDiagram(string? oldResults)
    {
        string xml = Simulation(Scenario()); var patch = SimulationPatch(xml, discard: true);
        var report = NativeMetadataPolicy.Compare(Archive(results: oldResults), Archive(simulation: xml), patch, SimulationReadback(xml));
        Assert.True(report.Preserved);
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(Archive(results: oldResults),
            Archive(simulation: xml, results: "<ScenarioResults><Result scenarioId='Scenario_One'>Uncleared</Result></ScenarioResults>"), patch, SimulationReadback(xml)));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(Archive(results: oldResults), Archive(simulation: xml, results: null), patch, SimulationReadback(xml)));
    }

    [Fact]
    public void MetadataTransactionsRetainEveryUntargetedXmlAndBinaryLeaf()
    {
        string xml = Simulation(Scenario()); var change = Upsert(); var patch = SimulationPatch(xml); patch.Resources = [change];
        var reopened = ResourceReadback(change); reopened.Simulations = SimulationReadback(xml).Simulations;
        var before = Archive(Participant()); string updated = Participant(name: change.Name, description: change.Description);
        Assert.True(NativeMetadataPolicy.Compare(before, Archive(updated, xml), patch, reopened).Preserved);
        var report = NativeMetadataPolicy.Compare(before, Archive(updated, xml, attachment: [0, 1, 3, 255]), patch, reopened);
        Assert.False(report.Preserved);
        Assert.Contains(report.Differences, d => d.Entry == "attachments/keep.bin" && d.Classification == "binary_changed");
        report = NativeMetadataPolicy.Compare(before, Archive(updated, xml, extra: "<Unknown keep='yes'/><!--unrequested-->"), patch, reopened);
        Assert.False(report.Preserved);
        Assert.Contains(report.Differences, d => d.Entry == "unknown.xml" && d.Classification == "unexpected_xml_change");
    }

    private static NativeResourceAssignments Raci(string elementId = ActivityId) => new()
    {
        ElementId = elementId,
        Responsible = [ResourceId],
        Accountable = [OtherResourceId],
        Consulted = [ResourceId, OtherResourceId],
        Informed = [OtherResourceId, ResourceId]
    };
    private static NativeMetadataPatch AssignmentPatch(params NativeResourceAssignments[] assignments) => new() { Assignments = assignments };
    private static NativeMetadataSnapshot AssignmentReadback(params NativeResourceAssignments[] assignments) => new()
    {
        Assignments = assignments,
        Resources = [new() { Id = ResourceId, BpmnId = "Resource_Reviewer", Name = "Reviewer", Description = "", Type = "Role" },
            new() { Id = OtherResourceId, BpmnId = "Resource_Operations", Name = "Operations", Description = "", Type = "Entity" }]
    };
    private static void SetRole(NativeResourceAssignments assignment, string role, string[] ids)
    {
        switch (role)
        {
            case "Responsible": assignment.Responsible = ids; break;
            case "Accountable": assignment.Accountable = ids; break;
            case "Consulted": assignment.Consulted = ids; break;
            case "Informed": assignment.Informed = ids; break;
            default: throw new ArgumentOutOfRangeException(nameof(role));
        }
    }
    private static string Activity(string id = ActivityId, string performers = "", string attributes = "", string body = "") =>
        $"<WorkflowProcesses><WorkflowProcess Id='{DiagramId}'><Activities><Activity Id='{id}' Name='Own task'>{performers}<ExtendedAttributes>{attributes}</ExtendedAttributes>{body}</Activity></Activities></WorkflowProcess></WorkflowProcesses>";
    private static string AssignmentActivity(NativeResourceAssignments assignment, string extra = "")
    {
        // Native responsible values are GUIDs; ACI values are names from the independent resource catalog.
        string Name(string id) => id == ResourceId ? "Reviewer" : id == OtherResourceId ? "Operations" : throw new InvalidDataException("Unknown fixture resource.");
        // Fragments inherit the enclosing native Package namespace without adding serializer-only declarations.
        string performers = assignment.Responsible.Length == 0 ? "" : new XElement("Performers",
            assignment.Responsible.Select(id => new XElement("Performer", id))).ToString(SaveOptions.DisableFormatting);
        string Attributes(string name, string[] ids) => ids.Length == 0 ? "" : new XElement("ExtendedAttribute",
            new XAttribute("Name", name), new XAttribute("Value", string.Join(",", ids.Select(Name)))).ToString(SaveOptions.DisableFormatting);
        return Activity(assignment.ElementId, performers, Attributes("BizagiAccountables", assignment.Accountable) +
            Attributes("BizagiConsulted", assignment.Consulted) + Attributes("BizagiInformed", assignment.Informed) + extra);
    }
    private static byte[] AssignmentArchive(string diagramBody, string otherDiagram = "<Untargeted keep='yes'/>") =>
        Archive(Participant(name: "Reviewer", description: "").Replace("Resource_One", "Resource_Reviewer") +
            Participant(OtherResourceId, "Operations", "RESOURCE", "").Replace("Resource_One", "Resource_Operations"),
            diagramBody: diagramBody, otherDiagram: otherDiagram);

    [Fact]
    public void AssignmentValidationAcceptsClearSetsAndOverlappingRolesButLimitsTheWholeTransaction()
    {
        var assignment = Raci();
        NativeMetadataPolicy.Validate(AssignmentPatch(assignment, new() { ElementId = OtherActivityId }));
        var changes = Enumerable.Range(1, 1000).Select(i => new NativeResourceAssignments
        { ElementId = new Guid(i, 1, 1, new byte[8]).ToString() }).ToArray();
        NativeMetadataPolicy.Validate(AssignmentPatch(changes));
        var patch = AssignmentPatch(changes); patch.Resources = [Upsert()];
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(patch));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(new() { Resources = [Upsert()], Assignments = null! }));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(AssignmentPatch(null!)));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(AssignmentPatch([null!])));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(AssignmentPatch(assignment, assignment)));
    }

    [Theory]
    [InlineData("Responsible")]
    [InlineData("Accountable")]
    [InlineData("Consulted")]
    [InlineData("Informed")]
    public void AssignmentValidationRejectsNullDuplicateMalformedAndExcessiveRoleReferences(string role)
    {
        var assignment = Raci();
        foreach (var invalid in new string[]?[] { null, [ResourceId, ResourceId], ["not-a-guid"], ["ABCDEF12-3456-4789-ABCD-123456789ABC"],
            Enumerable.Range(1, 1001).Select(i => new Guid(i, 1, 1, new byte[8]).ToString()).ToArray() })
        {
            SetRole(assignment, role, invalid!);
            Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(AssignmentPatch(assignment)));
        }
        SetRole(assignment, role, Enumerable.Range(1, 1000).Select(i => new Guid(i, 1, 1, new byte[8]).ToString()).ToArray());
        NativeMetadataPolicy.Validate(AssignmentPatch(assignment));
        assignment.ElementId = Guid.Empty.ToString();
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Validate(AssignmentPatch(assignment)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssignmentReplacementSetsOrClearsOnlyRequestedNativeRoles(bool clear)
    {
        var populated = Raci(); var empty = new NativeResourceAssignments { ElementId = ActivityId };
        var request = clear ? empty : populated; string unrelated = Activity(OtherActivityId, body: "<Unknown keep='yes'/>");
        string before = AssignmentActivity(clear ? populated : empty) + unrelated;
        string after = AssignmentActivity(request) + unrelated;
        var report = NativeMetadataPolicy.Compare(AssignmentArchive(before), AssignmentArchive(after), AssignmentPatch(request), AssignmentReadback(request));
        Assert.True(report.Preserved);
        Assert.True(report.CheckedAtoms > 0);
        Assert.False(NativeMetadataPolicy.Compare(AssignmentArchive(before), AssignmentArchive(after.Replace("keep='yes'", "keep='no'")),
            AssignmentPatch(request), AssignmentReadback(request)).Preserved);
    }

    [Theory]
    [InlineData("Responsible")]
    [InlineData("Accountable")]
    [InlineData("Consulted")]
    [InlineData("Informed")]
    public void AssignmentReplacementRequiresExactOrderedRoleSetsInIndependentReadback(string role)
    {
        var request = Raci(); var actual = Raci();
        SetRole(actual, role, role == "Responsible" ? [OtherResourceId] : [ResourceId]);
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity()), AssignmentArchive(AssignmentActivity(request)),
            AssignmentPatch(request), AssignmentReadback(actual)));
        SetRole(actual, role, []);
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity()), AssignmentArchive(AssignmentActivity(request)),
            AssignmentPatch(request), AssignmentReadback(actual)));
    }

    [Fact]
    public void AssignmentReplacementRequiresEveryDurableResponsibleReferenceInOrder()
    {
        var request = Raci(); request.Responsible = [ResourceId, OtherResourceId];
        string valid = AssignmentActivity(request);
        string reversed = valid.Replace(ResourceId, "PLACEHOLDER").Replace(OtherResourceId, ResourceId).Replace("PLACEHOLDER", OtherResourceId);
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity()), AssignmentArchive(reversed),
            AssignmentPatch(request), AssignmentReadback(request)));
        Assert.True(NativeMetadataPolicy.Compare(AssignmentArchive(Activity()), AssignmentArchive(valid), AssignmentPatch(request), AssignmentReadback(request)).Preserved);
    }

    [Theory]
    [InlineData("BizagiAccountables")]
    [InlineData("BizagiConsulted")]
    [InlineData("BizagiInformed")]
    public void AssignmentReplacementChecksNativeAciDisplayNamesAgainstReferencedResources(string role)
    {
        var request = Raci(); var doc = XDocument.Parse($"<Root xmlns='{XpdlNamespace}'>{AssignmentActivity(request)}</Root>");
        doc.Descendants(XName.Get("ExtendedAttribute", XpdlNamespace)).Single(e => (string?)e.Attribute("Name") == role).SetAttributeValue("Value", "Unrequested name");
        string wrong = string.Concat(doc.Root!.Elements().Select(e => e.ToString(SaveOptions.DisableFormatting)));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity()), AssignmentArchive(wrong),
            AssignmentPatch(request), AssignmentReadback(request)));
        var readback = AssignmentReadback(request); readback.Resources = readback.Resources.Where(r => r.Id != OtherResourceId).ToArray();
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity()), AssignmentArchive(AssignmentActivity(request)),
            AssignmentPatch(request), readback));
    }

    [Fact]
    public void ResponsibleReferencesCannotBeAccreditedAgainstAMissingResourceCatalog()
    {
        var request = new NativeResourceAssignments { ElementId = ActivityId, Responsible = [ResourceId] };
        var readback = AssignmentReadback(request); readback.Resources = [];
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity()), AssignmentArchive(AssignmentActivity(request)),
            AssignmentPatch(request), readback));
    }

    [Fact]
    public void AssignmentReadbackRejectsAmbiguousCatalogIdentityAndReorderedAciSets()
    {
        var request = Raci(); var readback = AssignmentReadback(request);
        readback.Resources = [.. readback.Resources, readback.Resources[0]];
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity()), AssignmentArchive(AssignmentActivity(request)),
            AssignmentPatch(request), readback));
        var actual = Raci(); actual.Consulted = [OtherResourceId, ResourceId];
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity()), AssignmentArchive(AssignmentActivity(request)),
            AssignmentPatch(request), AssignmentReadback(actual)));
    }

    [Theory]
    [InlineData("<Performers><Performer>not-a-guid</Performer></Performers>")]
    [InlineData("<Performers><Performer>00000000-0000-0000-0000-000000000000</Performer></Performers>")]
    [InlineData("<Performers><Performer>ABCDEF12-3456-4789-ABCD-123456789ABC</Performer></Performers>")]
    public void ClearingAssignmentsCannotHideMalformedExistingResponsibleReferences(string performers)
    {
        var request = new NativeResourceAssignments { ElementId = ActivityId };
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity(performers: performers)),
            AssignmentArchive(AssignmentActivity(request)), AssignmentPatch(request), AssignmentReadback(request)));
    }

    [Fact]
    public void AssignmentProjectionCannotEraseMeaningfulWhitespaceFromInheritedPerformerXmlSpace()
    {
        var request = new NativeResourceAssignments { ElementId = ActivityId };
        string before = Activity(performers: $"<Performers> <Performer>{ResourceId}</Performer> </Performers>").Replace("<Activity ", "<Activity xml:space='preserve' ");
        string after = AssignmentActivity(request).Replace("<Activity ", "<Activity xml:space='preserve' ");
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(before), AssignmentArchive(after),
            AssignmentPatch(request), AssignmentReadback(request)));
    }

    [Theory]
    [InlineData("<Performers unknown='keep'><Performer>abcdef12-3456-4789-abcd-123456789abc</Performer></Performers>")]
    [InlineData("<Performers><Performer unknown='keep'>abcdef12-3456-4789-abcd-123456789abc</Performer></Performers>")]
    [InlineData("<Performers><!--keep--><Performer>abcdef12-3456-4789-abcd-123456789abc</Performer></Performers>")]
    [InlineData("<Performers><Performer>abcdef12-3456-4789-abcd-123456789abc<!--keep--></Performer></Performers>")]
    [InlineData("<Performers><Performer>abcdef12-3456-4789-abcd-123456789abc</Performer><Unknown/></Performers>")]
    public void AssignmentProjectionCannotEraseUnknownPerformerAttributesCommentsOrSiblings(string performers)
    {
        var request = new NativeResourceAssignments { ElementId = ActivityId, Responsible = [ResourceId] };
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity(performers: performers)),
            AssignmentArchive(AssignmentActivity(request)), AssignmentPatch(request), AssignmentReadback(request)));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity()),
            AssignmentArchive(Activity(performers: performers)), AssignmentPatch(request), AssignmentReadback(request)));
    }

    [Theory]
    [InlineData("<ExtendedAttribute Name='BizagiAccountables' Value='Operations' unknown='keep'/>")]
    [InlineData("<ExtendedAttribute Name='BizagiAccountables' Value='Operations'><!--keep--></ExtendedAttribute>")]
    [InlineData("<ExtendedAttribute Name='BizagiAccountables' Value='Operations'><Unknown/></ExtendedAttribute>")]
    [InlineData("<ExtendedAttribute Name='BizagiAccountables' Value='Operations'/><ExtendedAttribute Name='BizagiAccountables' Value='Operations'/>")]
    public void AssignmentProjectionRejectsUnknownOrDuplicateAciContentOnEitherSide(string attributes)
    {
        var request = new NativeResourceAssignments { ElementId = ActivityId, Accountable = [OtherResourceId] };
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity(attributes: attributes)),
            AssignmentArchive(AssignmentActivity(request)), AssignmentPatch(request), AssignmentReadback(request)));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity()),
            AssignmentArchive(Activity(attributes: attributes)), AssignmentPatch(request), AssignmentReadback(request)));
    }

    [Fact]
    public void AssignmentProjectionPreservesUnrelatedExtendedAttributesContainerMetadataAndComments()
    {
        var request = Raci(); const string extra = "<ExtendedAttribute Name='Unknown' Value='keep'/><!--keep comment-->";
        string before = Activity(attributes: extra).Replace("<ExtendedAttributes>", "<ExtendedAttributes unknown='keep'>");
        string after = AssignmentActivity(request, extra).Replace("<ExtendedAttributes>", "<ExtendedAttributes unknown='keep'>");
        Assert.True(NativeMetadataPolicy.Compare(AssignmentArchive(before), AssignmentArchive(after), AssignmentPatch(request), AssignmentReadback(request)).Preserved);
        foreach (string changed in new[] { after.Replace("unknown='keep'", "unknown='lost'"), after.Replace("keep comment", "lost comment"), after.Replace("Value='keep'", "Value='lost'") })
            Assert.False(NativeMetadataPolicy.Compare(AssignmentArchive(before), AssignmentArchive(changed), AssignmentPatch(request), AssignmentReadback(request)).Preserved);
    }

    [Fact]
    public void AssignmentProjectionPreservesUnknownNestedWhitespaceWithInheritedXmlSpace()
    {
        var request = Raci();
        string Unknown(string whitespace) => $"<Unknown xml:space='preserve'><ExtendedAttributes>{whitespace}<UnknownValue/>{whitespace}</ExtendedAttributes></Unknown>";
        string before = Activity(body: Unknown(" "));
        string after = AssignmentActivity(request).Replace("</Activity>", Unknown("  ") + "</Activity>");
        Assert.False(NativeMetadataPolicy.Compare(AssignmentArchive(before), AssignmentArchive(after), AssignmentPatch(request), AssignmentReadback(request)).Preserved);
    }

    [Fact]
    public void MultipleAssignmentChangesInOneDiagramRetainEarlierProjectionsAndUntargetedValues()
    {
        var first = Raci(); var second = Raci(OtherActivityId); second.Responsible = [OtherResourceId]; second.Accountable = [];
        string before = Activity() + Activity(OtherActivityId);
        string after = AssignmentActivity(first) + AssignmentActivity(second);
        var report = NativeMetadataPolicy.Compare(AssignmentArchive(before), AssignmentArchive(after), AssignmentPatch(first, second), AssignmentReadback(first, second));
        Assert.True(report.Preserved);
        Assert.False(NativeMetadataPolicy.Compare(AssignmentArchive(before), AssignmentArchive(after.Replace("Name='Own task'", "Name='Unrequested'")),
            AssignmentPatch(first, second), AssignmentReadback(first, second)).Preserved);
    }

    [Fact]
    public void AssignmentTargetsMustExistExactlyOnceAcrossNativeDiagramsAndReadback()
    {
        var request = Raci(); var patch = AssignmentPatch(request); string updated = AssignmentActivity(request);
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(""), AssignmentArchive(updated), patch, AssignmentReadback(request)));
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity(), Activity()), AssignmentArchive(updated, updated), patch, AssignmentReadback(request)));
        Assert.Throws<InvalidOperationException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity() + Activity()), AssignmentArchive(updated + updated), patch, AssignmentReadback(request)));
        Assert.Throws<InvalidOperationException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity()), AssignmentArchive(updated), patch, AssignmentReadback()));
        Assert.Throws<InvalidOperationException>(() => NativeMetadataPolicy.Compare(AssignmentArchive(Activity()), AssignmentArchive(updated), patch, AssignmentReadback(request, request)));
    }
}
