using System.Text;
using System.Xml.Linq;
using McpBizagi.Contracts;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Pure migration policy tests. Synthetic BPSim records never accredit the installed engine.</summary>
public sealed class NativeSimulationMigrationTests
{
    private static readonly XNamespace Ns = NativeMetadataPolicy.BpsimNamespace;
    private static string Id(int n) => $"00000000-0000-4000-8000-{n:000000000000}";
    private static XElement Global() => new(Ns + "ScenarioParameters", new XAttribute("baseTimeUnit", "min"), new XAttribute("baseCurrencyUnit", "USD"), new XElement(Ns + "PropertyParameters"));
    private static XElement Record(string reference, string kind, int n) => new(Ns + "ElementParameters", new XAttribute("elementRef", reference),
        new XElement(Ns + kind, new XElement(Ns + (kind == "TimeParameters" ? "ProcessingTime" : "Quantity"),
            new XElement(Ns + "NumericParameter", new XAttribute("value", n)))), new XElement(Ns + "PropertyParameters"));
    private static XElement Scenario(string id, bool configured) => new(Ns + "Scenario", new XAttribute("id", id), Global(),
        configured ? new object[] { Record("Activity_task", "TimeParameters", 3), Record("Resource_reviewer", "ResourceParameters", 2),
            new XElement(Ns + "Calendar", new XAttribute("id", "Work"), new XAttribute("name", "Work Ω"), "calendar content") } : []);
    private static NativeScenarioMapping Map(string from = "Source", string to = "Target") => new() { SourceDiagramId = Id(1), SourceScenarioId = from, TargetDiagramId = Id(4), TargetScenarioId = to };
    private static NativeSimulationMigration Intent => new() { Mappings = [Map()], CopyMissingDependencies = true };
    private static (EngineReply Source, NativeElement[] Expected, Dictionary<string, byte[]> Archive) Fixture(XElement? source = null, XElement? target = null)
    {
        XElement Root(params XElement[] scenes) => new(Ns + "BPSimData", new XAttribute("simulationLevel", "LevelThree"), scenes);
        var metadata = new NativeMetadataSnapshot { Resources = [new() { Id = Id(9), BpmnId = "Resource_reviewer" }], Simulations = [
            new() { DiagramId = Id(1), Xml = (source ?? Root(Scenario("Source", true))).ToString() },
            new() { DiagramId = Id(4), Xml = (target ?? Root(Scenario("Target", false))).ToString() }] };
        var graph = new[] { new NativeElement { Id = Id(7), DiagramId = Id(1), ParentId = Id(3), Kind = "Task", BpmnId = "Activity_task" } };
        var expected = new[] { new NativeElement { Id = Id(7), DiagramId = Id(4), ParentId = Id(6), Kind = "Task", BpmnId = "Activity_task" } };
        return (new EngineReply { Elements = graph, Metadata = metadata }, expected,
            metadata.Simulations.ToDictionary(s => s.DiagramId + ".diag!/BPSimData.xml", s => Encoding.UTF8.GetBytes(s.Xml)));
    }
    private static NativeSimulationMigrationPlan Prepare((EngineReply Source, NativeElement[] Expected, Dictionary<string, byte[]> Archive) f, NativeSimulationMigration? intent) =>
        NativeSimulationMigrationPolicy.Prepare(f.Archive, f.Source, f.Expected, intent);
    [Fact]
    public void ParameterMovesAndDependencyCopiesRetainTheirCompleteNativeRecords()
    {
        var f = Fixture(); var original = f.Source.Metadata!.Simulations[0].Xml; var plan = Prepare(f, Intent);
        Assert.Equal(["Activity_task"], plan.Transfers.Single().ElementRefs);
        Assert.Equal(["Resource_reviewer"], plan.Transfers.Single().ResourceRefsToCopy); Assert.Equal(["Work"], plan.Transfers.Single().CalendarIdsToCopy);
        var source = XDocument.Parse(plan.ExpectedConfigurations.Single(c => c.DiagramId == Id(1)).Xml);
        var target = XDocument.Parse(plan.ExpectedConfigurations.Single(c => c.DiagramId == Id(4)).Xml);
        Assert.Single(source.Descendants(Ns + "ElementParameters")); Assert.Equal(2, target.Descendants(Ns + "ElementParameters").Count());
        Assert.True(XNode.DeepEquals(source.Descendants(Ns + "Calendar").Single(), target.Descendants(Ns + "Calendar").Single()));
        Assert.Equal(original, f.Source.Metadata.Simulations[0].Xml); Assert.Empty(plan.DiscardResultDiagrams);
    }
    [Fact] public void MissingMigrationDoesNotSilentlyDiscardConfiguredParameters() => Assert.Throws<NotSupportedException>(() => Prepare(Fixture(), null));
    [Fact] public void DependencyCopyRequiresExplicitIntent()
    { var intent = Intent; intent.CopyMissingDependencies = false; Assert.Throws<InvalidDataException>(() => Prepare(Fixture(), intent)); }
    [Theory] [InlineData("calendar")] [InlineData("resource")] [InlineData("units")] [InlineData("vendor")]
    public void ConflictingTargetContextRejectsWithoutOverwrite(string kind)
    {
        var target = Scenario("Target", false);
        if (kind == "calendar") target.Add(new XElement(Ns + "Calendar", new XAttribute("id", "Work"), "different"));
        if (kind == "resource") target.Add(Record("Resource_reviewer", "ResourceParameters", 99));
        if (kind == "units") target.Element(Ns + "ScenarioParameters")!.SetAttributeValue("baseTimeUnit", "hour");
        if (kind == "vendor") target.Add(new XElement(Ns + "VendorExtension", new XAttribute("name", "Different")));
        var f = Fixture(target: new XElement(Ns + "BPSimData", new XAttribute("simulationLevel", "LevelThree"), target));
        Assert.Throws<InvalidDataException>(() => Prepare(f, Intent));
    }
    [Fact]
    public void EqualDependenciesAreReusedWithoutCopyPermission()
    {
        var target = Scenario("Target", true); target.Elements(Ns + "ElementParameters").First().Remove();
        var f = Fixture(target: new XElement(Ns + "BPSimData", new XAttribute("simulationLevel", "LevelThree"), target));
        var intent = Intent; intent.CopyMissingDependencies = false;
        var plan = Prepare(f, intent); Assert.Empty(plan.Transfers.Single().ResourceRefsToCopy); Assert.Empty(plan.Transfers.Single().CalendarIdsToCopy);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void InheritedEffectiveParametersRequireMappedInheritance(bool complete)
    {
        XElement Root(params XElement[] scenarios) => new(Ns + "BPSimData", new XAttribute("simulationLevel", "LevelThree"), scenarios);
        var child = Scenario("Child", false); child.SetAttributeValue("inherits", "Source");
        var targetChild = Scenario("TargetChild", false); targetChild.SetAttributeValue("inherits", "Target");
        var f = Fixture(Root(Scenario("Source", true), child), Root(Scenario("Target", false), targetChild));
        var intent = Intent; if (complete) intent.Mappings = [Map(), Map("Child", "TargetChild")];
        if (!complete) { Assert.Throws<InvalidDataException>(() => Prepare(f, intent)); return; }
        var plan = Prepare(f, intent); Assert.Equal(2, plan.Transfers.Length); Assert.Empty(plan.Transfers[1].ElementRefs);
        Assert.Equal("Target", (string?)XDocument.Parse(plan.ExpectedConfigurations.Single(c => c.DiagramId == Id(4)).Xml).Descendants(Ns + "Scenario").Last().Attribute("inherits"));
    }
    [Fact]
    public void UnknownDurableSourceContentCannotBeHiddenByReadback()
    {
        var f = Fixture(); f.Archive[Id(1) + ".diag!/BPSimData.xml"] = Encoding.UTF8.GetBytes(f.Source.Metadata!.Simulations[0].Xml.Replace("<Scenario ", "<!--Unknown--><Scenario "));
        Assert.Throws<InvalidDataException>(() => Prepare(f, Intent));
    }
    [Fact]
    public void ResultRetirementIsExplicitAndCoversAffectedDiagrams()
    { var intent = Intent; intent.DiscardSimulationResults = true; Assert.Equal([Id(1), Id(4)], Prepare(Fixture(), intent).DiscardResultDiagrams); }
    [Fact]
    public void TargetCannotIntroduceAnOverrideAbsentFromInheritedSource()
    {
        XElement Root(params XElement[] scenes) => new(Ns + "BPSimData", new XAttribute("simulationLevel", "LevelThree"), scenes);
        var child = Scenario("Child", false); child.SetAttributeValue("inherits", "Source");
        var targetChild = Scenario("TargetChild", false); targetChild.SetAttributeValue("inherits", "Target"); targetChild.Add(Record("Activity_task", "TimeParameters", 99));
        var f = Fixture(Root(Scenario("Source", true), child), Root(Scenario("Target", false), targetChild));
        var intent = Intent; intent.Mappings = [Map(), Map("Child", "TargetChild")];
        Assert.Throws<InvalidDataException>(() => Prepare(f, intent));
    }
    [Theory]
    [InlineData("<ScenarioResults unknown='preserve'/>")]
    [InlineData("<ScenarioResults><!--preserve--></ScenarioResults>")]
    [InlineData("<!--preserve--><ScenarioResults/>")]
    [InlineData("<?preserve content?><ScenarioResults/>")]
    [InlineData("<ScenarioResults><Result scenarioId='Source'>one</Result><Result scenarioId='Source'>two</Result></ScenarioResults>")]
    [InlineData("<ScenarioResults><Unknown/></ScenarioResults>")]
    [InlineData("<ScenarioResults><Result scenarioId='Missing'>opaque</Result></ScenarioResults>")]
    [InlineData("<ScenarioResults><Result scenarioId='Source'><Unknown/></Result></ScenarioResults>")]
    public void ResultRetirementCannotDiscardUnrepresentedContainerContent(string xml)
    {
        var f = Fixture(); f.Archive[Id(1) + ".diag!/BPSimDataResult.xml"] = Encoding.UTF8.GetBytes(xml);
        var intent = Intent; intent.DiscardSimulationResults = true;
        Assert.Throws<InvalidDataException>(() => Prepare(f, intent));
    }

    [Theory] [InlineData("id")] [InlineData("inherits")] [InlineData("result")] [InlineData("name")]
    public void NamespacedIdentityLookalikesRemainProtectedScenarioContext(string localName)
    {
        var target = Scenario("Target", false);
        target.SetAttributeValue(XName.Get(localName, "urn:independent-vendor"), "preserve");
        var f = Fixture(target: new XElement(Ns + "BPSimData", new XAttribute("simulationLevel", "LevelThree"), target));
        Assert.Throws<InvalidDataException>(() => Prepare(f, Intent));
    }

    [Theory] [InlineData("editor")] [InlineData("reader")] [InlineData("archive")]
    public void ComparisonCannotMaskUnrequestedParameterChanges(string changedSurface)
    {
        var f = Fixture(); var plan = Prepare(f, Intent);
        EngineReply Reply() => new() { Metadata = new() { Simulations = plan.ExpectedConfigurations.Select(c =>
            new NativeSimulationConfiguration { DiagramId = c.DiagramId, Xml = c.Xml }).ToArray() } };
        var editor = Reply(); var reader = Reply();
        var right = plan.ExpectedConfigurations.ToDictionary(c => c.DiagramId + ".diag!/BPSimData.xml", c => Encoding.UTF8.GetBytes(c.Xml));
        // Change one already-verified field on each independent evidence surface.
        string Tamper(string xml) => xml.Replace("value=\"3\"", "value=\"999\"", StringComparison.Ordinal);
        if (changedSurface == "archive")
        {
            string key = Id(4) + ".diag!/BPSimData.xml";
            right[key] = Encoding.UTF8.GetBytes(Tamper(Encoding.UTF8.GetString(right[key])));
        }
        else
        {
            var config = (changedSurface == "editor" ? editor : reader).Metadata!.Simulations.Single(c => c.DiagramId == Id(4));
            config.Xml = Tamper(config.Xml);
        }
        Assert.Throws<InvalidDataException>(() => NativeSimulationMigrationPolicy.Project(f.Archive, right, plan, editor, reader));
    }
}
