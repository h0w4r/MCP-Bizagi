namespace McpBizagi.Contracts;

/// <summary>Explicit native data-object, store-catalog or store-reference property intent.</summary>
public sealed class NativeDataProperties
{
    public string? State { get; set; }
    public bool? IsCollection { get; set; }
    public string? Capacity { get; set; }
    public bool? IsUnlimited { get; set; }
    public string? StoreId { get; set; }
}

/// <summary>Native data values and separate resolved/store QName identities; no synthetic catalog identity.</summary>
public sealed class NativeDataInfo
{
    public string State { get; set; } = "";
    public bool? IsCollection { get; set; }
    public string? Capacity { get; set; }
    public bool? IsUnlimited { get; set; }
    public string StoreId { get; set; } = "";
    public string StoreBpmnName { get; set; } = "";
    public string StoreBpmnNamespace { get; set; } = "";
}

/// <summary>Owned native I/O ports, associations and ordered sets; set runtime GUIDs are not durable identities.</summary>
public sealed class NativeDataFlowInfo
{
    public bool HasSpecification { get; set; }
    public NativeElement[] Inputs { get; set; } = System.Array.Empty<NativeElement>();
    public NativeElement[] Outputs { get; set; } = System.Array.Empty<NativeElement>();
    public NativeElement[] InputAssociations { get; set; } = System.Array.Empty<NativeElement>();
    public NativeElement[] OutputAssociations { get; set; } = System.Array.Empty<NativeElement>();
    public string[][] InputSets { get; set; } = System.Array.Empty<string[]>();
    public string[][] OutputSets { get; set; } = System.Array.Empty<string[]>();
}
