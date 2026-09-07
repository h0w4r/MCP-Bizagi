using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static NativeArtifactInfo? DescribeArtifact(object element)
    {
        if (Optional(element, "ArtifactType") == null) return null;
        string kind = element.GetType().Name;
        return new NativeArtifactInfo
        {
            Type = Text(element, "ArtifactType"),
            Text = kind == "TextAnnotation" ? Text(Get(element, "Text"), "Content") : kind == "FormattedTextArtifact" ? Text(element, "Text") : null,
            TextFormat = kind == "TextAnnotation" ? Text(element, "TextFormat") : null,
            HeaderDiagramId = kind == "HeaderArtifact" ? Optional(element, "Diagram") is object diagram ? Text(diagram, "Id") : "" : null
        };
    }

    private static void ApplyArtifactProperties(object element, NativeArtifactProperties patch)
    {
        // Use the actual native text carrier; DisplayName is not persisted for these artifact kinds.
        if (patch.Text == null) throw new InvalidDataException("ArtifactProperties requires an explicit Text value.");
        switch (element.GetType().Name)
        {
            case "TextAnnotation": Set(Get(element, "Text"), "Content", patch.Text); break;
            case "FormattedTextArtifact": Set(element, "Text", patch.Text); break;
            default: throw new InvalidDataException("Artifact text requires a text annotation or formatted-text artifact.");
        }
    }

    private static void ValidateArtifactMutation(object element, NativeMutation change)
    {
        string kind = element.GetType().Name;
        if (change.Name != null && kind is "TextAnnotation" or "FormattedTextArtifact" or "HeaderArtifact")
            throw new InvalidDataException("This native artifact does not persist DisplayName; use artifact content or diagram metadata instead.");
        if (change.Documentation != null && kind == "Group")
            throw new InvalidDataException("The installed group loader does not preserve group documentation.");
        if (kind == "Group" && change.Geometry is { Expanded: false })
            throw new InvalidDataException("Native groups require their intrinsic Expanded=true view; they are not collapsible subprocesses.");
        if (kind != "Group" && change.Geometry?.Expanded == true && change.ExpandedSize == null)
            throw new InvalidDataException("Subprocess expansion requires an explicit ExpandedSize.");
    }

    private static void PrepareArtifact(object element, object parent, object diagram)
    {
        string kind = element.GetType().Name;
        if (kind == "Group" && parent.GetType().Name != "Collaboration")
            throw new InvalidDataException("Native groups require a diagram parent, not a process or subprocess.");
        if (kind == "Group") Set(Get(element, "GraphicalProperties"), "Expanded", true);
        if (kind is "TextAnnotation" or "FormattedTextArtifact" or "HeaderArtifact" && parent.GetType().Name == "Collaboration")
            throw new InvalidDataException("Native content artifacts require a process or embedded subprocess parent.");
        if (kind == "HeaderArtifact")
        {
            // The installed loader wires root-level header context to its collaboration.
            if (parent.GetType().Name != "Process") throw new InvalidDataException("Native headers require a root process parent.");
            Set(element, "Diagram", diagram);
        }
    }
}
