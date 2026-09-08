using System.Security.Cryptography;
using McpBizagi.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace McpBizagi.BizagiAdapter;

/// <summary>Read the vendor's durable site data as bounded JSON, never as executable script.</summary>
public static class NativeWebPublicationReader
{
    public static NativePublicationReadback Read(string entry)
    {
        if (Path.GetFileName(entry) != "index.html") throw new InvalidDataException("Expected the native Web entry document.");
        string root = Path.GetFullPath(Path.GetDirectoryName(entry)!);
        var assets = new List<NativeWebAsset>();
        // Walk explicitly so a link is rejected before traversing it. Output belongs
        // to one operation; this reader never follows links or consults the network.
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            RejectLink(directory);
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                RejectLink(path);
                if (Directory.Exists(path)) { pending.Push(path); continue; }
                if (assets.Count >= 20000) throw new InvalidDataException("Web publication exceeds the asset inventory limit.");
                var info = new FileInfo(path);
                assets.Add(new NativeWebAsset { Path = path.Substring(root.Length + 1).Replace('\\', '/'), Length = info.Length, Sha256 = Hash(path) });
            }
        }
        var inventory = assets.ToDictionary(a => a.Path, StringComparer.OrdinalIgnoreCase);
        foreach (string required in new[] { "index.html", "key.json.js", "libs/js/json/configuration.json.js" })
            if (!inventory.TryGetValue(required, out var asset) || asset.Length == 0) throw new InvalidDataException("Missing native Web asset: " + required);
        JObject model = ReadConfiguration(root);
        if (model["pages"] is not JArray roots || roots.Count == 0) throw new InvalidDataException("Native Web publication has no pages.");
        var pages = new List<NativeWebPage>(); var sizes = new List<NativeImageSize>();
        void Page(JToken page)
        {
            string id = (string?)page["id"] ?? "";
            if (!Guid.TryParseExact(id, "D", out _) || pages.Any(p => p.Id == id)) throw new InvalidDataException("Missing or duplicate native Web page ID.");
            string image = (string?)page["image"] ?? "";
            string normalized = image.Replace('\\', '/');
            if (!normalized.StartsWith("files/diagrams/", StringComparison.Ordinal) || normalized.Split('/').Any(p => p == ".." || p == "." || p == "") || normalized.Contains(':') ||
                !inventory.TryGetValue(normalized, out var asset)) throw new InvalidDataException("Missing or unconfined native Web page image.");
            string imagePath = Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar));
            // Read the actual PNG header, not dimensions reported by the publisher.
            using (var file = File.OpenRead(imagePath))
            {
                byte[] header = new byte[24]; int read = file.Read(header, 0, header.Length);
                byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                if (read != header.Length || !header.Take(8).SequenceEqual(signature) || System.Text.Encoding.ASCII.GetString(header, 12, 4) != "IHDR")
                    throw new InvalidDataException("Invalid native Web diagram PNG header.");
                int Dimension(int offset) => checked((int)((uint)header[offset] << 24 | (uint)header[offset + 1] << 16 | (uint)header[offset + 2] << 8 | header[offset + 3]));
                var size = new NativeImageSize { Width = Dimension(16), Height = Dimension(20) };
                if (size.Width <= 0 || size.Height <= 0) throw new InvalidDataException("Invalid native Web diagram dimensions.");
                sizes.Add(size);
            }
            pages.Add(new NativeWebPage { Id = id, ParentId = (string?)page["parentRef"] ?? "", Name = (string?)page["name"] ?? "", ImagePath = normalized, ImageSha256 = asset.Sha256 });
            if (page["subPages"] is JArray children) foreach (JToken child in children) Page(child);
        }
        foreach (JToken page in roots) Page(page);
        var web = new NativeWebPublication
        {
            ModelName = (string?)model["modelName"] ?? "", Pages = pages.ToArray(), Assets = assets.ToArray(),
            RootPageIds = roots.Select(p => (string)p["id"]!).ToArray(),
            SearchContainerIds = (model["searchMap"] as JArray ?? throw new InvalidDataException("Missing native search map."))
                .Select(p => (string?)p["containerId"] ?? throw new InvalidDataException("Missing native search container ID.")).ToArray()
        };
        return new NativePublicationReadback { Format = "web", Web = web, PagesOrSheets = pages.Count, Images = sizes.Count,
            ImageSizes = sizes.ToArray(), Text = string.Join("\n", model.Descendants().OfType<JValue>().Where(v => v.Type == JTokenType.String).Select(v => (string?)v)) };
    }

    internal static JObject ReadConfiguration(string root)
    {
        string configuration = Path.Combine(root, "libs", "js", "json", "configuration.json.js");
        if (new FileInfo(configuration).Length > 32 * 1024 * 1024) throw new InvalidDataException("Native Web configuration exceeds the bounded reader limit.");
        string raw = File.ReadAllText(configuration);
        const string prefix = "Bizagi.AppModel = ";
        if (!raw.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Unexpected native Web configuration envelope.");
        using var reader = new JsonTextReader(new StringReader(raw.Substring(prefix.Length))) { MaxDepth = 128, DateParseHandling = DateParseHandling.None };
        JObject model = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (reader.Read()) throw new InvalidDataException("Unexpected trailing Web configuration content.");
        return model;
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Links are not supported inside Web publications.");
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path); using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}
