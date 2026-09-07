# Architecture

## Boundaries

The MCP host owns client-facing contracts. Core XML operations preserve the
original XML tree instead of roundtripping it through a simplified graph DTO.
Native `.bpm` operations never fall back to the XML implementation.

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

## Persistence

XML edits require SHA-256 revision matching. Writes use a same-directory staging
file, flush before replacement, preserve a backup, and verify persisted bytes.
Unknown XML extension content is retained; unsupported edit operations fail.

Native diagnostics are opt-in and cannot overwrite the operator's original model.
Operation state is durable. Server restart marks unfinished work `interrupted`,
never automatically repeats a potentially completed write.

## Concurrency and recovery

Native operations are serialized. Each worker's RPC channel and process lifetime
belong to the host; cleanup targets only owned children. Cancellation stops the
isolated worker rather than closing an existing Modeler instance.

Operation duration is not a cancellation criterion. The inactivity window is
renewed by native phases, CPU changes, or additional diagnostic output. Connection
and cleanup stages have separate bounded deadlines.

## Explicitly not implemented

No generic AI agent framework, hosted inference dependency, network API, UI
automation fallback, live unsaved-document synchronization, documentation publisher,
or simulation command is silently included in this foundation.
