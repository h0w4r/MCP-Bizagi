using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using McpBizagi.Contracts;
using Microsoft.Msagl.Layout.Layered;

namespace McpBizagi.Core;

/// <summary>Complete selected-diagram position intent, independently validated before any native writer.</summary>
public static class NativeDiagramLayoutPlanner
{
    public const int MaximumRecords = 1000;
    public sealed record Plan(string DiagramId, string Direction, string Dependency, string AssemblySha256,
        string SourceGraphSha256, NativeMutation[] Changes, object[] Pools, object[] Surfaces, NativePortGeometryPolicy.Proof[] Ports);

    public static void Validate(NativeDiagramLayoutRequest request)
    {
        if (request == null || !Guid.TryParseExact(request.DiagramId, "D", out _) || request.Direction is not "Right" and not "Down")
            throw new InvalidDataException("Diagram layout requires an actual diagram UUID and direction Right or Down.");
    }

    public static Plan Calculate(NativeElement[] source, NativeDiagramLayoutRequest request, Action<string> progress, CancellationToken token)
        => CalculateAsync(source, request, progress, token).GetAwaiter().GetResult();

    /// <summary>Native resolution awaits isolated workers without blocking the host transport.</summary>
    public static async Task<Plan> CalculateAsync(NativeElement[] source, NativeDiagramLayoutRequest request, Action<string> progress, CancellationToken token,
        Func<NativeAnchorResizeRequest, Task<NativeMutation[]>>? resolveAnchors = null,
        Func<NativeElement[], Task<NativePortGeometryPolicy.Proof[]>>? resolvePorts = null)
    {
        Validate(request); token.ThrowIfCancellationRequested();
        if (!source.Any(e => e.Id == request.DiagramId && e.Kind == "Collaboration")) throw new InvalidDataException("Unknown native diagram identity.");
        var selected = source.Where(e => e.DiagramId == request.DiagramId).ToArray();
        if (selected.Length > MaximumRecords) throw new NotSupportedException("Diagram exceeds the complete 1000-record transaction bound.");
        if (source.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != source.Length) throw new InvalidDataException("Duplicate native graph identity.");
        // Unknown diagram surfaces cannot disappear simply because the planner has
        // no movement rule. Cross-diagram routes likewise require a separate contract.
        var identities = selected.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var e in source.Where(e => e.SourceId != "" || e.TargetId != ""))
            if ((identities.Contains(e.SourceId) || identities.Contains(e.TargetId)) &&
                (!identities.Contains(e.Id) || !identities.Contains(e.SourceId) || !identities.Contains(e.TargetId)))
                throw new NotSupportedException("Cross-diagram or unresolved connectors are not a partial layout surface.");
        foreach (var e in selected)
        {
            // Connector snapshots include a zero-size graphical placeholder; their
            // geometry is the actual point sequence, not a movable node rectangle.
            bool connector = e.Kind is "SequenceFlow" or "MessageFlow" or "Association";
            if (connector && (e.Points.Length < 2 || e.Points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || Math.Abs(p.X) > 1000000 || Math.Abs(p.Y) > 1000000)))
                throw new InvalidDataException("Invalid or unbounded connector points.");
            foreach (var box in new[] { e.Geometry, e.ExpandedGeometry }.Where(b => b != null && !connector))
                if (new[] { box!.X, box.Y, box.Width, box.Height }.Any(n => !double.IsFinite(n) || Math.Abs(n) > 1000000) || box.Width <= 0 || box.Height <= 0)
                    throw new InvalidDataException("Invalid or unbounded native geometry.");
            if (e.Style?.LabelBounds is { } label && new[] { label.X, label.Y, label.Width, label.Height }.Any(n => !double.IsFinite(n) || Math.Abs(n) > 1000000))
                throw new InvalidDataException("Invalid native label geometry.");
        }
        var context = new NativeDiagramLayoutContext(request.Direction, progress, token) { ResolveAnchors = resolveAnchors, AllowPortQueries = resolvePorts != null };
        NativeMutation[] changes;
        try { changes = await NativeDiagramPartitionPlanner.PlanAsync(source, request.DiagramId, context).ConfigureAwait(false); }
        catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        token.ThrowIfCancellationRequested();
        NativeEditPlan.Validate(changes);
        var predicted = Predict(source, changes);
        var ports = selected.Any(NativePortGeometryPolicy.NeedsQuery)
            ? await (resolvePorts ?? throw new NotSupportedException("Offset ports require actual native classification."))(predicted).ConfigureAwait(false)
            : [];
        NativePortGeometryPolicy.VerifyProofs(source, predicted, request.DiagramId, ports);
        NativeDiagramLayoutGeometry.Verify(predicted, request.DiagramId, ports);
        progress("native_diagram_layout_plan_verified");
        return new(request.DiagramId, request.Direction, NativeSurfaceLayoutPlanner.Dependency,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(LayeredLayout).Assembly.Location))),
            BpmnDocument.Revision(JsonSerializer.SerializeToUtf8Bytes(source)), changes, context.Pools.ToArray(), context.Surfaces.ToArray(), ports);
    }

    internal static NativeElement[] Predict(NativeElement[] source, NativeMutation[] changes)
    {
        // Geometry-only projection. Native persistence and whole-archive fidelity
        // remain independent; this is never substituted for an engine result.
        var graph = JsonSerializer.Deserialize<NativeElement[]>(JsonSerializer.Serialize(source))!;
        var byId = graph.ToDictionary(e => e.Id, StringComparer.Ordinal);
        foreach (var change in changes)
        {
            var item = byId[change.ElementId];
            if (change.Geometry != null)
            {
                item.Geometry = change.Geometry;
                if (item.ExpandedGeometry != null) { item.ExpandedGeometry.X = change.Geometry.X; item.ExpandedGeometry.Y = change.Geometry.Y; }
            }
            if (change.ExpandedSize is { } size)
            {
                if (item.ExpandedGeometry == null) throw new InvalidDataException("Missing expanded geometry.");
                item.ExpandedGeometry.Width = size.Width; item.ExpandedGeometry.Height = size.Height;
            }
            if (change.Style?.LabelBounds is { } label)
                item.Style!.LabelBounds = new() { X = label.X!.Value, Y = label.Y!.Value, Width = label.Width!.Value, Height = label.Height!.Value };
            if (change.Operation == "reconnect")
            {
                item.Points = change.Points;
                if (change.SourcePort != null) item.SourcePort = change.SourcePort;
                if (change.TargetPort != null) item.TargetPort = change.TargetPort;
            }
        }
        return graph;
    }
}

/// <summary>Per-call diagnostics and cooperative cancellation; no shared layout state.</summary>
internal sealed class NativeDiagramLayoutContext(string direction, Action<string> progress, CancellationToken token)
{
    public Func<NativeAnchorResizeRequest, Task<NativeMutation[]>>? ResolveAnchors { get; init; }
    public bool AllowPortQueries { get; init; }
    public Dictionary<string, NativeSize> ResolvedHosts { get; } = new(StringComparer.Ordinal);
    public string Direction { get; } = direction;
    public CancellationToken Token { get; } = token;
    public List<object> Pools { get; } = [];
    public List<object> Surfaces { get; } = [];
    public void Report(string phase, string owner, double ratio)
    {
        Token.ThrowIfCancellationRequested();
        progress("native_diagram_layout_" + phase + ":" + owner + ":" + ratio.ToString("0.000000", CultureInfo.InvariantCulture));
    }
}
