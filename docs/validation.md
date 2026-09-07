# Verification baselines

## Native reusable-call lifecycle and black-box simulation — 2026-09-07

Current source exposes **29 MCP tools** and passes **444 unit tests**. The native
call contract extends existing mutation/inspection/analysis tools rather than
introducing arbitrary reflection. Tests use the real installed **4.3.0.008**
engine, independent stdio MCP, private state outside the workspace, durable
native artifacts and fresh worker processes.

Run `20260907-151811-376a31` passed **22 operations**: 17 completed and five
expected failures, with **37 worker observations / 211 periodic samples**.
Transcript SHA-256:
`598934fefea78efa91831633f478137e0ca6dd75ce2782ac3461df6c93664b9e`.

The lifecycle circuit verified native `CallActivity` creation, local and embedded
callers, unrelated name/documentation edits retaining references, invalid target
and wrong-kind rejection, target-diagram and pool/process deletion protection,
nested incoming-call protection, relinking/unlinking, call deletion, native clone
remapping, offscreen rendering and no-op fidelity. Calls to a different diagram
retain the original target; cloned internal targets use the actual native ID map.

Run `20260907-151436-d10f9d` passed **10 completed operations**, with
**16 worker observations / 260 periodic samples**. Transcript SHA-256:
`45e74c76fcd21283c62379ad86fec42602bf631ed9b4f559b83a4ee1079c5ee6`.
Its valid two-diagram model completed native validation without findings, then
persisted explicit call-shape simulation settings. Native single-scenario and
four what-if replication results verified 12 completions with 3/9-minute
black-box durations. The caller end and generated black-box task were correlated
to their actual IDs; the linked service task was absent from caller simulation.
Structured limitations retained the original local target. Word/PDF publications
passed separate native reader/image gates; retained reader text contains the
call name, call documentation, target diagram and target service task. Subsequent
native inspection confirmed the original durable call target and source hash.

### Failures retained during integration

- `20260907-145436-d2cb1e`: requested target redirection failed whole-container
  fidelity because the exact native `SubFlow` reference field was not projected.
  The fix projects only that verified requested field, not unknown XML content.
- `20260907-145658-a641da`: the native low-level clone reset invisible main-pool
  dimensions to zero, then the next reader materialized default dimensions.
  The adapter now preserves source dimensions in the cloned native object before
  persistence. No geometry-comparison exception was added to conceal the change.
- `20260907-150347-ca0c47`: an initial behavior assertion incorrectly expected
  linked-service execution and used the wrong end-event metric key. Actual native
  output and [official simulation semantics](https://help.bizagi.com/platform/en/simulation_in_bizagi.htm)
  establish a reusable-subprocess black box. The replacement test asserts actual
  configured duration/count and explicitly rejects claims that internal linked
  tasks executed. The failed run is not counted as acceptance.

These circuits do not accredit external-model references, expanded reusable
rendering, every simulation semantic, independent GUI equivalence or complete
Modeler automation. See the [local call contract](native-calls.md). Raw evidence
and proprietary integration research remain private.

## Native adoption and interrupted-write reconciliation — 2026-09-07

That source checkpoint exposed **29 MCP tools** and passed **427 unit tests**. A full
non-incremental Release build completed with no warnings. The new
`native_commit` and `native_commit_reconcile` tools have real MCP acceptance
against installed Modeler **4.3.0.008**, using an existing rich attribute/file/image
corpus and private state outside the workspace.

Run `20260907-143049-af0801` completed the expanded adoption circuit:
**21 operations** (14 completed, five expected failures, one cancelled and one
interrupted), with 22 owned-worker observations / 106 periodic samples.
Transcript SHA-256:
`ab410d6b851df15412e7d0e5e2bfcc3018fddf532ec73968c4177acc75f6f53a`.

The independent client verified:

- Native source inspection, a native name edit and byte-exact workspace adoption.
- Unicode/spaced paths, create-if-absent, revision-guarded replacement and an
  original backup whose full byte hash matches the prior native revision.
- Separate native readers before publication and from the actual destination.
- Stale source and destination, absent expected target, omitted replacement
  revision, locked target, corrupt source, wrong extension and path escape.
- A real source-file change during native snapshot validation: the commit failed
  before publication, retained its staged bytes and reconciled `not_applied`.
- Cancellation after publication while a native destination reader was active:
  the operation remained cancelled, but reconciliation verified the applied file.
- Actual host death after publication and before native-readback completion:
  Job Object child cleanup, interrupted status after restart, and two fresh
  reconciliations with unchanged destination/backup bytes and timestamps.
- Later external restoration of old bytes (`ambiguous`), divergent external
  bytes (`conflict`) and temporarily removed intent evidence (`missing_intent`).
  No case silently replayed or rolled back a file. Source content stayed intact.

An earlier expanded attempt (`20260907-142758-3d038c`) failed in the harness:
post-publication cancellation happened before the worker entered the native
engine, while its verifier required native settings-isolation evidence. The
successful circuit waits for actual native entry before cancelling that reader;
it does not waive the isolation or process-exit checks. The failed run remains
retained privately and is not counted as a pass.

These are process-death and observed native-readback results, not a power-loss,
network-storage, every persistence-boundary or desktop visual certification.
See the [write/reconciliation contract](native-commit.md). Full Modeler automation
remains open; the existing published 0.4 archive does not contain these new tools.

The expanded source regression also passed:
`20260907-143355-34219c`, 17 operations (13 completed, two expected failures, one
cancelled and one interrupted), 21 owned-worker observations / 130 periodic
samples, transcript
`a4c2e38fb30d76f67f8515f64d389b61ae537e9a634028d346825efda615df9f`.
It exercised the real XML protocol, nested/multi-diagram native roundtrip and
edits, no-op fidelity, simulation, rendering, settings conflict/recovery and
host-state ownership/Job Object cleanup. No complete Modeler GUI equivalence is
inferred from those results.

### Extracted native-commit candidate

A clean framework-dependent ZIP from source
`50f956671d0bb18372848336b2a90889c65793f8` was extracted without modification.
Its **317 immutable manifest entries / 318 total files** matched before and after
all three actual MCP circuits. Archive size: **6,577,293 bytes**. SHA-256:
`8e31732d1261071848dc84d62c3b70f7573a2e4852c4bfee517e06c364c789f9`.

All runs used private state outside the workspace:

| Circuit | Run | Terminal operations | Worker observations / samples |
| --- | --- | --- | --- |
| Rich native adoption, cancellation and host-death reconciliation | `20260907-143919-d9484c` | 14 completed / 5 expected failures / 1 cancelled / 1 interrupted | 22 / 117 |
| Native constructor → new model → edit → workspace adoption and recovery | `20260907-144218-7bd2d7` | 15 completed / 5 expected failures / 1 cancelled / 1 interrupted | 25 / 123 |
| Expanded native/XML regression, rendering, simulation and host recovery | `20260907-144441-a4f50f` | 13 completed / 2 expected failures / 1 cancelled / 1 interrupted | 21 / 121 |

Transcript hashes, in the same order:

- `86de933983d99f58cb1b481bc1f8fa2450366bc60d5a72589720ba00682c1225`
- `901ebb1acdfcc04bd165e8ae5326811abbb3696502ee07c67a5a7de777e4c0e0`
- `e9f5da0aff852311d162d2fa87ec9be5598518c363a71ce80df6824fde88e753`

No owned MCP worker remained after the circuits. Desktop observations are
periodic read-only samples, not continuous proof or independent Modeler visual
accreditation. This is a verified source candidate, **not** a replacement of the
older published 0.4 release or completion of the full-automation objective.

## Native blank-model lifecycle — 2026-09-07

That source checkpoint exposed **27 MCP tools** and passed **415 unit tests**. The new
`native_model_create` circuit uses the installed **4.3.0.008** native model
constructor and domain defaults, with no BPMN input or import substitution.

Run `20260907-135214-c0e048` passed nine real native operations: eight completed
and one expected failure, with 15 owned-worker observations / 338 periodic
samples. Transcript SHA-256:
`37d1091090f73555202c189468b31bf4eeda033bb92f1740b294ea68757b885a`.

The MCP client created single- and two-diagram native files, checked exact
fresh-worker identities/preferences and whole-container no-op stability, inserted
start/task/end nodes and connecting flows into a blank process, rendered the
result and ran the installed simulator. The explicitly identified user task
completed **1,000** instances. Rejected deletion of an opened diagram preserved
the output; subsequent independent reads confirmed both edited and original
revisions. Finally, explicit removal of all added nodes/flows restored a file
equivalent to the original blank model under the native whole-container policy,
without an expected-change waiver. Duplicate initial names were also rejected at
the real MCP tool boundary before starting a native operation.

Earlier failed attempts exposed concrete boundary mistakes, which were corrected
instead of weakening the acceptance result:

- The native model GUID is regenerated for scratch storage. Root-level
  `NativeElement.ParentId` is now empty, rather than presenting that transient
  GUID as a durable owner.
- Native first insertion/last deletion can materialize/omit structural XML
  collections. The comparison projects only empty, known native wrappers touched
  by verified child lifecycle; unknown attributes, namespaces, comments, content
  and `xml:space` remain checked. Native-name lookalikes in extension paths cannot
  impersonate the actual mutation target.
- An empty native main participant has an invisible boundary and no required SVG
  shape. Render checks exclude only that shell, retain all graphical children,
  and record the required/excluded identities. `NativeElement.IsMainParticipant`
  exposes this distinction without localized-name heuristics.
- Native simulation also emits generated black-box task rows. Acceptance now
  selects the requested task by exact `NativeElement.BpmnId`; raw generated
  metrics remain in the report rather than being removed or mistaken for it.

No visible or foreground owned worker window was observed in the periodic
samples. This does not accredit independent desktop visual compatibility or
live unsaved sessions. Package and broader regression evidence are separate;
this source circuit alone does not complete full Modeler automation. See the
[native model contract](native-models.md).

The corrected source also passed the configured clone and expanded recovery
regressions through the actual MCP client:

| Circuit | Run | Operations | Workers / samples | Transcript SHA-256 |
| --- | --- | --- | --- | --- |
| Native diagram cloning with resource/RACI/calendar behavior | `20260907-135534-bdb521` | 12 completed, 3 expected failures | 22 / 151 | `fc02d7324512af6d27b5e19a3347ee8d61d26cdebfc4ffad64963bf14c4d1ce1` |
| Expanded native, simulation, rendering and host/worker recovery | `20260907-135753-e78f68` | 13 completed, 2 expected failures, 1 cancelled, 1 interrupted | 21 / 223 | `2cd5bb345870d4a60d734cb04a58623d3de2eea9916637256ce4ba258559270f` |

The configured cloned scenarios retained their real quantitative timing, cost,
resource-contention and calendar assertions. The full regression also exercised
nested/multi-diagram fidelity, corrupt input, stale revisions, settings contention,
active cancellation, host termination, owned Job Object cleanup and journal/state
lease recovery. The new-blank-model final comparison checked 20 native archive
entries and 435 atoms and reported preservation of the original blank semantics.

### Extracted native-model candidate

A clean candidate from `9d1d5536ef00dfedb113c53c592488c702d17738` passed both
actual packaged circuits below, with private state outside the workspace:

| Circuit | Run | Operations | Workers / samples | Transcript SHA-256 |
| --- | --- | --- | --- | --- |
| Native blank-model creation, edit, render, simulation and clearing | `20260907-140606-0f5fbb` | 8 completed, 1 expected failure | 15 / 117 | `bd29c7153d8719dd914ece930210c51e701cfa50fe0a32dd02308ce68530a612` |
| Expanded native and recovery regression | `20260907-140753-485377` | 13 completed, 2 expected failures, 1 cancelled, 1 interrupted | 21 / 120 | `8dbcf4c926519564f2305d6297c2ec7b7b17048c3b8e2282586477a28af3f5c2` |

ZIP size: 6,559,148 bytes. SHA-256:
`5a1776bd75896cd2a5397e22c36ff333828a40ec1a5e4eb55ecd9fc449fef8a4`.
All 315 immutable manifest entries (316 total extracted files) matched before
and after acceptance. The extracted package was not patched. Code CI for that
commit also passed. This is an independently verified source candidate, not a new
published release or a claim that the older 0.4 asset contains these tools.

## Native configuration observer correction — 2026-09-07

The clean `025df56` diagram candidate failed its rich packaged lifecycle at native
graphical configuration initialization (run `20260907-124745-8ac31f`, operation
`c0f7c82329ab4f64aac818f9a332a96f`). The candidate is **not accredited**: earlier
completed editing operations do not make the failed full circuit pass.

Inspection found that the observer used `File.ReadAllText` while the native
initializer rewrote the same MCP-owned defaults. The observer now waits for
in-memory readiness and uses bounded read-only snapshots with writer/delete
sharing. It still requires a fully parsed document and the native completion
marker; partial content, existence or a live worker does not imply readiness.
No personal Modeler settings are reset, and revision-protected model reads retain
their existing stricter sharing. The original fire-and-forget task exception was
not captured, so this is a corrected concrete observer defect, not proof of the
sole cause of every initialization failure. New bounded first-chance diagnostics
retain I/O failures only within the verified MCP-owned settings namespace.

The corrected source passed **365 unit tests** and three independent real MCP
renderer startups against the exact previously failing cloned diagram in run
`20260907-131122-29a3fb`. Every attempt must pass; the acceptance harness does not
skip failed attempts. That run also exercised actual settings-lock contention,
verified the original scoped I/O diagnostic, released the lock and successfully
started a new native worker. Source transcript SHA-256:
`4b470b09660fced6afacde143b953a3fecb43a88b6354e20856941a267c42955`.

The clean corrected source candidate `e2df9b097f3c430f71454e24fa8837ee8b7ea083`
was then packaged, extracted and accepted without editing its contents. ZIP:
6,547,612 bytes; SHA-256
`912990af6d1adba80d1c8087216d41bbcb9552ebb9bcccb82c9c06f05a0cd58e`.
All 313 immutable manifest entries (314 files including the manifest) matched
before and after the following actual packaged circuits:

| Circuit | Run | Operations | Workers / samples | Transcript SHA-256 |
| --- | --- | --- | --- | --- |
| Initially absent MCP graphical defaults; three exact-clone renders, settings contention and recovery | `20260907-131607-6be22d` | 5 completed, 1 expected failure | 6 / 1019 | `ac70275773625c448c1869787762c4491771fb434009f1e9ee44f3abcfaa356f` |
| Rich native diagram lifecycle | `20260907-132109-e65c5e` | 10 completed, 3 expected failures | 20 / 151 | `45167c927374f926f38887f32c1f00d75493e6e9814260f79bbd716dbbb289f2` |
| Expanded native, simulation, rendering and host/worker recovery | `20260907-132343-a4f310` | 13 completed, 2 expected failures, 1 cancelled, 1 interrupted | 21 / 150 | `6dbe5ebbef7a7b1056d55a647ea1b151cadb6e3b1829eeb5a8fb265b71749e93` |

The cold-default test temporarily backed up only the exact MCP worker defaults
and restored their original bytes afterward; generated defaults were retained
separately. No personal Modeler profile was copied or changed. No visible or
foreground owned window was seen in periodic samples, which are not continuous
proof or independent Modeler visual accreditation. This is a private verified
source candidate, not a replacement for the existing published 0.4 release asset.

The focused acceptance supports `--render-only --input <native.bpm>` with optional
`--diagram-id <native-guid>` and `--render-repetitions <1..20>`. The explicit ID is
checked against a fresh native inspection, not inferred from ZIP enumeration.

## Unreleased native diagram lifecycle — 2026-09-07

That checkpoint exposed **26 MCP tools** and passed **360 unit tests**. The actual
stdio MCP lifecycle passed with Modeler **4.3.0.008**, native persistence and
independent fresh-worker readback. This is source after 0.4.0-alpha.1, not a claim
about that older release asset or full Modeler automation.

| Circuit | Run | Operations | Worker observations / samples | Transcript SHA-256 |
| --- | --- | --- | --- | --- |
| Nested/message-flow model | `20260907-122919-0edf61` | 14: 11 completed, 3 expected failures | 22 / 441 | `1bba008f5aa9e754b8d2ac2b07af2574fa99018e0ea41d5cc045d2e7da49288e` |
| Existing rich native model | `20260907-123401-abc39a` | 13: 10 completed, 3 expected failures | 20 / 132 | `2bcfb20643400e4d0a516b93b9c45baeaf5f00c933040063d8927fd0405282f5` |

Both runs created a diagram, changed ordered/selected native tab preferences,
renamed it, cloned an existing diagram using native-generated ID maps, retabbed,
saved without edits, rendered the clone and deleted only the requested diagrams.
Subprocess tabs were exercised on the nested model. Duplicate names, deleting an
opened diagram without replacement preferences and deleting the final diagram
failed for the expected reasons; a subsequent fresh read confirmed recovery and
the retained output revision.

The rich input carried 11 extended-attribute definitions, table/scalar values
and an embedded image. Clone comparison mapped known native owners/references
and checked every copied leaf and attachment byte, while the original diagram
and all unrelated entries remained under the existing whole-container gate.

Actual native serializer/cloner behavior drove the implementation: initial empty
collections are materialized before the first save; package-header description
is a derived diagram-name field, not documentation; explicit empty element-value
containers omitted by the native cloner are retained; scenario objects are not
shared with the source; message-flow references are checked at their exact native
locations. Unknown XML changes are still rejected.

No visible or foreground owned worker window was observed in the periodic
samples. This is not continuous proof or independent desktop visual accreditation.
See the [diagram contract](native-diagrams.md) for scope and remaining families.

The expanded source regression also passed: run `20260907-123755-4e0424`,
17 operations (13 completed, two failed, one cancelled and one interrupted as
expected), 21 owned-worker observations / 128 periodic samples. Transcript
SHA-256: `a0b2bd6438e5e7a8ffb4d4b2f70ae986f77d4d1897c9ee19036c7c4c8729857e`.
This included actual simulation results, nesting, rendering, settings contention,
active cancellation, host termination, Job Object cleanup and journal recovery.

The configured simulation/RACI corpus also passed after native cloning in run
`20260907-124216-99853e`: 15 operations, 12 completed and three expected failures,
22 worker observations / 128 samples, transcript
`39b2d5ba3846dae996d59dc540eea94418299751ab1be84ab18e5b46c6584fcb`.
The copied resource/calendar scenarios completed 12 task instances each, with
three-minute average processing time, total task cost 84, one resource used at
a time and actual contention. The shift calendar delayed work by at least 450
minutes on average. These are real cloned-engine results, not just preserved
configuration strings. Saved-result cloning and other distributions remain open.

## Unreleased native container lifecycle — 2026-09-07

That checkpoint exposed **24 MCP tools** and passed **327 unit tests**. The
container circuit uses the official MCP client, stdio host, isolated worker,
installed Modeler **4.3.0.008**, durable `.bpm` output and fresh-reader workers.
No mocks or direct adapter calls accredit these operations.

Run `20260907-114749-5fc8a6` passed **12 operations**: 10 completed and two
expected failures, with **19 owned-worker observations / 103 periodic samples**.
Transcript SHA-256:
`304c10ef0decd06039e921a3c7e2b7eae54c28db627c2b0a8c043497afc826be`.

- Created an explicit participant/process pair, two stable process-owned lanes,
  two milestones and two embedded subprocess levels with two tasks and a flow.
- Independently reloaded IDs and containment; runtime lane sets were not exposed
  as durable identities.
- Rejected deletion of a populated pool and an inconsistent lane partition;
  subsequent valid operations succeeded.
- Updated names, documentation and complete lane/milestone partitions; expanded
  both subprocess levels with separate collapsed and expanded dimensions.
- Ran the installed offscreen renderer with explicit top-level process and local
  subprocess coordinates, not automatic layout.
- Saved without edits, cleared documentation, collapsed both levels while
  retaining expanded sizes, then deleted connections and children before their
  containers. Original durable identities remained.

The lifecycle also passed on an existing native model with **11 extended
attribute definitions, table/scalar values and an embedded image**, checking all
non-targeted archive leaves through every mutation. Run
`20260907-113824-3244c6`: 11 operations, 17 worker observations / 107 samples;
transcript `f5629f3a0dc3630a38c2be5b9523a1a35be7fa29177d924fc51f23a1e1bbe92a`.
That earlier rich run predates the final fixture-coordinate refinement; it
accredits native preservation, not graphical-layout quality.

Related real regressions during this implementation:

| Circuit | Run | Operations | Worker observations / samples | Transcript SHA-256 |
| --- | --- | --- | --- | --- |
| Attributes and attachments | `20260907-114034-0a8fb2` | 18: 15 completed, 3 expected failures | 29 / 157 | `437a1dcea603cd5de17283c3c99b5e1691a6bd903b0cbd0316541cb0495e6c86` |
| Resources, RACI, configured simulation and what-if | `20260907-114313-36ae75` | 16: 14 completed, 2 expected failures | 25 / 126 | `799798f40082d8094b98d857ddc142a7e30753e240e38ae4654c0c9ca37019f4` |
| XML/native, nesting, rendering and recovery | `20260907-114528-3ae4bf` | 17: 13 completed, 2 failed, 1 cancelled, 1 interrupted as expected | 21 / 102 | `e80e0ce7d393163735549e49c4145a5e16331bd2029796a31caa626e862dbd4f` |

Recovery includes real settings contention, host termination, Job Object cleanup
and journal/state restart. No visible or foreground owned worker window was
observed; periodic sampling is not continuous proof. The structural corpus does
not claim an executable process or independent visual compatibility inside Modeler.

Real serializer behavior is handled explicitly: an omitted `BlockActivity.View`
means `COLLAPSED`; clearing pool documentation uses the native absent value to
avoid a transient empty runtime key on the next save. Unknown XML/JSON changes
are not suppressed. The final name policy also rejects unknown same-ID payloads
that impersonate native name-bearing structures.

These source changes are not in the published 0.4.0-alpha.1 archive. See the
[container contract](native-containers.md) for open diagram, move, call-activity,
layout and visual families. **Full automation remains open.**

### Extracted container snapshot

A clean snapshot of commit `ea1ff5573fa031c4f0cec46ef79486223f13a75c` was built,
ZIP-extracted and executed independently of development output. Its inherited
assembly label is still 0.4.0-alpha.1; it is **not** that published release asset.

- ZIP: **6,523,028 bytes**; SHA-256 `f2db6f1c66036134ca9e3886b003e3cf387badfd37c7304107115cfc9321c562`.
- **312 manifest entries** verified before and after both circuits, with no extra package files.
- Rich container lifecycle: `20260907-115445-28a8f9`, 11 operations (9 completed, 2 expected failures), 17 worker observations / 112 samples.
- Rich transcript: `a8190c35405b0a6dea0b7c99ac6cf4b5e98b78ea71a59a0bf1cd1d625709976b`.
- Packaged general native/recovery circuit: `20260907-115631-e77547`, 17 operations (13 completed, 2 failed, 1 cancelled, 1 interrupted as expected), 21 worker observations / 111 samples.
- General transcript: `bf33f733d54e640765e89cad4a326278c03622b1e07ecb2bb2f7db173ab95e9c`.

This rich package run includes the final explicit coordinates, documentation
clearing, expanded/collapsed sizes, native attributes and embedded-image
preservation. [Public CI for the code commit](https://github.com/h0w4r/MCP-Bizagi/actions/runs/34118920328)
also passed locked restore, build, **327 unit tests** and the actual MCP XML
circuit. Native accreditation remains the separate controlled Windows evidence,
not the public CI badge. The full-automation goal is still active.

## Current source — extended attributes and attachments (unreleased)

Real MCP acceptance on **2026-09-07 UTC**, using installed **Modeler 4.3.0.008**.
The source now exposes **24 MCP tools** and passes **298 unit tests**. The native
comparator identifies its expanded policy as
`native-v5-content-with-explicit-engine-metadata-v2`.

The actual `--native --attributes-only` circuit imported our own BPMN, created
definitions and values for all 12 native attribute kinds, persisted a two-row
Text/Number table, embedded an actual installed-renderer PNG and an opaque
XML-named file, and reloaded every mutation in a separate worker. It also verified:

- Definition rename and guarded deletion; values explicitly cleared first.
- Native-loaded attachment export, checked against the source archive and independently reread output bytes.
- Embedded byte replacement without changing its reference.
- Explicit file removal while preserving the separate image attachment.
- Rich no-op save and an unrelated task-name edit, preserving all other native archive content.
- Three expected real failures: missing export file, unknown native definition field and deletion of a referenced definition; subsequent valid operations succeeded.

Run identifier: `20260907-105816-6c24d6`.
Transcript SHA-256: `76458d0f7b9dfbde8a2e2394d7c65d229f28b8f45cbcb24afb1d799684a42763`.
The circuit observed **18 operations** (15 completed and 3 expected failures),
**29 owned-worker observations** and **164 periodic samples**, without observing
a visible or foreground owned worker window. Sampling is not continuous proof.

The wider native regression also passed on current source: XML MCP, native
roundtrip, nested/multi-diagram names, rich guards, native validation, default
1,000-instance simulation, rendering, corrupt/native failures, active cancellation,
settings contention, host death, Job Object cleanup and journal/state recovery.
Run `20260907-110104-b6aaa8`, transcript SHA-256
`933875f5498e705a5b47d1bac4fe6127480b12274406e83cb5422ab94021ec24`:
**21 worker observations / 113 samples**. Deliberate cancellation, failure and
interruption are expected recovery scenarios, not successful modeling operations.

This verifies the documented **source circuit**, not inclusion in the old 0.4
release archive, every desktop editor constraint, rich publication behavior or
independent Modeler visual compatibility. [Contract and open corpus](native-attributes.md).

### Extracted source-snapshot package

The same attribute/attachment circuit also passed from an independently built,
ZIP-extracted **unreleased snapshot** of commit
`f10683c699251971480ddf8e2b2ff7325dfc0718`, with a clean checkout recorded in
`package.json`. This private validation candidate is **not** the published
0.4.0-alpha.1 release asset; identify it by commit and hash, not the inherited
assembly version label.

- ZIP bytes: **6,509,623**; SHA-256: `8fd09e28831e7f17dbaa4842b3fe146676bec2b8a5322fc75c5c27a2c0d34296`.
- **311 manifest entries**, verified before and after actual execution; no additional package files appeared.
- Run: `20260907-110637-ccaa1f`; **18 operations**, including the three expected native failure cases.
- **29 worker observations / 162 periodic samples**, with no visible or foreground owned worker window observed.
- Transcript SHA-256: `24c74f537a31423c79f253bddc10a18c6f169da706ace4c77ccaabef3c2ef15c`.

[Public CI for that code commit](https://github.com/h0w4r/MCP-Bizagi/actions/runs/34114729926)
also passed the locked restore, build, 298 unit tests and actual MCP XML circuit.
The native engine is not installed in public CI; its evidence remains the
separate controlled Windows runs described above. Full automation remains open.

## Released baseline — 0.4.0-alpha.1

Real source acceptance on **2026-09-07 UTC**, against installed **Modeler 4.3.0.008**.
That release exposes **21 MCP tools**. Its unit suite passed **255 tests**;
unit results remain separate from actual native acceptance.

## Native metadata and configured simulation

The `--native --metadata-only` circuit used the actual official MCP stdio client,
host, isolated worker, native APIs, durable `.bpm` output and independent reader
workers. Source transcript SHA-256:

`e1b3f6dd9a6b69dbcf07a394eacbb9f7f75bf463fed742e313b1c65b14022497`.

| Capability | Verified result |
| --- | --- |
| Resource catalog | Role and Entity creation; name/description/type update; deletion; native BPMN identity readback |
| Activity RACI | Responsible, Accountable, Consulted and Informed assignment, then explicit clearing |
| BPSim configuration | Complete native per-diagram replacement, all settings verified after restart; other archive leaves compared |
| Timing | 12 completed instances; 3-minute and 9-minute processing times; totals 36 and 108 minutes |
| What-if | Two selected scenarios, two actual replications each; separately captured results and identities |
| Resource analysis | Actual contention: average wait 2.5 minutes; task completion cost 84 |
| Calendar analysis | 08:00 working shift produced average resource wait of 482 minutes; 12 instances completed |
| Structured results | MCP metric dictionaries checked against native output XML |
| Rejected writes | Referenced-resource deletion and unknown simulation-element reference failed as expected; subsequent valid operations succeeded |
| Native preservation | No-op save of the resource/scenario/calendar model passed the whole-container gate |

Selected operation identities:

- What-if: `2fed7781c69a468bb384c87c2a53d70d`.
- Resource analysis: `a9b607cefbae4741af36f54ba58dabe6`.
- Calendar analysis: `9fc2e489697548c391ecd2f6ce3a7469`.
- Configured-model no-op save: `cb90ad1d94c84c619ff9d3bce60b7670`.

The run retained **25** owned-worker desktop observations with **156** samples;
none observed a visible owned window or foreground takeover. This is sampled
evidence, not continuous proof. Native process exits and separate settings
namespaces were verified. Logs, archives and generated simulation files remain
private because they can contain operator information.

The real integration exposed native resource catalog replication into diagram
packages, serialization of empty BPSim property arrays, and a UTF-8 BOM in saved
result containers. These were handled explicitly; no entire diagram or unknown
XML subtree was excluded from comparison to make acceptance pass.

See [native simulation boundaries](native-simulation.md). Full Modeler automation
is **not complete**: broad container/layout editing, extended attributes and
attachments, additional publication formats, live unsaved sessions, and independent
Modeler visual compatibility remain open. The sections below retain prior-version
evidence and do not silently promote those old results to new-version acceptance.

## Extracted 0.4.0-alpha.1 package acceptance

The final ZIP was built from clean source commit
`ec07576b338c4cbb58b10b779ac9fe356f6a233c`. It contains **310** verified manifest
entries and **60** collected runtime dependency notices. Size: **6,482,636 bytes**.

ZIP SHA-256:
`7e7e67b5ff3aaa0d47822022701b06518b4d8a8b641fe4e84bdc0e9ddf3db5f5`.

Both circuits below ran the extracted host and worker, not development binaries:

| Circuit | Workers / desktop samples | Transcript SHA-256 |
| --- | --- | --- |
| Resources, RACI, scenarios, levels 2–4, four replications, failures/recovery and rich no-op save | 25 / 131 | `9a05f5e4d4b20f8354136c644bcf59f91ca265cf6953e8be756f557943e202f8` |
| XML/native regression, nested/multi-diagram names, native validation, 1,000-instance simulation, offscreen rendering, cancellation, host death and journal recovery | 21 / 135 | `f04cf2642e6e5d35563e0d7094c735778ac3aa05ff8b531457b3e01a752d86b6` |

No visible owned window or foreground takeover was observed in these **266**
samples. The extracted distribution's manifest remained unchanged after both
circuits. The source [CI run](https://github.com/h0w4r/MCP-Bizagi/actions/runs/34110147756)
also passed build, 255 unit tests and the real non-native MCP XML circuit.

These checks accredit the described paths on the installed engine. They do not
close the full-automation or broad rich-content/visual gates listed above.

## Prior baseline — 0.3.0-alpha.1

Real local acceptance on **2026-09-07 UTC**, against installed **Modeler 4.3.0.008**.
The source contains 18 MCP tools; the checks below invoked the actual tools and
native worker, not implementation methods or mock engine results.

## New native circuits

| Circuit | Observed result |
| --- | --- |
| Unit suite | 150 passed, zero failures/skips; separate from native acceptance |
| Native structural batch | UserTask and SequenceFlow creation, existing flow reconnection, bounds/colors/descriptions, independent readback and whole-container proof |
| Native deletion | Inserted connection/node removed in a subsequent revision; original archive unchanged |
| Native palette | 22 task/event/gateway types created, persisted, reopened and deleted; every requested native type/parent verified |
| Basic native publication | Excel, Word and PDF; documented Unicode tasks; separate native parser worker; source unchanged |
| Multi-diagram publication | Two diagrams and two nested subprocess levels; six Excel sheets, seven-page Word/PDF documents |
| Native document images | Four generated surfaces in the multi-diagram document: two diagrams and two embedded subprocesses |
| PDF image fidelity | Large surfaces downsampled by the native persister; strict mode rejected them; explicit acceptance recorded dimensions and resampling warnings |

The basic four-page native PDF was independently rasterized and visually reviewed.
That review is not a claim about all layouts, all corpus files or visual matching
inside the interactive Modeler application. Generated files can contain operator
identity metadata and remain private.

The first seven-page nested PDF exposed two real integration defects: an
asynchronous child render was captured too early, and the native BPMN importer
tripled dimensions even for already expanded DI shapes. Both failures were kept
as evidence rather than accepted as visual success.

The corrected route composes native render surfaces bottom-up and waits for each
surface to finish. New imports preserve expanded DI bounds, record the correction
in `EngineReply.IntegrationAdjustments`, and verify `NativeElement.ExpandedGeometry`
after native persistence and fresh-worker loading. Existing native archives are
never silently resized. The dedicated nested acceptance checks all graphical IDs
at both levels and the actual SVG rectangle dimensions, then publishes PDF through
MCP. Independent rasterization of that six-page PDF confirmed that the oversized
subprocess overlap was removed. The installed template still leaves a diagram
heading on a separate sparse page; document pagination and independent in-Modeler
visual equivalence remain open. This is not a blanket visual-layout accreditation.

## Traceable source evidence

| Circuit | Transcript SHA-256 |
| --- | --- |
| Structural batch and deletion | `373d91544c298fd7f4e5d3919a75255efb4672ea62b831fcdd5088ce25703ba5` |
| 22-type palette | `8d6cb28ee8052a4a65c3b67e0247278381d4e657f848d25a235b91b995141fc0` |
| Basic publication with image dimensions | `60976acabee2b710e1a67599e17d01bc08792345a9b1f3205f97085f7d47a092` |
| Multi-diagram/nested publication | `38f8fe5192fd91991d4673400934d89fed593915413741a11c3305012d15d152` |
| Structural real failure, stale revision and recovery | `fe1e97e0702ae0b13a034aba383fffc6cf427a65b6f4f87cb70da52a4d0558c8` |
| Corrected expanded import, all-level render and PDF | `2daa0cf83b833376553fc5a15383ab5c7f92889c89d8879c4c9c6916d41a88a8` |

Structural creation/edit operation: `4923ad1023e249eda86432ee84eacfdb`;
subsequent deletion: `dc7f8b6f966b449da89266a89b114390`.
Palette creation/deletion: `5ad61458275f4cb0859ce56404f2d620` and
`8127b572a489449faef26d40edb82084`.
Multi-diagram publication operations: `81e390a1df0f4c968423b877377ab306`,
`84abf0cc0d594537b4dd807eec0f039c`, `8876781f7e084a41a4863305c560ef98`.
Corrected expanded import: `95bedf4241904bb6a0cc7620ce224b17`;
render: `07a3a2f9f5004bfd89b52b0155f8e68d`;
PDF: `39bdb1b8321742d199a5c7e1b7360be3`.

Each successful native writer was followed by a separate reader. Operation
directories retain native module hashes, phases, process exit records, sampled
desktop independence, requests and readback. Native diagrams and proprietary
research files are not included in public evidence.

## Final source regression

After the expanded rendering corrections, the full source acceptance was rerun:
MCP XML, native roundtrip, batch names, no-op fidelity, multi-diagram/nested edits,
validation, 1,000-instance simulation, offscreen rendering, real engine failures,
active cancellation/recovery, cross-state native settings contention, host death,
Job Object cleanup, journal restart and exclusive state ownership all passed.

Transcript SHA-256: `6dff868d7dfb57315089d07cb7b8dfe62c05f5a242d629cdc979a30f569959ba`.

The 150 unit tests also passed after final formatting and rebuilding, with zero
warnings/errors. Unit results remain separate from this native MCP circuit.
## Extracted release package acceptance

The **0.3.0-alpha.1** ZIP was built from clean source commit
`80ef6ecc0c89529aa6b0337481f041df190045d9`. All **309** manifest entries matched
before and after acceptance; **60** runtime dependencies have collected notices.
The ZIP contains no Bizagi assemblies, native operator files or private evidence.

ZIP SHA-256: `c8406f1d77bebfbb827cc21ddc99d3092ff3b03a4eec65d8a9528a4e528c7b11`.
Size: **6,450,312 bytes**.

All six circuits below ran the **extracted distribution**, not development host
or worker binaries. The client used the official MCP SDK and actual stdio tools.

| Packaged circuit | Transcript SHA-256 |
| --- | --- |
| Full native regression and recovery | `eeb677ea891e0f84ff8712d3034fb7a4b584a477b335fc41f0f6f0704468d5cb` |
| Structural edits, real failure and recovery | `5307b2cde38b2457b5c915d4d0b0dce4d7d766e0ce7b3cda99f4ede29eb92c67` |
| 22-type creation/deletion | `6b93e9c4aaba3f4ea6005b6d546e94c1e8793948d57f013721104055146451f3` |
| Basic Excel/Word/PDF | `683ffaba234ef87572f66dc769a04d3460071e22724f48b3a66dd146edd6736f` |
| Expanded import, all-level SVG and PDF | `13e00b37c39797c90a7fdb80991a3262eafae0eb2cfcb0018f764ce0a0d43375` |
| Multi-diagram Excel/Word/PDF with explicit PDF resampling | `fe52a4b880e4f2f8b8504f3b1ae651e3fabd583974cbc4822fefd37a57549b7f` |

There were **49** owned-worker desktop-observation records and **979** samples;
none observed a visible owned window or owned foreground process. These are
sampled observations, not continuous desktop traces. The acceptance client also
verified owned-worker exit, source preservation and fresh-reader results in the
applicable circuits. Expected failures remained failures and were followed by
real recovery operations rather than simulated success.

[Public CI for the source commit](https://github.com/h0w4r/MCP-Bizagi/actions/runs/34102958821)
passed restore, build, all 150 unit tests and the real MCP XML circuit. Public CI
does not contain Bizagi and is separate from the local native package evidence.

This record was added after building the immutable package. Its source commit
and packaged documentation remain exactly those identified above.
## Full automation remains open

- Native container creation/reparenting/deletion, the remaining BPMN categories,
  full style/layout semantics and independent Modeler visual compatibility.
- Native extended-attribute, attachment and resource editing, plus broad
  unknown-content preservation on a rich real-file corpus.
- Configured simulation levels 2–4, scenarios, calendars, resources and what-if.
- Additional publication/interchange formats, custom templates, selection/filter
  options and rich document attachment workflows.
- In-flight persistence faults, all commit/recovery boundaries, independent
  missing-dependency/new-version installations, offline rendering and live unsaved sessions.

These are missing capabilities or unaccredited gates, not features that a green
build or unit suite can declare complete. This release does **not** close the
full Modeler automation objective. See the [capability ledger](capabilities.md).

---

# Historical verification baseline — 0.2.0-alpha.1

This latest section records real local execution on **2026-09-07 UTC** against
**Bizagi Modeler 4.3.0.008**. The earlier baseline below is historical, not the
current capability inventory. No native mocks, desktop clicks or simulated
success responses were used. Both input BPMN examples are original project files.

## Expanded source acceptance

| Circuit | Observed result |
| --- | --- |
| Build and core unit suite | Seven projects, zero warnings/errors; 35 tests passed, zero skips |
| Independent MCP client | 16 tools discovered, followed by real stdio calls |
| Basic XML and native workflows | Unicode readback, revision checks, native persistence and fresh-reader verification |
| Multi-diagram native model | Two diagrams, two lanes, a gateway, message flow and two nested subprocess levels |
| Nested task edit | Requested name survived a fresh worker; whole native container passed the fidelity gate |
| No-op native save | Basic model preserved under the explicit native metadata policy |
| Native comparison | Requested names accepted; unrequested names rejected through MCP |
| Native validation | Installed validator executed and returned its findings collection |
| Native simulation | Default level-one scenario: 1,000 started, 1,000 completed, zero failed instances |
| Offscreen SVG/PNG | Expected graphical IDs and persisted task text; transparent PNG corners |
| Cancellation/recovery | Active native cancellation, owned worker exit, subsequent native request |
| Abrupt host death | Job Object worker cleanup, interrupted journal, successful restarted host |
| Competing host | A second real host explicitly rejected the occupied state lease |
| State outside workspace | Native artifact references supported MCP-only inspection, edits and comparison |
| Desktop observation | 145 samples in 19 normally finalized worker runs, covering 25 observed process instances; no visible/foreground ownership observed |

The intentionally killed host cannot finish its worker's observation journal.
That worker's termination was verified independently by the client. Sampling is
read-only and not continuous proof or Session 0/Windows Service accreditation.

Expanded source transcript SHA-256:

`ee53489ada8ead59322d7799a781a94c1590cdae0d7b4f4fd8344c25fbea6ca8`

| Operation | Correlation ID |
| --- | --- |
| Native no-op save | `fd434215a94e4adfb40e30adad4163a1` |
| Two-diagram import/export | `353cbb0a05b642b7941a98d15e32c767` |
| Nested native edit | `616ee9989e59432797a4a9e09a2026d6` |
| Simulation | `188022fd3a644aa0ba70c2191a782305` |
| Offscreen SVG/PNG | `76f405408d384ce59a63b5d78a6c55a6` |
| Interrupted host-death operation | `b2a3923c28c342cb921bb86b2ad371c7` |
| Post-restart native operation | `38e4f5f6249b48fd9b400289c3e02413` |

Observed PNG SHA-256:
`b87e4fe0199eba38eb30bd5d1d380ff16dc4ec3a473bb4d641da93e1492edfbc`.
The real PNG was visually inspected; a comparison against the interactive
Modeler editor remains a separate gate. Raw archives, renderer research and
logs remain private because native files can contain Windows identity metadata.

## Renderer startup and configuration regression

Repeated extracted-package runs exposed incomplete diagrams. They were rejected,
not published as successful images. Investigation identified asynchronous native
configuration initialization before DTO creation. The adapter now waits for the
actual native configuration and reports asynchronous browser errors.

Five independent source MCP renderer startups then passed. The private regression
index (run IDs, operation IDs and transcript hashes) has SHA-256
`ef72dd939a4c889e333f89a47269a8a2eb4b1dd9caed5d922797888bb7c32d16`.

The full expanded source suite was rerun with initially absent **MCP-owned**
graphical defaults, real settings-file contention and recovery, and actual
worker settings-path assertions. It passed, including simulation, rendering,
multi-diagram fidelity and host-death recovery. The original MCP defaults were
backed up privately; the operator's Modeler profile was not copied or reset.
Cold-start transcript SHA-256:
`779dde3e7167d8b253bc37004acb7b50002215aefb4e2a6b43be053cd1e0a1ed`.

| Regression circuit | Correlation ID |
| --- | --- |
| Expected settings contention failure | `3f65f3fc23ed46638eef6dc792030c94` |
| Cold-default native SVG/PNG | `b25c0c62f6d548b4a27119b7962dcbbd` |
| Native simulation | `c53bb89119444ab1a78c3ce95e8e259b` |
| Nested multi-diagram edit | `2e62219cb5d94e29940d22977f160c61` |

This regression evidence is additional to the earlier source baseline, not a
claim that every possible diagram, offline environment or native library build
has been validated.

## Current open gates

- Native creation/deletion, reconnection, geometry, styles and layout editing.
- Broad no-op/edit preservation with real extended attributes, attachments,
  resources, documentation settings and configured simulation data.
- Native documentation publication and additional interchange formats.
- Simulation levels 2–4 with configured resources/calendars/scenarios and what-if.
- Cancellation during persistence and faults at every publication boundary.
- Missing dependencies and other/new engine builds on independently prepared installations.
- Rich/offline rendering, independent visual comparison inside Modeler, and
  live unsaved-document synchronization.

Full Modeler automation is **not closed**. See [capabilities](capabilities.md),
[native fidelity](native-fidelity.md) and [rendering](rendering.md). Native edits
remain experimental and copy-only. Green unit tests cannot close these gates.

The earlier package pass does not validate this newer source. New release notes
must record the new ZIP hash, manifest verification and expanded packaged-client
run. Public CI remains GitHub-hosted compilation/unit/MCP-XML only; it never runs
unreviewed PR code on the operator's native-engine machine.

---

# Historical verification baseline — 0.1.0-alpha.1

## Scope

This is a sanitized record of real local execution on **2026-09-07 UTC** with
Bizagi Modeler **4.3.0.008**, a Windows x64 worker, the official MCP C# client,
the official stdio server, StreamJsonRpc, and durable files. No native engine
mocks, desktop clicks, UI Automation, or simulated success responses were used.

It establishes the tested basic route, **not** complete Modeler automation or
lossless preservation of every native feature. The test input is the project's
own `examples/minimal.bpmn`, including supplied Diagram Interchange geometry.

## Observed acceptance

| Circuit | Observed result |
| --- | --- |
| Build | Seven projects; zero warnings and errors |
| Core unit tests | 22 passed, zero failures/skips |
| Independent stdio MCP client | 11 tools discovered, then actual tool calls |
| XML create, inspect, name batch, reread | Exact Unicode value persisted |
| XML stale revision and path escape | Actual tool errors, no successful overwrite |
| Native import → `.bpm` → fresh reader → BPMN | Completed through the installed engine |
| Existing native file inspection | Native IDs returned; original bytes unchanged |
| Native task + start-event name batch | Both requested names verified after another worker loaded the new `.bpm` |
| Native stale revision | Rejected before starting the edit |
| Missing native element | Real engine error journaled as `failed` |
| Corrupt native input | Rejected rather than passed off as BPMN |
| Cancellation during native registration | `cancelled`, worker exit recorded, owned PID no longer active |
| Native operation after cancellation | Fresh bootstrap completed |
| Owned-worker desktop observation | 38 samples across eight workers; no visible window or foreground ownership observed |

The desktop check is sampled and read-only. It is not a continuous trace, a
claim about Session 0/Windows Services, or proof that every internal library
operation is GUI-independent. The actual engine execution thread in this run
was MTA. Loaded-module fingerprints and x64/CLR metadata are retained locally.

## Correlation references

The full local transcript has SHA-256:

`3a2ebe90f706fe4cff6c1b1e4ba565c98cedfd870b06e334123f36a7c22d45ee`

| Operation | Correlation ID | Terminal state |
| --- | --- | --- |
| Native roundtrip | `463d8e526a5c49b2868740a7ffdd571e` | completed |
| Existing native inspection | `cfa0ee42bdd0408dab726c1a593cd4ce` | completed |
| Two-element native name batch | `16008a21b5a942c0a2582085056e2710` | completed |
| Invalid native identity | `ec6721bdf9bf4e858e9672bcf9559278` | failed, as expected |
| Active cancellation | `77c174b7b9604fafa6e1c7cd8d4b816e` | cancelled |
| Recovery probe | `225641bab0d9472a92e1f1f102ef3d82` | completed |

Raw archives and logs remain private because Bizagi can embed Windows user
metadata in generated native files. The correlation IDs and transcript hash
make this record traceable to retained operator evidence; they are not a
substitute for rerunning the acceptance client on a different installation.

## Fidelity findings, not hidden differences

The basic native interchange regenerated identifiers and normalized some names
and structural metadata. `BpmnFidelity.Compare` reports identifier differences,
selected element-count/name changes, and limitations of the comparison.
It does not claim full graph, geometry, attribute, or attachment equivalence.

Bizagi explicitly documents that BPMN export excludes extended attributes:
[official BPMN export documentation](https://help.bizagi.com/platform/en/exporting_to_bpmn.htm).
The native `.bpm` artifact is retained separately; exported XML is a projection,
not an interchangeable replacement for the native model.

## Gates still open

- Multi-diagram and deeply nested native preservation, with pools, lanes,
  gateways, messages, and every supported BPMN element category.
- No-op native save equivalence for extended attributes, attachments, resources,
  documentation, and simulation configurations.
- Cancellation specifically during persistence, interruption at each publication
  boundary, abrupt host termination, and journal recovery across a restarted host.
- Missing dependency and unaccredited-version circuits on independently prepared
  real installations; version rejection in code alone is not operational proof.
- Independent visual compatibility inside Modeler.
- Rich semantic edits beyond names, documentation publishing, and simulation.

Native editing remains opt-in and artifact-first. Explicit workspace adoption
uses the separate [native commit and recovery contract](native-commit.md), not
an implicit overwrite or XML conversion fallback. Broader preservation and
desktop equivalence are still separate gates.

## Packaged-server acceptance

The same complete acceptance client was also executed against a freshly
extracted framework-dependent ZIP, not the development output directory.
All 301 manifest entries matched their SHA-256 values. XML calls, native
roundtrip, existing native inspection, two-element native edits, real failure
reporting, active cancellation, and subsequent recovery passed in that run.

This package validation checks executable behavior and dependency inclusion.
It does not close any rich-preservation or visual gate listed above. The
packaging script records the source commit and whether its checkout was dirty;
preview packages must not be described as reproducible release builds solely
because the extraction test passed.

## Repeat the tests

Use the commands in [README](../README.md#verification) and
[packaging](packaging.md#verify-the-packaged-server). Public CI runs the unit
suite and actual MCP XML circuit only; it must not imply native accreditation.
