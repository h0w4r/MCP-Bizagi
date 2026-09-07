using System.Xml;
using System.Xml.Linq;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class IntegrityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mcp-bizagi-tests-" + Guid.NewGuid().ToString("N"));
    private WorkspaceFiles Files => new(root);
    private const string Xml = """
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:custom="urn:test:extension" id="defs" targetNamespace="urn:test">
          <process id="process"><subProcess id="sub"><task id="task" name="Original"><extensionElements><custom:data important="preserve">sentinel</custom:data></extensionElements></task></subProcess></process>
        </definitions>
        """;

    [Fact] public void NestedElementsAndExtensionsSurviveNameChanges()
    {
        string updated = BpmnDocument.Apply(Xml, [new("task", "name", "Revisión — 東京")]);
        Assert.Contains("sentinel", updated);
        Assert.Contains("preserve", updated);
        var summary = BpmnDocument.Inspect(updated, "revision");
        Assert.Contains(summary.Elements, e => e.Id == "task" && e.ParentId == "sub" && e.Name == "Revisión — 東京");
    }
    [Fact] public void SaveCreatesDurableRevisionAndBackup()
    {
        var first = Files.SaveBpmn("with spaces/diagram.bpmn", Xml);
        var changed = Files.SaveBpmn("with spaces/diagram.bpmn", BpmnDocument.Apply(Xml, [new("task", "name", "Changed")]), first.Revision);
        Assert.NotEqual(first.Revision, changed.Revision);
        Assert.NotNull(changed.BackupPath);
        Assert.Equal(first.Revision, BpmnDocument.Revision(File.ReadAllBytes(changed.BackupPath!)));
        Assert.Equal(changed.Revision, Files.ReadBpmn(changed.Path).Revision);
    }
    [Fact] public void StaleRevisionDoesNotOverwrite()
    {
        var file = Files.SaveBpmn("model.bpmn", Xml);
        Assert.Throws<IOException>(() => Files.SaveBpmn("model.bpmn", Xml, "stale"));
        Assert.Equal(file.Revision, Files.ReadBpmn("model.bpmn").Revision);
    }
    [Fact] public void ExistingFileNeedsExplicitRevision()
    { Files.SaveBpmn("model.bpmn", Xml); Assert.Throws<IOException>(() => Files.SaveBpmn("model.bpmn", Xml)); }
    [Fact] public void DeletedDestinationIsConflict()
    { Assert.Throws<IOException>(() => Files.SaveBpmn("model.bpmn", Xml, "previous")); }
    [Theory] [InlineData("../outside.bpmn")] [InlineData("model.bpmn:stream")]
    public void RejectsEscapingPaths(string path) => Assert.Throws<UnauthorizedAccessException>(() => Files.Resolve(path));
    [Fact] public void NativeFilesCannotUseXmlFallback()
    { Assert.Throws<NotSupportedException>(() => Files.SaveBpmn("model.bpm", Xml)); }
    [Fact] public void RejectsDtdBeforeAnyExternalEntityResolution()
    { Assert.Throws<XmlException>(() => BpmnDocument.Parse("<!DOCTYPE definitions [<!ENTITY x SYSTEM 'file:///never-read'>]>" + Xml)); }
    [Fact] public void RejectsMalformedAndWrongNamespace()
    { Assert.Throws<XmlException>(() => BpmnDocument.Parse("<")); Assert.Throws<InvalidDataException>(() => BpmnDocument.Parse("<definitions/>")); }
    [Fact] public void UnsupportedBatchDoesNotPartiallyPersist()
    {
        var first = Files.SaveBpmn("model.bpmn", Xml);
        Assert.Throws<NotSupportedException>(() => BpmnDocument.Apply(Xml, [new("task", "name", "changed"), new("sub", "delete", "")]));
        Assert.Equal(first.Revision, Files.ReadBpmn("model.bpmn").Revision);
    }
    [Fact] public void DuplicateIdentifiersAreReported()
    { var duplicate = Xml.Replace("id=\"sub\"", "id=\"task\""); Assert.Contains(BpmnDocument.Validate(duplicate), f => f.Code == "duplicate_id"); }
    [Fact] public void UnresolvedFlowsAreReported()
    { var invalid = Xml.Replace("</process>", "<sequenceFlow id=\"flow\" sourceRef=\"task\" targetRef=\"missing\"/></process>"); Assert.Contains(BpmnDocument.Validate(invalid), f => f.Code == "unresolved_reference"); }
    [Fact] public void LockedFileDoesNotGetOverwritten()
    {
        var first = Files.SaveBpmn("locked.bpmn", Xml);
        using var handle = new FileStream(first.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<IOException>(() => Files.SaveBpmn("locked.bpmn", Xml, first.Revision));
    }
    public void Dispose()
    {
        // Delete only the test-owned directory after validating its known parent and prefix.
        if (Directory.Exists(root) && Path.GetDirectoryName(root) == Path.TrimEndingDirectorySeparator(Path.GetTempPath())
            && Path.GetFileName(root).StartsWith("mcp-bizagi-tests-", StringComparison.Ordinal)) Directory.Delete(root, true);
    }
}
