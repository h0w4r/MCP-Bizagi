namespace McpBizagi.Contracts;

/// <summary>Versioned, explicitly allowlisted operations; never arbitrary reflection.</summary>
public sealed class EngineRequest
{
    public int ProtocolVersion { get; set; } = 1;
    public string OperationId { get; set; } = "";
    public string Action { get; set; } = "probe";
    public string InputPath { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public string ModelName { get; set; } = "Model";
}

/// <summary>A native-engine result is distinct from an independently verified capability.</summary>
public sealed class EngineReply
{
    public bool Success { get; set; }
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public string EngineVersion { get; set; } = "";
    public string OperationId { get; set; } = "";
    public string[] Artifacts { get; set; } = System.Array.Empty<string>();
    public string[] Diagrams { get; set; } = System.Array.Empty<string>();
}
