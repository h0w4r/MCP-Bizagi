namespace McpBizagi.Contracts;

/// <summary>Versioned requests for an explicitly managed native desktop session, never arbitrary reflection.</summary>
public sealed class LiveSessionRequest
{
    public int ProtocolVersion { get; set; } = 1;
    public string SessionId { get; set; } = "";
    public string OperationId { get; set; } = "";
    public string Action { get; set; } = "read";
    public string ExpectedRevision { get; set; } = "";
    public string ExpectedDiskRevision { get; set; } = "";
    public LiveElementPatch[] Changes { get; set; } = System.Array.Empty<LiveElementPatch>();
}

/// <summary>Explicit native property edits. Null preserves a property; an empty string clears it.</summary>
public sealed class LiveElementPatch
{
    public string ElementId { get; set; } = "";
    public string? Name { get; set; }
    public string? Documentation { get; set; }
}

/// <summary>Actual in-process editor state. Readiness, execution outcome and capability accreditation are separate.</summary>
public sealed class LiveSessionSnapshot
{
    public string SessionId { get; set; } = "";
    public string DocumentId { get; set; } = "";
    public string Revision { get; set; } = "";
    public string EngineVersion { get; set; } = "";
    public string Path { get; set; } = "";
    public string DiskRevision { get; set; } = "";
    public string ActiveDiagramId { get; set; } = "";
    public bool Dirty { get; set; }
    public bool CanUndo { get; set; }
    public bool CanRedo { get; set; }
    public bool Visible { get; set; }
    public NativeElement[] Elements { get; set; } = System.Array.Empty<NativeElement>();
    public NativeMetadataSnapshot? Metadata { get; set; }
    public NativeDocumentationSnapshot? Documentation { get; set; }
    public NativeDiagramSnapshot? DiagramState { get; set; }
    public LiveSynchronizationEvidence? EditorSynchronization { get; set; }
}

/// <summary>Observed browser-to-native callback completion, not merely script dispatch.</summary>
public sealed class LiveSynchronizationEvidence
{
    public int BarrierVersion { get; set; }
    public long EditorEpoch { get; set; }
    public long StartedCallbacks { get; set; }
    public long CompletedCallbacks { get; set; }
    public int PendingCallbacks { get; set; }
    public int VerifiedPendingLabels { get; set; }
}

/// <summary>A retained receipt is authoritative after a transport disconnect; clients must not replay uncertain edits.</summary>
public sealed class LiveSessionReply
{
    public string OperationId { get; set; } = "";
    public string State { get; set; } = "rejected";
    public string Code { get; set; } = "not_executed";
    public string Message { get; set; } = "";
    public LiveSessionSnapshot? Snapshot { get; set; }
    public LiveCheckpoint? Checkpoint { get; set; }
    public string[] Warnings { get; set; } = System.Array.Empty<string>();
}

/// <summary>A durable working-copy checkpoint, never an implicit replacement of the operator's original.</summary>
public sealed class LiveCheckpoint
{
    public string ArtifactPath { get; set; } = "";
    public string Revision { get; set; } = "";
    public string PreviousArtifactPath { get; set; } = "";
    public string PreviousRevision { get; set; } = "";
    public string DocumentRevision { get; set; } = "";
    public bool DestinationPublished { get; set; }
}

/// <summary>Shared netstandard validation runs at both sides of the pipe before native dispatch.</summary>
public static class LiveSessionProtocol
{
    public const int MaximumBatchSize = 128;
    public static void Validate(LiveSessionRequest request)
    {
        if (request == null) throw new System.ArgumentNullException(nameof(request));
        if (request.ProtocolVersion != 1) throw new System.NotSupportedException("Unsupported live-session protocol version.");
        RequireId(request.SessionId, "SessionId"); RequireId(request.OperationId, "OperationId");
        if (request.Action != "read" && request.Action != "update" && request.Action != "undo" && request.Action != "redo" && request.Action != "checkpoint")
            throw new System.NotSupportedException("Unsupported live-session action.");
        if (request.Action != "read" && string.IsNullOrWhiteSpace(request.ExpectedRevision))
            throw new System.ArgumentException("A live document revision is required before changing the editor.");
        if (request.ExpectedRevision == null || request.ExpectedRevision.Length > 256)
            throw new System.ArgumentException("Invalid live revision.");
        if (request.ExpectedDiskRevision == null || (request.Action == "checkpoint"
            ? request.ExpectedDiskRevision.Length != 64 || !System.Linq.Enumerable.All(request.ExpectedDiskRevision, c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))
            : request.ExpectedDiskRevision.Length != 0))
            throw new System.ArgumentException("Only checkpoint requires an observed lowercase SHA-256 disk revision.");
        if (request.Changes == null) throw new System.ArgumentException("Changes cannot be null.");
        if (request.Action != "update" && request.Changes.Length != 0)
            throw new System.ArgumentException("Only update accepts element changes.");
        if (request.Action == "update" && (request.Changes.Length == 0 || request.Changes.Length > MaximumBatchSize))
            throw new System.ArgumentException("An update must contain between 1 and 128 element changes.");
        var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        long characters = 0;
        foreach (var patch in request.Changes)
        {
            if (patch == null) throw new System.ArgumentException("A live patch cannot be null.");
            RequireId(patch.ElementId, "ElementId");
            if (!ids.Add(patch.ElementId)) throw new System.ArgumentException("An element can occur only once in a live batch.");
            if (patch.Name == null && patch.Documentation == null) throw new System.ArgumentException("A live patch must change an explicit property.");
            RequireText(patch.Name, 16384); RequireText(patch.Documentation, 1048576);
            characters += (patch.Name?.Length ?? 0) + (patch.Documentation?.Length ?? 0);
            if (characters > 4 * 1024 * 1024) throw new System.ArgumentException("Live batch text exceeds the aggregate size limit.");
        }
    }

    private static void RequireId(string value, string field)
    {
        if (!System.Guid.TryParseExact(value, "D", out var id) || id == System.Guid.Empty)
            throw new System.ArgumentException(field + " must be a nonempty canonical UUID.");
    }

    private static void RequireText(string? value, int maximum)
    {
        if (value == null) return;
        if (value.Length > maximum) throw new System.ArgumentException("Live property text exceeds its size limit.");
        // Native persistence is XML-based; reject invalid control/surrogate characters before mutation.
        System.Xml.XmlConvert.VerifyXmlChars(value);
    }
}
