using System.Text.Json;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Explicit blank-model creation and independent durable identity verification.</summary>
public static class NativeModelCreation
{
    public static NativeDiagramPatch Prepare(string[] names)
    {
        if (names == null || names.Length is < 1 or > 100) throw new InvalidDataException("Supply 1-100 initial diagram names.");
        foreach (string name in names)
        {
            // Diagram names also become native BPMN export filenames, irrespective of the host OS.
            if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || name is "." or ".." || name.EndsWith('.') || name.EndsWith(' ') ||
                name.Any(c => c < 32 || "<>:\"/\\|?*".Contains(c))) throw new InvalidDataException("Initial diagram name is not a safe native export label.");
            string stem = name.Split('.')[0].ToUpperInvariant();
            if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem)) throw new InvalidDataException("Reserved native export label.");
        }
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length) throw new InvalidDataException("Initial diagram names must be unique ignoring case.");
        var changes = names.Select(name => new NativeDiagramChange { Operation = "create", DiagramId = Guid.NewGuid().ToString(), Name = name }).ToArray();
        return new NativeDiagramPatch
        {
            Changes = changes,
            OpenedItems = changes.Select((c, index) => new NativeOpenedItem { DiagramId = c.DiagramId, IsSelected = index == 0 }).ToArray()
        };
    }

    public static void Verify(byte[] durable, NativeDiagramPatch request, EngineReply created, EngineReply reopened)
    {
        NativeDiagramPolicy.Validate(request);
        if (request.Changes.Length == 0 || request.Changes.Any(c => c.Operation != "create")) throw new InvalidDataException("Model creation requires only explicit new diagrams.");
        var wanted = request.Changes.ToDictionary(c => c.DiagramId, c => c.Name!);
        var diagrams = NativeDiagramPolicy.Diagrams(NativeArchive.ReadEntries(durable));
        if (diagrams.Count != wanted.Count || diagrams.Any(p => !wanted.TryGetValue(p.Key, out var name) || name != p.Value))
            throw new InvalidDataException("Persisted initial diagram identities/names differ from the creation request.");
        foreach (var reply in new[] { created, reopened })
        {
            var state = reply.DiagramState ?? throw new InvalidDataException("Missing native creation diagram snapshot.");
            if (!reply.Success || state.Diagrams.Length != wanted.Count || state.Diagrams.Select(d => d.Id).Distinct().Count() != wanted.Count ||
                state.Diagrams.Any(d => !wanted.TryGetValue(d.Id, out var name) || name != d.Name)) throw new InvalidDataException("Native creation snapshot differs from the request.");
            if (reply.Elements.Length != wanted.Count * 5 || reply.Elements.Select(e => e.Id).Distinct().Count() != reply.Elements.Length || reply.Scenarios.Length != wanted.Count)
                throw new InvalidDataException("Blank native model contains missing, duplicate or extra elements/scenarios.");
            foreach (string id in wanted.Keys)
            {
                var nodes = reply.Elements.Where(e => e.DiagramId == id).ToArray();
                if (nodes.Length != 5 || nodes.Count(e => e.Kind == "Collaboration" && e.Id == id && e.Name == wanted[id] && e.ParentId == "") != 1 ||
                    nodes.Count(e => e.Kind == "Participant" && e.IsMainParticipant == true) != 1 || nodes.Count(e => e.Kind == "Participant" && e.IsMainParticipant == false) != 1 ||
                    nodes.Count(e => e.Kind == "Process") != 2 || reply.Scenarios.Count(s => s.DiagramId == id) != 1)
                    throw new InvalidDataException("Blank diagram did not retain installed native defaults.");
            }
            if (JsonSerializer.Serialize(state.OpenedItems) != JsonSerializer.Serialize(request.OpenedItems))
                throw new InvalidDataException("Initial ordered/selected diagram preferences did not survive native persistence.");
        }
        // Compare all exposed semantics, not only names; the separate no-op gate covers unexposed archive content.
        if (JsonSerializer.Serialize(created.Elements.OrderBy(e => e.Id)) != JsonSerializer.Serialize(reopened.Elements.OrderBy(e => e.Id)) ||
            JsonSerializer.Serialize(created.Scenarios.OrderBy(s => s.DiagramId).ThenBy(s => s.Id)) != JsonSerializer.Serialize(reopened.Scenarios.OrderBy(s => s.DiagramId).ThenBy(s => s.Id)))
            throw new InvalidDataException("Native blank model semantics changed during fresh-worker readback.");
    }
}
