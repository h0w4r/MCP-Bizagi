using System.Security.Cryptography;
using McpBizagi.BizagiAdapter;
using Newtonsoft.Json.Linq;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Filesystem/JSON policy fixtures only; these never accredit a native generator or browser.</summary>
public sealed class NativeWebPublicationReaderTests
{
    [Fact]
    public void ReadsDurableNestedPagesAndExactImageHashesWithoutExecutingConfiguration()
    {
        using var fixture = new Site();
        var result = NativeWebPublicationReader.Read(fixture.Entry);
        Assert.Equal("web", result.Format);
        Assert.Equal(2, result.PagesOrSheets);
        Assert.Equal(2, result.Images);
        Assert.Equal("Publication Ω 日本語", result.Web!.ModelName);
        Assert.Equal([fixture.RootId], result.Web.RootPageIds);
        Assert.Equal([fixture.RootId, fixture.ChildId], result.Web.SearchContainerIds);
        Assert.All(result.Web.Pages, page => Assert.Equal(fixture.ImageHash, page.ImageSha256));
        Assert.Equal(fixture.RootId, result.Web.Pages[1].ParentId);
        Assert.Equal(5, result.Web.Assets.Length);
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("files/diagrams/../../outside.png")]
    [InlineData("https://example.invalid/diagram.png")]
    [InlineData("C:/private/diagram.png")]
    [InlineData("files/diagrams/missing.png")]
    public void RejectsMissingExternalOrEscapingPageImages(string image)
    {
        using var fixture = new Site();
        fixture.Model["pages"]![0]!["image"] = image; fixture.Save();
        Assert.Throws<InvalidDataException>(() => NativeWebPublicationReader.Read(fixture.Entry));
    }

    [Theory]
    [InlineData("index.html")]
    [InlineData("key.json.js")]
    [InlineData("libs/js/json/configuration.json.js")]
    public void RequiresEveryEntryAsset(string relative)
    {
        using var fixture = new Site();
        File.Delete(Path.Combine(fixture.Root, relative));
        Assert.Throws<InvalidDataException>(() => NativeWebPublicationReader.Read(fixture.Entry));
    }

    [Fact]
    public void RejectsDuplicatePageIdentity()
    {
        using var fixture = new Site();
        fixture.Model["pages"]![0]!["subPages"]![0]!["id"] = fixture.RootId; fixture.Save();
        Assert.Throws<InvalidDataException>(() => NativeWebPublicationReader.Read(fixture.Entry));
    }

    [Fact]
    public void RejectsDuplicateJsonProperties()
    {
        using var fixture = new Site();
        File.WriteAllText(fixture.Configuration, "Bizagi.AppModel = {\"pages\":[],\"pages\":[]}");
        Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => NativeWebPublicationReader.Read(fixture.Entry));
    }

    [Fact]
    public void RejectsExecutableSuffixWithoutEvaluatingIt()
    {
        using var fixture = new Site();
        File.AppendAllText(fixture.Configuration, "; untrustedSideEffect();");
        Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => NativeWebPublicationReader.Read(fixture.Entry));
    }

    [Fact]
    public void RejectsUnsupportedEnvelope()
    {
        using var fixture = new Site();
        File.WriteAllText(fixture.Configuration, "Other.Model = " + fixture.Model);
        Assert.Throws<InvalidDataException>(() => NativeWebPublicationReader.Read(fixture.Entry));
    }

    [Fact]
    public void DetectsCorruptPngHeader()
    {
        using var fixture = new Site();
        File.WriteAllText(Path.Combine(fixture.Root, "files/diagrams/" + fixture.RootId + ".png"), "not an image");
        Assert.Throws<InvalidDataException>(() => NativeWebPublicationReader.Read(fixture.Entry));
    }

    [Fact]
    public void RetainsDifferentBytesEvenWhenDimensionsMatch()
    {
        using var fixture = new Site();
        // A downstream comparison must not confuse dimensions with byte identity.
        string image = Path.Combine(fixture.Root, "files/diagrams/" + fixture.ChildId + ".png");
        File.AppendAllText(image, "owned policy evidence");
        var result = NativeWebPublicationReader.Read(fixture.Entry);
        Assert.NotEqual(result.Web!.Pages[0].ImageSha256, result.Web.Pages[1].ImageSha256);
        Assert.Equal(result.ImageSizes[0].Width, result.ImageSizes[1].Width);
    }

    private sealed class Site : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "mcp-web-policy-" + Guid.NewGuid().ToString("N"));
        public string Entry => Path.Combine(Root, "index.html");
        public string Configuration => Path.Combine(Root, "libs/js/json/configuration.json.js");
        public string RootId { get; } = Guid.NewGuid().ToString();
        public string ChildId { get; } = Guid.NewGuid().ToString();
        public JObject Model { get; }
        public string ImageHash { get; }

        public Site()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Configuration)!);
            Directory.CreateDirectory(Path.Combine(Root, "files/diagrams"));
            File.WriteAllText(Entry, "<!doctype html><title>Own policy fixture, not native evidence</title>");
            File.WriteAllText(Path.Combine(Root, "key.json.js"), "{}");
            byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j3ioAAAAASUVORK5CYII=");
            ImageHash = Convert.ToHexStringLower(SHA256.HashData(png));
            foreach (string id in new[] { RootId, ChildId }) File.WriteAllBytes(Path.Combine(Root, "files/diagrams/" + id + ".png"), png);
            Model = JObject.FromObject(new { modelName = "Publication Ω 日本語", pages = new[] { new { id = RootId, name = "Root",
                image = "files/diagrams/" + RootId + ".png", subPages = new[] { new { id = ChildId, name = "Nested", parentRef = RootId, image = "files/diagrams/" + ChildId + ".png" } } } },
                searchMap = new[] { new { containerId = RootId }, new { containerId = ChildId } } });
            Save();
        }

        public void Save() => File.WriteAllText(Configuration, "Bizagi.AppModel = " + Model.ToString(Newtonsoft.Json.Formatting.None));
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
