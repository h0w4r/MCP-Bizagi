# 0.7.0-alpha.1 Windows package verification

## Verified artifact

- Code commit: `a58eea9e65bbb359ad562b538bfea793adeb3401`, clean checkout.
- ZIP: `MCP-Bizagi-0.7.0-alpha.1-win-x64.zip`.
- SHA-256: `97c33729754d1bba933eefeb5050d772d6d79578b38b58e721f19e6340e6409a`.
- **541 manifest entries** verified before testing and unchanged after each circuit.
- Installed native engine: **Bizagi Modeler 4.3.0.008**, Windows x64.
- **58 actual MCP tools** enumerated by the independent protocol client.
- Build: zero warnings/errors; **1504 unit tests** passed separately from runtime
  acceptance. Package integrity policy: 19 cases; dependency-license policy: 8 cases.
- [Code-commit CI](https://github.com/h0w4r/MCP-Bizagi/actions/runs/34340651902)
  passed. Public CI does not run proprietary-engine tests or operator desktop work.

This is the verified consolidated package for the bounded modeling and managed
session scope. It is still an experimental prerelease, **not universal Modeler
parity**. Historical ZIPs remain immutable. This post-execution report supplements
the documentation captured inside the archive at its exact code commit.

## Real extracted-package circuits

Each circuit used the same extracted ZIP, not source server/worker binaries.
The independent acceptance client used actual stdio MCP calls. The managed-live
case also used default sibling discovery for its packaged owner, companion and
native worker. Native operations used the installed engine; no runtime mocks
substituted for persistence, desktop commands or recovery.

| Circuit | Private run ID | Completed | Expected failed | Expected cancelled |
| --- | --- | ---: | ---: | ---: |
| Managed live sessions | `20260909-103305-5b0dc1` | 21 | 4 | 0 |
| General file/native flow | `20260909-103516-fc4cfc` | 6 | 1 | 1 |
| Native model creation | `20260909-103618-128dcc` | 8 | 1 | 0 |
| Reusable-body inlining | `20260909-103820-c296fb` | 4 | 1 | 1 |
| Complete diagram layout | `20260909-103947-3fc339` | 13 | 2 | 1 |
| Missing dependency and recovery | `20260909-104256-e67c53` | 1 | 1 | 0 |
| **Total** | **6 passing circuits** | **53** | **10** | **3** |

These are **66 terminal journaled operations**, not 66 successful operations.
Immediate XML calls and argument rejections are additional protocol traffic.
No operation was left running or silently counted as success after cancellation.

### Managed live behavior

- A dedicated visible editor opened a staged multi-diagram native model; its
  owner and editor were verified at normal interactive process priority.
- Native Name/Documentation edits, undo/redo and live-revision conflicts were
  exercised against the actual unsaved editor document.
- The entire MCP process tree was terminated while the document was unsaved.
  A new MCP process observed the same native revision and edits.
- Checkpointing retained the previous archive and a separate saved artifact;
  an independent packaged native worker reopened it.
- Explicit publication verified destination revision, backups, whole-archive
  fidelity and fresh native readback without publishing later unsaved changes.
- Dirty-document and unretained-checkpoint close attempts were rejected.
- The native editor exited normally. The independent owner verified its job empty,
  all **16 observed native processes** exited, and its exact launch task was removed.
- `live_close` reported `OwnedTreeVerified: true`. A fresh MCP process also recovered
  close completion after the test withheld its own MCP close-observer file, using
  genuine native admission and independent-owner evidence without replay.

### Saved-model behavior

The general circuit exercised BPMN operations and installed-engine native
roundtrip/editing, plus active cancellation and recovery. Native model creation
tested durable new models, Unicode names, populated diagram edits, rendering and
fresh-reader/no-op checks. Inlining converted a local reusable call to an embedded
subprocess while retaining the shared source and other callers, with separate
native conversion/copy fidelity gates. Diagram layout exercised the represented
whole-diagram planner, installed-engine persistence/readback, independent geometry
checks, explicit rejection and cancellation/recovery cases.

### Startup failure and recovery

The dependency test removed `StreamJsonRpc.dll` only from a newly copied private
worker directory. It retained the actual loader failure and pre-dispatch diagnosis,
verified worker exit, then successfully invoked the intact packaged worker through
a new MCP process. Neither the release directory nor Bizagi's installation changed.

Earlier candidates are not this artifact: one exposed background-priority native
startup delays; another exposed a fast worker-exit/job-assignment diagnostic race.
Their errors and cleanup evidence remain private and were not counted as passing
release circuits. The final implementation records the actual startup stage,
uses ordinary interactive priority and never replays model writes to recover.

## Transcript fingerprints

Raw transcripts can contain local paths and model information; only their SHA-256
digests and sanitized outcomes are published.

| Circuit | Transcript SHA-256 |
| --- | --- |
| Managed live | `5fbb46f0cc8b7f86c82c3f40eeac21967b0e6780addf441e34617b59fbb10eb0` |
| General | `a8d2ac2d15e977e3314937a0ddd25bb0d1c3fa54e7f1d08403c5de4a0f319301` |
| Model creation | `27346a9644c5b0e823e37bf6b8524c3bb69def5d23b18b8addd13d120fc37618` |
| Inlining | `6b082e6f7ec5879c592f9402d61e29b08e2efac0f6cc80ed83032cc5006e6add` |
| Diagram layout | `8bba7ff62d2982b34a6388b7bf34715289a120042708d82c012928c2773cd1b7` |
| Connection failure | `0d9fd9cd6a1f5434abc44afdf9b1391fe6f003fa2ab4bd2c105019288e8ca3a7` |

## Reproduction and boundaries

Use the independent `tests/McpBizagi.Acceptance` client with `--package` pointing
to an extracted, verified archive. The focused flags are `--managed-live`,
`--native`, `--native --model-create-only`, `--native --inlining-only`,
`--native --diagram-layout-only` and `--native --connection-failure`. Run native
circuits serially; their account-level native settings leases are intentional.
The live acceptance input is a disposable multi-diagram fixture with a CallActivity
on its active diagram, matching `LiveSessionAcceptance` and the native inlining
fixture construction. That test assumption is not a restriction on `live_open`.

The package requires .NET 10 x64, .NET Framework 4.8 and a separately installed
supported Modeler. Managed live launch additionally requires an interactive user
session and on-demand Task Scheduler access; it does not request elevation.
See [configuration](configuration.md) and [live-session contracts](live-sessions.md).

Arbitrary existing-window attachment, every property-panel draft, exhaustive
simulation/interchange parity, future engine certification and independent
pixel-level GUI equivalence remain outside this bounded release. Previous
per-feature source evidence is not silently promoted into a fresh package test
for every individual feature combination.
