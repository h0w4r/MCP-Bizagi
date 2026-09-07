using System.Xml;
using System.Xml.Linq;
using McpBizagi.Contracts;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    private static NativeSimulationReport[] SimulationReports(string[] artifacts, EngineRequest request)
    {
        XDocument Read(string path)
        {
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 64 * 1024 * 1024 });
            return XDocument.Load(reader);
        }
        return artifacts.Where(p => Path.GetFileName(p) == "Results.xml" || Path.GetFileName(p).StartsWith("replication-", StringComparison.Ordinal) && !p.EndsWith(".identity.xml", StringComparison.Ordinal))
            .Select(path =>
            {
                var doc = Read(path);
                if (doc.Root?.Name != "model") throw new InvalidDataException("Unknown native simulation result schema.");
                XElement? identity = File.Exists(path + ".identity.xml") ? Read(path + ".identity.xml").Root : null;
                return new NativeSimulationReport
                {
                    ScenarioId = (string?)identity?.Attribute("scenarioId") ?? request.ScenarioId,
                    Replication = (int?)identity?.Attribute("number"),
                    Artifact = path,
                    Elements = doc.Descendants("element").SelectMany(e => e.Elements().Where(c => c.HasAttributes).Select(c => new NativeSimulationElement
                    {
                        Id = (string?)e.Attribute("name") ?? "",
                        Name = (string?)e.Attribute("friendlyName") ?? "",
                        Kind = c.Name.LocalName,
                        Metrics = c.Attributes().ToDictionary(a => a.Name.ToString(), a => a.Value)
                    })).ToArray()
                };
            }).ToArray();
    }
}
