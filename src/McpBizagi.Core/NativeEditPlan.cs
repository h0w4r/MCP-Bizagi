using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Shared deterministic mutation validation and independent native readback postconditions.</summary>
public static class NativeEditPlan
{
    public static readonly string[] CreatableTypes = ["AbstractTask", "UserTask", "ManualTask", "ServiceTask", "ScriptTask", "SendTask", "ReceiveTask", "BusinessRuleTask",
        "NoneStart", "MessageStart", "TimerStart", "NoneEnd", "MessageEnd", "TerminateEnd", "NoneIntermediate", "MessageIntermediate", "TimerIntermediate",
        "ExclusiveGateway", "InclusiveGateway", "ParallelGateway", "EventBasedGateway", "ComplexGateway", "SubProcess", "CallActivity", "Participant", "Lane", "Milestone",
        "SequenceFlow", "MessageFlow", "TextAnnotation", "Group", "DataObject", "DataStoreReference"];

    public static void Validate(NativeMutation[] changes)
    {
        if (changes.Length is < 1 or > 1000) throw new InvalidDataException("Supply 1 to 1000 explicit native mutations.");
        if (changes.Select(c => c.ElementId).Distinct(StringComparer.Ordinal).Count() != changes.Length)
            throw new InvalidDataException("Each identity may appear once per batch; use a subsequent revision for another mutation.");
        var ownedIds = changes.Select(c => c.ElementId).Concat(changes.Where(c => c.ProcessId != "").Select(c => c.ProcessId)).ToArray();
        if (ownedIds.Distinct(StringComparer.Ordinal).Count() != ownedIds.Length)
            throw new InvalidDataException("Created process identities must be unique and distinct from mutation identities.");
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
            if (c.Operation == "create" && c.ElementType == "Participant") Id(c.ProcessId);
            else if (c.ProcessId != "") throw new InvalidDataException("ProcessId applies only to participant creation.");
            bool connection = c.Operation == "reconnect" || c.Operation == "create" && c.ElementType is "SequenceFlow" or "MessageFlow";
            if (connection)
            {
                Id(c.SourceId); Id(c.TargetId);
                if (c.Points.Length is < 2 or > 10000 || c.Geometry != null) throw new InvalidDataException("Connections require 2 to 10000 points and no node bounds.");
                foreach (var p in c.Points) { Number(p.X); Number(p.Y); }
            }
            else if (c.SourceId != "" || c.TargetId != "" || c.Points.Length != 0) throw new InvalidDataException("Connection fields require creation of a flow or reconnect.");
            if (c.Operation is "delete" or "reconnect" && (c.Name != null || c.Documentation != null || c.Geometry != null || c.ExpandedSize != null || c.CallTarget != null || c.ActivityProperties != null || c.ActivityLoop != null || c.FlowCondition != null || c.GatewayDirection != null))
                throw new InvalidDataException("Delete/reconnect do not accept node property updates.");
            if (c.Operation == "update" && c.Name == null && c.Documentation == null && c.Geometry == null && c.CallTarget == null && c.ActivityProperties == null && c.ActivityLoop == null && c.FlowCondition == null && c.GatewayDirection == null) throw new InvalidDataException("An update must specify an actual property.");
            NativeSemanticPolicy.Validate(c);
            if (c.ActivityLoop != null)
            {
                NativeLoopPolicy.Validate(c.ActivityLoop);
                if (c.Operation == "create" && !c.ElementType.EndsWith("Task", StringComparison.Ordinal) && c.ElementType is not "SubProcess" and not "CallActivity")
                    throw new InvalidDataException("ActivityLoop applies only to native activities.");
            }
            if (c.CallTarget is { } call)
            {
                if (call.ProcessId != "") Id(call.ProcessId);
                if (c.Operation == "create" && c.ElementType != "CallActivity") throw new InvalidDataException("CallTarget applies only to call activities.");
            }
            if (c.Name?.Length > 10000 || c.Documentation?.Length > 1024 * 1024) throw new InvalidDataException("Native text exceeds the operation bound.");
            if (c.Geometry is { } g)
            {
                Number(g.X); Number(g.Y); Number(g.Width); Number(g.Height);
                if (g.Width <= 0 || g.Height <= 0 || g.Expanded && c.ExpandedSize == null)
                    throw new InvalidDataException("Positive node bounds are required; expanding requires an explicit ExpandedSize.");
            }
            if (c.ExpandedSize is { } size)
            {
                Number(size.Width); Number(size.Height);
                if (size.Width <= 0 || size.Height <= 0 || c.Geometry == null || c.Operation == "create" && c.ElementType != "SubProcess")
                    throw new InvalidDataException("ExpandedSize requires positive dimensions and explicit embedded-subprocess node geometry.");
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
            NativeSemanticPolicy.Verify(c, e, elements);
            if (c.ActivityLoop != null) NativeLoopPolicy.Verify(c.ActivityLoop, e.ActivityLoop);
            if (c.Operation == "create" && (e.ParentId != c.ParentId || e.ElementType != c.ElementType)) throw new InvalidDataException("Created native type/containment differs from the request.");
            if (c.ProcessId != "" && elements.Count(p => p.Id == c.ProcessId && p.Kind == "Process" && p.ParentId == c.ElementId && p.DiagramId == e.DiagramId) != 1)
                throw new InvalidDataException("Created participant process identity/ownership differs from the request.");
            if (c.Name != null && e.Name != c.Name || c.Documentation != null && e.Documentation != c.Documentation) throw new InvalidDataException("Native text readback differs from the request.");
            if (c.CallTarget is { } call)
            {
                var reference = e.CallReference;
                if (e.Kind != "CallActivity" || reference == null || reference.CatalogProcessId != call.ProcessId || reference.External != null || reference.BpmnNamespace != "" ||
                    reference.BpmnName != "" && (call.ProcessId == "" || reference.BpmnName != call.ProcessId && reference.BpmnName != "Id_" + call.ProcessId))
                    throw new InvalidDataException("Native call target did not survive fresh-worker readback.");
                if (call.ProcessId != "" && elements.Count(p => p.Kind == "Process" && p.Id == call.ProcessId) != 1)
                    throw new InvalidDataException("Called native process is not present exactly once after readback.");
            }
            if (c.SourceId != "" && (e.SourceId != c.SourceId || e.TargetId != c.TargetId || e.Points.Length != c.Points.Length ||
                e.Points.Where((p, i) => !Same(p.X, c.Points[i].X) || !Same(p.Y, c.Points[i].Y)).Any())) throw new InvalidDataException("Native connector readback differs from the request.");
            if (c.Geometry is { } a)
            {
                var b = e.Geometry ?? throw new InvalidDataException("Native geometry disappeared.");
                if (!Same(a.X, b.X) || !Same(a.Y, b.Y) || !Same(a.Width, b.Width) || !Same(a.Height, b.Height) || a.Expanded != b.Expanded ||
                    a.BackgroundArgb.HasValue && a.BackgroundArgb != b.BackgroundArgb || a.BorderArgb.HasValue && a.BorderArgb != b.BorderArgb)
                    throw new InvalidDataException("Native geometry/style readback differs from the request.");
            }
            if (c.ExpandedSize is { } size && (e.ExpandedGeometry is not { } expanded || !Same(size.Width, expanded.Width) || !Same(size.Height, expanded.Height)))
                throw new InvalidDataException("Native expanded subprocess size differs from the request.");
        }
    }
    private static bool Same(double a, double b) => Math.Abs((double)(float)a - b) <= 0.001;
}
