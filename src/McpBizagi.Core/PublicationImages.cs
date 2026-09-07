using McpBizagi.Contracts;

namespace McpBizagi.Core;

public sealed record PublicationImageMatch(int SourceWidth, int SourceHeight, int OutputWidth, int OutputHeight, bool Resampled)
{
    public string Fidelity => Resampled ? "explicitly_accepted_native_pdf_downsampling_not_pixel_equivalence" : "dimensions_preserved_not_pixel_equivalence";
}

/// <summary>Do not let cover logos satisfy a diagram-image count or silently accept native PDF downsampling.</summary>
public static class PublicationImages
{
    public static PublicationImageMatch[] Match(NativeImageSize[] expected, NativeImageSize[] actual, bool allowResampling = false)
    {
        if (expected.Concat(actual).Any(i => i.Width <= 0 || i.Height <= 0)) throw new InvalidDataException("Invalid publication image dimensions.");
        var remaining = actual.ToList(); var matches = new List<PublicationImageMatch>();
        var pending = new List<NativeImageSize>();
        // Reserve exact matches first so a downsample candidate cannot consume another source's exact image.
        foreach (var source in expected)
        {
            var found = remaining.FirstOrDefault(i => i.Width == source.Width && i.Height == source.Height);
            if (found == null) { pending.Add(source); continue; }
            remaining.Remove(found); matches.Add(new(source.Width, source.Height, found.Width, found.Height, false));
        }
        var assignments = new Dictionary<int, int>();
        bool Assign(int sourceIndex, HashSet<int> visited)
        {
            var source = pending[sourceIndex];
            // Augmenting paths avoid a greedy match consuming the only candidate for another diagram.
            for (int index = 0; index < remaining.Count; index++)
            {
                var image = remaining[index];
                if (!allowResampling || image.Width > source.Width || image.Height > source.Height ||
                    image.Width < source.Width / 2.0 || image.Height < source.Height / 2.0 ||
                    Math.Abs((decimal)image.Width * source.Height - (decimal)image.Height * source.Width) > 0.002m * source.Width * source.Height || !visited.Add(index)) continue;
                if (!assignments.TryGetValue(index, out int previous) || Assign(previous, visited))
                { assignments[index] = sourceIndex; return true; }
            }
            return false;
        }
        for (int i = 0; i < pending.Count; i++)
            if (!Assign(i, new HashSet<int>())) throw new InvalidDataException("Publication omits or resamples a diagram image. PDF downsampling requires explicit allowImageResampling; inspect the image evidence.");
        foreach (var pair in assignments)
        {
            var source = pending[pair.Value]; var image = remaining[pair.Key];
            matches.Add(new(source.Width, source.Height, image.Width, image.Height, true));
        }
        return matches.ToArray();
    }
}
