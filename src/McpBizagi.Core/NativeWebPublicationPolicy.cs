using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Compare independent Web readback to the actual native graph and rendered bytes.</summary>
public static class NativeWebPublicationPolicy
{
    public static void Verify(NativeElement[] source, NativeWebPublication web, string[] selected, string title, IReadOnlyDictionary<string, string> renderedImages)
    {
        var byId = source.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var roots = source.Where(e => e.Kind == "Collaboration" && (selected.Length == 0 || selected.Contains(e.Id)))
            .Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var rendered = renderedImages.Keys.ToHashSet(StringComparer.Ordinal);
        if (!roots.SetEquals(web.RootPageIds) || web.RootPageIds.Distinct(StringComparer.Ordinal).Count() != web.RootPageIds.Length || web.ModelName != title)
            throw new InvalidDataException("Native Web selection/title readback mismatch.");
        if (!rendered.SetEquals(web.Pages.Select(p => p.Id)) || !rendered.SetEquals(web.SearchContainerIds) ||
            web.Pages.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != web.Pages.Length ||
            web.SearchContainerIds.Distinct(StringComparer.Ordinal).Count() != web.SearchContainerIds.Length ||
            web.Pages.Any(p => renderedImages[p.Id] != p.ImageSha256))
            throw new InvalidDataException("Native Web page/search inventory or image bytes differ from actual rendered surfaces.");
        foreach (var page in web.Pages)
        {
            if (!byId.TryGetValue(page.Id, out var element)) throw new InvalidDataException("Native Web page has no source identity.");
            string expectedParent = "";
            if (element.Kind != "Collaboration")
            {
                expectedParent = element.DiagramId;
                string owner = element.ParentId;
                var visited = new HashSet<string>(StringComparer.Ordinal);
                while (byId.TryGetValue(owner, out var parent))
                {
                    if (!visited.Add(owner)) throw new InvalidDataException("Cyclic native Web page containment.");
                    if (parent.SubProcess != null) { expectedParent = parent.Id; break; }
                    owner = parent.ParentId;
                }
            }
            if (page.ParentId != expectedParent || (!string.IsNullOrWhiteSpace(element.Name) && page.Name != element.Name))
                throw new InvalidDataException("Native Web page name or parent navigation differs from the native source.");
        }
    }
}
