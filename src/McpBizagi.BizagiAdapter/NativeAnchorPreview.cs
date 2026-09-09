using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private NativeAlignmentReceipt PreviewAnchors(object model, object persistence, EngineRequest request, Action<string> progress)
    {
        var resize = request.AnchorResize ?? throw new InvalidDataException("Missing anchor resize intent.");
        if (request.OutputPath != "" || request.Mutations.Length != 0 || request.Changes.Length != 0)
            throw new InvalidDataException("Anchor preview cannot carry durable write intent.");
        var graph = Graph(model).Select(Describe).ToArray();
        var host = graph.SingleOrDefault(e => e.Id == resize.HostId && e.DiagramId == resize.DiagramId)
            ?? throw new InvalidDataException("Unknown anchor host.");
        var bounds = host.ExpandedGeometry;
        if (host.SubProcess == null || host.Geometry?.Expanded != true || bounds == null || resize.Size == null ||
            new[] { resize.Size.Width, resize.Size.Height }.Any(n => double.IsNaN(n) || double.IsInfinity(n) || n <= 0 || n > 1000000 || n != Math.Truncate(n)))
            throw new InvalidDataException("Anchor preview requires an expanded embedded host and bounded whole sizes.");
        var anchors = graph.Where(e => e.Kind == "BoundaryEvent" && e.Event?.AttachedToActivityId == host.Id).ToArray();
        if (anchors.Length == 0 || anchors.Any(e => e.DiagramId != host.DiagramId || e.ParentId != host.ParentId))
            throw new InvalidDataException("Missing or cross-surface boundary attachments.");
        if (bounds.Width == resize.Size.Width && bounds.Height == resize.Size.Height)
            throw new InvalidDataException("An unchanged host does not need native anchor resolution.");
        var parent = graph.Single(e => e.Id == host.ParentId);
        // Reuse the accredited editor bootstrap, actual callbacks and command factory.
        // Its transient pool/neighbor/route changes are deliberately NOT persisted.
        var inner = new EngineRequest {
            Action = "anchor_preview", AtomicStepSeconds = request.AtomicStepSeconds, InactivitySeconds = request.InactivitySeconds,
            AnchorResize = resize,
            Alignment = new() { Mode = "AnchorPreview", DiagramId = host.DiagramId, ElementIds = [host.Id],
                SubProcessId = parent.SubProcess != null ? parent.Id : "" },
            AlignmentExpected = graph.Where(e => e.DiagramId == host.DiagramId)
                .Select(e => new NativeMutation { Operation = "update", ElementId = e.Id }).ToArray()
        };
        if (inner.AlignmentExpected.Length > 1000) throw new NotSupportedException("Anchor preview exceeds the diagram transaction bound.");
        return Align(model, persistence, inner, progress);
    }
}
