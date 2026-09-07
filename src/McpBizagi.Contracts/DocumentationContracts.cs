namespace McpBizagi.Contracts;

/// <summary>Explicit native documentation transactions; XML uses the installed Modeler schema, never BPMN.</summary>
public sealed class NativeDocumentationPatch
{
    public NativeAttributeDefinition[] Definitions { get; set; } = System.Array.Empty<NativeAttributeDefinition>();
    public NativeAttributeValues[] Values { get; set; } = System.Array.Empty<NativeAttributeValues>();
    public NativeAttachmentChange[] Attachments { get; set; } = System.Array.Empty<NativeAttachmentChange>();
}

public sealed class NativeAttributeDefinition
{
    public string Operation { get; set; } = "upsert";
    public string Id { get; set; } = "";
    public string Xml { get; set; } = "";
}

/// <summary>Complete replacement of one element's extended values. Empty Values explicitly clears them.</summary>
public sealed class NativeAttributeValues
{
    public string DiagramId { get; set; } = "";
    public string ElementId { get; set; } = "";
    public string Xml { get; set; } = "";
}

/// <summary>Embedded bytes only; linked files are not opened or copied. Names cannot contain paths.</summary>
public sealed class NativeAttachmentChange
{
    public string Operation { get; set; } = "upsert";
    public string DiagramId { get; set; } = "";
    public string ElementId { get; set; } = "";
    public string FileName { get; set; } = "";
    public string DataBase64 { get; set; } = "";
}

public sealed class NativeAttachmentInfo
{
    public string DiagramId { get; set; } = "";
    public string ElementId { get; set; } = "";
    public string FileName { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class NativeDocumentationSnapshot
{
    public NativeAttributeDefinition[] Definitions { get; set; } = System.Array.Empty<NativeAttributeDefinition>();
    public NativeAttributeValues[] Values { get; set; } = System.Array.Empty<NativeAttributeValues>();
    public NativeAttachmentInfo[] Attachments { get; set; } = System.Array.Empty<NativeAttachmentInfo>();
}
