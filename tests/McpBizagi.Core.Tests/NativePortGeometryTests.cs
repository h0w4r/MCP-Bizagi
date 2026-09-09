using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class NativePortGeometryTests
{
    // Synthetic policy inputs exercise rejection only; the installed MCP corpus
    // is separately required for any operational claim.
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static (NativePortQueryRequest Request, NativePortQueryReceipt Receipt) Sample()
    {
        var q = new NativePortQuery { ConnectionId = "edge", SourceId = "a", TargetId = "b",
            SourceBounds = new() { X = 100, Y = 100, Width = 120, Height = 80 },
            TargetBounds = new() { X = 400, Y = 100, Width = 120, Height = 80 },
            SourcePoint = new() { X = 220, Y = 120 }, TargetPoint = new() { X = 400, Y = 120 } };
        return (new() { Queries = [q] }, new() { EditorAssetSha256 = NativePortGeometryPolicy.EditorHash, RegistryUnchanged = true,
            Observations = [new() { Query = Copy(q), SourcePort = "17", TargetPort = "13", Route = [Copy(q.SourcePoint), Copy(q.TargetPoint)] }] });
    }

    [Theory]
    [InlineData("5", true)] [InlineData("74", true)] [InlineData("17", true)]
    [InlineData("4", false)] [InlineData("0", false)] [InlineData("75", false)]
    [InlineData("017", false)] [InlineData("+17", false)] [InlineData(" 17", false)] [InlineData(null, false)]
    public void OffsetVocabularyIsExact(string? value, bool expected) => Assert.Equal(expected, NativePortGeometryPolicy.Offset(value));

    [Theory]
    [InlineData(120, 100, 240, 200)] [InlineData(120, 180, 240, 360)]
    [InlineData(100, 120, 200, 240)] [InlineData(220, 120, 440, 240)]
    public void CandidatePreservesObservedSideFraction(double x, double y, double expectedX, double expectedY)
    {
        var q = NativePortGeometryPolicy.Candidate(new() { X = 100, Y = 100, Width = 120, Height = 80 },
            new() { X = 200, Y = 200, Width = 240, Height = 160 }, new() { X = (float)x, Y = (float)y });
        Assert.Equal(expectedX, q.X); Assert.Equal(expectedY, q.Y);
    }

    [Theory]
    [InlineData(100, 100)] [InlineData(120, 120)] [InlineData(99, 120)] [InlineData(220, 200)]
    public void CandidateRejectsAmbiguousOrOffPerimeterPoint(float x, float y) => Assert.Throws<InvalidDataException>(() =>
        NativePortGeometryPolicy.Candidate(new() { X = 100, Y = 100, Width = 120, Height = 80 },
            new() { Width = 120, Height = 80 }, new() { X = x, Y = y }));

    [Fact]
    public void RectangleQueryDoesNotCarryResolvedStylesOrExpandedFlags()
    {
        var s = Sample(); var q = s.Request.Queries[0]; var a = Copy(q.SourceBounds);
        a.Expanded = true; a.BackgroundArgb = 123; a.BorderArgb = 456;
        NativeElement[] graph = [new() { Id = "a", Geometry = a, ExpandedGeometry = a }, new() { Id = "b", Geometry = q.TargetBounds },
            new() { Id = "edge", SourceId = "a", TargetId = "b", Points = [q.SourcePoint, q.TargetPoint] }];
        var actual = NativePortGeometryPolicy.Describe(graph, "edge");
        Assert.Equal(JsonSerializer.Serialize(q), JsonSerializer.Serialize(actual));
        Assert.Equal(123, graph[0].Geometry!.BackgroundArgb);
    }

    [Fact]
    public void CompleteReceiptPassesStructuralPolicy()
    {
        var s = Sample(); NativePortGeometryPolicy.VerifyReceipt(s.Request, s.Receipt);
    }

    [Theory]
    [InlineData("asset")] [InlineData("mutated")] [InlineData("missing")]
    [InlineData("identity")] [InlineData("bounds")] [InlineData("fallback")]
    [InlineData("docking")] [InlineData("emptyroute")] [InlineData("nonfinite")]
    public void ReceiptRejectsIncompleteOrAlteredEvidence(string fault)
    {
        var s = Sample(); var r = s.Receipt; var o = r.Observations[0];
        switch (fault)
        {
            case "asset": r.EditorAssetSha256 = "unverified"; break;
            case "mutated": r.RegistryUnchanged = false; break;
            case "missing": r.Observations = []; break;
            case "identity": o.Query.SourceId = "other"; break;
            case "bounds": o.Query.SourceBounds.Width++; break;
            case "fallback": o.Errors = ["native routing failed"]; break;
            case "docking": o.Route[0].X++; break;
            case "emptyroute": o.Route = []; break;
            case "nonfinite": o.Route[0].X = float.NaN; break;
        }
        Assert.Throws<InvalidDataException>(() => NativePortGeometryPolicy.VerifyReceipt(s.Request, r));
    }

    [Theory]
    [InlineData("sourcebin")] [InlineData("afterbin")] [InlineData("missing")] [InlineData("duplicate")]
    public void CompleteProofRequiresBothActualClassifications(string fault)
    {
        var s = Sample(); var q = s.Request.Queries[0];
        NativeElement[] graph = [new() { Id = "a", Geometry = q.SourceBounds }, new() { Id = "b", Geometry = q.TargetBounds },
            new() { Id = "edge", DiagramId = "diagram", SourceId = "a", TargetId = "b", SourcePort = "17", TargetPort = "13", Points = [q.SourcePoint, q.TargetPoint] }];
        var proof = new NativePortGeometryPolicy.Proof(Copy(s.Receipt.Observations[0]), Copy(s.Receipt.Observations[0]), NativePortGeometryPolicy.EditorHash);
        NativePortGeometryPolicy.VerifyProofs(graph, graph, "diagram", [proof]);
        NativePortGeometryPolicy.Proof[] proofs = [proof];
        if (fault == "sourcebin") proof.Before.SourcePort = "18";
        if (fault == "afterbin") proof.After.TargetPort = "14";
        if (fault == "missing") proofs = [];
        if (fault == "duplicate") proofs = [proof, proof];
        Assert.Throws<InvalidDataException>(() => NativePortGeometryPolicy.VerifyProofs(graph, graph, "diagram", proofs));
    }
}
