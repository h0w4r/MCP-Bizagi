using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.Core;

public sealed record VisioPreparedExport(byte[] Bytes, string[] RemovedReservedPages);

public static partial class VisioDocument
{
    public static VisioPreparedExport PrepareExport(byte[] nativeOutput, NativeElement[] source, string[] selected, NativeVisioPageReceipt[] receipts)
    {
        if (selected.Length is < 1 or > 100 || selected.Distinct(StringComparer.Ordinal).Count() != selected.Length)
            throw new InvalidDataException("Select distinct native diagrams for Visio projection.");
        var byId = source.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var expected = new List<(string Diagram, string Sub, string Name)>();
        foreach (string id in selected)
        {
            if (!byId.TryGetValue(id, out var diagram) || diagram.Kind != "Collaboration") throw new InvalidDataException("Visio source diagram is missing.");
            expected.Add((id, "", diagram.Name));
        }
        foreach (string diagram in selected)
            foreach (var sub in source.Where(e => e.DiagramId == diagram && e.SubProcess != null && source.Any(child => child.ParentId == e.Id &&
                child.Geometry != null && child.Kind is not "Lane" and not "LaneSet" and not "Milestone")))
                expected.Add((diagram, sub.Id, sub.Name));
        if (expected.Count is < 1 or > 100 || receipts.Length != expected.Count ||
            receipts.Select(r => r.PageId).Distinct(StringComparer.Ordinal).Count() != receipts.Length)
            throw new InvalidDataException("Visio source/page receipt coverage is incomplete or ambiguous.");
        var document = ReadXml(nativeOutput);
        var pageCollections = document.Root!.Elements(Namespace + "Pages").ToArray();
        if (pageCollections.Length != 1) throw new InvalidDataException("Visio output must have one Pages collection.");
        var pages = pageCollections[0].Elements(Namespace + "Page").ToArray();
        if (pages.Length < receipts.Length || pages.Length > 10000 || pages.Select(RequiredId).Distinct(StringComparer.Ordinal).Count() != pages.Length)
            throw new InvalidDataException("Native Visio output has missing or ambiguous pages.");
        for (int i = 0; i < receipts.Length; i++)
        {
            var receipt = receipts[i]; var wanted = expected[i];
            if ((receipt.SourceDiagramId, receipt.SourceSubProcessId, receipt.PageName) != wanted ||
                RequiredId(pages[i]) != receipt.PageId || (string?)pages[i].Attribute("Name") != receipt.PageName)
                throw new InvalidDataException("Visio page receipt does not match its actual source surface.");
            if (receipt.SourceSubProcessId.Length != 0 && !pages[i].Descendants(Namespace + "Shape").Any())
                throw new InvalidDataException("A populated native subprocess still exported as an empty page.");
        }
        var removed = pages.Skip(receipts.Length).ToArray();
        foreach (var page in removed) RequireReservedPage(page);
        // Removing even an empty background page would change its referring foreground page.
        var removedIds = removed.Select(RequiredId).ToHashSet(StringComparer.Ordinal);
        if (pages.Take(receipts.Length).Any(p => removedIds.Contains((string?)p.Attribute("BackPage") ?? "")))
            throw new InvalidDataException("A requested Visio page refers to an empty reservation as its background.");
        // Keep all requested pages, masters, styles and other document content. Only newly generated,
        // strictly recognized empty reservations are removed; never merge pages from another document.
        foreach (var page in removed) page.Remove();
        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), Indent = false })) document.Save(writer);
        byte[] bytes = output.ToArray();
        Inspect(bytes);
        return new(bytes, removed.Select(RequiredId).ToArray());
    }

    private static void RequireReservedPage(XElement page)
    {
        bool OnlyWhitespaceAroundChildren(XElement element) => element.Nodes().All(n => n is XElement || n is XText t && string.IsNullOrWhiteSpace(t.Value));
        bool OnlyNamespaceAttributes(XElement element) => element.Attributes().All(a => a.IsNamespaceDeclaration);
        if ((string?)page.Attribute("NameU") != "Page NameU" || page.Attributes().Any(a => !a.IsNamespaceDeclaration && a.Name != "ID" && a.Name != "NameU") ||
            !OnlyWhitespaceAroundChildren(page)) throw new InvalidDataException("An unrequested Visio page is not an empty native reservation.");
        var sheets = page.Elements().ToArray();
        if (sheets.Length != 1 || sheets[0].Name != Namespace + "PageSheet" || !OnlyNamespaceAttributes(sheets[0]) || !OnlyWhitespaceAroundChildren(sheets[0]))
            throw new InvalidDataException("Reserved Visio page contains unknown sheet content.");
        var properties = sheets[0].Elements().ToArray();
        if (properties.Length != 1 || properties[0].Name != Namespace + "PageProps" || !OnlyNamespaceAttributes(properties[0]) || !OnlyWhitespaceAroundChildren(properties[0]))
            throw new InvalidDataException("Reserved Visio page contains unknown properties.");
        var fields = properties[0].Elements().ToArray();
        string[] names = ["PageWidth", "PageHeight", "PageScale", "DrawingScale"];
        if (!fields.Select(e => e.Name).SequenceEqual(names.Select(n => Namespace + n)) || fields.Any(e => e.HasElements || !OnlyNamespaceAttributes(e) ||
            e.Nodes().Any(n => n is not XText) || !double.TryParse(e.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value) || value <= 0))
            throw new InvalidDataException("Reserved Visio page contains nondefault or unknown payload.");
    }
}
