using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using McpBizagi.Contracts;
using McpBizagi.Core;
using Xunit;

namespace McpBizagi.Core.Tests;

/// <summary>Host/parser policy tests only; these do not accredit the proprietary engine.</summary>
public sealed class NativeCustomArtifactTests
{
    private const string Id = "aaaaaaaa-1111-2222-3333-444444444444";
    private const string Parent = "bbbbbbbb-1111-2222-3333-444444444444";
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10];
    private static NativeImageImport Image() => new() { SourcePath = "image.png", ExpectedRevision = new string('a', 64), AllowPngReencoding = true };
    private static NativeCustomArtifactPatch Patch() => new() { Changes = [new() { Operation = "create", Id = Id, Name = "Example Ω", Image = Image(), AllowNativeRasterization = true }] };
    private static XElement Definition() => new("CustomArtifactType", new XAttribute("Id", Id), new XAttribute("Name", "Example Ω"), new XElement("Image", Convert.ToBase64String(Png)));
    private static byte[] Bytes(XElement element) => Encoding.UTF8.GetBytes(element.ToString());
    private static byte[] Zip(params (string Name, byte[] Bytes)[] entries)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            foreach (var entry in entries) { using var output = zip.CreateEntry(entry.Name).Open(); output.Write(entry.Bytes); }
        return memory.ToArray();
    }
    private static byte[] Bca(XElement? definition = null, string? imageName = null, byte[]? image = null) => Zip(
        ("CustomArtifactDefinitions.xml", Bytes(new XElement("CustomArtifactTypesDefinitions", definition ?? Definition()))),
        ("CustomArtifactDefinitionsImages.zip", Zip((imageName ?? Id + ".png", image ?? Png))));

    [Fact] public void TypedLifecycleIntentsAreAccepted() { NativeCustomArtifactPolicy.Validate(Patch()); }
    [Theory] [InlineData("operation")] [InlineData("id")] [InlineData("missing-name")] [InlineData("missing-image")]
    [InlineData("duplicate")] [InlineData("delete-name")] [InlineData("unused-ack")] [InlineData("empty-update")]
    public void InvalidDefinitionIntentsFail(string fault)
    {
        var patch = Patch(); var c = patch.Changes[0];
        switch (fault) { case "operation": c.Operation = "reflect"; break; case "id": c.Id = "../type"; break;
            case "missing-name": c.Name = null; break; case "missing-image": c.Image = null; break; case "duplicate": patch.Changes = [c, c]; break;
            case "delete-name": c.Operation = "delete"; c.Image = null; break;
            case "unused-ack": c.Operation = "update"; c.Image = null; break;
            case "empty-update": c.Operation = "update"; c.Image = null; c.Name = null; c.AllowNativeRasterization = false; break; }
        Assert.Throws<InvalidDataException>(() => NativeCustomArtifactPolicy.Validate(patch));
    }
    [Fact] public void NativeArchiveReadIsBoundedAndPreservesBothImageRepresentations()
    {
        var payload = Assert.Single(NativeCustomArtifactArchive.Read(Bca()));
        Assert.Equal(Id, payload.Id); Assert.Equal("Example Ω", payload.Name); Assert.Equal(Png, payload.EmbeddedPng); Assert.Equal(Png, payload.SidecarPng);
    }
    [Theory] [InlineData("../type.png")] [InlineData("dir/type.png")] [InlineData("dir\\type.png")] [InlineData("C:type.png")]
    [InlineData("..")] [InlineData("wrong.png")]
    public void UnsafeOrUnmatchedSidecarsFail(string name) => Assert.Throws<InvalidDataException>(() => NativeCustomArtifactArchive.Read(Bca(imageName: name)));
    [Theory] [InlineData("root")] [InlineData("unknown-attribute")] [InlineData("unknown-element")] [InlineData("duplicate-image")]
    [InlineData("image-attribute")] [InlineData("comment")] [InlineData("bad-png")] [InlineData("empty-id")]
    public void UnknownOrMalformedDefinitionContentCannotDisappear(string fault)
    {
        var d = Definition();
        switch (fault) { case "root": d.Name = "Other"; break; case "unknown-attribute": d.SetAttributeValue("Unknown", "preserve"); break;
            case "unknown-element": d.Add(new XElement("Future", "preserve")); break; case "duplicate-image": d.Add(new XElement(d.Element("Image")!)); break;
            case "image-attribute": d.Element("Image")!.SetAttributeValue("unknown", "preserve"); break; case "comment": d.Add(new XComment("preserve")); break;
            case "bad-png": d.Element("Image")!.Value = Convert.ToBase64String([1, 2, 3]); break; case "empty-id": d.SetAttributeValue("Id", Guid.Empty); break; }
        Assert.Throws<InvalidDataException>(() => NativeCustomArtifactArchive.Read(Bca(d)));
    }
    [Fact] public void DuplicateArchiveMembersFail() => Assert.Throws<InvalidDataException>(() => NativeCustomArtifactArchive.Read(Zip(("a", Png), ("a", Png))));
    [Fact] public void DuplicateDefinitionIdentitiesFail()
    {
        var xml = Bytes(new XElement("CustomArtifactTypesDefinitions", Definition(), Definition()));
        Assert.Throws<InvalidDataException>(() => NativeCustomArtifactArchive.Read(Zip(("CustomArtifactDefinitions.xml", xml), ("CustomArtifactDefinitionsImages.zip", Zip((Id + ".png", Png))))));
    }
    [Fact] public void DtdNeverReachesTheNativeDeserializer()
    {
        byte[] xml = Encoding.UTF8.GetBytes("<!DOCTYPE CustomArtifactTypesDefinitions [<!ENTITY x 'blocked'>]><CustomArtifactTypesDefinitions>&x;</CustomArtifactTypesDefinitions>");
        Assert.Throws<System.Xml.XmlException>(() => NativeCustomArtifactArchive.Read(Zip(("CustomArtifactDefinitions.xml", xml), ("CustomArtifactDefinitionsImages.zip", Zip((Id + ".png", Png))))));
    }
    [Fact] public void CustomInstanceRequiresExactlyOneTypedDefinitionIntent()
    {
        var change = new NativeMutation { Operation = "create", ElementId = Id, ParentId = Parent, ElementType = "CustomArtifact", ArtifactProperties = new() { CustomArtifactTypeId = Parent } };
        NativeEditPlan.Validate([change]); change.ArtifactProperties.Text = "unrelated";
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([change])); change.ArtifactProperties = null;
        Assert.Throws<InvalidDataException>(() => NativeEditPlan.Validate([change]));
    }
    [Fact] public void DefinitionDeletionRejectsUnknownNativeReferences()
    {
        var patch = new NativeCustomArtifactPatch { Changes = [new() { Operation = "delete", Id = Id }] };
        var bytes = Zip(("ModelInfo.xml", Encoding.UTF8.GetBytes("<ModelInfo/>")), (NativeCustomArtifactPolicy.Entry(Id), Bytes(Definition())),
            ("Future.xml", Encoding.UTF8.GetBytes("<Future Ref='" + Id + "'/>")));
        Assert.Throws<InvalidDataException>(() => NativeCustomArtifactPolicy.Preflight(bytes, patch));
    }
    [Fact] public void RequestedReferenceProjectionCannotHideOtherFields()
    {
        XNamespace ns = "http://www.wfmc.org/2009/XPDL2.2";
        var before = new XElement(ns + "Artifact", new XAttribute("Id", Id), new XAttribute("ArtifactType", "Annotation"), new XAttribute("BizAgiArtifactType", "Custom"), new XAttribute("CustomArtifactTypeId", Id));
        var after = new XElement(before); after.SetAttributeValue("CustomArtifactTypeId", Parent);
        _ = new XElement(ns + "Package", new XElement(ns + "Artifacts", before)); _ = new XElement(ns + "Package", new XElement(ns + "Artifacts", after));
        NativeArtifactPolicy.Project(before, after, new() { ElementId = Id, ArtifactProperties = new() { CustomArtifactTypeId = Parent } });
        Assert.Equal(Id, (string?)after.Attribute("CustomArtifactTypeId"));
    }
    [Fact] public void MissingDefinitionsFailBeforeTheNativeLoaderCanOmitInstances()
    {
        XNamespace ns = "http://www.wfmc.org/2009/XPDL2.2";
        var xml = new XElement(ns + "Package", new XElement(ns + "Artifacts", new XElement(ns + "Artifact",
            new XAttribute("Id", Parent), new XAttribute("BizAgiArtifactType", "Custom"), new XAttribute("CustomArtifactTypeId", Id))));
        var entries = new Dictionary<string, byte[]> { [Parent + ".diag!/Diagram.xml"] = Bytes(xml) };
        Assert.Throws<InvalidDataException>(() => NativeCustomArtifactPolicy.ValidateArchiveReferences(entries));
        entries.Add(NativeCustomArtifactPolicy.Entry(Id), Bytes(Definition())); NativeCustomArtifactPolicy.ValidateArchiveReferences(entries);
    }
    [Fact] public void UnknownUtf16ReferencesAlsoBlockDeletion()
    {
        var patch = new NativeCustomArtifactPatch { Changes = [new() { Operation = "delete", Id = Id }] };
        byte[] unicode = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("<?xml version='1.0' encoding='utf-16'?><Future>" + Id + "</Future>")).ToArray();
        var bytes = Zip(("ModelInfo.xml", Encoding.UTF8.GetBytes("<ModelInfo/>")), (NativeCustomArtifactPolicy.Entry(Id), Bytes(Definition())), ("Future.xml", unicode));
        Assert.Throws<InvalidDataException>(() => NativeCustomArtifactPolicy.Preflight(bytes, patch));
    }
}
