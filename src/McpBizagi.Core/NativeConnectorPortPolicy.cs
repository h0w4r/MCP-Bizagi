using System.Globalization;
using System.Xml.Linq;

namespace McpBizagi.Core;

/// <summary>Explicit port metadata intent; route coordinates and port geometry are separate concerns.</summary>
public static class NativeConnectorPortPolicy
{
    public static void Validate(string? port)
    {
        // The installed 4.3 editor maps midpoint ports 1-4, activity offsets 5-20,
        // pool offsets 5-74 and the unmapped value 0. Unknown readback remains opaque.
        if (port is null or "") return;
        if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value is < 0 or > 74 ||
            value.ToString(CultureInfo.InvariantCulture) != port)
            throw new InvalidDataException("Native port must be empty (clear) or a canonical installed identifier from 0 through 74.");
    }

    public static void Verify(string? requested, string? actual)
    {
        if (requested != null && (requested == "" ? actual is not null and not "" : actual != requested))
            throw new InvalidDataException("Native connector port differs after fresh-worker readback.");
    }

    internal static void Project(XElement before, XElement after, string name, string? requested)
    {
        if (requested == null) return;
        Verify(requested, (string?)after.Attribute(name));
        // Restore only the exact requested attribute for comparison. Other attributes,
        // unknown port values on the opposite endpoint and all children remain protected.
        after.SetAttributeValue(name, (string?)before.Attribute(name));
    }
}
