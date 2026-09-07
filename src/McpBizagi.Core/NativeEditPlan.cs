using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Shared deterministic mutation validation and independent native readback postconditions.</summary>
public static class NativeEditPlan
{
    public static readonly string[] CreatableTypes = ["AbstractTask", "UserTask", "ManualTask", "ServiceTask", "ScriptTask", "SendTask", "ReceiveTask", "BusinessRuleTask",
        "NoneStart", "MessageStart", "TimerStart", "NoneEnd", "MessageEnd", "TerminateEnd", "NoneIntermediate", "MessageIntermediate", "TimerIntermediate",
        "ExclusiveGateway", "InclusiveGateway", "ParallelGateway", "EventBasedGateway", "ComplexGateway", "SubProcess", "Participant", "Lane", "Milestone",
        "SequenceFlow", "MessageFlow", "TextAnnotation", "Group", "DataObject", "DataStoreReference"];

    public static void Validate(NativeMutation[] changes)
    {
        if (changes.Length is < 1 or > 1000) throw new InvalidDataException("Supply 1 to 1000 explicit native mutations.");
        if (changes.Select(c => c.ElementId).Distinct(StringComparer.Ordinal).Count() != changes.Length)
            throw new InvalidDataException("Each identity may appear once per batch; use a subsequent revision for another mutation.");
        foreach (var c in changes)
        {
            Id(c.ElementId);
            if (c.Operation is not "create" and not "update" and not "delete" and not "reconnect") throw new NotSupportedException("Unsupported native mutation.");
            if (c.Operation == "create")
            {
                Id(c.ParentId);
                if (!CreatableTypes.Contains(c.ElementType, StringComparer.Ordinal)) throw new NotSupportedException("Unsupported native element type.");
            }
            else if (c.ParentId != "" || c.ElementType != "") throw new InvalidDataException("Parent and element type apply only to creation.");
            bool connection = c.Operation == "reconnect" || c.Operation == "create" && c.ElementType is "SequenceFlow" or "MessageFlow";
            if (connection)
            {
                Id(c.SourceId); Id(c.TargetId);
                if (c.Points.Length is < 2 or > 10000 || c.Geometry != null) throw new InvalidDataException("Connections require 2 to 10000 points and no node bounds.");
                foreach (var p in c.Points) { Number(p.X); Number(p.Y); }
            }
            else if (c.SourceId != "" || c.TargetId != "" || c.Points.Length != 0) throw new InvalidDataException("Connection fields require creation of a flow or reconnect.");
            if (c.Operation is "delete" or "reconnect" && (c.Name != null || c.Documentation != null || c.Geometry != null))
                throw new InvalidDataException("Delete/reconnect do not accept node property updates.");
            if (c.Operation == "update" && c.Name == null && c.Documentation == null && c.Geometry == null) throw new InvalidDataException("An update must specify an actual property.");
            if (c.Name?.Length > 10000 || c.Documentation?.Length > 1024 * 1024) throw new InvalidDataException("Native text exceeds the operation bound.");
            if (c.Geometry is { } g)
            {
                Number(g.X); Number(g.Y); Number(g.Width); Number(g.Height);
                if (g.Width <= 0 || g.Height <= 0 || g.Expanded) throw new InvalidDataException("Positive node bounds are required; expanded subprocess geometry has a separate contract.");
            }
        }
    }
    private static void Id(string value)
    {
        if (!Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty || id.ToString() != value)
            throw new InvalidDataException("Use canonical lowercase, nonempty native GUID identities.");
    }
    private static void Number(double value)
    {
        if (!double.IsFinite(value) || Math.Abs(value) > 1000000) throw new InvalidDataException("Geometry must contain finite values within the coordinate bound.");
    }
    public static void Verify(NativeMutation[] changes, NativeElement[] elements)
    {
        foreach (var c in changes)
        {
            var matches = elements.Where(e => e.Id == c.ElementId).ToArray();
            if (c.Operation == "delete") { if (matches.Length != 0) throw new InvalidDataException("Deleted native element survived readback."); continue; }
            if (matches.Length != 1) throw new InvalidDataException("Native identity did not survive readback exactly once.");
            var e = matches[0];
            if (c.Operation == "create" && (e.ParentId != c.ParentId || e.ElementType != c.ElementType)) throw new InvalidDataException("Created native type/containment differs from the request.");
            if (c.Name != null && e.Name != c.Name || c.Documentation != null && e.Documentation != c.Documentation) throw new InvalidDataException("Native text readback differs from the request.");
            if (c.SourceId != "" && (e.SourceId != c.SourceId || e.TargetId != c.TargetId || e.Points.Length != c.Points.Length ||
                e.Points.Where((p, i) => !Same(p.X, c.Points[i].X) || !Same(p.Y, c.Points[i].Y)).Any())) throw new InvalidDataException("Native connector readback differs from the request.");
            if (c.Geometry is { } a)
            {
                var b = e.Geometry ?? throw new InvalidDataException("Native geometry disappeared.");
                if (!Same(a.X, b.X) || !Same(a.Y, b.Y) || !Same(a.Width, b.Width) || !Same(a.Height, b.Height) || a.Expanded != b.Expanded ||
                    a.BackgroundArgb.HasValue && a.BackgroundArgb != b.BackgroundArgb || a.BorderArgb.HasValue && a.BorderArgb != b.BorderArgb)
                    throw new InvalidDataException("Native geometry/style readback differs from the request.");
            }
        }
    }
    private static bool Same(double a, double b) => Math.Abs((double)(float)a - b) <= 0.001;
}
