# Verification baselines

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

Native editing remains opt-in and copy-only until these preservation gates are
accredited. This restriction is explicit, not an XML conversion fallback.

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
