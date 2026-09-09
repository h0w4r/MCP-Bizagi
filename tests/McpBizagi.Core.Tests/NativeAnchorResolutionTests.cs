using System.Text;
using System.Text.Json;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

public sealed class NativeAnchorResolutionTests
{
    private static string Id(int value) => new Guid(value, 0, 0, new byte[8]).ToString();
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    // Policy fixtures test rejection and source projection only. Real API/MCP
    // accreditation lives in --native --diagram-layout-anchors-only.
    private static (NativeElement[] Source, NativeAnchorResizeRequest Request, EngineReply Reply, string Callback) Fixture(string side)
    {
        var bounds = side switch { "top" => (289d, 139d), "bottom" => (539d, 439d), "left" => (139d, 249d), _ => (639d, 319d) };
        var source = new[] {
            new NativeElement { Id = Id(1), DiagramId = Id(9), ParentId = Id(8), Kind = "SubProcess", SubProcess = new(),
                Geometry = new() { X = 150, Y = 150, Width = 120, Height = 80, Expanded = true },
                ExpandedGeometry = new() { X = 150, Y = 150, Width = 500, Height = 300, Expanded = true } },
            new NativeElement { Id = Id(2), DiagramId = Id(9), ParentId = Id(8), Kind = "BoundaryEvent", Name = "Timer",
                Event = new() { AttachedToActivityId = Id(1), Mode = "Boundary", IsInterrupting = false },
                Geometry = new() { X = bounds.Item1, Y = bounds.Item2, Width = 22, Height = 22 },
                Style = new() { LabelBounds = new() { X = bounds.Item1 + 25, Y = bounds.Item2 + 25, Width = 100, Height = 30 } } }
        };
        var request = new NativeAnchorResizeRequest { DiagramId = Id(9), HostId = Id(1), Size = new() { Width = 700, Height = 500 } };
        var after = Clone(source); after[0].ExpandedGeometry!.Width = 700; after[0].ExpandedGeometry!.Height = 500;
        if (side == "bottom") after[1].Geometry!.Y += 200;
        if (side == "right") after[1].Geometry!.X += 200;
        after[1].Style!.LabelBounds!.Width = 22; after[1].Style!.LabelBounds!.Height = 22;
        string callback = Callback(after);
        var reply = new EngineReply { Success = true, Elements = after, Alignment = new() { Mode = "AnchorPreview", SelectedElementIds = [Id(1)],
            EditorAssetSha256 = NativeAnchorResolutionPolicy.EditorSha256, CallbackSha256 = BpmnDocument.Revision(Encoding.UTF8.GetBytes(callback)) } };
        return (source, request, reply, callback);
    }

    private static string Callback(NativeElement[] graph) => JsonSerializer.Serialize(graph.Select(e => {
        var g = e.ExpandedGeometry ?? e.Geometry!;
        return new { changed = (string?)null, element = JsonSerializer.Serialize(new { id = e.Id, x = g.X, y = g.Y,
            width = g.Width, height = g.Height, attachedToRefId = e.Event?.AttachedToActivityId }) };
    }));

    [Theory] [InlineData("top")] [InlineData("bottom")] [InlineData("left")] [InlineData("right")]
    public void NativeCoordinatesDoNotAuthorizeNativeLabelDamage(string side)
    {
        var f = Fixture(side); string original = JsonSerializer.Serialize(f.Source);
        var result = Assert.Single(NativeAnchorResolutionPolicy.Resolve(f.Source, f.Request, f.Reply, f.Callback));
        Assert.Equal(f.Reply.Elements[1].Geometry!.X, result.Geometry!.X);
        Assert.Equal(f.Reply.Elements[1].Geometry!.Y, result.Geometry.Y);
        Assert.Equal(100d, result.Style!.LabelBounds!.Width); Assert.Equal(30d, result.Style.LabelBounds.Height);
        Assert.Equal(result.Geometry.X + 25, result.Style.LabelBounds.X);
        Assert.Equal(result.Geometry.Y + 25, result.Style.LabelBounds.Y);
        Assert.Equal(original, JsonSerializer.Serialize(f.Source));
    }

    [Theory]
    [InlineData("hash")] [InlineData("asset")] [InlineData("noop")] [InlineData("mode")]
    [InlineData("artifact")] [InlineData("failure")] [InlineData("identity")]
    [InlineData("side")] [InlineData("event")] [InlineData("parent")]
    [InlineData("host-size")] [InlineData("host-origin")] [InlineData("anchor-size")]
    [InlineData("callback-position")] [InlineData("callback-duplicate")]
    public void InvalidResolutionFailsClosed(string defect)
    {
        var f = Fixture("bottom");
        switch (defect)
        {
            case "hash": f.Reply.Alignment!.CallbackSha256 = new string('0', 64); break;
            case "asset": f.Reply.Alignment!.EditorAssetSha256 = new string('0', 64); break;
            case "noop": f.Reply.Alignment!.NoOp = true; break;
            case "mode": f.Reply.Alignment!.Mode = "Calculated"; break;
            case "artifact": f.Reply.Artifacts = ["must-not-write.bpm"]; break;
            case "failure": f.Reply.Success = false; break;
            case "identity": f.Reply.Elements = f.Reply.Elements.Append(new() { Id = Id(5) }).ToArray(); break;
            case "side": f.Reply.Elements[1].Geometry!.Y = 139; break;
            case "event": f.Reply.Elements[1].Event!.IsInterrupting = true; break;
            case "parent": f.Reply.Elements[1].ParentId = Id(7); break;
            case "host-size": f.Reply.Elements[0].ExpandedGeometry!.Width++; break;
            case "host-origin": f.Reply.Elements[0].ExpandedGeometry!.X++; break;
            case "anchor-size": f.Reply.Elements[1].Geometry!.Width++; break;
            case "callback-position": f.Reply.Elements[1].Geometry!.X++; break;
            case "callback-duplicate":
                f.Callback = f.Callback.Replace("\"changed\":null", "\"changed\":null,\"changed\":null");
                f.Reply.Alignment!.CallbackSha256 = BpmnDocument.Revision(Encoding.UTF8.GetBytes(f.Callback)); break;
        }
        Assert.ThrowsAny<Exception>(() => NativeAnchorResolutionPolicy.Resolve(f.Source, f.Request, f.Reply, f.Callback));
    }

    [Fact]
    public void AutomaticLabelsStayAutomatic()
    {
        var f = Fixture("right"); f.Source[1].Style!.LabelBounds = new();
        var result = Assert.Single(NativeAnchorResolutionPolicy.Resolve(f.Source, f.Request, f.Reply, f.Callback));
        Assert.Null(result.Style);
    }
}
