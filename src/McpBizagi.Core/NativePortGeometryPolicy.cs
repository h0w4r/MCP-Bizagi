using System.Globalization;
using System.Text.Json;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Observed perimeter intent plus actual native classification; never a port-to-coordinate table.</summary>
public static class NativePortGeometryPolicy
{
    public const string EditorHash = "eec7db9e1474632e0e712c5df29ddc5b93aecb765cd8bc93422a1007ad8de1a9";
    public sealed record Proof(NativePortObservation Before, NativePortObservation After, string EditorAssetSha256);
    public static bool Offset(string? port) => int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n is >= 5 and <= 74 && port == n.ToString(CultureInfo.InvariantCulture);
    public static bool NeedsQuery(NativeElement flow) => Offset(flow.SourcePort) || Offset(flow.TargetPort);
    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.01;
    private static bool Same(NativePoint a, NativePoint b) => Near(a.X, b.X) && Near(a.Y, b.Y);

    /// <summary>Preserve the observed fractional position on one unambiguous rectangle side.
    /// Native source and proposed-point queries must independently approve the resulting intent.</summary>
    public static NativePoint Candidate(NativeGeometry old, NativeGeometry next, NativePoint observed)
    {
        bool top = Near(observed.Y, old.Y), bottom = Near(observed.Y, old.Y + old.Height),
            left = Near(observed.X, old.X), right = Near(observed.X, old.X + old.Width);
        if (new[] { top, bottom, left, right }.Count(b => b) != 1 || old.Width <= 0 || old.Height <= 0 || next.Width <= 0 || next.Height <= 0 ||
            observed.X < old.X || observed.X > old.X + old.Width || observed.Y < old.Y || observed.Y > old.Y + old.Height)
            throw new InvalidDataException("Offset route has no unique observed perimeter position.");
        double x = left ? next.X : right ? next.X + next.Width : next.X + (observed.X - old.X) / old.Width * next.Width;
        double y = top ? next.Y : bottom ? next.Y + next.Height : next.Y + (observed.Y - old.Y) / old.Height * next.Height;
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new InvalidDataException("Nonfinite offset port intent.");
        // Native routing quantizes coordinates to its whole-unit grid. Rounding
        // is explicit proposal intent, still independently classified afterward.
        return new() { X = (float)Math.Round(x, MidpointRounding.AwayFromZero), Y = (float)Math.Round(y, MidpointRounding.AwayFromZero) };
    }

    public static NativePortQuery Describe(NativeElement[] graph, string id)
    {
        var byId = graph.ToDictionary(e => e.Id); var flow = byId[id];
        // A NativeGeometry also carries optional style patches. Those are not
        // layouter arguments, and a geometric projection intentionally leaves
        // them absent while durable reads contain resolved native colors.
        // Keep the query envelope strictly rectangular; archive fidelity still
        // independently checks the original styles and every untouched entry.
        NativeGeometry Rectangle(string endpoint)
        {
            var b = NativeDiagramSurfacePlanner.Visual(byId[endpoint]);
            return new() { X = b.X, Y = b.Y, Width = b.Width, Height = b.Height };
        }
        return new() { ConnectionId = id, SourceId = flow.SourceId, TargetId = flow.TargetId,
            SourceBounds = Rectangle(flow.SourceId), TargetBounds = Rectangle(flow.TargetId),
            SourcePoint = flow.Points[0], TargetPoint = flow.Points[^1] };
    }

    // Check the native response against immutable caller intent, including the
    // full endpoint rectangle. A successful service return alone is insufficient.
    public static void VerifyReceipt(NativePortQueryRequest request, NativePortQueryReceipt receipt)
    {
        if (receipt.EditorAssetSha256 != EditorHash || !receipt.RegistryUnchanged || request.Queries.Length == 0 ||
            receipt.Observations.Length != request.Queries.Length) throw new InvalidDataException("Incomplete or unverified native port receipt.");
        for (int i = 0; i < request.Queries.Length; i++)
        {
            var observed = receipt.Observations[i];
            if (JsonSerializer.Serialize(request.Queries[i]) != JsonSerializer.Serialize(observed.Query) ||
                observed.Errors.Length != 0 || observed.Route.Length < 2 || observed.Route.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)) ||
                !Same(observed.Route[0], observed.Query.SourcePoint) || !Same(observed.Route[^1], observed.Query.TargetPoint))
                throw new InvalidDataException("Native port service changed requested docking or returned fallback/error evidence.");
        }
    }

    public static void VerifyProofs(NativeElement[] before, NativeElement[] after, string diagram, Proof[] proofs)
    {
        var flows = before.Where(e => e.DiagramId == diagram && NeedsQuery(e)).ToArray();
        if (!proofs.Select(p => p.Before.Query.ConnectionId).Order().SequenceEqual(flows.Select(e => e.Id).Order()))
            throw new InvalidDataException("Native port evidence does not cover the exact offset connection set.");
        foreach (var flow in flows)
        {
            var proof = proofs.Single(p => p.Before.Query.ConnectionId == flow.Id);
            foreach (var (graph, observation) in new[] { (before, proof.Before), (after, proof.After) })
            {
                var expected = Describe(graph, flow.Id);
                VerifyReceipt(new() { Queries = [expected] }, new() { EditorAssetSha256 = proof.EditorAssetSha256, RegistryUnchanged = true, Observations = [observation] });
                // Never normalize away a source port mismatch, including bin
                // changes caused by resizing a shape's width or height.
                if (Offset(flow.SourcePort) && observation.SourcePort != flow.SourcePort || Offset(flow.TargetPort) && observation.TargetPort != flow.TargetPort)
                    throw new InvalidDataException("Observed native port bin differs from preserved port intent.");
            }
        }
    }

    public static void VerifyEndpoint(NativeElement flow, NativeElement endpoint, bool source, Proof[] proofs)
    {
        var proof = proofs.SingleOrDefault(p => p.After.Query.ConnectionId == flow.Id)
            ?? throw new InvalidDataException("Offset route lacks actual native endpoint evidence.");
        var observation = proof.After; var query = observation.Query;
        var expected = source ? query.SourceBounds : query.TargetBounds; var actual = NativeDiagramSurfacePlanner.Visual(endpoint);
        var point = source ? flow.Points[0] : flow.Points[^1]; var port = source ? flow.SourcePort : flow.TargetPort;
        if (proof.EditorAssetSha256 != EditorHash || observation.Errors.Length != 0 || observation.Route.Length < 2 ||
            (source ? query.SourceId : query.TargetId) != endpoint.Id ||
            expected.X != actual.X || expected.Y != actual.Y || expected.Width != actual.Width || expected.Height != actual.Height ||
            !Same(point, source ? query.SourcePoint : query.TargetPoint) ||
            !Same(point, source ? observation.Route[0] : observation.Route[^1]) || port != (source ? observation.SourcePort : observation.TargetPort))
            throw new InvalidDataException("Durable offset route differs from its checked native intent.");
    }
}
