# Architecture

## Boundaries

The MCP host owns client-facing contracts. Core XML operations preserve the
original XML tree instead of roundtripping it through a simplified graph DTO.
Native `.bpm` operations never fall back to the XML implementation.

`ModelTools` owns MCP schemas and result envelopes. `NativeWorkflows` owns the
native use cases. `WorkerClient` supervises transport and processes; `NativeEngine`
is the isolated, version-specific adapter. The core has no vendor references.

The Windows worker uses .NET Framework to load current installed Bizagi libraries.
The modern MCP host does not load those libraries. StreamJsonRpc carries versioned
requests through a randomly named pipe with an ACL restricted to the current user.
Only allowlisted request actions are exposed.

## Native adapter

The 4.3 adapter uses the installation's module registration without building or
initializing its desktop application. `IFileSystemPersistenceManager` and
`IBpmnInteropManager` are resolved through the native dependency injector.

Internal API signatures are not a vendor support contract. The integration is
version gated, and resolving services is not a completed workflow. Some native
overloads have no implementation, so calls require postconditions on real files.

The original input is copied to operation-specific evidence. A writer worker
imports BPMN and persists `.bpm`; a separate reader worker reopens and exports it.
Export results are parsed and recorded, but comprehensive semantic and visual
equivalence requires additional acceptance coverage.

Native name changes use IDs returned by the real model, not presumed source XML
IDs. `NativeWorkflows.ApplyNames` snapshots the original, checks its revision,
edits a new `.bpm`, and starts another reader to verify **every** requested ID/name
pair. No acceptance-test strings are hardcoded into runtime behavior.
The full native container must also pass `NativeFidelity.Compare`; unknown
content is not reconstructed or silently excluded from that check.

## Persistence

XML edits require SHA-256 revision matching. Writes use a same-directory staging
file, flush before replacement, preserve a backup, and verify persisted bytes.
Unknown XML extension content is retained; unsupported edit operations fail.
Cooperating MCP writers also use a named mutex. External tools do not honor this
mutex: an unexpected replacement detected in the backup fails explicitly while
retaining both versions. This is optimistic concurrency, not a filesystem-wide
transaction or a defense against hostile code running as the same user.

Native editing/analysis are opt-in and artifact-first. Only the separate explicit
`native_commit` tool adopts native bytes into a workspace destination, requiring
source and previous-target revisions, native readers and a recoverable backup.
`WorkspaceFiles.Commit` invokes its durable-intent callback before publication;
native phase receipts are independent of ordinary operation status. Read-only
file-outcome reconciliation never replays, deletes or rolls back model content.
See the [native commit contract](native-commit.md).

Operation state is durable. Server restart marks unfinished work `interrupted`,
never automatically repeats a potentially completed write. Cancellation after
publication does not mean no side effect; the intent and destination/backup
observations determine whether a fresh native read can verify the applied file.

## Concurrency and recovery

Native operations are serialized. Each worker's RPC channel and process lifetime
belong to the host; cleanup targets only owned children. Cancellation stops the
isolated worker rather than closing an existing Modeler instance.
A kill-on-close Windows Job Object owns each process tree. Sampled, read-only
desktop observations check the owned PID for visible windows and foreground
ownership; no unrelated window names are recorded. These samples are evidence
of the observed runs, not a continuous proof about every possible engine path.

Operation duration is not a cancellation criterion. The inactivity window is
renewed by native phases, owned-job CPU/I/O changes, or additional diagnostic output. Connection
and cleanup stages have separate bounded deadlines.

An initial pipe deadline is a failed connection, not an operator cancellation.
`worker-connection-error.json` records the connection stage, configured deadline,
elapsed time, owned-job CPU/I/O counters and `requestDispatched: false`. The
operation journal reports `cancelled` only when its own cancellation token was
requested. No connection failure automatically replays a write.

## Explicitly not implemented

No generic AI agent framework, hosted inference dependency, network API or UI
automation fallback is silently included. Managed live-document synchronization
uses the explicit lifecycle below; it is not inferred from saved-file operations.
Simulation, offscreen rendering, structural edits and local publication use explicit
experimental tools with separate acceptance boundaries. Publication retains the
installed generators and parsers. The Excel adapter excludes the native launcher's
desktop-opening step while retaining native mapping, generation and persistence.

## Independent live owner

The stdio server registers an on-demand, interactive-current-user Windows task
whose only action is the configured `McpBizagi.LiveOwner` executable. It has no
time trigger, recurring schedule, password or elevation. The owner acquires the
account's exclusive live slot and owns `McpBizagi.LiveHost` plus native descendants
in a Windows job, independent of the stdio client's ancestry. A launcher crash
does not implicitly close an already running editor.

Versioned launch/start/exit journals bind session UUID, source hash, staged copy,
executables, process IDs and creation times. The native host authenticates local
pipe access; the MCP client also verifies the actual pipe-server process. Native
commands execute on the real editor's STA thread after its initialization barrier.
The owner's job-empty evidence and exact task removal complement—not replace—
native clean-checkpoint admission and OS exit observation. See the
[session contract](live-sessions.md) and [source acceptance](validation-live-owner.md).
