using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Pure comparison policy; synthetic graphs are never native accreditation.</summary>
public sealed class NativeWebPublicationPolicyTests
{
    [Fact]
    public void AcceptsExactRootNestedNavigationAndImageBytes()
    {
        var (source, web, images) = Fixture();
        NativeWebPublicationPolicy.Verify(source, web, [], "Own publication", images);
    }

    [Theory]
    [InlineData("missing-page")]
    [InlineData("duplicate-page")]
    [InlineData("missing-search")]
    [InlineData("duplicate-search")]
    [InlineData("wrong-parent")]
    [InlineData("wrong-name")]
    [InlineData("wrong-bytes")]
    [InlineData("wrong-title")]
    [InlineData("extra-root")]
    public void RejectsUnexplainedPublicationDifferences(string fault)
    {
        var (source, web, images) = Fixture();
        switch (fault)
        {
            case "missing-page": web.Pages = web.Pages[..^1]; break;
            case "duplicate-page": web.Pages = [.. web.Pages, web.Pages[0]]; break;
            case "missing-search": web.SearchContainerIds = web.SearchContainerIds[..^1]; break;
            case "duplicate-search": web.SearchContainerIds = [.. web.SearchContainerIds, web.SearchContainerIds[0]]; break;
            case "wrong-parent": web.Pages[2].ParentId = web.RootPageIds[0]; break;
            case "wrong-name": web.Pages[2].Name = "Different name"; break;
            case "wrong-bytes": web.Pages[2].ImageSha256 = new string('b', 64); break;
            case "wrong-title": web.ModelName = "Different title"; break;
            case "extra-root": web.RootPageIds = [.. web.RootPageIds, Guid.NewGuid().ToString()]; break;
        }
        Assert.Throws<InvalidDataException>(() => NativeWebPublicationPolicy.Verify(source, web, [], "Own publication", images));
    }

    [Fact]
    public void SelectedRootsDoNotPermitAnUnselectedPage()
    {
        var (source, web, images) = Fixture();
        string other = Guid.NewGuid().ToString();
        source = [.. source, new NativeElement { Id = other, Kind = "Collaboration", Name = "Excluded", DiagramId = other }];
        NativeWebPublicationPolicy.Verify(source, web, [web.RootPageIds[0]], "Own publication", images);
        Assert.Throws<InvalidDataException>(() => NativeWebPublicationPolicy.Verify(source, web, [], "Own publication", images));
    }

    private static (NativeElement[], NativeWebPublication, Dictionary<string, string>) Fixture()
    {
        string root = Guid.NewGuid().ToString(), process = Guid.NewGuid().ToString(), sub = Guid.NewGuid().ToString(), nested = Guid.NewGuid().ToString();
        NativeElement[] source = [new() { Id = root, Kind = "Collaboration", Name = "Root", DiagramId = root },
            new() { Id = process, Kind = "Process", ParentId = root, DiagramId = root },
            new() { Id = sub, Kind = "SubProcess", Name = "Subprocess", ParentId = process, DiagramId = root, SubProcess = new() },
            new() { Id = nested, Kind = "SubProcess", Name = "Nested", ParentId = sub, DiagramId = root, SubProcess = new() }];
        string hash = new('a', 64);
        var images = new[] { root, sub, nested }.ToDictionary(id => id, _ => hash);
        var web = new NativeWebPublication { ModelName = "Own publication", RootPageIds = [root], SearchContainerIds = [root, sub, nested],
            Pages = [new() { Id = root, Name = "Root", ImageSha256 = hash }, new() { Id = sub, Name = "Subprocess", ParentId = root, ImageSha256 = hash },
                new() { Id = nested, Name = "Nested", ParentId = sub, ImageSha256 = hash }] };
        return (source, web, images);
    }
}
