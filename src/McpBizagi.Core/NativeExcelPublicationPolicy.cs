using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Validate native Excel pool projection without exempting arbitrary missing names.</summary>
public static class NativeExcelPublicationPolicy
{
    public static void VerifyReadback(NativeExcelPoolProjection[] projection, NativeElement[] source, NativeExcelRow[] rows)
    {
        // The installed sheet/row mapper writes each participant and immediate
        // process element identity in column zero and its name in column one.
        // IDs embedded in descriptions or the hidden index cannot prove these rows.
        var byId = source.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var expected = projection.Where(p => p.MappedElementIds.Length != 0)
            .SelectMany(p => p.MappedElementIds.Prepend(p.ElementId)).Distinct(StringComparer.Ordinal);
        foreach (string id in expected)
        {
            if (!byId.TryGetValue(id, out var element) || !rows.Any(row => row.RowNumber > 0 && !string.IsNullOrWhiteSpace(row.Sheet) &&
                row.ElementId == id && (string.IsNullOrWhiteSpace(element.Name) || row.Name == element.Name)))
                throw new InvalidDataException("Native Excel visible row identity/name did not survive independent workbook readback.");
        }
    }

    public static NativePublicationOmission[] Verify(NativeElement[] source, NativeExcelPoolProjection[]? projection, string[] selected)
    {
        if (projection is null) throw new InvalidDataException("Missing native Excel pool projection.");
        var pools = source.Where(e => e.Kind == "Participant" && (selected.Length == 0 || selected.Contains(e.DiagramId)))
            .ToDictionary(e => e.Id, StringComparer.Ordinal);
        if (projection.Select(p => p.ElementId).Distinct(StringComparer.Ordinal).Count() != projection.Length ||
            !pools.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(projection.Select(p => p.ElementId)))
            throw new InvalidDataException("Native Excel pool projection does not match selected source participants.");
        var omissions = new List<NativePublicationOmission>();
        foreach (var projected in projection)
        {
            var pool = pools[projected.ElementId];
            if (projected.DiagramId != pool.DiagramId || projected.MappedElementIds.Distinct(StringComparer.Ordinal).Count() != projected.MappedElementIds.Length)
                throw new InvalidDataException("Native Excel projection has inconsistent ownership or duplicate child identities.");
            var processes = source.Where(e => e.Kind == "Process" && e.ParentId == pool.Id && e.DiagramId == pool.DiagramId).ToArray();
            if (processes.Length != 1) throw new InvalidDataException("Native Excel participant must own one source process.");
            // Be conservative: do not silently discard any source-owned item merely
            // because the mapper produced an empty list. LaneSet is structural only.
            var children = source.Where(e => e.ParentId == processes[0].Id && e.Kind != "LaneSet").ToArray();
            if (projected.MappedElementIds.Any(id => !children.Any(e => e.Id == id)))
                throw new InvalidDataException("Native Excel projection contains an unknown or foreign process child.");
            if (!children.Select(e => e.Id).ToHashSet(StringComparer.Ordinal).SetEquals(projected.MappedElementIds))
                throw new InvalidDataException("Native Excel mapped child inventory differs from the source process; publication is not accredited.");
            if (projected.MappedElementIds.Length != 0) continue;
            omissions.Add(new NativePublicationOmission
            {
                Code = "native_excel_empty_pool_sheet_omitted", DiagramId = pool.DiagramId, ElementId = pool.Id, Name = pool.Name,
                Message = "The installed Excel generator emits no sheet for this pool because its mapped process has no child elements. The pool's name, documentation and attributes are not promised in this workbook. The original native model is unchanged."
            });
        }
        return omissions.ToArray();
    }
}
