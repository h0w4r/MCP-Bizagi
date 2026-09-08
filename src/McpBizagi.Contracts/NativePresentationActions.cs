namespace McpBizagi.Contracts;

/// <summary>One installed presentation action per native graphical element.</summary>
public sealed class NativePresentationAction
{
    public string DiagramId { get; set; } = "";
    public string ElementId { get; set; } = "";
    public string Type { get; set; } = "None";
    public string TypeValue { get; set; } = "Normal";
    public string ExtendedAttributeId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    /// <summary>Normal file/image content uses action-file:name; referenced embedded content uses attachment:name.</summary>
    public string Content { get; set; } = "";
}

/// <summary>Complete action replacement or explicit deletion; referenced content is derived from the native owner.</summary>
public sealed class NativePresentationActionChange
{
    public string Operation { get; set; } = "upsert";
    public NativePresentationAction Action { get; set; } = new();
    /// <summary>Bytes for the explicitly named normal file/image action; never a pathname to execute.</summary>
    public string DataBase64 { get; set; } = "";
}

public sealed class NativePresentationSnapshot
{
    public NativePresentationAction[] Actions { get; set; } = System.Array.Empty<NativePresentationAction>();
    public NativeAttachmentInfo[] Files { get; set; } = System.Array.Empty<NativeAttachmentInfo>();
}
