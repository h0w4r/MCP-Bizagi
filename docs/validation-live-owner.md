# Managed live-session source acceptance

## Verified circuit

The final source run `20260909-095330-1757bb` used the official MCP SDK's
independent stdio client, production server, production Windows-scheduled owner,
production native companion and installed Modeler **4.3.0.008**. No engineering
supervisor, simulated input, substitute renderer or runtime mock was used.

- Actual `live_open` registered and activated a least-privileged current-user task.
- The dedicated native editor loaded the owned multi-diagram acceptance model.
- Name and Documentation commands changed the actual unsaved native document.
- Native undo/redo and stale-revision rejection were observed through MCP.
- The acceptance client terminated the entire MCP process tree while work was
  unsaved. A new MCP process observed the same native document revision and edits.
- Checkpointing retained full previous/current archives. A fresh native worker
  reopened the saved file and verified the edited properties.
- Guarded publication rejected destination conflicts and unaccounted differences,
  retained its backup, verified archive fidelity and preserved newer unsaved edits.
- Dirty-document and unretained-checkpoint close attempts were rejected.
- Native closing returned exit code zero. The independent owner verified its
  native job empty and all **19 observed owned processes** exited, then removed
  its exact launch task and exited normally.
- `live_close` returned `OwnedTreeVerified: true` only after that cleanup.
- The test withheld the MCP observer's close-exit file from its own disposable
  registry. A new MCP process recovered completion from actual native admission,
  independent-owner exit evidence, task removal and retained file hashes. Close
  was not replayed.

There were **21 completed and 4 intentionally failed terminal operations** in this
live transcript, in addition to direct argument/conflict rejections. Policy tests
are separate: **1504 unit tests passed**, with zero build warnings/errors;
package-manifest and dependency-license policy suites passed independently.

## Evidence fingerprints

SHA-256, lowercase:

| Artifact | Digest |
| --- | --- |
| Private final MCP transcript | `4e1a2560d6914da78266f1b8830ba390044eb0e4d72e64e9de0544c54dd14876` |
| Installed `BizagiModeler.exe` | `12af34e6343d1288f4af4ebdcae27e20a9d7aa1e04085fdd2766f57fedec2f96` |
| Installed `Bizagi.ProcessModeler.UI.dll` | `0d5eeecc3417f3af5cb9cddafe74350672aa495516f1803191915b73d719f5ae` |
| Installed `CefSharp.WinForms.dll` | `49a4c5ec4b9d7efa7ebec95fe313c11ae1ea2d8baff7f9c21e05d12d8b91846e` |

Raw transcripts, native files, paths and process diagnostics remain private.
Only sanitized technical findings and hashes are published here.

## Failures preserved rather than hidden

An earlier owner sampler hit a Windows sharing/rename access error. Telemetry is
now best-effort and such errors do not end an otherwise healthy native editor.
Task Scheduler normalized the user's SID into an account name; cleanup now checks
the normalized SID plus the fixed executable and arguments before deleting a task.

A separate failed native startup exhausted Modeler's default 12-second page-load
handshake and left a native error modal. A focused snapshot captured the real
exception. Original and working-copy hashes were unchanged and no MCP mutation
had been dispatched. Its pinned failed test editor was explicitly terminated;
the owner verified all 34 observed descendants exited and removed its task.
**That run was not counted as a successful native close.**

Native form/document/context readiness now guards RPC admission. The dedicated
host's configurable page handshake is 120 seconds; lifetime observation still
uses actual activity, not a total-operation kill timeout. Two subsequent managed
lifecycle circuits completed, including the final stronger close proof above.

## Boundary

This record accredits the stated **source** circuit, not an arbitrary future
package, arbitrary existing Modeler processes, every property-panel draft,
pixel-level GUI equivalence or universal Modeler feature parity. Package tests
must execute their own binaries and preserve their own immutable manifest.
