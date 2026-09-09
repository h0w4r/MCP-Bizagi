# Native file adoption and recovery

`native_commit_reconcile` also accepts terminal `live_publish` operations. Live
checkpoint publication uses this same durable intent, replacement, backup and
fresh-reader contract; see [live checkpoint publication](live-sessions.md#publish-a-retained-checkpoint-safely).

`native_commit` publishes byte-exact native model content into the configured
workspace. Use it after reviewing a verified edit artifact, or to save an existing
native model under a new path. It does not turn `.bpm` into BPMN, reserialize the
archive, change unknown properties, or synchronize an open desktop document.

This is a separate, explicit write tool. Edit tools remain copy-only. Source and
destination revisions are distinct preconditions; neither an artifact reference
nor an operation ID authorizes overwriting an existing destination by itself.

## Request and result

| Argument | Meaning |
| --- | --- |
| `path` | Workspace `.bpm` or completed `artifact:<id>:model.bpm` / `artifact:<id>:edited.bpm` |
| `expectedRevision` | Exact lowercase SHA-256 of the source, obtained from a completed native result |
| `destinationPath` | `.bpm` path confined to `MCP_BIZAGI_ROOT`; spaces and Unicode supported |
| `expectedDestinationRevision` | Omit only for create-if-absent; supply the previous destination SHA-256 for replacement |

Inspect an existing destination through `native_inspect` to obtain its
`Result.sourceRevision`. A stale revision, missing expected target, busy writer,
locked file, unsupported path or unsupported native engine is a failure, not a
fallback to an unguarded copy. Native bytes must pass the installed engine before
the destination is touched. The live source revision is checked again immediately
before publication, after the worker has read its private snapshot.

The [JSON example](../examples/native-commit.json) uses explicit placeholders;
replace them with actual artifact and revision values. Poll the returned
`OperationView.OperationId` using `operation_get`.

A completed `native_commit` returns:

- `Result.commit.Path`, `Revision`, `Bytes`, and `BackupPath` (`null` for creation).
- `Result.opened`: the source reader's actual native graph and engine evidence.
- `Result.readback`: a separate destination reader, current file observations,
  `nativeReadbackVerified`, exact revision, and verification time.
- `Result.byteIdentityPreserved`: byte-exact adoption, not a lossy projection.
- `Result.receipt`: the private durable intent path used for recovery.

The destination reader loads a snapshot of the **actual published destination**,
not the prepublication source. Destination, required backup and snapshot hashes
are checked after the new worker completes. Matching file bytes preserve every
archive entry, including native extended attributes and binary attachments. This
does not independently accredit screen appearance or an open unsaved session.

## Publication phases

The existing `WorkspaceFiles.Commit` owns confinement, cooperative writer locking,
same-directory staging, revision checking, atomic move/replacement, backup and
post-write checks. Native adoption reuses that primitive rather than bypassing it.

1. Read and validate the source through the real native worker.
2. Flush the stage file and inspect the expected destination revision.
3. Persist `commit-intent.json` before attempting publication.
4. Recheck the source and cancellation; then create or replace the destination.
5. Persist `commit-published.json` independently of the operation status journal.
6. Start a fresh destination reader and verify actual committed content.
7. Persist `commit-native-readback.json`, then complete the ordinary operation.

`NativeCommitIntent` version 1 binds `OperationId`, `Workspace`, `Source`,
`SourceRevision`, `PreparedAt` and `FileCommitIntent`. The latter binds its own
transaction ID, confined destination/stage/backup paths, previous and desired
revisions, and byte count. Recovery validates the version and derived paths.

Phase receipts are append-only and flushed before their temporary files are
published. Stage and backup files share the destination directory. A referenced
but unconsumed stage is retained on failure or cancellation. Never delete these
files merely because the generic operation says `failed` or `interrupted`.

The underlying BCL mechanisms are documented by Microsoft:
[File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-10.0)
and [FileStream.Flush](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0).
Process-death acceptance is not a power-loss, network-filesystem or failing-disk
durability certification. Atomic publication also does not prevent a different
program from making a later authorized write.

## Cancellation and interruption

Cancellation before publication prevents that write. Cancellation **after**
publication can leave `OperationView.State = cancelled` with no ordinary result,
even though the destination already changed. Host death similarly leaves an
`interrupted` operation after restart. The independent intent and publication
receipts retain the side-effect evidence; status alone is not the write outcome.

Call `native_commit_reconcile` with the **original** `operationId`. The original
operation must be terminal and of kind `native_commit`. Reconciliation creates a
new operation for its own evidence. It never alters the original terminal state,
replays a write, deletes staging, removes backups, or rolls a newer target back.

| `Result.observedState` | Meaning |
| --- | --- |
| `applied` | Desired destination, consumed stage and required previous-version backup match; a new native reader also succeeded |
| `not_applied` | Original target state, intact intended stage and no backup; no publication is observed |
| `ambiguous` | Incomplete or contradictory facts do not establish an outcome, including a removed backup or later restoration of prior bytes |
| `conflict` | Actual content differs from the recorded revisions; retain all versions and resolve explicitly |
| `unreadable` | A destination, stage or backup could not be read; locked is not absent |
| `missing_intent` | Intent evidence is absent; this is **not** proof that nothing was written |

A completed reconciliation means the investigation executed. Only
`observedState = applied` together with `nativeReadbackVerified = true` accredits
the observed native file. Non-applied states are structured outcomes, not a
successful save. Inspect `Result.observation` for paths, present/absent/unreadable
states, hashes, sizes and actual read errors. A failure during native readback
remains a failed reconciliation, with its initial observation retained privately.

These are observations, not forensic proof against an external actor replacing
files with identical bytes. If the evidence was removed, modified, or moved to
another workspace, do not infer a successful or unapplied transaction. After an
explicit decision, a new write request must use **current** revisions. Repeating
reconciliation does not create another backup or change model timestamps.

## Acceptance

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- `
  D:/MCP-Bizagi --native --commit-only --external-state
```

Add `--input <existing-native-corpus.bpm>` to exercise a richer native source.
Without `--input`, the client first creates a native model through MCP. The real
suite covers create/replace, backups, Unicode paths, stale source/destination,
locked/corrupt inputs, source modification during validation, post-publication
cancellation, actual host death during destination readback, repeated independent
reconciliation, divergent external writes and missing intent evidence.

The harness kills only its own host after checking PID **and** creation time.
The Job Object must clean up its actual child worker. No production test pause,
fault-injection tool argument, desktop click or operator process termination is
used. See [recorded execution evidence](validation.md); tests and schemas alone
do not accredit this circuit. Broader desktop equivalence remains a separate goal.
