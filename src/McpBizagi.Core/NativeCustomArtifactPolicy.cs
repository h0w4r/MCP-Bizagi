using System.Xml;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

/// <summary>Definition lifecycle intent and full-container fidelity, with explicit native conversion receipts.</summary>
public static class NativeCustomArtifactPolicy
{
    public static string Entry(string id) => "BizAgiArtifacts/" + id + ".xml";
    public static void Validate(NativeCustomArtifactPatch patch)
    {
        if (patch?.Changes == null || patch.Changes.Length is < 1 or > 100 || patch.Changes.Any(c => c == null) ||
            patch.Changes.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != patch.Changes.Length)
            throw new InvalidDataException("Supply 1-100 distinct custom artifact definition changes.");
        foreach (var c in patch.Changes)
        {
            NativeMetadataPolicy.RequireId(c.Id);
            if (c.Operation is not "create" and not "update" and not "delete") throw new InvalidDataException("Unknown custom artifact definition operation.");
            if (c.Name != null) { if (c.Name.Length > 4096) throw new InvalidDataException("Custom artifact name exceeds the bound."); XmlConvert.VerifyXmlChars(c.Name); }
            if (c.Operation == "create" && (c.Name == null || c.Image == null) || c.Operation == "update" && c.Name == null && c.Image == null ||
                c.Operation == "delete" && (c.Name != null || c.Image != null || c.AllowNativeRasterization) || c.Image == null && c.AllowNativeRasterization)
                throw new InvalidDataException("Custom artifact change has missing or inapplicable properties.");
            if (c.Image != null) NativeImagePolicy.Validate(c.Image);
        }
    }
    public static Dictionary<string, NativeCustomArtifactArchive.Payload> Catalog(IReadOnlyDictionary<string, byte[]> entries)
    {
        var result = new Dictionary<string, NativeCustomArtifactArchive.Payload>(StringComparer.Ordinal);
        foreach (var entry in entries.Where(e => e.Key.StartsWith("BizAgiArtifacts/", StringComparison.OrdinalIgnoreCase)))
        {
            var payload = NativeCustomArtifactArchive.ReadDefinition(NativeCustomArtifactArchive.Xml(entry.Value).Root!);
            if (entry.Key != Entry(payload.Id) || !result.TryAdd(payload.Id, payload)) throw new InvalidDataException("Unsupported native custom artifact definition layout.");
        }
        return result;
    }
    public static void Preflight(byte[] original, NativeCustomArtifactPatch patch)
    {
        Validate(patch); var entries = NativeArchive.ReadEntries(original); var definitions = Catalog(entries);
        foreach (var c in patch.Changes)
        {
            if ((c.Operation == "create") == definitions.ContainsKey(c.Id)) throw new InvalidDataException("Custom definition existence conflicts with the request.");
            if (c.Operation != "delete") continue;
            foreach (var entry in entries.Where(e => e.Key != Entry(c.Id)))
            {
                // Fail conservatively for unknown references too: deletion must not orphan applicability/order or instances.
                bool reference = entry.Key.Contains(c.Id, StringComparison.OrdinalIgnoreCase);
                if (entry.Key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    // Honor the actual XML declaration/BOM, including UTF-16; raw UTF-8 decoding would miss references.
                    using var stream = new MemoryStream(entry.Value, false);
                    using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                        MaxCharactersInDocument = BpmnDocument.MaxXmlCharacters });
                    var xml = System.Xml.Linq.XDocument.Load(reader);
                    reference |= xml.ToString().Contains(c.Id, StringComparison.OrdinalIgnoreCase);
                }
                if (reference)
                    throw new InvalidDataException("Custom artifact definition remains referenced by native model content: " + entry.Key);
            }
        }
    }
    public static void VerifyStored(byte[] bytes, EngineReply reply)
    {
        var catalog = Catalog(NativeArchive.ReadEntries(bytes));
        if (reply.CustomArtifacts.Length != catalog.Count || reply.CustomArtifacts.Select(d => d.Id).Distinct().Count() != catalog.Count)
            throw new InvalidDataException("Native custom catalog readback count mismatch.");
        foreach (var definition in reply.CustomArtifacts)
            if (!catalog.TryGetValue(definition.Id, out var payload) || definition.Name != payload.Name ||
                definition.SerializedImageSha256 != BpmnDocument.Revision(payload.EmbeddedPng))
                throw new InvalidDataException("Native custom artifact serialization differs from its durable payload.");
        foreach (var element in reply.Elements.Where(e => e.Kind == "CustomArtifact"))
            if (element.Artifact?.CustomArtifactTypeId is not { } id || !catalog.ContainsKey(id)) throw new InvalidDataException("Dangling custom artifact instance after native readback.");
    }
    public static void ValidateArchiveReferences(IReadOnlyDictionary<string, byte[]> entries)
    {
        var catalog = Catalog(entries);
        System.Xml.Linq.XNamespace ns = "http://www.wfmc.org/2009/XPDL2.2";
        foreach (var entry in entries.Where(e => e.Key.EndsWith(".diag!/Diagram.xml", StringComparison.OrdinalIgnoreCase)))
        {
            using var input = new MemoryStream(entry.Value, false);
            using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = BpmnDocument.MaxXmlCharacters });
            var xml = System.Xml.Linq.XDocument.Load(reader);
            foreach (var artifact in xml.Descendants(ns + "Artifact").Where(NativeFidelity.IsNativeNameOwner))
                if ((string?)artifact.Attribute("BizAgiArtifactType") == "Custom" &&
                    !catalog.ContainsKey((string?)artifact.Attribute("CustomArtifactTypeId") ?? ""))
                    throw new InvalidDataException("Native custom instance references a missing definition; the installed loader would omit it.");
        }
    }
    public static NativeFidelityReport Compare(byte[] before, byte[] after, NativeCustomArtifactPatch patch, EngineReply edited, EngineReply reopened)
    {
        Preflight(before, patch); VerifyStored(after, reopened);
        if (edited.CustomArtifacts.Length != reopened.CustomArtifacts.Length || edited.CustomArtifacts.Any(a => !reopened.CustomArtifacts.Any(b =>
            a.Id == b.Id && a.Name == b.Name && a.SerializedImageSha256 == b.SerializedImageSha256 && NativeImagePolicy.SamePixels(a.Image, b.Image))))
            throw new InvalidDataException("Custom definitions changed across the independent worker restart.");
        var left = NativeArchive.ReadEntries(before).ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
        var right = NativeArchive.ReadEntries(after).ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
        var previous = Catalog(left); var current = Catalog(right);
        if (edited.CustomArtifactImports.Length != patch.Changes.Count(c => c.Image != null)) throw new InvalidDataException("Custom image receipt count mismatch.");
        foreach (var c in patch.Changes)
        {
            string path = Entry(c.Id);
            if (c.Operation == "delete")
            {
                if (right.ContainsKey(path)) throw new InvalidDataException("Deleted native custom definition was resurrected.");
                left.Remove(path); continue;
            }
            if (!current.TryGetValue(c.Id, out var saved) || saved.Name != (c.Name ?? previous[c.Id].Name)) throw new InvalidDataException("Native custom definition name or identity differs from intent.");
            if (c.Image is { } image)
            {
                var receipt = edited.CustomArtifactImports.Single(r => r.Id == c.Id);
                var loaded = reopened.CustomArtifacts.Single(d => d.Id == c.Id);
                if (receipt.Source.SourceSha256 != image.ExpectedRevision || !receipt.RepeatedSerializationStable ||
                    receipt.Result.SerializedImageSha256 != loaded.SerializedImageSha256 || !NativeImagePolicy.SamePixels(receipt.Result.Image, loaded.Image) ||
                    receipt.NativeRasterizationAcknowledged != c.AllowNativeRasterization || receipt.PixelsChanged && !c.AllowNativeRasterization ||
                    receipt.PixelsChanged == NativeImagePolicy.SamePixels(receipt.Source.Image, loaded.Image))
                    throw new InvalidDataException("Native custom image conversion receipt does not match the request and durable result.");
            }
            else if (!saved.EmbeddedPng.SequenceEqual(previous[c.Id].EmbeddedPng)) throw new InvalidDataException("Unrequested custom artifact pixels or encoded bytes changed.");
            // Compare-only projection of the strictly parsed requested definition, never a saved archive rewrite.
            if (c.Operation == "create") right.Remove(path); else right[path] = left[path];
        }
        return NativeFidelity.CompareEntries(left, right);
    }
}
