using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using McpBizagi.Contracts;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Adversarial tests of the extraction comparison policy, not installed-engine accreditation.</summary>
public sealed class NativeExtractionTests
{
    private const string D = "11111111-1111-4111-8111-111111111111", Pool = "22222222-2222-4222-8222-222222222222", P = "33333333-3333-4333-8333-333333333333";
    private const string Sub = "44444444-4444-4444-8444-444444444444", Child = "55555555-5555-4555-8555-555555555555";
    private const string Target = "66666666-6666-4666-8666-666666666666", NewPool = "77777777-7777-4777-8777-777777777777", NewProcess = "88888888-8888-4888-8888-888888888888";
    private const string N = "http://www.wfmc.org/2009/XPDL2.2";
    private static readonly NativeSubProcessExtraction Request = new() { ElementId = Sub, NewDiagramName = "Reusable Ω" };
    private static NativeElement E(string id, string kind, string parent, string diagram = D) => new() {
        Id = id, Kind = kind, ElementType = kind, ParentId = parent, DiagramId = diagram };

    private static (EngineReply Before, EngineReply Edited, EngineReply After) Snapshots()
    {
        NativeElement[] before = [E(D, "Collaboration", ""), E(Pool, "Participant", D), E(P, "Process", Pool), E(Sub, "SubProcess", P), E(Child, "UserTask", Sub)];
        before[3].SubProcess = new();
        var after = JsonSerializer.Deserialize<NativeElement[]>(JsonSerializer.Serialize(before))!.ToList();
        after[3].Kind = "CallActivity"; after[3].ElementType = "CallActivity"; after[3].SubProcess = null;
        after[3].CallReference = new() { CatalogProcessId = NewProcess, BpmnName = NewProcess };
        after[4].ParentId = NewProcess; after[4].DiagramId = Target;
        var diagram = E(Target, "Collaboration", "", Target); diagram.Name = Request.NewDiagramName;
        var pool = E(NewPool, "Participant", Target, Target); pool.IsMainParticipant = true;
        after.AddRange([diagram, pool, E(NewProcess, "Process", NewPool, Target)]);
        return (new() { Elements = before }, new() { Extraction = new() { ElementId = Sub, SourceDiagramId = D, TargetDiagramId = Target,
            TargetParticipantId = NewPool, TargetProcessId = NewProcess, MovedElementIds = [Child] } }, new() { Elements = after.ToArray() });
    }

    private static Dictionary<string, byte[]> Entries(bool after)
    {
        string Activity(string id, string inner) => $"<Activity Id='{id}'>{inner}</Activity>";
        string child = Activity(Child, "<Implementation><Task><TaskUser/></Task></Implementation><Documentation>日本語 Ω</Documentation>");
        string body = after ? Activity(Sub, $"<Implementation><SubFlow Id='{NewProcess}'/></Implementation>") : Activity(Sub, $"<BlockActivity ActivitySetId='{Sub}'/>");
        string sets = after ? "" : $"<ActivitySets><ActivitySet Id='{Sub}' Name='Sub'><Associations/><Artifacts/><Activities>{child}</Activities><Transitions/></ActivitySet></ActivitySets>";
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        void Add(string key, string xml) => result.Add(key, Encoding.UTF8.GetBytes(xml));
        Add("ModelInfo.xml", "<ModelInfo/>");
        Add(D + ".diag!/Diagram.xml", $"<Package xmlns='{N}' Id='{D}'><Pools><Pool Id='{Pool}' Process='{P}'/></Pools><WorkflowProcesses><WorkflowProcess Id='{P}'>{sets}<Activities>{body}</Activities></WorkflowProcess></WorkflowProcesses></Package>");
        Add(D + ".diag!/ExtendedAttributeValues.xml", "<DiagramAttributeValues/>");
        Add((after ? Target : D) + ".diag!/Files/" + Child + "/owned.bin", "original attachment bytes");
        Add("unknown.bin", "unrelated native payload");
        if (after)
        {
            Add(Target + ".diag!/Diagram.xml", $"<Package xmlns='{N}' Id='{Target}' Name='Reusable Ω'><Pools><Pool Id='{NewPool}' Process='{NewProcess}'/></Pools><WorkflowProcesses><WorkflowProcess Id='{NewProcess}'><Activities>{child}</Activities></WorkflowProcess></WorkflowProcesses></Package>");
            Add(Target + ".diag!/ExtendedAttributeValues.xml", "<DiagramAttributeValues/>");
        }
        return result;
    }

    private static byte[] Archive(Dictionary<string, byte[]> entries)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in entries.Where(e => !e.Key.Contains("!/"))) { using var stream = zip.CreateEntry(entry.Key).Open(); stream.Write(entry.Value); }
            foreach (var group in entries.Where(e => e.Key.Contains("!/")).GroupBy(e => e.Key.Split("!/")[0]))
            {
                using var bytes = new MemoryStream();
                using (var nested = new ZipArchive(bytes, ZipArchiveMode.Create, true))
                    foreach (var entry in group) { using var stream = nested.CreateEntry(entry.Key.Split("!/")[1]).Open(); stream.Write(entry.Value); }
                using var destination = zip.CreateEntry(group.Key).Open(); destination.Write(bytes.ToArray());
            }
        }
        return output.ToArray();
    }

    private static NativeFidelityReport Compare(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
    {
        var snapshots = Snapshots(); return NativeExtractionPolicy.Compare(Archive(before), Archive(after), Request, snapshots.Before, snapshots.Edited, snapshots.After);
    }

    private static void Reject(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
    {
        try { Assert.False(Compare(before, after).Preserved); }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException) { /* Explicit rejection is a valid failed gate. */ }
    }

    [Fact] public void ExactRelocationPreservesOriginalNativePayload() => Assert.True(Compare(Entries(false), Entries(true)).Preserved);

    [Theory]
    [InlineData("unknown.bin")]
    [InlineData(Target + ".diag!/Files/" + Child + "/owned.bin")]
    public void ChangedBytesReject(string entry)
    {
        var after = Entries(true); after[entry] = Encoding.UTF8.GetBytes("changed"); Reject(Entries(false), after);
    }

    [Fact] public void LostAttachmentRejects()
    {
        var after = Entries(true); after.Remove(Target + ".diag!/Files/" + Child + "/owned.bin"); Reject(Entries(false), after);
    }

    [Fact] public void ChangedNestedXmlRejects()
    {
        var after = Entries(true); string key = Target + ".diag!/Diagram.xml";
        after[key] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(after[key]).Replace("日本語 Ω", "lost user content")); Reject(Entries(false), after);
    }

    [Fact] public void UnknownSourceSetAttributeRejects()
    {
        var before = Entries(false); string key = D + ".diag!/Diagram.xml";
        before[key] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(before[key]).Replace("Name='Sub'", "Name='Sub' Unknown='must not disappear'")); Reject(before, Entries(true));
    }

    [Fact] public void GeneratedUnknownFileRejects()
    {
        var after = Entries(true); after.Add(Target + ".diag!/unexpected.bin", [1, 2, 3]); Reject(Entries(false), after);
    }

    [Fact] public void MismatchedNativeProcessReferenceRejects()
    {
        var after = Entries(true); string key = Target + ".diag!/Diagram.xml";
        after[key] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(after[key]).Replace("Process='" + NewProcess + "'", "Process='" + P + "'")); Reject(Entries(false), after);
    }

    [Fact] public void MissingActualDescendantCannotPassReceiptValidation()
    {
        var snapshots = Snapshots(); snapshots.Edited.Extraction!.MovedElementIds = [];
        Assert.Throws<InvalidDataException>(() => NativeExtractionPolicy.Verify(snapshots.Before.Elements, snapshots.After.Elements, Request, snapshots.Edited.Extraction));
    }

    [Fact] public void NamespaceChangesAreNotHiddenByComparisonReconstruction()
    {
        var before = Entries(false); var after = Entries(true);
        before[D + ".diag!/ExtendedAttributeValues.xml"] = Encoding.UTF8.GetBytes("<DiagramAttributeValues xmlns:custom='urn:original'/>");
        after[D + ".diag!/ExtendedAttributeValues.xml"] = Encoding.UTF8.GetBytes("<DiagramAttributeValues xmlns:custom='urn:changed'/>");
        after[Target + ".diag!/ExtendedAttributeValues.xml"] = before[D + ".diag!/ExtendedAttributeValues.xml"];
        Reject(before, after);
    }

    [Fact] public void ConfiguredChildSimulationNeedsExplicitMigration()
    {
        var snapshots = Snapshots(); snapshots.Before.Metadata = new() { Simulations = [new() { DiagramId = D,
            Xml = "<BPSimData><Scenario><ElementParameters elementRef='" + Child + "'/></Scenario></BPSimData>" }] };
        snapshots.Before.Documentation = new();
        Assert.Throws<NotSupportedException>(() => NativeExtractionPolicy.Preflight(Archive(Entries(false)), Request, snapshots.Before));
    }

    [Fact] public void SourceAttributeScopeCannotBecomeInvisibleSilently()
    {
        var snapshots = Snapshots(); snapshots.Before.Metadata = new(); snapshots.Before.Documentation = new() {
            Definitions = [new() { Id = "definition", Xml = "<ExtendedAttribute><ElementTypes><AttributeElementType Type='SubProcess'/></ElementTypes></ExtendedAttribute>" }],
            Values = [new() { DiagramId = D, ElementId = Sub, Xml = "<ElementAttributeValues><Values><ExtendedAttributeValue Id='definition'/></Values></ElementAttributeValues>" }] };
        Assert.Throws<InvalidDataException>(() => NativeExtractionPolicy.Preflight(Archive(Entries(false)), Request, snapshots.Before));
        snapshots.Before.Documentation.Definitions[0].Xml = "<ExtendedAttribute><ElementTypes><AttributeElementType Type='CallActivity'/></ElementTypes></ExtendedAttribute>";
        NativeExtractionPolicy.Preflight(Archive(Entries(false)), Request, snapshots.Before);
    }

    [Fact] public void PresentationActionsAreNotLeftBehindAsOrphanedBehavior()
    {
        var snapshots = Snapshots(); snapshots.Before.Metadata = new(); snapshots.Before.Documentation = new();
        var entries = Entries(false); entries.Add(D + ".diag!/Actions.xml", Encoding.UTF8.GetBytes("<DiagramActions><Action/></DiagramActions>"));
        Assert.Throws<NotSupportedException>(() => NativeExtractionPolicy.Preflight(Archive(entries), Request, snapshots.Before));
    }
}
