using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Exact native XPDL call-reference projection; all unrequested XML remains compared.</summary>
public static class NativeCallFidelity
{
    private static readonly XNamespace Xpdl = "http://www.wfmc.org/2009/XPDL2.2";
    public static bool IsCallReference(XElement node) => node.Name == Xpdl + "SubFlow" && node.Parent?.Name == Xpdl + "Implementation" &&
        node.Parent.Parent is { } activity && activity.Name == Xpdl + "Activity" && NativeFidelity.IsNativeNameOwner(activity);

    public static void ProjectTarget(XElement before, XElement after, NativeCallTarget target)
    {
        XElement SubFlow(XElement activity)
        {
            var implementations = activity.Elements(Xpdl + "Implementation").ToArray();
            if (implementations.Length != 1) throw new InvalidDataException("Call activity requires exactly one native Implementation.");
            var calls = implementations[0].Elements(Xpdl + "SubFlow").Where(IsCallReference).ToArray();
            if (calls.Length != 1) throw new InvalidDataException("Call activity requires exactly one native SubFlow.");
            return calls[0];
        }
        var a = SubFlow(before); var b = SubFlow(after);
        if (((string?)b.Attribute("Id") ?? "") != target.ProcessId)
            throw new InvalidDataException("Durable native SubFlow target differs from the requested process.");
        // Restore only the requested reference in the comparison copy. Unknown attributes, child
        // nodes, namespaces and runtime metadata are never removed to make a link test pass.
        b.SetAttributeValue("Id", (string?)a.Attribute("Id"));
    }
}
