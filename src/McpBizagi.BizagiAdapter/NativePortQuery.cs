using McpBizagi.Contracts;
using Newtonsoft.Json;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private NativePortQueryReceipt QueryPorts(object model, object persistence, EngineRequest request, Action<string> progress)
    {
        var query = request.PortQuery ?? throw new InvalidDataException("Missing native port query.");
        if (request.OutputPath != "" || request.Mutations.Length != 0 || request.Changes.Length != 0 ||
            query.Queries.Length == 0 || query.Queries.Length > 1000 ||
            query.Queries.Select(q => q.ConnectionId).Distinct().Count() != query.Queries.Length)
            throw new InvalidDataException("Port queries require a bounded unique connection set and no write intent.");
        var graph = Graph(model).Select(Describe).ToArray(); var byId = graph.ToDictionary(e => e.Id);
        if (!byId.TryGetValue(query.DiagramId, out var diagram) || diagram.Kind != "Collaboration" ||
            query.SubProcessId != "" && (!byId.TryGetValue(query.SubProcessId, out var sub) || sub.SubProcess == null || sub.DiagramId != query.DiagramId))
            throw new InvalidDataException("Unknown native query surface.");
        foreach (var item in query.Queries)
        {
            if (!byId.TryGetValue(item.ConnectionId, out var connection) || connection.DiagramId != query.DiagramId ||
                connection.Kind is not ("SequenceFlow" or "MessageFlow" or "Association") ||
                connection.SourceId != item.SourceId || connection.TargetId != item.TargetId ||
                !byId.TryGetValue(item.SourceId, out var source) || !byId.TryGetValue(item.TargetId, out var target))
                throw new InvalidDataException("Port query does not identify an existing native connection.");
            // Boundary routing changes the effective endpoint to its host. Its
            // numeric result cannot accredit an ordinary endpoint rectangle.
            if (source.Kind == "BoundaryEvent" || target.Kind == "BoundaryEvent")
                throw new NotSupportedException("Host-relative boundary ports require a separate native docking contract.");
            string surface = byId[connection.ParentId].SubProcess != null ? connection.ParentId : "";
            if (surface != query.SubProcessId) throw new InvalidDataException("Cross-surface port query.");
            foreach (var b in new[] { item.SourceBounds, item.TargetBounds })
                if (b == null || b.Width <= 0 || b.Height <= 0 || new[] { b.X, b.Y, b.Width, b.Height }
                    .Any(n => double.IsNaN(n) || double.IsInfinity(n) || Math.Abs(n) > 1000000))
                    throw new InvalidDataException("Invalid query rectangle.");
            foreach (var p in new[] { item.SourcePoint, item.TargetPoint })
                if (p == null || new[] { (double)p.X, p.Y }.Any(n => double.IsNaN(n) || double.IsInfinity(n) || Math.Abs(n) > 1000000))
                    throw new InvalidDataException("Invalid query endpoint.");
        }
        string before = JsonConvert.SerializeObject(graph), hash = AlignmentHash(File.ReadAllBytes(request.InputPath));
        var receipt = Align(model, persistence, new() { Action = "port_query", PortQuery = query,
            AtomicStepSeconds = request.AtomicStepSeconds, InactivitySeconds = request.InactivitySeconds,
            Alignment = new() { Mode = "PortQuery", DiagramId = query.DiagramId, SubProcessId = query.SubProcessId,
                ElementIds = query.Queries.Select(q => q.SourceId).Distinct().ToArray() } }, progress);
        if (!receipt.NoOp || before != JsonConvert.SerializeObject(Graph(model).Select(Describe)) ||
            hash != AlignmentHash(File.ReadAllBytes(request.InputPath))) throw new InvalidDataException("Port query changed native input.");
        var result = JsonConvert.DeserializeObject<NativePortQueryReceipt>(File.ReadAllText(Path.Combine(workRoot, "native-port-query.json")))!;
        result.EditorAssetSha256 = receipt.EditorAssetSha256;
        if (!result.RegistryUnchanged || result.Observations.Length != query.Queries.Length) throw new InvalidDataException("Incomplete read-only port query evidence.");
        return result;
    }
}
