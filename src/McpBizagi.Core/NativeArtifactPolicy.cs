using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Explicit artifact content and derived group-label verification, never an archive rewrite.</summary>
public static class NativeArtifactPolicy
{
    private static readonly XNamespace Xpdl = "http://www.wfmc.org/2009/XPDL2.2";
    public static bool IsGroupIdentityReference(XElement element) => element.Name == Xpdl + "Group" &&
        element.Parent is { } owner && owner.Name == Xpdl + "Artifact" && NativeFidelity.IsNativeNameOwner(owner) &&
        (string?)owner.Attribute("ArtifactType") == "Group" && owner.Elements(element.Name).Count() == 1;
    public static void Validate(NativeMutation change)
    {
        if (change.ArtifactProperties is { } patch)
        {
            if (patch.Text == null || patch.Text.Length > 1024 * 1024) throw new InvalidDataException("Supply an artifact Text value within the operation bound.");
            XmlConvert.VerifyXmlChars(patch.Text);
            if (change.Operation == "create" && change.ElementType is not "TextAnnotation" and not "FormattedTextArtifact")
                throw new InvalidDataException("Artifact text requires a text annotation or formatted-text artifact.");
        }
        if (change.Operation == "create") ValidateKind(change, change.ElementType);
    }
    private static void ValidateKind(NativeMutation change, string kind)
    {
        if (change.Name != null && kind is "TextAnnotation" or "FormattedTextArtifact" or "HeaderArtifact")
            throw new InvalidDataException("This artifact does not persist DisplayName; use artifact content or diagram metadata.");
        if (change.Documentation != null && kind == "Group") throw new InvalidDataException("Native group documentation is not persisted by the installed loader.");
        if (kind == "Group" && change.Geometry is { Expanded: false }) throw new InvalidDataException("Native groups require the intrinsic Expanded=true view.");
    }
    public static void Verify(NativeMutation change, NativeElement element, NativeElement[] graph)
    {
        ValidateKind(change, element.Kind);
        if (change.ArtifactProperties is { } patch && (element.Kind is not "TextAnnotation" and not "FormattedTextArtifact" ||
            element.Artifact?.Text != patch.Text)) throw new InvalidDataException("Native artifact text did not survive independent readback.");
        if (element.Kind == "Group" && graph.Count(e => e.Kind == "Collaboration" && e.Id == element.ParentId) != 1)
            throw new InvalidDataException("Native group ownership must remain at the diagram level.");
        if (element.Kind == "HeaderArtifact" && element.Artifact?.HeaderDiagramId != element.DiagramId)
            throw new InvalidDataException("Native header diagram context did not survive independent readback.");
    }
    public static void Project(XElement before, XElement after, NativeMutation change)
    {
        if (before.Name != Xpdl + "Artifact" || after.Name != before.Name || !NativeFidelity.IsNativeNameOwner(before) || !NativeFidelity.IsNativeNameOwner(after))
            throw new InvalidDataException("Artifact projection requires exact native artifact owners.");
        if (change.ArtifactProperties is { } patch)
        {
            string? vendor = (string?)after.Attribute("BizAgiArtifactType");
            if (vendor != "FormattedText" && (vendor != null || (string?)after.Attribute("ArtifactType") != "Annotation"))
                throw new InvalidDataException("Native artifact text carrier has an incompatible kind.");
            ProjectText(before, after, patch.Text!);
        }
        if (change.Name != null && (string?)after.Attribute("ArtifactType") == "Group")
        {
            // Group.Name is a derived duplicate, not another independently renameable identity.
            var a = before.Elements(Xpdl + "Group").ToArray(); var b = after.Elements(Xpdl + "Group").ToArray();
            if (a.Length != 1 || b.Length != 1 || (string?)a[0].Attribute("Id") != change.ElementId ||
                (string?)b[0].Attribute("Id") != change.ElementId || (string?)b[0].Attribute("Name") != change.Name)
                throw new InvalidDataException("Native group label or identity differs from the request.");
            b[0].SetAttributeValue("Name", (string?)a[0].Attribute("Name"));
        }
    }
    private static void ProjectText(XElement before, XElement after, string expected)
    {
        // XPDL's native TextAnnotation carrier is an unqualified attribute, not an XML child.
        // Restore that one verified leaf only; markup is native string content, never reparsed here.
        if (((string?)after.Attribute("TextAnnotation") ?? "") != expected)
            throw new InvalidDataException("Native artifact text differs from the requested content.");
        after.SetAttributeValue("TextAnnotation", (string?)before.Attribute("TextAnnotation"));
    }
}
