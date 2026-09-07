using System.Text;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Pure image intent/fidelity negatives. Installed codec/persistence acceptance is a separate circuit.</summary>
public sealed class NativeImageTests
{
    private const string Id = "11111111-1111-4111-8111-111111111111", Diagram = "22222222-2222-4222-8222-222222222222";
    private static NativeImageImport Input() => new() { SourcePath = "image.png", ExpectedRevision = new string('a', 64), AllowPngReencoding = true };
    private static NativeImageInfo Pixels() => new() { Width = 64, Height = 48, HasTransparency = true, PixelSha256 = new string('b', 64) };
    private static NativeImageFile File(byte[] bytes) => new() { DiagramId = Diagram, ElementId = Id, FileName = Id + ".png", Length = bytes.Length, Sha256 = BpmnDocument.Revision(bytes) };
    private static NativeElement Node() => new() { Id = Id, DiagramId = Diagram, Kind = "ImageArtifact", Artifact = new() { Image = Pixels() } };
    private static NativeMutation Change(string op = "update") => new() { Operation = op, ElementId = Id, ArtifactProperties = op == "delete" ? null : new() { Image = Input() } };
    private static Dictionary<string, byte[]> Archive(byte[] bytes, bool owner = true)
    {
        // Minimal comparison input, not a simulated native worker or operational accreditation.
        string xml = $"<Package xmlns='http://www.wfmc.org/2009/XPDL2.2' Id='{Diagram}'><Artifacts>" +
            (owner ? $"<Artifact Id='{Id}' BizAgiArtifactType='Image'/>" : "") + "</Artifacts></Package>";
        var result = new Dictionary<string, byte[]> { [Diagram + ".diag!/Diagram.xml"] = Encoding.UTF8.GetBytes(xml), ["unknown.bin"] = [7, 8, 9] };
        if (owner) result.Add(NativeImagePolicy.Entry(File(bytes)), bytes);
        return result;
    }
    private static NativeImageImportReceipt Receipt(byte[] bytes) => new() { ElementId = Id, SourceSha256 = Input().ExpectedRevision,
        PngReencoded = true, PayloadEncoder = "System.Drawing.PNG.straight-alpha", FrameCount = 1, FrameDimension = "Page", Image = Pixels(), File = File(bytes) };

    [Theory] [InlineData(null)] [InlineData("Page")] [InlineData("Time")] [InlineData("Resolution")]
    public void ExplicitSourceAndNativeFrameDimensionsAreValid(string? dimension)
    { var input = Input(); input.FrameDimension = dimension; input.FrameIndex = 0; NativeImagePolicy.Validate(input); }
    [Theory] [InlineData("consent")] [InlineData("path")] [InlineData("hash")] [InlineData("hash-case")] [InlineData("dimension")] [InlineData("frame")]
    public void AmbiguousOrInvalidImageIntentFails(string bad)
    {
        var input = Input();
        switch (bad) { case "consent": input.AllowPngReencoding = false; break; case "path": input.SourcePath = ""; break;
            case "hash": input.ExpectedRevision = "wrong"; break; case "hash-case": input.ExpectedRevision = new string('A', 64); break;
            case "dimension": input.FrameDimension = "time"; break; case "frame": input.FrameIndex = -1; break; }
        Assert.Throws<InvalidDataException>(() => NativeImagePolicy.Validate(input));
    }
    [Fact] public void TextAndImageAreMutuallyExclusive()
    {
        var change = Change(); NativeEditPlan.Validate([change]); change.ArtifactProperties!.Text = "";
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([change]));
    }
    [Theory] [InlineData("ImageArtifact", true)] [InlineData("ImageArtifact", false)] [InlineData("UserTask", true)]
    public void CreationRequiresImageTypeAndSource(string kind, bool hasSource)
    {
        var change = Change("create"); change.ElementType = kind; change.ParentId = Diagram;
        if (!hasSource) change.ArtifactProperties = null;
        if (kind == "ImageArtifact" && hasSource) NativeEditPlan.Validate([change]);
        else Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([change]));
    }
    [Theory] [InlineData("width")] [InlineData("height")] [InlineData("alpha")] [InlineData("pixels")]
    public void DimensionAlphaAndPixelDriftAreDistinctFailures(string field)
    {
        var a = Pixels(); var b = Pixels(); Assert.True(NativeImagePolicy.SamePixels(a, b));
        switch (field) { case "width": b.Width++; break; case "height": b.Height++; break; case "alpha": b.HasTransparency = false; break; case "pixels": b.PixelSha256 = new string('c', 64); break; }
        Assert.False(NativeImagePolicy.SamePixels(a, b)); Assert.False(NativeImagePolicy.SamePixels(a, null));
    }
    [Theory] [InlineData("missing")] [InlineData("hash")] [InlineData("owner")] [InlineData("pixels")] [InlineData("duplicate")]
    public void RealFileInventoryMustAgreeWithEveryImageAndArchive(string field)
    {
        byte[] bytes = [1, 2, 3]; var entries = Archive(bytes); var file = File(bytes); var node = Node();
        var reply = new EngineReply { Elements = [node], ImageFiles = [file] }; NativeImagePolicy.VerifyFiles(entries, reply);
        switch (field) { case "missing": reply.ImageFiles = []; break; case "hash": entries[NativeImagePolicy.Entry(file)] = [8, 9, 0]; break;
            case "owner": node.DiagramId = Id; break; case "pixels": node.Artifact!.Image = null; break; case "duplicate": reply.ImageFiles = [file, file]; break; }
        Assert.Throws<InvalidDataException>(() => NativeImagePolicy.VerifyFiles(entries, reply));
    }
    [Theory] [InlineData("create")] [InlineData("update")] [InlineData("delete")]
    public void AuthorizedPayloadProjectionPreservesUnknownEntries(string operation)
    {
        byte[] a = [1, 2], b = [3, 4, 5]; var before = Archive(a, operation != "create"); var after = Archive(b, operation != "delete");
        NativeImagePolicy.Project(before, after, [Change(operation)], operation == "delete" ? [] : [Node()],
            operation == "delete" ? [] : [Receipt(b)], operation == "delete" ? [] : [File(b)]);
        Assert.True(before.ContainsKey("unknown.bin") && after.ContainsKey("unknown.bin"));
        Assert.DoesNotContain(NativeImagePolicy.Entry(File(b)), before.Keys); Assert.DoesNotContain(NativeImagePolicy.Entry(File(b)), after.Keys);
    }
    [Theory] [InlineData("source")] [InlineData("pixels")] [InlineData("bytes")] [InlineData("consent")] [InlineData("frame")] [InlineData("missing-owner")] [InlineData("duplicate-payload")]
    public void UpdateProjectionCannotConcealIncorrectEvidence(string field)
    {
        byte[] bytes = [3, 4, 5]; var before = Archive([1, 2]); var after = Archive(bytes); var receipt = Receipt(bytes);
        switch (field) { case "source": receipt.SourceSha256 = new string('d', 64); break; case "pixels": receipt.Image.Width++; break;
            case "bytes": after[NativeImagePolicy.Entry(File(bytes))] = [9, 9, 9]; break; case "consent": receipt.PngReencoded = false; break;
            case "frame": receipt.FrameCount = 2; break; case "missing-owner": before = Archive([1, 2], false); break;
            case "duplicate-payload": before.Add(Diagram + ".diag!/ImageArtifactImages/" + Id + ".gif", [9]); break; }
        Assert.Throws<InvalidDataException>(() => NativeImagePolicy.Project(before, after, [Change()], [Node()], [receipt], [File(bytes)]));
    }
    [Fact] public void ImportReceiptsCannotAppearWithoutIntent()
    {
        Assert.Throws<InvalidDataException>(() => NativeImagePolicy.Project(Archive([1]), Archive([1]), [], [Node()], [Receipt([1])], [File([1])]));
        Assert.Throws<InvalidDataException>(() => NativeImagePolicy.Project(Archive([1]), Archive([1]), [Change()], [Node()], [], [File([1])]));
    }
    [Fact] public void ImageDeletionMustRemoveActualPayload()
    {
        Assert.Throws<InvalidDataException>(() => NativeImagePolicy.Project(Archive([1]), Archive([1]), [Change("delete")], [], [], []));
    }
    [Fact] public void CreationCannotOverwriteAnOrphanFileAndForeignNamesCannotAuthorizeDeletion()
    {
        byte[] bytes = [1, 2]; var before = Archive(bytes, false); var after = Archive(bytes);
        before.Add(NativeImagePolicy.Entry(File(bytes)), [8]);
        Assert.Throws<InvalidDataException>(() => NativeImagePolicy.Project(before, after, [Change("create")], [Node()], [Receipt(bytes)], [File(bytes)]));
        before = Archive(bytes);
        string key = Diagram + ".diag!/Diagram.xml";
        before[key] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(before[key]).Replace("http://www.wfmc.org/2009/XPDL2.2", "urn:foreign"));
        after = Archive(bytes, false);
        NativeImagePolicy.Project(before, after, [Change("delete")], [], [], []);
        Assert.Contains(NativeImagePolicy.Entry(File(bytes)), before.Keys);
    }
    [Theory] [InlineData("../image.png")] [InlineData("image.png")] [InlineData("11111111-1111-4111-8111-111111111111.png:stream")]
    public void PayloadPathsCannotEscapeOrUseAnUnrelatedIdentity(string fileName)
    {
        var file = File([1]); file.FileName = fileName;
        Assert.Throws<InvalidDataException>(() => NativeImagePolicy.Entry(file));
    }
}
