namespace McpBizagi.Contracts;

/// <summary>Explicit extraction of an existing embedded subprocess, never a desktop selection or clipboard command.</summary>
public sealed class NativeSubProcessExtraction
{
    public string ElementId { get; set; } = "";
    public string NewDiagramName { get; set; } = "";
}

/// <summary>Identities returned by the installed refactoring command; child identities remain unchanged.</summary>
public sealed class NativeExtractionReceipt
{
    public string ElementId { get; set; } = "";
    public string SourceDiagramId { get; set; } = "";
    public string TargetDiagramId { get; set; } = "";
    public string TargetParticipantId { get; set; } = "";
    public string TargetProcessId { get; set; } = "";
    public string[] MovedElementIds { get; set; } = System.Array.Empty<string>();
}
