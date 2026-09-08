using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace McpBizagi.Contracts;

/// <summary>Internal current-operation result artifact; never a public arbitrary XML input.</summary>
public sealed class NativeSimulationResultWrite
{
    public string DiagramId { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public string ResultPath { get; set; } = "";
    public string ResultFileSha256 { get; set; } = "";
}

/// <summary>Digest of the actual native Scenario.SimulationResult string, encoded as UTF-8.</summary>
public sealed class NativeSavedSimulationResult
{
    public string DiagramId { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public int CharacterCount { get; set; }
}

/// <summary>Match native completion's XmlDocument.OuterXml while prohibiting external resolution.</summary>
public static class NativeSimulationResultPayload
{
    public const int MaximumBytes = 64 * 1024 * 1024;
    public static string Read(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumBytes)
            throw new InvalidDataException("Native simulation result artifact is empty or exceeds its limit.");
        // A byte reader honors the file's encoding declaration, including native ISO-8859-1.
        using var input = new MemoryStream(bytes, writable: false);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = MaximumBytes });
        var document = new XmlDocument { XmlResolver = null, PreserveWhitespace = false };
        document.Load(reader);
        if (document.DocumentElement?.Name != "model" || document.DocumentElement.NamespaceURI != "")
            throw new InvalidDataException("Expected an actual native simulation model result document.");
        return document.OuterXml;
    }

    public static string Hash(byte[] bytes)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    public static NativeSavedSimulationResult Describe(string diagramId, string scenarioId, string xml) => new()
    {
        DiagramId = diagramId, ScenarioId = scenarioId, CharacterCount = xml.Length,
        Sha256 = Hash(Encoding.UTF8.GetBytes(xml))
    };
}
