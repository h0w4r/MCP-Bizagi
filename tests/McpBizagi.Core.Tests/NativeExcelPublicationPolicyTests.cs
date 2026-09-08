using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Pure projection policy fixtures; actual native publication has a separate MCP test.</summary>
public sealed class NativeExcelPublicationPolicyTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReportsEmptyPoolRegardlessOfHiddenBoundary(bool main)
    {
        var (source, projection) = Fixture(main);
        var omitted = Assert.Single(NativeExcelPublicationPolicy.Verify(source, projection, []));
        Assert.Equal("pool", omitted.ElementId);
        Assert.Equal("Native pool Ω", omitted.Name);
        Assert.Equal("native_excel_empty_pool_sheet_omitted", omitted.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PopulatedPoolIsNeverExempted(bool main)
    {
        var (source, projection) = Fixture(main);
        source = [.. source, new() { Id = "task", Kind = "UserTask", ParentId = "process", DiagramId = "diagram" }];
        Assert.Throws<InvalidDataException>(() => NativeExcelPublicationPolicy.Verify(source, projection, []));
        projection[0].MappedElementIds = ["task"];
        Assert.Empty(NativeExcelPublicationPolicy.Verify(source, projection, []));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("extra")]
    [InlineData("foreign-owner")]
    [InlineData("foreign-child")]
    [InlineData("duplicate-child")]
    [InlineData("missing-process")]
    public void RejectsUnprovenProjection(string fault)
    {
        var (source, projection) = Fixture(false);
        switch (fault)
        {
            case "missing": projection = []; break;
            case "duplicate": projection = [projection[0], projection[0]]; break;
            case "extra": projection = [.. projection, new() { ElementId = "other" }]; break;
            case "foreign-owner": projection[0].DiagramId = "other"; break;
            case "foreign-child": projection[0].MappedElementIds = ["other"]; break;
            case "duplicate-child": projection[0].MappedElementIds = ["other", "other"]; break;
            case "missing-process": source = source[..1]; break;
        }
        Assert.Throws<InvalidDataException>(() => NativeExcelPublicationPolicy.Verify(source, projection, []));
    }

    [Fact]
    public void NonemptyProjectionCannotOmitAnotherSourceChildWithTheSameName()
    {
        var (source, projection) = Fixture(false);
        source = [.. source,
            new() { Id = "first", Kind = "UserTask", Name = "Same", ParentId = "process", DiagramId = "diagram" },
            new() { Id = "second", Kind = "UserTask", Name = "Same", ParentId = "process", DiagramId = "diagram" }];
        projection[0].MappedElementIds = ["first"];
        Assert.Throws<InvalidDataException>(() => NativeExcelPublicationPolicy.Verify(source, projection, []));
    }

    [Fact]
    public void StructuralLaneSetDoesNotInventAProcessRow()
    {
        var (source, projection) = Fixture(false);
        source = [.. source, new() { Id = "lanes", Kind = "LaneSet", ParentId = "process", DiagramId = "diagram" }];
        Assert.Single(NativeExcelPublicationPolicy.Verify(source, projection, []));
    }

    [Fact]
    public void DuplicateNamesCannotHideMissingWorkbookRowIdentity()
    {
        NativeExcelPoolProjection[] projection = [new() { ElementId = "pool", MappedElementIds = ["task-1", "task-2"] }];
        var source = new[] { "pool", "task-1", "task-2" }.Select(id => new NativeElement { Id = id, Name = "Same name" }).ToArray();
        var rows = source.Select((e, i) => new NativeExcelRow { Sheet = "Process", RowNumber = i + 1, ElementId = e.Id, Name = e.Name }).ToArray();
        Assert.Throws<InvalidDataException>(() => NativeExcelPublicationPolicy.VerifyReadback(projection, source, rows[..^1]));
        NativeExcelPublicationPolicy.VerifyReadback(projection, source, rows);
        rows[2].Name = "Changed name";
        Assert.Throws<InvalidDataException>(() => NativeExcelPublicationPolicy.VerifyReadback(projection, source, rows));
    }

    [Fact]
    public void EmptyPoolDoesNotRequireAnUnpublishedWorkbookRow() =>
        NativeExcelPublicationPolicy.VerifyReadback([new() { ElementId = "empty-pool" }], [], []);

    [Fact]
    public void MissingProjectionCannotAccreditAnEmptyResult() =>
        Assert.Throws<InvalidDataException>(() => NativeExcelPublicationPolicy.Verify([], null, []));

    [Fact]
    public void SelectionDoesNotAdmitUnselectedPools()
    {
        var (source, projection) = Fixture(false);
        Assert.Throws<InvalidDataException>(() => NativeExcelPublicationPolicy.Verify(source, projection, ["other"]));
    }

    private static (NativeElement[], NativeExcelPoolProjection[]) Fixture(bool main) =>
        ([new() { Id = "pool", Kind = "Participant", Name = "Native pool Ω", IsMainParticipant = main, DiagramId = "diagram" },
          new() { Id = "process", Kind = "Process", ParentId = "pool", DiagramId = "diagram" }],
         [new() { ElementId = "pool", DiagramId = "diagram" }]);
}
