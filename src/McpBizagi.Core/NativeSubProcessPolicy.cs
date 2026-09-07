using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Subprocess intent, independent native readback and exact ActivitySet attribute projection.</summary>
public static class NativeSubProcessPolicy
{
    public static readonly string[] Kinds = ["SubProcess", "Transaction", "AdHoc"];
    private static readonly XNamespace Ns = "http://www.wfmc.org/2009/XPDL2.2";

    public static void Validate(NativeMutation change)
    {
        if (change.SubProcessKind != null && (change.Operation != "create" || change.ElementType != "SubProcess" || !Kinds.Contains(change.SubProcessKind)))
            throw new InvalidDataException("SubProcessKind is a SubProcess, Transaction or AdHoc creation option for embedded subprocesses only.");
        if (change.SubProcessProperties is not { } p) return;
        if (change.Operation is not "create" and not "update" || change.Operation == "create" && change.ElementType != "SubProcess" ||
            p.TriggeredByEvent == null && p.AdHocOrdering == null && p.AdHocCompletionCondition == null)
            throw new InvalidDataException("SubProcessProperties requires a nonempty embedded-subprocess create/update patch.");
        if (p.AdHocOrdering != null && p.AdHocOrdering is not "Parallel" and not "Sequential") throw new InvalidDataException("AdHocOrdering must be Parallel or Sequential.");
        if (p.AdHocCompletionCondition?.Length > 1024 * 1024) throw new InvalidDataException("Ad hoc completion text exceeds the native operation bound.");
        if (change.Operation == "create")
        {
            string kind = change.SubProcessKind ?? "SubProcess";
            if (p.TriggeredByEvent == true && kind != "SubProcess") throw new InvalidDataException("Event-triggered subprocess creation cannot also select Transaction or AdHoc.");
            if ((p.AdHocOrdering != null || p.AdHocCompletionCondition != null) && kind != "AdHoc") throw new InvalidDataException("Ad hoc properties require an AdHoc subprocess.");
        }
    }

    public static void Verify(NativeMutation change, NativeElement element)
    {
        var actual = element.SubProcess;
        if (change.Operation == "create" && change.ElementType == "SubProcess" && (actual == null || actual.Kind != (change.SubProcessKind ?? "SubProcess")))
            throw new InvalidDataException("Created native subprocess kind differs after fresh-worker readback.");
        if (change.SubProcessProperties is not { } p) return;
        if (actual == null || p.TriggeredByEvent.HasValue && p.TriggeredByEvent != actual.TriggeredByEvent ||
            p.AdHocOrdering != null && p.AdHocOrdering != actual.AdHocOrdering ||
            p.AdHocCompletionCondition != null && p.AdHocCompletionCondition != actual.AdHocCompletionCondition)
            throw new InvalidDataException("Native subprocess properties differ after fresh-worker readback.");
    }

    public static void Project(XElement before, XElement after, XElement? oldSet, XElement? newSet, NativeSubProcessProperties patch)
    {
        if (before.Name != Ns + "Activity" || after.Name != before.Name || !NativeFidelity.IsNativeNameOwner(before) || !NativeFidelity.IsNativeNameOwner(after) ||
            oldSet?.Name != Ns + "ActivitySet" || newSet?.Name != oldSet.Name || !NativeFidelity.IsNativeNameOwner(oldSet) || !NativeFidelity.IsNativeNameOwner(newSet))
            throw new InvalidDataException("Subprocess projection requires actual native Activity and ActivitySet owners.");
        string? id = (string?)oldSet.Attribute("Id");
        var oldBlocks = before.Elements(Ns + "BlockActivity").ToArray(); var newBlocks = after.Elements(Ns + "BlockActivity").ToArray();
        if (id == null || id != (string?)newSet.Attribute("Id") || id != (string?)before.Attribute("Id") || id != (string?)after.Attribute("Id") ||
            oldBlocks.Length != 1 || newBlocks.Length != 1 || id != (string?)oldBlocks[0].Attribute("ActivitySetId") || id != (string?)newBlocks[0].Attribute("ActivitySetId") ||
            before.Ancestors(Ns + "WorkflowProcess").Single() != oldSet.Ancestors(Ns + "WorkflowProcess").Single() ||
            after.Ancestors(Ns + "WorkflowProcess").Single() != newSet.Ancestors(Ns + "WorkflowProcess").Single())
            throw new InvalidDataException("Subprocess companion reference changed or became ambiguous.");
        void Scalar(string field, string expected, string absent)
        {
            if (((string?)newSet.Attribute(field) ?? absent) != expected) throw new InvalidDataException("Durable native subprocess attribute differs: " + field);
            newSet.SetAttributeValue(field, (string?)oldSet.Attribute(field));
        }
        // Only three requested attributes are projected; descendants, unknown fields and
        // namespaces remain compared. This policy never writes a native model.
        if (patch.TriggeredByEvent is bool triggered) Scalar("TriggeredByEvent", triggered ? "true" : "false", "false");
        if (patch.AdHocOrdering != null || patch.AdHocCompletionCondition != null)
        {
            if ((string?)oldSet.Attribute("AdHoc") != "true" || (string?)newSet.Attribute("AdHoc") != "true")
                throw new InvalidDataException("Ad hoc projection requires two actual AdHoc activity sets.");
            if (patch.AdHocOrdering != null) Scalar("AdHocOrdering", patch.AdHocOrdering, "Parallel");
            if (patch.AdHocCompletionCondition != null) Scalar("AdHocCompletionCondition", patch.AdHocCompletionCondition, "");
        }
    }
}
