using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Synthetic policy tests, not accreditation of native simulation or persistence.</summary>
public sealed class NativeSavedSimulationPolicyTests
{
    private const string Diagram = "00000000-0000-4000-8000-000000000001";
    private static readonly byte[] Result = Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='utf-8'?><model><element name='Task_A'/></model>");
    private static string Container(params (string Id, string Xml)[] records) => new XElement("ScenarioResults",
        records.Select(r => new XElement("Result", new XAttribute("scenarioId", r.Id), r.Xml))).ToString();
    private static byte[] Archive(string? results = null, string opaque = "retain", string configExtra = "")
    {
        byte[] Zip(params (string Name, byte[] Bytes)[] entries)
        {
            using var bytes = new MemoryStream();
            using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
                foreach (var entry in entries) { using var stream = zip.CreateEntry(entry.Name).Open(); stream.Write(entry.Bytes); }
            return bytes.ToArray();
        }
        var config = $"<BPSimData xmlns='{NativeMetadataPolicy.BpsimNamespace}' simulationLevel='LevelThree'><Scenario id='First'/><Scenario id='Second'/>{configExtra}</BPSimData>";
        var entries = new List<(string, byte[])> { ("BPSimData.xml", Encoding.UTF8.GetBytes(config)), ("opaque.bin", Encoding.UTF8.GetBytes(opaque)) };
        if (results != null) entries.Add(("BPSimDataResult.xml", Encoding.UTF8.GetBytes(results)));
        return Zip(("ModelInfo.xml", Encoding.UTF8.GetBytes("<Model/>")), (Diagram + ".diag", Zip(entries.ToArray())));
    }
    private static EngineReply Reply(NativeSavedSimulationPlan plan) => new() { SavedSimulationResults = plan.ExpectedReadback };

    [Theory] [InlineData(true)] [InlineData(false)]
    public void ExactCurrentResultIsSavedAndOtherScenarioPayloadRemainsOpaque(bool replace)
    {
        string other = "<?xml version='1.0'?><old> Ω &amp; text </old>";
        byte[] original = Archive(replace ? Container(("First", "old first"), ("Second", other)) : Container(("Second", other)));
        var plan = NativeSavedSimulationPolicy.Prepare(original, Diagram, "First", Result);
        Assert.Equal(replace, plan.PreviousResult != null);
        // Native writers may order result records by scenario collection order.
        var after = Archive(Container(("First", NativeSimulationResultPayload.Read(Result)), ("Second", other)));
        Assert.True(NativeSavedSimulationPolicy.Compare(original, after, plan, Reply(plan), Reply(plan)).Preserved);
    }

    [Fact]
    public void AbsentOriginalResultLeafCanBeCreatedExplicitly()
    {
        var original = Archive(); var plan = NativeSavedSimulationPolicy.Prepare(original, Diagram, "First", Result);
        var after = Archive(Container(("First", NativeSimulationResultPayload.Read(Result))));
        Assert.True(NativeSavedSimulationPolicy.Compare(original, after, plan, Reply(plan), Reply(plan)).Preserved);
    }

    [Theory] [InlineData("First")] [InlineData("Second")]
    public void UnexpectedResultPayloadChangeRejects(string id)
    {
        var original = Archive(Container(("Second", "retain"))); var plan = NativeSavedSimulationPolicy.Prepare(original, Diagram, "First", Result);
        var after = Archive(Container(("First", id == "First" ? "tampered" : NativeSimulationResultPayload.Read(Result)), ("Second", id == "Second" ? "tampered" : "retain")));
        Assert.Throws<InvalidDataException>(() => NativeSavedSimulationPolicy.Compare(original, after, plan, Reply(plan), Reply(plan)));
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public void BothNativePropertyReadbacksMustMatch(bool editor)
    {
        var original = Archive(); var plan = NativeSavedSimulationPolicy.Prepare(original, Diagram, "First", Result);
        var after = Archive(Container(("First", NativeSimulationResultPayload.Read(Result))));
        Assert.Throws<InvalidDataException>(() => NativeSavedSimulationPolicy.Compare(original, after, plan,
            editor ? new EngineReply() : Reply(plan), editor ? Reply(plan) : new EngineReply()));
    }

    [Fact]
    public void UnrelatedNativeBytesRemainInTheFullArchiveGate()
    {
        var original = Archive(); var plan = NativeSavedSimulationPolicy.Prepare(original, Diagram, "First", Result);
        var after = Archive(Container(("First", NativeSimulationResultPayload.Read(Result))), opaque: "changed");
        Assert.False(NativeSavedSimulationPolicy.Compare(original, after, plan, Reply(plan), Reply(plan)).Preserved);
    }

    [Theory]
    [InlineData("<ScenarioResults extra='retain'/>")]
    [InlineData("<!--retain--><ScenarioResults/>")]
    [InlineData("<ScenarioResults><!--retain--></ScenarioResults>")]
    [InlineData("<ScenarioResults><Result scenarioId='Unknown'>text</Result></ScenarioResults>")]
    [InlineData("<ScenarioResults><Result scenarioId='First' extra='retain'>text</Result></ScenarioResults>")]
    [InlineData("<ScenarioResults><Result scenarioId='First'><model/></Result></ScenarioResults>")]
    [InlineData("<ScenarioResults><Result scenarioId='First'>one</Result><Result scenarioId='First'>two</Result></ScenarioResults>")]
    [InlineData("<ScenarioResults><Result scenarioId='First'/></ScenarioResults>")]
    public void UnrepresentedContainersRejectBeforeNativeExecution(string xml) =>
        Assert.Throws<InvalidDataException>(() => NativeSavedSimulationPolicy.ValidateTarget(Archive(xml), Diagram, "First"));

    [Theory]
    [InlineData("<!--retain--><ScenarioResults/>")]
    [InlineData("<ScenarioResults><Result scenarioId='First'>one</Result><Result scenarioId='First'>two</Result></ScenarioResults>")]
    [InlineData("<ScenarioResults><Result scenarioId='Missing'>retain</Result></ScenarioResults>")]
    public void MetadataReplacementCannotBypassSharedResultRetirementGuard(string xml)
    {
        string config = $"<BPSimData xmlns='{NativeMetadataPolicy.BpsimNamespace}' simulationLevel='LevelThree'><Scenario id='First'><ScenarioParameters/></Scenario></BPSimData>";
        var patch = new NativeMetadataPatch { Simulations = [new() { DiagramId = Diagram, Xml = config }], DiscardSimulationResults = true };
        Assert.Throws<InvalidDataException>(() => NativeMetadataPolicy.Compare(Archive(xml), Archive("<ScenarioResults/>"), patch, new()));
    }

    [Theory] [InlineData("")] [InlineData("Missing")]
    public void ExplicitExistingScenarioIsRequired(string id) =>
        Assert.Throws<InvalidDataException>(() => NativeSavedSimulationPolicy.ValidateTarget(Archive(), Diagram, id));

    [Fact]
    public void NativeByteEncodingIsHonoredWithoutTreatingItAsUtf8()
    {
        byte[] bytes = Encoding.Latin1.GetBytes("<?xml version='1.0' encoding='iso-8859-1'?><model label='café'>\n <element name='été'/>\n</model>");
        string actual = NativeSimulationResultPayload.Read(bytes);
        Assert.Contains("café", actual); Assert.Contains("été", actual); Assert.DoesNotContain('\uFFFD', actual);
        var document = new XmlDocument { XmlResolver = null }; using var input = new MemoryStream(bytes); document.Load(input);
        Assert.Equal(document.OuterXml, actual);
    }

    [Theory] [InlineData("<wrong/>")] [InlineData("<model xmlns='urn:wrong'/>")]
    public void NonNativeResultsAreRejected(string xml) =>
        Assert.Throws<InvalidDataException>(() => NativeSimulationResultPayload.Read(Encoding.UTF8.GetBytes(xml)));

    [Fact]
    public void ExternalEntitiesAreNeverResolved() => Assert.Throws<XmlException>(() => NativeSimulationResultPayload.Read(
        Encoding.UTF8.GetBytes("<!DOCTYPE model [<!ENTITY x SYSTEM 'file:///unavailable'>]><model>&x;</model>")));
}
