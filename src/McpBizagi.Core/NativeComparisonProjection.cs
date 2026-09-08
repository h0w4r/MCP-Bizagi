using System.Xml.Linq;

namespace McpBizagi.Core;

/// <summary>Small, strict transformations of comparison copies; never a native archive writer.</summary>
internal static class NativeComparisonProjection
{
    public static void RemoveVerifiedNode(XElement element)
    {
        var parent = element.Parent ?? throw new InvalidDataException("A comparison projection requires an owned element.");
        if (element.AncestorsAndSelf().Any(e => (string?)e.Attribute(XNamespace.Xml + "space") == "preserve"))
            throw new InvalidDataException("Comparison cannot remove nodes under explicit whitespace preservation.");
        element.Remove();
        // Removing an already-verified requested node can leave indentation as text-only content.
        // Clear only its now-empty wrapper's formatting; comments, text and unknown siblings survive.
        if (parent.Nodes().All(n => n is XText t && string.IsNullOrWhiteSpace(t.Value))) parent.RemoveNodes();
    }
}
