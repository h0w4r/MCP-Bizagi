using System.IO.Compression;
using McpBizagi.Contracts;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Projection tests only: installed-engine lifecycle acceptance runs separately through actual MCP.</summary>
public sealed class NativeDiagramPolicyTests
{
    private const string A = "10000000-0000-4000-8000-000000000001";
    private const string B = "20000000-0000-4000-8000-000000000002";
    private const string P = "30000000-0000-4000-8000-000000000003";
    private const string Q = "40000000-0000-4000-8000-000000000004";
    private const string T = "50000000-0000-4000-8000-000000000005";
    private const string U = "60000000-0000-4000-8000-000000000006";
    private const string Ns = "http://www.wfmc.org/2009/XPDL2.2";
    private static readonly string[] Scope = ["Users/Default/UserPreferences.xml", "Users/operator/UserPreferences.xml"];
    private static string Diagram(string id, string name, string contents = "") => $"<Package xmlns='{Ns}' Id='{id}' Name='{name}'>{contents}</Package>";
    private static string Flow(string process, string task, string extra = "") => $"<WorkflowProcesses><WorkflowProcess Id='{process}'><Activities><Activity Id='{task}' Name='Task'/></Activities>{extra}</WorkflowProcess></WorkflowProcesses>";
    private static byte[] Archive(Dictionary<string, string> diagrams, Dictionary<string, string>? extras = null)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            using (var info = new StreamWriter(zip.CreateEntry("ModelInfo.xml").Open())) info.Write("<BizAgiModelInfo/>");
            foreach (var pair in diagrams)
            {
                using var innerBuffer = new MemoryStream();
                using (var inner = new ZipArchive(innerBuffer, ZipArchiveMode.Create, true))
                {
                    using var writer = new StreamWriter(inner.CreateEntry("Diagram.xml").Open()); writer.Write(pair.Value);
                }
                using var entry = zip.CreateEntry(pair.Key + ".diag").Open(); entry.Write(innerBuffer.ToArray());
            }
            foreach (var pair in extras ?? []) { using var writer = new StreamWriter(zip.CreateEntry(pair.Key).Open()); writer.Write(pair.Value); }
        }
        return buffer.ToArray();
    }
    private static EngineReply State(params (string Id, string Name)[] diagrams) => new()
    { DiagramState = new() { Diagrams = diagrams.Select(d => new NativeDiagramInfo { Id = d.Id, Name = d.Name }).ToArray(), PreferenceEntries = Scope } };
    private static NativeDiagramPatch Patch(string operation, string id, string? name = null) => new()
    { Changes = [new() { Operation = operation, DiagramId = id, Name = name }] };

    [Theory] [InlineData(false)] [InlineData(true)]
    public void RenamePreservesUnknownContentIncludingSameIdentity(bool unknownChanged)
    {
        string Unknown(string name) => $"<Unknown Id='{A}' Name='{name}'/>";
        var before = Archive(new() { [A] = Diagram(A, "Old", Unknown("Keep")) });
        var after = Archive(new() { [A] = Diagram(A, "Renamed Ω", Unknown(unknownChanged ? "Changed" : "Keep")) });
        var state = State((A, "Renamed Ω"));
        var report = NativeDiagramPolicy.Compare(before, after, Patch("rename", A, "Renamed Ω"), state, state);
        Assert.Equal(!unknownChanged, report.Preserved);
        Assert.Contains(report.Differences, d => d.Classification == "verified_diagram_lifecycle" && d.Location == "rename");
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void DeleteRemovesOnlyTheExplicitDiagramArchive(bool changedSurvivor)
    {
        var before = Archive(new() { [A] = Diagram(A, "Keep"), [B] = Diagram(B, "Delete") }, new() { ["opaque.bin"] = "Keep bytes" });
        var after = Archive(new() { [A] = Diagram(A, "Keep", changedSurvivor ? "<Unexpected/>" : "") }, new() { ["opaque.bin"] = "Keep bytes" });
        var state = State((A, "Keep"));
        Assert.Equal(!changedSurvivor, NativeDiagramPolicy.Compare(before, after, Patch("delete", B), state, state).Preserved);
        Assert.Throws<InvalidDataException>(() => NativeDiagramPolicy.Compare(after, Archive([]), Patch("delete", A), State(), State()));
    }
    [Theory] [InlineData("none")] [InlineData("documentation")] [InlineData("attribute")] [InlineData("unknown")]
    public void RenameProjectsOnlyTheNativeDerivedDescription(string fault)
    {
        string Header(string name, string docs, string attr = "", string body = "") => $"<PackageHeader><Description {attr}>{name}{body}</Description><Documentation>{docs}</Documentation></PackageHeader>";
        var before = Archive(new() { [A] = Diagram(A, "Old", Header("Old", "Keep")) });
        var after = Archive(new() { [A] = Diagram(A, "Renamed", Header("Renamed", fault == "documentation" ? "Lost" : "Keep", fault == "attribute" ? "extra='keep visible'" : "", fault == "unknown" ? "<!--keep visible-->" : "")) });
        var state = State((A, "Renamed"));
        if (fault == "unknown") Assert.Throws<InvalidDataException>(() => NativeDiagramPolicy.Compare(before, after, Patch("rename", A, "Renamed"), state, state));
        else Assert.Equal(fault == "none", NativeDiagramPolicy.Compare(before, after, Patch("rename", A, "Renamed"), state, state).Preserved);
    }
    [Fact] public void ReadbackCannotReplaceAMissingDiagramWithADuplicate()
    {
        var before = Archive(new() { [A] = Diagram(A, "Old"), [B] = Diagram(B, "Keep") });
        var after = Archive(new() { [A] = Diagram(A, "Renamed"), [B] = Diagram(B, "Keep") });
        var state = State((A, "Renamed"), (A, "Renamed"));
        Assert.Throws<InvalidDataException>(() => NativeDiagramPolicy.Compare(before, after, Patch("rename", A, "Renamed"), state, state));
    }
    private static string Preferences(string contents) => $"<UserPreferences><OpenedItems>{contents}</OpenedItems><Keep>value</Keep></UserPreferences>";
    private static string Tab(string id, bool selected) => $"<ModelItem ItemType='Diagram' DiagramId='{id}' IsSelected='{selected.ToString().ToLowerInvariant()}'/>";
    private static Dictionary<string, string> Prefs(string tabs) => new()
    { [Scope[0]] = Preferences(tabs), [Scope[1]] = Preferences(tabs), ["Users/other/UserPreferences.xml"] = Preferences("") };

    [Fact] public void OrderedTabsIgnoreIndentationButPreserveOtherUsersAndUnknownFields()
    {
        var opened = new NativeOpenedItem[] { new() { DiagramId = B, IsSelected = true }, new() { DiagramId = A } };
        var diagrams = new Dictionary<string, string> { [A] = Diagram(A, "First"), [B] = Diagram(B, "Second") };
        var before = Archive(diagrams, Prefs(""));
        var after = Archive(diagrams, Prefs("\n    " + Tab(B, true) + "\n    " + Tab(A, false) + "\n  "));
        var state = State((A, "First"), (B, "Second")); state.DiagramState!.OpenedItems = opened;
        Assert.True(NativeDiagramPolicy.Compare(before, after, new() { OpenedItems = opened }, state, state).Preserved);
    }
    [Theory] [InlineData("order")] [InlineData("selected")] [InlineData("comment")] [InlineData("duplicate")]
    public void TabProjectionRejectsNonRequestedDurablePayload(string fault)
    {
        var opened = new NativeOpenedItem[] { new() { DiagramId = B, IsSelected = true }, new() { DiagramId = A } };
        var diagrams = new Dictionary<string, string> { [A] = Diagram(A, "First"), [B] = Diagram(B, "Second") };
        string tabs = fault switch { "order" => Tab(A, false) + Tab(B, true), "selected" => Tab(B, false) + Tab(A, true),
            "comment" => Tab(B, true) + "<!--not requested-->" + Tab(A, false), _ => Tab(B, true) + Tab(A, false) + Tab(A, false) };
        var state = State((A, "First"), (B, "Second")); state.DiagramState!.OpenedItems = opened;
        Assert.Throws<InvalidDataException>(() => NativeDiagramPolicy.Compare(Archive(diagrams, Prefs("")), Archive(diagrams, Prefs(tabs)), new() { OpenedItems = opened }, state, state));
    }
    [Theory] [InlineData("other-user")] [InlineData("other-field")]
    public void TabProjectionCannotMaskChangesOutsideOpenedItems(string fault)
    {
        var diagrams = new Dictionary<string, string> { [A] = Diagram(A, "Keep") };
        var extras = Prefs(Tab(A, true));
        if (fault == "other-user") extras["Users/other/UserPreferences.xml"] = Preferences(Tab(A, true));
        else extras[Scope[0]] = extras[Scope[0]].Replace("<Keep>value", "<Keep>changed");
        var state = State((A, "Keep")); state.DiagramState!.OpenedItems = [new() { DiagramId = A, IsSelected = true }];
        Assert.False(NativeDiagramPolicy.Compare(Archive(diagrams, Prefs("")), Archive(diagrams, extras), new() { OpenedItems = state.DiagramState.OpenedItems }, state, state).Preserved);
    }
    [Theory] [InlineData("missing")] [InlineData("traversal")] [InlineData("different-worker")]
    public void TabProjectionRequiresAnIndependentConfinedScope(string fault)
    {
        var diagrams = new Dictionary<string, string> { [A] = Diagram(A, "Keep") };
        var edited = State((A, "Keep")); var read = State((A, "Keep"));
        read.DiagramState!.PreferenceEntries = fault switch { "missing" => [], "traversal" => [Scope[0], "Users/../UserPreferences.xml"], _ => [Scope[0], "Users/other/UserPreferences.xml"] };
        if (fault != "different-worker") edited.DiagramState!.PreferenceEntries = read.DiagramState.PreferenceEntries;
        Assert.Throws<InvalidDataException>(() => NativeDiagramPolicy.Compare(Archive(diagrams, Prefs("")), Archive(diagrams, Prefs("")), new() { OpenedItems = [] }, edited, read));
    }
    private static EngineReply CloneState()
    {
        var state = State((A, "Original"), (B, "Copy Ω"));
        state.Elements = new[] { (B, "Collaboration"), (Q, "Process"), (U, "UserTask") }.Select(e => new NativeElement { Id = e.Item1, Kind = e.Item2, DiagramId = B }).ToArray();
        state.DiagramClones = [new() { SourceId = A, TargetId = B, Identities = new[] { (A, B), (P, Q), (T, U) }.Select(e => new NativeCloneIdentity { SourceId = e.Item1, TargetId = e.Item2 }).ToArray() }];
        return state;
    }
    [Theory] [InlineData("native", true)] [InlineData("multiple", true)] [InlineData("intermediate", true)] [InlineData("wrong-mode", false)] [InlineData("unknown", false)] [InlineData("namespace", false)]
    public void CloneProjectsOnlyNativeCompensationReferences(string mode, bool expected)
    {
        string Content(string id) => mode switch {
            "wrong-mode" => $"<Event><StartEvent><TriggerIntermediateMultiple><TriggerResultCompensation ActivityId='{id}'/></TriggerIntermediateMultiple></StartEvent></Event>",
            "intermediate" => $"<Event><IntermediateEvent><TriggerIntermediateMultiple><TriggerResultCompensation ActivityId='{id}'/></TriggerIntermediateMultiple></IntermediateEvent></Event>",
            "multiple" => $"<Event><EndEvent><ResultMultiple><TriggerResultCompensation ActivityId='{id}'/></ResultMultiple></EndEvent></Event>",
            "unknown" => $"<Event><EndEvent><Unknown><TriggerResultCompensation ActivityId='{id}'/></Unknown></EndEvent></Event>",
            "namespace" => $"<Event><EndEvent><TriggerResultCompensation xmlns='urn:unknown' ActivityId='{id}'/></EndEvent></Event>",
            _ => $"<Event><EndEvent><TriggerResultCompensation ActivityId='{id}'/></EndEvent></Event>" };
        string Body(string process, string task) => Flow(process, task).Replace($"<Activity Id='{task}' Name='Task'/>", $"<Activity Id='{task}' Name='Task'>{Content(task)}</Activity>");
        string source = Diagram(A, "Original", Body(P, T)), target = Diagram(B, "Copy Ω", Body(Q, U)); var state = CloneState();
        Assert.Equal(expected, NativeDiagramPolicy.Compare(Archive(new() { [A] = source }), Archive(new() { [A] = source, [B] = target }), Patch("clone", A, "Copy Ω"), state, state).Preserved);
    }
    [Fact] public void CloneVerifiesDurableIdentityBijectionAndPreservesTheOriginal()
    {
        string source = Diagram(A, "Original", Flow(P, T));
        var state = CloneState();
        var report = NativeDiagramPolicy.Compare(Archive(new() { [A] = source }), Archive(new() { [A] = source, [B] = Diagram(B, "Copy Ω", Flow(Q, U)) }), Patch("clone", A, "Copy Ω"), state, state);
        Assert.True(report.Preserved);
        Assert.Contains(report.Differences, d => d.Location == "clone" && d.ElementId == A);
    }
    [Theory] [InlineData("drop")] [InlineData("rename-child")] [InlineData("change-original")]
    public void CloneDoesNotWaivePayloadLossOrSourceMutation(string fault)
    {
        string source = Diagram(A, "Original", Flow(P, T, "<Unknown>Keep</Unknown>"));
        string target = Diagram(B, "Copy Ω", Flow(Q, U, fault == "drop" ? "" : "<Unknown>Keep</Unknown>"));
        if (fault == "rename-child") target = target.Replace("Name='Task'", "Name='Lost'");
        var state = CloneState();
        Assert.False(NativeDiagramPolicy.Compare(Archive(new() { [A] = source }), Archive(new() { [A] = fault == "change-original" ? source.Replace("Keep", "Lost") : source, [B] = target }), Patch("clone", A, "Copy Ω"), state, state).Preserved);
    }
    [Theory] [InlineData("Unknown")] [InlineData("Unknown><Activities")]
    public void CloneDoesNotRemapUnknownSameNamedIdentityOwners(string wrapper)
    {
        string Unknown(string id) => wrapper == "Unknown" ? $"<Unknown Id='{id}'/>" : $"<Unknown><Activities><Activity Id='{id}'/></Activities></Unknown>";
        string source = Diagram(A, "Original", Flow(P, T, Unknown(T)));
        string target = Diagram(B, "Copy Ω", Flow(Q, U, Unknown(U)));
        var state = CloneState();
        Assert.False(NativeDiagramPolicy.Compare(Archive(new() { [A] = source }), Archive(new() { [A] = source, [B] = target }), Patch("clone", A, "Copy Ω"), state, state).Preserved);
    }
    [Theory] [InlineData("missing")] [InlineData("same")] [InlineData("extra")]
    public void CloneReceiptCannotDefineItsOwnIncompleteOrReusedIdentityCoverage(string fault)
    {
        string source = Diagram(A, "Original", Flow(P, T)); var state = CloneState();
        if (fault == "missing") { state.DiagramClones[0].Identities = state.DiagramClones[0].Identities[..2]; state.Elements = state.Elements[..2]; }
        else if (fault == "same") state.DiagramClones[0].Identities[2].TargetId = T;
        else state.DiagramClones[0].Identities = [..state.DiagramClones[0].Identities, new() { SourceId = Guid.NewGuid().ToString(), TargetId = Guid.NewGuid().ToString() }];
        Assert.Throws<InvalidDataException>(() => NativeDiagramPolicy.Compare(Archive(new() { [A] = source }), Archive(new() { [A] = source, [B] = Diagram(B, "Copy Ω", Flow(Q, U)) }), Patch("clone", A, "Copy Ω"), state, state));
    }
    [Theory] [InlineData("native", true)] [InlineData("unknown-wrapper", false)] [InlineData("not-attached", false)] [InlineData("unknown-namespace", false)]
    public void CloneRemapsOnlyAnActualBoundaryTargetAttribute(string shape, bool expected)
    {
        // This small XML fixture tests the reference projection boundary, not BPMN validity.
        string Event(string target)
        {
            string node = $"<IntermediateEvent IsAttached='{(shape == "not-attached" ? "false" : "true")}' Target='{target}' {(shape == "unknown-namespace" ? "xmlns='urn:extension'" : "")}/>";
            return shape == "unknown-wrapper" ? $"<Unknown><Event>{node}</Event></Unknown>" : $"<Event>{node}</Event>";
        }
        string BoundaryFlow(string process, string activity) => $"<WorkflowProcesses><WorkflowProcess Id='{process}'><Activities><Activity Id='{activity}' Name='Boundary'>{Event(activity)}</Activity></Activities></WorkflowProcess></WorkflowProcesses>";
        string source = Diagram(A, "Original", BoundaryFlow(P, T));
        string target = Diagram(B, "Copy Ω", BoundaryFlow(Q, U));
        var state = CloneState();
        Assert.Equal(expected, NativeDiagramPolicy.Compare(Archive(new() { [A] = source }), Archive(new() { [A] = source, [B] = target }), Patch("clone", A, "Copy Ω"), state, state).Preserved);
    }
    [Theory] [InlineData("empty")] [InlineData("name")] [InlineData("delete-name")] [InlineData("unknown")] [InlineData("null")]
    public void ValidateRejectsAmbiguousOrIgnoredIntent(string fault)
    {
        var patch = fault switch { "empty" => new(), "name" => Patch("create", A, " "), "delete-name" => Patch("delete", A, "Ignored"),
            "unknown" => Patch("reflect", A), _ => new NativeDiagramPatch { Changes = [null!] } };
        Assert.Throws<InvalidDataException>(() => NativeDiagramPolicy.Validate(patch));
    }
}
