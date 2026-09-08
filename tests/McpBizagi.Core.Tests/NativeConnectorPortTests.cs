using System.IO.Compression;
using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class NativeConnectorPortTests
{
    private const string Id = "11111111-2222-4333-8444-555555555555";
    private const string Source = "22222222-3333-4444-8555-666666666666";
    private const string Target = "33333333-4444-4555-8666-777777777777";
    private static NativeMutation Change(string? port) => new() { Operation = "reconnect", ElementId = Id,
        SourceId = Source, TargetId = Target, SourcePort = port, Points = [new() { X = 10, Y = 20 }, new() { X = 30, Y = 20 }] };
    private static NativeElement Read(string? port) => new() { Id = Id, SourceId = Source, TargetId = Target,
        SourcePort = port, Points = Change(null).Points };

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("0")] [InlineData("1")] [InlineData("20")] [InlineData("39")] [InlineData("74")]
    public void ExplicitKnownIdentifiersValidate(string? value) => NativeEditPlan.Validate([Change(value)]);

    [Theory]
    [InlineData("-1")] [InlineData("75")] [InlineData("04")] [InlineData("+4")] [InlineData("4.0")] [InlineData(" 4")] [InlineData("right")]
    public void NoncanonicalOrUnaccreditedIdentifiersReject(string value) =>
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([Change(value)]));

    [Fact]
    public void OmissionPreservesWhileEmptyClears()
    {
        Assert.Null(JsonSerializer.Deserialize<NativeMutation>("{}")!.SourcePort);
        Assert.Null(JsonSerializer.Deserialize<NativeMutation>("{\"SourcePort\":null}")!.SourcePort);
        Assert.Equal("", JsonSerializer.Deserialize<NativeMutation>("{\"SourcePort\":\"\"}")!.SourcePort);
        NativeEditPlan.Verify([Change(null)], [Read("unknown imported identifier")]);
        NativeEditPlan.Verify([Change("")], [Read(null)]);
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([Change("4")], [Read("3")]));
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Verify([Change("")], [Read("0")]));
    }

    [Theory]
    [InlineData("update")] [InlineData("delete")]
    public void NonconnectorMutationCannotSmugglePorts(string operation)
    {
        var change = new NativeMutation { Operation = operation, ElementId = Id, SourcePort = "4", Name = operation == "update" ? "Name" : null };
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([change]));
    }

    [Fact]
    public void ArchiveProjectionRequiresBothReadbackAndExactXmlIntent()
    {
        // Synthetic archives test the comparator, not the installed engine.
        Assert.True(NativeMutationFidelity.Compare(Archive("FromPort='3' ToPort='opaque'"), Archive("FromPort='4' ToPort='opaque'"), [Change("4")], Graph("4")).Preserved);
        Assert.False(NativeMutationFidelity.Compare(Archive("FromPort='3' ToPort='opaque'"), Archive("FromPort='4' ToPort='changed'"), [Change("4")], Graph("4")).Preserved);
        Assert.False(NativeMutationFidelity.Compare(Archive("FromPort='3'"), Archive("FromPort='4'"), [Change(null)], Graph("4")).Preserved);
        Assert.Throws<InvalidDataException>(() => NativeMutationFidelity.Compare(Archive("FromPort='3'"), Archive("FromPort='2'"), [Change("4")], Graph("4")));
        Assert.True(NativeMutationFidelity.Compare(Archive("FromPort='3'"), Archive(""), [Change("")], Graph(null)).Preserved);
        Assert.False(NativeMutationFidelity.Compare(Archive("FromPort='3' Keep='yes'"), Archive("FromPort='4' Keep='no'"), [Change("4")], Graph("4")).Preserved);
    }

    private static NativeElement[] Graph(string? port) => [Read(port), new() { Id = Source, Kind = "UserTask", ActivityProperties = new() { CompletionQuantity = 1 } }];

    private static byte[] Archive(string attributes)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            using (var info = new StreamWriter(zip.CreateEntry("ModelInfo.xml").Open())) info.Write("<ModelInfo/>");
            using var diagram = new MemoryStream();
            using (var nested = new ZipArchive(diagram, ZipArchiveMode.Create, true))
            using (var writer = new StreamWriter(nested.CreateEntry("Diagram.xml").Open()))
                writer.Write($"<Package xmlns='http://www.wfmc.org/2009/XPDL2.2'><WorkflowProcesses><WorkflowProcess Id='{Source}'><Transitions><Transition Id='{Id}' From='{Source}' To='{Target}'><ConnectorGraphicsInfos><ConnectorGraphicsInfo {attributes}><Coordinates XCoordinate='10' YCoordinate='20'/><Coordinates XCoordinate='30' YCoordinate='20'/></ConnectorGraphicsInfo></ConnectorGraphicsInfos></Transition></Transitions></WorkflowProcess></WorkflowProcesses></Package>");
            using var entry = zip.CreateEntry("diagram.diag").Open(); entry.Write(diagram.ToArray());
        }
        return output.ToArray();
    }
}
