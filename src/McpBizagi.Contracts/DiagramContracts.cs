namespace McpBizagi.Contracts;

/// <summary>Explicit document lifecycle and native persisted tab preferences; never filesystem enumeration order.</summary>
public sealed class NativeDiagramPatch
{
    public NativeDiagramChange[] Changes { get; set; } = System.Array.Empty<NativeDiagramChange>();
    /// <summary>Null preserves tab preferences. A supplied array replaces the complete ordered opened-item list.</summary>
    public NativeOpenedItem[]? OpenedItems { get; set; }
}

public sealed class NativeDiagramChange
{
    public string Operation { get; set; } = "";
    /// <summary>New identity for create, existing target for rename/delete, source identity for clone.</summary>
    public string DiagramId { get; set; } = "";
    public string? Name { get; set; }
}

public sealed class NativeOpenedItem
{
    public string DiagramId { get; set; } = "";
    public string SubProcessId { get; set; } = "";
    public bool IsSelected { get; set; }
}

public sealed class NativeDiagramInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class NativeDiagramSnapshot
{
    public NativeDiagramInfo[] Diagrams { get; set; } = System.Array.Empty<NativeDiagramInfo>();
    public NativeOpenedItem[] OpenedItems { get; set; } = System.Array.Empty<NativeOpenedItem>();
    /// <summary>Archive-relative preference entries used by the installed engine; contains local user identity.</summary>
    public string[] PreferenceEntries { get; set; } = System.Array.Empty<string>();
}

/// <summary>Native-generated clone identities, retained with the write receipt and independently checked against reload.</summary>
public sealed class NativeDiagramClone
{
    public string SourceId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public NativeCloneIdentity[] Identities { get; set; } = System.Array.Empty<NativeCloneIdentity>();
}

public sealed class NativeCloneIdentity
{
    public string SourceId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string SourceBpmnId { get; set; } = "";
    public string TargetBpmnId { get; set; } = "";
}
