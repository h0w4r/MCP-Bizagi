# Consolidated native package verification — 2026-09-07

This is an executed package checkpoint, not a full-automation completion claim
or a new public release. Five independent MCP client circuits used one freshly
extracted ZIP, the installed Bizagi Modeler **4.3.0.008** engine and external
per-run state directories. The server and worker ran from the package, not the
development build directories. Proprietary components were loaded locally and
were not included in the package.

## Package identity

| Field | Verified value |
| --- | --- |
| Source commit | `3c52d54e2af6a3f0dbaec5832e3ca08897908401` |
| Checkout at packaging | Clean; `dirtyCheckout: false` |
| Archive size | 6,690,438 bytes |
| Archive SHA-256 | `a240e76e36060d0c4c84e2c208aa621322f075b410136273ae7c7afcb76cb35b` |
| Manifest | 331 entries, all hashes verified before and after all circuits |
| Extracted files | 332 including the manifest itself; no unexpected files |
| Source unit/component baseline | 721 passed; separate from native acceptance |
| Public CI | [Successful run for this source](https://github.com/h0w4r/MCP-Bizagi/actions/runs/34162830001) |

Pre-verification completed at `2026-09-07T21:22:53Z`; post-verification completed
at `2026-09-07T21:43:04Z`. Runtime state did not alter the extracted distribution.
Newer source changes do not retroactively become part of this immutable ZIP.

## Actual circuit results

Counts below refer to unique terminal operation-journal identities. Rejected
preflight tool requests are not inflated into additional native operations.
Expected failures, cancellation and interruption are successful negative tests,
not successful model edits.

| Circuit | Run ID | Completed | Expected failures | Cancelled | Interrupted | Worker observations | Desktop samples |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Event data bindings | `20260907-212254-3a6c56` | 13 | 4 | 0 | 0 | 29 | 361 |
| Data/activity lifecycle | `20260907-212659-ebe94c` | 16 | 4 | 0 | 0 | 35 | 264 |
| Event definition payloads | `20260907-213040-eddafb` | 15 | 4 | 0 | 0 | 33 | 332 |
| Special subprocesses | `20260907-213435-3f9046` | 23 | 10 | 0 | 0 | 50 | 630 |
| General native/recovery | `20260907-214101-886c7d` | 13 | 2 | 1 | 1 | 21 | 118 |
| **Total** | **Five circuits** | **80** | **24** | **1** | **1** | **168** | **1,705** |

The package passed all five circuits: **106 terminal native operations**.
Periodic observations found no visible owned-worker window or worker foreground
ownership. These sampled checks are not a continuous desktop trace, Session 0
accreditation or independent visual review inside Modeler.

The focused circuits exercised their documented native lifecycle, fresh-worker
readback, unrelated-change preservation, cloning, rendering/publication and
explicit invalid-operation paths. Consult the bounded contracts for
[data and I/O](native-data.md), [event payloads](native-event-payloads.md) and
[special subprocesses](native-subprocesses.md).

The general circuit included BPMN/native roundtrip, native validation and default
simulation, offscreen SVG, real corrupt input, settings contention, active
cancellation and recovery, abrupt host death, owned-worker Job Object cleanup,
journal restart and competing-state ownership rejection. It did **not** exercise
every engine persistence interruption boundary or every simulation behavior.

## Retained transcript fingerprints

| Circuit | MCP transcript SHA-256 |
| --- | --- |
| Event data | `0d4f8e763a69e450644dc1f193d318b8c146d61b070e51014d5bb5aecdc35e62` |
| Data/activity | `4fbbefc30acbbd1bd98d0d54434b799c95a763f8a61ab3f074c8cb97e725854f` |
| Event payloads | `e294ae45ef66c0c51febccd3f38c19eb0929499ac3c3199763438d92348d72c7` |
| Subprocesses | `a5e438bec7f5fe96f884f04c5c65098183f5ce23775bf882da8f481636d09cbc` |
| General/recovery | `1fb2df5e36df4636111fb2aa6bf409f38e5dcd45fb64cdd07c8b71ba234b5d9a` |

Raw native models, worker logs, native library research and environment details
remain private. These hashes identify retained evidence; they do not replace
rerunning the circuit on another operator installation.

## Repetition and remaining scope

Follow [packaging verification](packaging.md#verify-the-packaged-server). The
independent acceptance client supports `--package <extracted-directory>`,
`--external-state` and `--native`, with these separate focused selectors:
`--event-data-only`, `--data-only`, `--event-payloads-only`, and
`--subprocesses-only`. The general run used `--extended --simulation --render
--recovery --settings-contention`. Every invocation had its own state directory.

Full Modeler automation remains open in the [capability ledger](capabilities.md).
This checkpoint does not accredit unrestricted I/O or event collection editing,
all artifact types, every exchange/publication format, live unsaved documents,
independent GUI compatibility, future Modeler versions or strict nondefault
token-quantity execution. The [native semantic execution limitation](native-semantics.md)
remains explicit; default simulation success does not resolve it.
