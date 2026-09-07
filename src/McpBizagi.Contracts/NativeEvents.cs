namespace McpBizagi.Contracts;

/// <summary>Patch existing native event properties; omission preserves the existing value.</summary>
public sealed class NativeEventProperties
{
    public bool? IsInterrupting { get; set; }
    /// <summary>Boundary target must be a native activity in the same flow container. Empty does not detach.</summary>
    public string? AttachedToActivityId { get; set; }
}

/// <summary>Actual native event mode and reference representations, not inferred from its display name.</summary>
public sealed class NativeEventInfo
{
    public string Mode { get; set; } = "";
    public bool? IsInterrupting { get; set; }
    public bool? IsParallelMultiple { get; set; }
    public string AttachedToActivityId { get; set; } = "";
    public string AttachedToBpmnName { get; set; } = "";
    public string AttachedToBpmnNamespace { get; set; } = "";
    public string AttachedToCatalogActivityId { get; set; } = "";
    /// <summary>Native definition kinds only. This does not flatten or expose all definition payloads.</summary>
    public string[] DefinitionKinds { get; set; } = System.Array.Empty<string>();
    public NativeEventDefinitionInfo[] Definitions { get; set; } = System.Array.Empty<NativeEventDefinitionInfo>();
}

/// <summary>Native instantiation and gateway kind, independent of gateway direction.</summary>
public sealed class NativeEventGatewayInfo
{
    public bool Instantiate { get; set; }
    public string Kind { get; set; } = "";
}
