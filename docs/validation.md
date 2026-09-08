# Verification baselines

## Explicit native action migration — source after 0.6, 2026-09-08 UTC

The final dedicated official-SDK stdio circuit `20260908-223045-f3a69b` passed
**15 terminal operations: 13 completed and two expected failures** against the
installed Modeler 4.3.0.008 engine. This is copy-only programmatic migration,
not presentation playback or independent desktop compatibility.

- Native-authored actions followed a root and its nested subtree between two
  diagrams and back. Text, Link, File, Image, Description and referenced embedded
  file/image/linked content survived native persistence and a fresh reader.
- An independently changed owner description did not silently refresh the earlier
  action cache. Shared source action bytes remained available to the unmoved owner;
  destination bytes and moved referenced attachments matched independent ZIP and
  attachment-export readback. A native no-op save retained the migrated content.
- Default missing consent and explicit migration into a different-byte filename
  collision failed as expected. Explicitly resolving the conflicting target
  action allowed recovery without changing the original input.
- All **29 workers / 34 owned processes** exited. **174 periodic desktop samples**
  observed no owned visible window or worker foreground takeover. A post-run
  audit rehashed **nine earlier output artifacts**, **seven captured inputs** and
  **111 distinct loaded vendor module contents**, without drift.

Dedicated transcript SHA-256:
`7e74946c27d8238505db5d60935b46a706effe812856be044b8674d5e2dcb308`.
Independent action-migration receipt SHA-256:
`8d73f13f0c186f5d63c7de627abbaa16d9ddbc9611bf82924a9601560e2ec4bf`.

The separate **combined action/scenario/saved-result circuit**
`20260908-223503-8c987d` passed **29 terminal operations: 22 completed and seven
expected failures**, plus two expected parameter/protocol rejections. It authored
actual File/Text actions through MCP and preserved both through forward/inverse
scenario migration. It also generated real saved simulation results, rejected
missing scenario/dependency/discard permissions, explicitly retired stale results,
and re-executed the existing quantitative resource/calendar corpus after movement.
The independent client checked action bytes and historical result metrics rather
than accepting native method completion alone.

All **52 workers / 62 owned processes** exited; **361 periodic desktop samples**
observed no owned visible window or worker foreground takeover. Post-run hashes
retained **nine prior outputs**, **15 captured inputs** and **108 distinct loaded
vendor module contents** unchanged. These are sampled observations, not continuous
desktop monitoring or GUI equivalence.

Combined transcript SHA-256:
`2e4aca6d62ae83d2c7917401ce622c2a2354b97ba07b573d9469013fae8da32f`.
Independent combined scenario/action receipt SHA-256:
`edc7bdc4d191a593602fdca03c248f5fea1e85ebc6bc2e04e4eb4cc4f4b6ff3b`.

Release build passed with zero warnings/errors; **1,277 unit/component tests**
passed, including 16 new migration-policy cases. XML-only MCP circuit
`20260908-223845-70feae` exposed 46 tools and passed with native execution
explicitly disabled. Eighteen package-policy cases passed separately; neither
those cases nor the XML-only circuit are packaged native acceptance. The immutable
0.6.0-alpha.1 ZIP was not replaced. See the [migration contract](native-action-migration.md).
Full automation, unsaved sessions, global layout, independent Modeler GUI
compatibility and broader scenario/corpus acceptance remain open.

## Native presentation actions — source after 0.6, 2026-09-08 UTC

Final official-SDK stdio circuit `20260908-215517-c1fc42` passed **27 terminal
operations: 22 completed and five expected failures**, plus two expected
parameter/protocol rejections. The installed Modeler 4.3.0.008 engine created,
persisted and independently reopened the actual native action collections.

- Normal Text, Link, None, File and Image actions were created and updated on
  native task/start/end owners. Description content came from the installed
  selected-owner resolver, not a host substitute. No content was activated.
- Eight referenced attribute kinds were exercised: Text, LongText, Number, Date,
  Link, FileLinked, FileEmbedded and Image. Native values/attachments were authored
  through MCP first; each selected native action cache matched the expected value.
- An actual installed-renderer PNG and an opaque non-XML `.xml` payload retained
  exact bytes. The independent client decoded the native ZIP/diagram archive and
  compared contents, in addition to the host's whole-archive and native hash gates.
- Native no-op saves preserved normal files and referenced embedded content.
  Byte replacement retained the unrelated image. Deleting the final actions
  removed their owned payloads and survived a new process; subsequent creation
  recovered after an expected absent-action deletion failure.
- Invalid image bytes failed in the worker. Wrong reference type, missing owner,
  unrelated-file overwrite and absent deletion were rejected; stale revision and
  unsafe filename requests were rejected at the protocol boundary. Expected error
  messages were retained and checked, not substituted with successful fixtures.
- All **61 workers / 66 owned processes** exited. **400 sampled desktop
  observations** recorded no owned visible window or worker foreground takeover;
  this is periodic observation, not continuous proof or independent GUI playback.
- A post-run audit rehashed **18 prior durable output artifacts**, **19 captured
  inputs** and **111 distinct loaded vendor module contents**. Earlier outputs
  remained unchanged after later writes/failures; module fingerprints still
  matched the local installation, including culture-specific resource assemblies.

Transcript SHA-256:
`5d088a5ecae9a651fa06e5d591fcdbce74764f0148c593af3ec607bb9a39a7a0`.
Independent presentation receipt SHA-256:
`9e10d116c8a03d0504e93914b79367fb4b1bddd1b3c900c9aab4e3a8905baefa`.

Locked restore and Release build passed with zero warnings/errors; **1,261
unit/component tests** passed. The separate XML-only MCP circuit
`20260908-215241-dabec0` exposed 46 tools and passed without running the native
engine. Eighteen package-integrity policy cases passed; these are not packaged
native acceptance. The immutable 0.6 ZIP was not replaced.

Earlier diagnostic runs exposed an incorrect interface assembly lookup and
inter-record whitespace retained by the expected XML after deleting the last
action. Both failures remain in private evidence. The final run used the corrected
installed service contract, targeted intent formatting and the existing native
image-size guard; unknown content and explicit whitespace-preservation remain
protected. See the [action contract](native-presentation.md).

The unchanged extended-attribute/attachment circuit also passed on the final
source: `20260908-220319-8a6554`, **18 terminal operations (15 completed, three
expected failures)**, 29 workers / 35 owned processes exited, and 224 sampled
desktop observations without an owned visible window/foreground takeover. This
regression covers twelve native attribute kinds, table rows, embedded file/image
bytes, export, no-op saving and unrelated edits. Its transcript SHA-256 is
`cf09bc59a15b6b711bfcf801e2989686f49f82f226d1d6308bae4e87e3ca7e68`;
independent receipt SHA-256 is
`f5a366ba35ddd5672c798d93febdb1a8b9512ff66c11838ead35ad7134bac908`.
The post-run audit retained ten prior outputs and six input snapshots unchanged.
This does **not** accredit playback, action migration between diagrams, unsaved
Modeler documents, independent GUI compatibility or full Modeler automation.

## Native saved simulation results — source after 0.6, 2026-09-08 UTC

Final official-SDK stdio circuit `20260908-210023-dbb545` passed **28 terminal
operations: 21 completed and seven expected failures**, plus two expected
parameter/protocol rejections. The installed Modeler 4.3.0.008 manager generated
the actual simulation results; separate editors loaded the original model,
persisted selected native result properties and independently reopened the copies.

- Five actual saved simulations and five historical `native_simulation_results_get`
  calls verified 12 completions, three minutes average processing, 36 minutes busy
  time, 84 fixed cost, resource contention and working-calendar waiting. Historical
  reading used the native persisted property, not another simulation. Every
  structured task metric matched the separately decoded historical XML.
- Replacing an existing result preserved the other scenario's opaque payload.
  Independent ZIP inspection, editor/reader property hashes and native no-op
  saving verified durable contents. The original revision remained unchanged.
- Native missing-scenario execution, missing historical results, metadata
  replacement without discard permission, and four migration preconditions
  failed as expected. Subsequent valid operations recovered on the same inputs.
- Explicit native metadata discard `f93ce982aaa3432384d858b5dde67843`, forward
  migration `66805e9153dd4a13ba7c0e727b1feab4`, and inverse migration
  `572a506453c24219a57920aca94e7438` retired genuine nonempty result sets with
  explicit consent. No synthetic ZIP payloads or fake simulator results were used.
- All **49 workers** and **51 owned-process records** were recorded exited.
  **386 periodic desktop samples** observed no visible worker window or worker
  foreground takeover. Independent Modeler GUI compatibility remains unverified.
- Locked restore, Release build (zero warnings/errors), **1,227 unit tests**,
  **18 package-policy cases**, and actual MCP XML circuit
  `20260908-210235-3b51f4` (44 tools; native explicitly not run) passed separately.

Transcript SHA-256:
`953454a3fdaad4c98a039de029ffca8d502713111beb26efba069eafa1537397`

Independent scenario/result receipt SHA-256:
`4f0b48243466d0fb12ec4761035e738a9df284a08936541273213ae63e429e4b`

The existing metadata/scenario/what-if regression passed on the same final build:
`20260908-210751-b6d26a`, **16 terminal operations: 14 completed and two expected
failures**. All 25 workers/owned-process records exited; 139 periodic desktop
samples observed no visible/foreground worker. Its quantitative timing, resource,
calendar and replication checks remained intact. Regression transcript SHA-256:
`856a8e0561181ea9c975fd0a250727807bbdb85edc0aaa8d2278d1ee7f846234`

The preliminary native write/retirement circuit `20260908-204914-bf6880` passed
before historical-result reading was added. A synthetic metadata-retirement unit
test initially represented an orphaned result with no owning scenario; the fixture
now includes that scenario. Production validation rejects orphaned/duplicate result
identities and unknown container annotations across both replacement and migration.
No acceptance assertion was replaced with mock or success-on-missing-data logic.

See [saved-result usage and boundaries](native-saved-simulation.md). This is a
source-only milestone; the immutable 0.6 ZIP is unchanged. What-if result-history
persistence, arbitrary historical-result editing, general inherited-scenario
execution, broader behavior/visual corpora and full Modeler automation remain open.

## Explicit scenario migration — source after 0.6, 2026-09-08 UTC

Final official-SDK MCP/native circuit `20260908-202129-e86615` passed **16
terminal operations: 13 completed and three expected failures**. Installed
Modeler 4.3.0.008, native persistence, independent workers and the actual
level-3/level-4 simulator were used. The client calls no production policy classes.

- Forward migration `d765b2446aa94847a895f2fc7c9adc47` and inverse migration
  `afe4e2df122149259ff4c855a6823c52` each moved five roots. The client checked
  complete parameter/resource/calendar records, mapped inheritance, untouched
  third-diagram configuration and the unchanged original revision.
- Before and after migration, real simulations verified 12 completions, three
  minutes average processing, 36 minutes total busy time, 84 fixed cost, resource
  contention and calendar waiting. Auxiliary native process/task records were
  retained in evidence and required to contribute no completed work.
- Missing migration, missing copy consent and incomplete inherited correspondence
  failed for the expected reasons. Valid recovery, native no-op save and inverse
  migration reusing existing dependencies completed.
- All **25 workers** and **27 owned-process records** were recorded exited.
  **181 periodic desktop samples** observed no visible worker window or worker
  foreground takeover. These are not independent Modeler GUI observations.
- Locked restore, Release build (zero warnings/errors), **1,199 unit tests**,
  **18 package-policy cases**, and actual MCP XML run `20260908-202427-472905`
  (43 tools; native explicitly not run) passed independently. This source
  milestone does not modify or reaccredit the immutable 0.6 package.

Transcript SHA-256:
`17e14998e284046093c32267c0a74b36b1e11618683b1982383569fb63d376e1`

Independent scenario receipt SHA-256:
`c7694f7e24fda370e4c7bd0c85a67b6ae79a0a1e172f4eba59f455e47f07be43`

The existing rich cross-diagram regression also passed on the final build:
`20260908-202637-5eb6fa`, **28 terminal operations: 26 completed, two expected
failures**, plus one expected stale-revision protocol rejection. Its six moves
retained their whole-archive gates. All 53 workers and 59 owned-process records
exited; 564 periodic desktop samples observed no visible/foreground worker.
Regression transcript SHA-256:
`b29805100114fd4e1962ce786abcf4e2cacd85673c3ed1570b38a844d72e2ff4`

Diagnostic run `20260908-200312-be41c7` stopped after forward migration because
its first result verifier assumed a single process. The installed engine also
returned auxiliary process records. The verifier now selects actual native
process/task IDs and rejects unexpected work from other records; timing and cost
assertions were not weakened. A subsequent preliminary run and the final run
above passed. Raw diagnostic evidence remains private.

See the [contract and reproduction command](native-scenario-migration.md).
Inherited-scenario configuration was verified, not inherited execution.
Nonempty saved-result retirement, presentation actions, arbitrary behavioral
equivalence, live unsaved sessions, independent GUI compatibility and a newer
packaged distribution remain separate unfinished work.

## Explicit cross-diagram reparenting — source after 0.6, 2026-09-08 UTC

Final real MCP/native circuit `20260908-192630-4daae1` passed **28 terminal
operations: 26 completed and two expected operation failures**, plus one
expected stale-revision protocol rejection. It used the official SDK stdio
client, installed Modeler 4.3.0.008 and separate source/editor/reopened workers.

- Forward subtree move `3ccf036e9d614eeda888cb634a9215f9` and reverse
  `90947ba768f7400f8d17c0320faa3ae6` retained all nine subtree graph elements and
  their native I/O records across diagrams. The client independently compared
  every observed graph field after changing only the requested parent, diagram
  and nested I/O diagram identities. The destination already contained a task.
- Standalone task forward/reverse moves
  `5145866eb5b848898f429f061f8a1ac9` /
  `82c4212070ec4eac960f21cc0ce149a5`, and image moves
  `83f8b59c3e9a44baa335628e22f436f8` /
  `8bce6372e1c144cdb7f7a040566c252a`, also passed. All six moves passed native
  restart and whole-archive fidelity, including the diagram-wide root-artifact
  ownership route, not only embedded subtree containment.
- Independent native exports verified exact original image and embedded-file
  bytes. Complete extended-definition/value and attachment records, RACI,
  current-user tab order/selection and unchanged destination content were
  checked. The existing native definition `ModificationDate` refresh was
  recorded separately; `ModifiedBy` and all substantive fields remained checked.
- Native no-op save, nested SVG generation, inverse graph equivalence, a final
  fresh reader and both original input revision checks passed. These are not
  Modeler GUI or simulated behavioral equivalence checks.
- Configured moved-element simulation parameters and omitted explicit diagram
  identities produced distinct checked failures. A separate explicit metadata
  replacement cleared only the test scenario before migration; this is **not**
  automatic scenario migration, and the configured original remained unchanged.
- **53 workers and 59 owned-process records exited.** Across **560 periodic
  read-only desktop samples**, no owned visible window or foreground process
  was observed. Sampling is not continuous proof or independent GUI testing.
- Transcript SHA-256:
  `07dddd264e4581fe0a875e684b22731c6f7c8e3b43dcd1dd15f2cd99a19cd082`.
  Cross-diagram receipt SHA-256:
  `035429384a309d61bad907668853e664cbad14bf19b952503ec806ce592967ca`.

Earlier run `20260908-191152-515b5b` passed the same 28-operation flow before
the independent full documentation-record comparison was added. Its 53 workers,
63 owned records and 353 periodic desktop samples passed. Transcript SHA-256:
`035c43189f74a794c64063c202ffa2da6e8881fb443b96700af21e803a30f191`.

Diagnostic runs remain recorded as diagnostics, not successes.
`20260908-190704-7851d4` exposed a missing editor preference-scope snapshot;
the editor reply was corrected, without weakening the restart gate.
`20260908-192033-40d896` stopped at the stronger documentation assertion because
the native loader refreshed the known definition audit timestamp; the verifier
now validates that exact timestamp scope while retaining all other fields.

Same-diagram regression `20260908-193338-5cd15d` subsequently passed **21 terminal
operations: 19 completed and two expected closure/cycle failures**, plus one
stale-revision protocol rejection. It retained the configured scenario, rich
attributes, native I/O, images and RACI through six-request forward/inverse
batches, whole-subprocess cross-process moves and explicit root-artifact
coordinates. No-op save, nested rendering, recovery and original revision
checks passed. **44 workers and 49 owned-process records exited**, with no owned
visible/foreground process observed in **294 periodic desktop samples**.
Transcript SHA-256:
`7c14e10615386aaa37f78c1b176076f1d48331bef3c5f26d7bda1386c67749a2`.
Receipt SHA-256:
`c4cb9a9caecb92546c5b6592bb19a44e4bf4a6b4679ec86535587bc8107d6ebc`.

Locked restore and Release compilation passed with zero warnings/errors;
**1,174 unit tests** and **18 package-policy fixtures** passed. Real MCP XML
acceptance `20260908-192121-f75dc0` passed with 43 tools and explicitly reported
native execution as **not run**. Unit/XML/package-policy results do not accredit
the native engine. The immutable 0.6.0-alpha.1 distribution is unchanged;
cross-diagram migration requires the later source build. Full automation,
configured scenario/action migration, global layout, live unsaved documents
and independent GUI accreditation remain open.

## Same-role native event conversion — source after 0.6, 2026-09-08 UTC

Final real MCP/native circuit `20260908-183108-989082` passed **19 terminal
operations: 17 completed and two expected native context failures**, plus three
expected protocol-level rejections. It used the official SDK stdio client,
installed Modeler 4.3.0.008 command and independent native readers.

- Forward `fd0d7157b7ca43a99507a76fb54b471c` and reverse
  `31a1db56df524457a31090ecceef8357` each verified **339 event conversions** with
  whole-archive fidelity. This covers all 336 directed pairs within Start (10
  types), End (9), Catch (7), Throw (7) and Boundary (10), plus an additional root
  start, nested catch and noninterrupting boundary. Roles did not change.
- Event-subprocess and transaction contexts, interruption, boundary aliases,
  incident sequence flow, native catch-output/throw-input data ports, Unicode
  documentation, label geometry and styles survived. Scoped event-owned embedded
  content was checked through independent attribute reads and byte-exact native
  attachment exports. Native no-op save, nested SVG generation and original-file
  revision verification also completed.
- A noninterrupting boundary converted to an incompatible error kind failed in
  the real worker (`dfac042e1dc04ba9b6cf7ca0ace19169`). An exception start requested
  outside an event subprocess failed separately
  (`0f2721ccca9c4427a3102fd927f61a87`). The client checked the actual error causes;
  subsequent valid conversions succeeded without reusing an uncertain write.
- Protocol-level rejections covered stale revision, missing explicit event role
  and a configured timer payload. Explicitly clearing that timer through native
  mutation enabled the subsequent valid batch; no source archive was fabricated
  or rewritten by the client.
- **33 workers and 38 owned-process records exited.** Across **235 periodic
  read-only desktop samples**, no owned visible window or foreground process was
  observed. Sampling is not continuous proof or independent Modeler GUI testing.
- Transcript SHA-256:
  `4dcf254fa77090fb486373e2bbe7c60704d88a0ed60c51190cf98874a0b275a2`.
  Operation receipt SHA-256:
  `8527e366087929c613af1a7de6cf8315bd0d246663d925381d70fff09b6aff96`.

The earlier enriched circuit `20260908-182153-a530c9` passed 17 completed
operations and both 339-change directions before the two native rejection cases
were added. Its 29 workers/37 owned records exited; 484 periodic samples observed
no owned visible/foreground process. Transcript SHA-256:
`974446ee5b121cae083ac9722db238ad984316e19eddd5a94e532004d8f10702`.

Diagnostic runs were retained rather than relabeled as successes:
`20260908-181516-83c118` exposed the distinct native-memory versus XPDL order of
multiple end-event definitions. `20260908-181722-deb41f` passed the graph check
but failed whole-archive comparison on type-owned runtime defaults. The final
policy checks those exact observed baselines; nondefault cost/priority and
unknown runtime fields remain protected, not globally ignored.

Locked restore and Release build passed with zero warnings/errors. **1,154
unit/component tests** passed without skips. XML-only MCP circuit
`20260908-183207-169ca3` passed the current tool/discovery contract and XML
workflows; native execution was explicitly not run in that circuit. Policy
fixtures are separate from native accreditation and cover configured payloads,
unknown XML, shared message identities and narrow runtime-default rules.

Same-category regression `20260908-183640-5c3310` passed **19 completed native
operations** and three expected protocol rejections, including the 99 forward
and 99 reverse task/gateway conversions with rich-content readback. All 31
workers and 36 owned-process records exited; 202 periodic desktop samples
observed no owned visible/foreground process. Transcript SHA-256:
`7e43778b10ced0e1c10ccb38cb8290f2154400e5aa2669653773fcaa87d5cdf2`.

Task/call regression `20260908-183959-47bfdf` passed **29 completed native
operations** and five expected protocol rejections: nine task-to-call and eleven
call-to-task changes, rich-content/native-I/O preservation, binding/unlinking,
native no-op and nested rendering. All 45 workers and 50 owned-process records
exited; 284 periodic samples observed no owned visible/foreground process.
Transcript SHA-256:
`371aa3086a2fd5e138e6e9a1c3dce009b127d385658b1292b59f325602bb5f70`.
Both native regressions ran sequentially after the event circuit. All 18 package
integrity policy fixtures also passed; they are not extracted-package native
accreditation of these new source changes.

See [the event conversion contract](native-conversions.md#events-preserve-the-role-replace-neutral-definitions).
This is not event-role conversion, configured payload migration, simulator
equivalence or independent desktop compatibility. It is a source addition, not
an alteration of the immutable 0.6 package, and does not complete full Modeler
automation.

## Bidirectional task / unbound-call conversion — source after 0.6, 2026-09-08 UTC

Final circuit `20260908-174732-45dbd8` passed **29 completed native operations**
and five expected protocol-level rejections through the actual SDK stdio client,
installed Modeler 4.3.0.008 commands and independent native reader processes.

- Forward operation `66ed1180e14a4414bf73b676c93f0d16` verified nine task-to-call
  changes. Reverse operation `43111ebe46754fe5b05d05bc13f93c73` verified eleven
  call-to-task changes, both with whole-archive fidelity preserved.
- Reverse coverage includes all eight task destination types, an additional
  nested task, and two independently factory-created calls: a root call converted
  to `ManualTask` and a nested call converted to `ScriptTask`. It is not merely
  a roundtrip of objects produced by the forward adapter.
- Native binding and unlinking of root/nested calls, standard/multi-instance
  loops, quantities, incident flows, data inputs, boundary/compensation references,
  geometry, Unicode descriptions, RACI, scoped attributes and byte-exact embedded
  content survived. The original file remained unchanged; no target process was
  deleted or inlined. Native no-op save and nested rendering also completed.
- The client independently verifies the two newly created activities' complete
  empty RACI rows before comparing every original metadata row and role array.
  These are declared additions, not permission to ignore assignment changes.
- Protocol rejections cover attribute applicability, stale revision with checked
  cause, stale source type, unrelated cross-category conversion and conversion
  while a call is still bound. Explicit unlinking then enabled conversion.
- **45 workers and 51 owned-process records exited.** Across **478 periodic
  read-only desktop samples**, no owned visible window or foreground process was
  observed. Sampled observation is not continuous proof or independent GUI
  compatibility accreditation.
- Transcript SHA-256:
  `87c165d3ad2b4ecfad4117815f1440eae7583ccb00d6f7b70d6c18b02d1fb66c`.
  Operation receipt SHA-256:
  `11491901b32a0f924bf649abc98084c754f04784bd47cc795b0c9c3b3a1eef04`.

Same-category regression `20260908-175417-000f8d` passed **19 completed operations**
and three expected protocol rejections, including the 99 forward and 99 reverse
task/gateway conversions with rich-content readback and whole-archive fidelity.
All 31 workers and 36 owned-process records exited; 194 periodic samples showed
no owned visible/foreground process. Transcript SHA-256:
`0f3228dada4158b804bb19ca216326adb6d701582fafbd36a6abb7943849a1ec`.

Release build passed with zero warnings/errors; **1,134 unit/component tests**
passed without skips. Final real XML-only MCP run `20260908-180058-2d26a2` passed
discovery and XML workflows, with native execution explicitly not run there.
Policy fixtures cover bound/external references, custom/mismatched latent layout,
foreign graphics tool IDs and nondefault/unknown call runtime content. Those
fixtures are not a native corpus of every possible call configuration.

The earlier `20260908-172112-66d3b3` circuit passed nine forward/nine reverse
changes before direct factory calls were added. Direct-factory attempt
`20260908-172729-bc0169` then exposed their actual paired 270 × 180 latent default;
the adapter checks the installed default and Core independently checks the
version-specific native XML baseline. Custom sizes are not broadly waived.
Run `20260908-173445-1fd32f` passed the eleven-change native/fidelity operation but
its client rejected the legitimate two new empty RACI rows. The final run above
uses exact declared-row verification instead of that incorrect equality assumption.
Slow native registration retained the same live processes; no total-duration
cancellation or synthetic result replaced a real execution.

See the [reverse conversion contract](native-conversions.md#unbound-call-to-a-task).
Reverse subprocess inlining, event conversion, live unsaved editing and independent
Modeler GUI compatibility remain separate. This addition is not in the immutable
0.6 package and does not complete full Modeler automation.

## Native task-to-unbound-call conversion — source after 0.6, 2026-09-08 UTC

Final circuit `20260908-170532-265468` passed through the actual official SDK
stdio client, installed Modeler 4.3.0.008 command and fresh native workers:
**24 completed operations and five expected protocol-level rejections**.

- All eight root task types and a nested task became unbound `CallActivity`
  objects with their original identities. Operation
  `d4723d655aa2494080a084fb3d8a6d1f` verified all nine requested changes and
  whole-archive fidelity; no diagram was created or implicitly chosen as a target.
- Standard/multi-instance loops, nondefault quantities, Unicode descriptions,
  shape/label geometry, incident sequence flow, root/nested native data inputs,
  boundary timers and compensation references survived the independent read.
- Four RACI assignments, scoped text/file attribute definitions, a Unicode
  value and byte-exact embedded file survived conversion, explicit binding of
  root/nested calls to another native process, and subsequent explicit unlinking.
- A definition initially excluding `CallActivity` caused a real rejection.
  Explicitly updating that definition through MCP enabled the subsequent
  conversion. Separate calls rejected stale revision (with checked error cause),
  stale source type, unrelated cross-category conversion and reverse call-to-task.
- Native no-op save, nested offscreen rendering, additional metadata/attachment
  reads and original-file revision verification completed. These do not establish
  independent GUI compatibility or equivalent simulation behavior.
- **37 worker records and 42 owned-process records, all exited.** Across
  **245 periodic read-only observations**, no owned visible window or foreground
  process was observed. These are sampled observations, not continuous proof.
- Transcript SHA-256:
  `981273fd43a7eadaef653b93801b393ec57010b998663fc23017543b3750dd56`.
  Operation receipt SHA-256:
  `8c353b6bb070a127931093aa05fe1ef73ce594cb250d002203ade11fc6134193`.

Same-category regression `20260908-165619-772c99` passed **19 operations**:
99 task/gateway conversions and their 99 reverse conversions, rich-content
readback, three expected protocol rejections, native no-op save and nested render.
All 31 workers and 36 owned-process records exited; 266 sampled observations
found no owned visible/foreground process. Transcript SHA-256:
`70e74648ba093c251eacbae53d11ef76e5fcd92a7038ce5399ddd44244897ad6`.
This regression preceded the final call-only default projection; the
same-category execution/projection paths were unchanged by that final adjustment.

General regression `20260908-171009-5afcbe` also passed: **15 terminal operations**
(12 completed, one expected native failure, one cancellation and one deliberate
host-death interruption). Native roundtrip, edits, no-op save, validation,
multi-diagram/nested content, simulation, rendering and recovery all completed
their expected paths. Nineteen worker-exit records and the independent client's
exact Job Object cleanup receipt account for all 20 workers; 24 normal owned-process
records also confirm exit. There were 116 periodic desktop samples with no owned
visible/foreground process observed. Transcript SHA-256:
`69d951a81d5e6d9ae057be6f5c1e66d83b494b5f98dfc6cf386ef1a0fe17e6e7`.
An earlier overlapping invocation was rejected by the existing per-user native
settings lease before engine work; the successful regression was run sequentially.

Locked restore, Release build with zero warnings/errors and **1,119 unit/component
tests** passed without skips. Real XML-only MCP run `20260908-170219-c80a8e`
passed discovery and XML workflows with native execution explicitly not run.
All 18 package-policy fixtures passed separately; fixtures are not native proof.
The earlier complete task-to-call run `20260908-170047-c50c13` passed before the
client's stale-revision rejection-cause assertion was strengthened; the final
run above supersedes that assertion's scope.

Initial attempts exposed a missing native configuration-service assignment and
then newly serialized call geometry/runtime defaults. Their failures were retained;
only exact observed neutral defaults were added to comparison copies. Nondefault
settings, unknown fields and original attachment bytes remain protected.
See the [conversion contract](native-conversions.md). This source addition is not
part of the immutable 0.6 package and does not close full Modeler automation.

## Native Excel pool projection — source after 0.6, 2026-09-08 UTC

Final visible-row run `20260908-161951-57f636` traversed actual MCP stdio, isolated installed
Modeler 4.3.0.008 workers and independent workbook/native readers. **10 terminal
operations: 8 completed and 2 expected native failures.**

- Both visible and implicit pools were tested empty and populated across two
  native diagrams, with Unicode names and nonempty pool documentation. The native
  mapper's selected participant/child inventory was compared to the source graph.
- The mixed publication reported exactly two omitted empty-pool sheets; selecting
  one diagram reported exactly one. After populating the remaining processes,
  both whole-model publications reported zero omissions and verified all expected
  populated pool/task row IDs, not merely duplicate-prone names.
- A wholly empty workbook reached the native generator and failed because it had
  no visible worksheet. A missing selected diagram also failed. Subsequent real
  publication and native inspection succeeded without changing the source revision.
- **18 worker records, 21 owned-process records, all exited.** Across 318 periodic
  read-only desktop observations, no owned visible window or foreground process
  was observed. These are sampled observations, not continuous monitoring.
- Transcript SHA-256:
  `86d2410bb7177319247e02c4b73901c61b616133be983a20edd01d094e59fadc`.
  Native receipt SHA-256:
  `e4d3f6c8aa9b440f4e7a3d63f5d20cdb9581d142de54d34dab81b0870680facf`.

Final-source rich Excel regression `20260908-162426-41c2ed` passed the originally
failing Web corpus with structured visible row readback. The earlier projection
regressions `20260908-161150-ddea2c` and `20260908-161218-b9c96d` passed before
the stronger row-position checks. That improvement now requires the native ID in
column zero and its source name in column one of a visible sheet, not a match
anywhere in flattened workbook text or its hidden index.
Final visible-row nested regression `20260908-162629-bde08a` and real XML MCP
run `20260908-162654-9ddc8b` also passed. All 18 independent package-policy
fixtures passed; those fixtures and the XML-only run are not native accreditation.
The real exported embedded file retained SHA-256
`c3b288b545076f681cb415f5f565a0848819512a69d4094acf88e2dc45be01a5`.
Excel/Word/PDF also passed regression run `20260908-160559-639d7e` before the
final stricter Excel child-identity assertion; Word/PDF logic was unchanged by
that assertion. Native Web regression `20260908-161243-a56847`
also passed independent directory/image readback; this is not a repeat browser
quality accreditation. Locked restore, Release build (zero warnings/errors) and
**1,097 unit/component tests** passed with no skips. Unit fixtures do not
accredit the native engine. The new source is not in the immutable 0.6 package.

The [publication contract](native-publication.md) explains structured omissions.
Initial all-empty run `20260908-155720-f99fab` failed for the actual native
visible-sheet constraint; no synthetic worksheet was introduced to hide it.

## Native Web publication — source after 0.6, 2026-09-08 UTC

The [Web execution record](validation-web-publication.md) retains 10 terminal
operations through real MCP/native workers, exact selected/nested page and image
checks, an actual embedded file, rejection/cancellation recovery and unchanged
source revision. Excel/Word/PDF passed the previous nested-corpus regression.
Desktop functional browser traversal passed; the broader accessibility/mobile
quality gate did not. This is not part of the immutable 0.6 package below.

## Consolidated 0.6.0-alpha.1 Windows package — 2026-09-08 UTC

The [exact extracted-package record](validation-package-0.6.md) reports twenty
real MCP/native circuits against clean source commit
`27e59ae7df098ec76a496f166708a5a901f516ca`. All clients passed; 274 native
operations reached expected terminal outcomes and all 499 recorded workers
exited. All 392 manifest entries remained unchanged before and after testing.

This packages the documented reparenting, selection-copying, alignment and
nested-body VDX capabilities alongside earlier native families. The 0.5 archive
is unchanged. The chronological source baselines below retain their original
scope and do not establish independent Modeler GUI or live-unsaved compatibility.

## Native Visio nested-body pages through MCP — 2026-09-08 UTC

Real client run `20260908-131355-dc5593` verified the new one-manager-call
subprocess canvas projection against installed Modeler 4.3.0.008. It supersedes
the blank-body export limitation in the historical baseline below, **not** the
remaining limits on hierarchy reconstruction, behavioral fidelity or GUI compatibility.
The immutable 0.5.0-alpha.1 package is unchanged.

- **10 completed operations:** native creation/mutation, four Visio exports,
  two Visio imports, native inspection and BPMN-to-native roundtrip setup.
- **2 expected native failures:** nonexistent selected diagram and actual
  unsupported stencil names in a generated VDX; no partial output was published.
- **1 active-registration cancellation** followed by successful export recovery.
  Actual MCP tools also rejected stale revisions, duplicate selection and missing
  acknowledgement of interchange losses.
- First corpus: two root diagrams plus two nested-body pages; all four tested
  flow endpoint pairs survive export verification and both artifact/local-Unicode
  VDX import routes. No output page is empty; three redundant native reservations
  are removed with actual source-page receipts checked against their source IDs.
- Second corpus: `examples/collaboration-nested.bpmn`, with pools, lanes, gateway,
  events and two expanded nesting levels. All **11 source sequence-flow endpoint
  pairs** survive import, persistence, strict native no-op and independent final
  readback across three nonempty pages. The imported pages are separate
  collaborations, not reconstructed BPMN subprocess ownership.
- Strict whole-archive fidelity, graph and metadata equality pass for every
  imported verification model after first-import initialization. Original native
  and local VDX source hashes remain unchanged.
- **39 worker-start receipts / 47 owned-process exit records**, all exited.
  **272 periodic desktop samples** saw no owned visible or foreground window.
  Sampling is not continuous observation or independent Modeler GUI validation.

Transcript SHA-256: `93563388c682f276f0198331e46b3252e7edf4e8c8b75c8be952cd724a825257`.
Final own-runtime fingerprints:

| Own runtime | SHA-256 |
| --- | --- |
| `McpBizagi.Server.dll` | `c5d5af7985bc864adb890385506ebc16505b0cb89612c53ed49e7558649307e2` |
| `McpBizagi.Core.dll` | `eabcf2561a900e5ea91cf4ddbeab403b276498a86c208e81da59bee40a3eeda7` |
| `McpBizagi.BizagiAdapter.dll` | `e930352cf01edf368bc6c54d364de453eb71bbd2376dc271648b9a8712a0488e` |

Release build: zero warnings/errors; **1,054 unit/component tests passed**.
Independent real MCP XML regression: `20260908-131457-d51c66` passed; 43 tools
were enumerated, not 43 universal operational accreditations. The four inspected
installed components were re-fingerprinted unchanged. Private diagnostic inputs,
native-generated files and vendor research are not redistributed.

Two failed promotion attempts remain documented rather than counted as passes:

1. A negative enclosing-pool margin caused the native restart loader to remove
   pools and their body contents. The adapter now uses a nonnegative wrapper
   origin, derives its size from actual child coordinates/vertices and explicitly
   rejects negative source body coordinates rather than silently translating them.
2. The natural corpus exposed a one-pixel pool-height change (634 to 633) on
   restart: the loader derives pool height from its contiguous lanes. New foreign
   imports now initialize that domain invariant before their **first** persistence,
   recording the actual adjustment. Gapped/overlapping or invalid lane partitions
   reject instead of being silently repacked. The global archive fidelity policy
   was not relaxed, and no existing native model is normalized by this path.

The [Visio contract](native-visio.md) documents source-page receipts, strict empty
reservation removal, initial normalization and remaining interchange boundaries.

## Historical native Visio VDX baseline — 2026-09-08 UTC

Real client run `20260908-122112-9fe939` used the stdio host, supervised net48 workers,
and installed Modeler 4.3.0.008 Aspose-backed Visio manager. This is source newer
than 0.5.0-alpha.1, not a replacement release or Full Modeler Automation.

- **8 completed operations:** native creation and mutation, three Visio exports,
  two Visio imports (completed artifact and local Unicode path), and final native
  source inspection. This count includes setup/inspection, not eight distinct
  newly accredited capabilities.
- **2 expected native failures:** absent selected diagram and genuinely unmapped
  stencil names in a real exported VDX. Neither partial result was published.
- **1 active-registration cancellation**, followed by a successful new export;
  stale native/VDX revisions, duplicate selection and missing loss acknowledgement
  were also rejected by the actual MCP tools.
- 32 worker-start receipts; 33 owned-process exit records, all exited.
  187 periodic desktop samples observed no owned visible/foreground window.
  These samples are not continuous capture or Modeler GUI compatibility evidence.
- Root task Unicode labels, the observed source/target flow relationship, selected
  page labels/order, single-diagram subset selection and local source SHA-256
  preservation were independently checked by the real client.
- All imported verification models passed strict whole-archive native no-op
  fidelity and subsequent graph/metadata equality from independent readers.
- **Explicit loss control:** the populated subprocess reserves a blank extra page;
  its nested task is omitted. A UserTask becomes Task. These are recorded native
  limitations, not successful nested-content/type-preservation gates. Page counts
  alone are never used to claim preserved content.

Transcript SHA-256: `72dc874e774d6794ae34a363df9a4bbb7d631bc72840e44e829ec06d27e19d97`.
The final runtime fingerprints retained with the run include:

| Own runtime | SHA-256 |
| --- | --- |
| `McpBizagi.Server.dll` | `c7aa68fae73ba48cc2da6dd52be5748dd3f22a9c0c5e2b4e05537d70d9004bec` |
| `McpBizagi.Core.dll` | `a9765b091dfe511e34911ea47f300f98718c816cd330e361d905fda2d04feaf0` |
| `McpBizagi.BizagiAdapter.dll` | `29e9ce58c8b19f8f6692e78d9a74c737c977c21f65e547aa8c6c48fba911f37a` |

The four inspected installed components were fingerprinted again unchanged.
No proprietary code, fixtures, dumps or local operation evidence is published.
Release build: zero warnings/errors; **1,029 unit/component tests passed**.
Independent XML MCP regression: `20260908-122230-2cf99e` passed. Its 43 enumerated
tools are schema inventory, not 43 operational accreditations.

The first promotion attempt rejected uninitialized invisible-pool dimensions
and missing empty attribute containers during no-op preservation. Initialization
now uses installed domain defaults before first persistence; the global native
fidelity verifier was not relaxed. An attempted malformed-numeric negative
control was tolerated by the vendor parser and is **not** counted as a native
failure; the final unsupported-stencil test records an actual native rejection.

See [the Visio contract](native-visio.md) and [request examples](../examples/native-visio.json).
VSD/VSDX, arbitrary stencils, nested-content preservation and independent visual
compatibility remain open. The existing package remains immutable.

## Native selection copying through MCP — 2026-09-08 UTC

Seven installed-engine copy corpora passed the official stdio MCP client,
isolated worker, native command/persistence and fresh-process readback:
**14 completed copies, seven expected failures, seven active-reader cancellations**.
All 56 workers exited; 864 periodic read-only desktop observations found no
owned visible window or foreground takeover. These are sampled observations,
not continuous proof or desktop visual compatibility.

The corpora covered roots/connections, cross-diagram and embedded subtrees,
nonempty timer/multiple/compensation payloads, exact attachments/images and
cross-root native I/O. See the [selection-copy contract](native-selection-copy.md)
for run identifiers, transcript hashes, failure history and remaining boundaries.
The full unit/component suite passed **999 tests**. Independent MCP XML run
`20260908-112651-42b781` passed with 41 tools enumerated, not 41 blanket
accreditations. The immutable 0.5.0-alpha.1 package is unchanged and does not
contain this later source feature. Full Modeler automation remains open.

## Native selection alignment through MCP — 2026-09-08 UTC

The final source build passed the actual official stdio client, isolated worker,
installed **4.3.0.008** editor/native commands, durable native persistence and
fresh-process readback. See the [alignment contract](native-layout.md) for
fingerprints, native integration details, retained development failures and limits.

| Corpus | Terminal native operations | Observed result |
| --- | --- | --- |
| `20260908-081959-a690d6` | 13 completed, one expected participant-containment failure | Eight modes, automatic no-op, stale revision/invalid mode rejection, flow-insertion prevention and failure recovery |
| `20260908-081644-6316c6` | Eight completed, one cancelled | Two-level embedded selection, exported byte-identical attachment, explicit manual-label offset/size, live offscreen cancellation and recovery |

Across the two runs, 60 workers and 137 observed process identities exited.
The 904 periodic read-only desktop samples observed no owned visible window
or foreground takeover; this is not continuous observation or desktop visual
compatibility. The cancelled alignment's editor job contained six observed
process identities, all exited. No cancelled native operation is counted as a
successful edit.

Separately, **970 unit/component tests** passed, and the independent MCP XML
circuit `20260908-082511-64e081` passed with 40 tools enumerated. Enumeration is
not operational accreditation. The build reported zero warnings/errors.
The immutable `0.5.0-alpha.1` archive predates this feature; no new package or
full-automation claim follows from these source records.

The shared worker changes also passed the general real MCP regression
`20260908-082545-0eba5a`: 12 completed native operations, one expected failure,
one cancellation and one intentional host-death interruption. BPMN/native
roundtrip, batch edits, stale revision, no-op comparison, nested/multidiagram
preservation, native validation, basic simulation results, offscreen SVG,
corrupt-input diagnostics, recovery, Job Object cleanup, journal restart and
state ownership all passed their existing acceptance checks. Its transcript
SHA-256 is `b73770c8c9ac4657a50f8d2a027600962220a32c447b533675a1f28672764058`.

## Native same-diagram reparenting — 2026-09-08 UTC

Independent MCP run `20260908-055324-7533b8` passed the full self-authored
reparenting corpus against installed Modeler **4.3.0.008**. Its **21 terminal
native operations** comprise **19 completed** and two expected failures
(incomplete reference closure and cyclic containment). A stale revision was
separately rejected by the MCP tool before dispatch. Recovery and both latest
artifact and original-source revisions were checked afterward.

The client created two native diagrams, two visible pools in the tested
diagram, root/nested subprocesses, tasks, a sequence flow, a data object and
its visible/native I/O associations, an attached boundary event, Unicode
attributes, an embedded file, a transparent nested image, all four RACI sets,
configured BPSim input and persisted subprocess tabs. Through actual MCP:

- Six selected roots moved to another subprocess and back
  (`7f1b223d7b6a4edf99541411992d4ba0`, `e8069b75730a480ea341b06f0598ecc7`).
- The rich subtree moved to another participant process and back with explicit
  selected-root positions (`bca8fb7e7ba74ed89ed7be2d217dac1f`,
  `6a58ee9659614c848986898f7cc91086`).
- The nested image moved to the target process's root and back
  (`d6524e3f2d35417583fa1a9d4855ce72`, `c2be26cf6fd14c10a1165d7221f8dcd6`).
- Native no-op save and nested SVG generation completed after the moves.

Each successful move used independent source-reader, editor and fresh-reader
workers and a whole-container fidelity gate, including separately serialized
I/O associations, process-level ports and flattened activity sets. Metadata
assignment rows were compared by element identity, retaining each row's ordered
RACI values and every other metadata field, including exact scenario XML.
Retaining scenario configuration does not prove unchanged execution behavior.

All **44 workers exited**. **485 periodic read-only samples** observed no visible
worker window or foreground ownership. These are sampled observations, not
continuous desktop tracing or independent Modeler GUI compatibility. The MCP
transcript SHA-256 is
`033b5d25afaad422b614014611941d0a0db94cfd9b34937acdb25c8ee062e58f`.
The build had zero warnings/errors; **934 unit/component tests** passed
separately, including adversarial unknown-payload and wrong-owner cases.

The subsequent general regression `20260908-055919-eff99c` also passed actual
MCP import, edits, validation, simulation, rendering, engine failure, active
cancellation, host-death cleanup, journal restart and recovery. Its 15 terminal
operations comprise 12 completed, one expected failure, one cancellation and
one interruption. All 19 workers exited; 144 periodic samples observed no
visible/foreground worker. Transcript SHA-256:
`ca0e7ab16d97021f6f2dd3a66552d5d58760764ec86b026dda9b6731c4f862dd`.
This regression also exercises the shared mutation-fidelity entry projection;
the new relocation policy does not replace the existing mutation checks.

Earlier private diagnostics are retained: `20260908-053152-1b5456` exposed a
missing comparison relocation for actual native data associations;
`20260908-054408-77fad9` exposed an overly strict client assertion about
graph-derived assignment-row order; `20260908-054828-47c809` exposed flattened
process-level I/O port relocation. These were not accepted as successful full
circuits. The policy now checks exact durable owners and retains actual record
payloads; unexplained differences remain failures.

This is **source after 0.5.0-alpha.1**, not a retroactive change to that ZIP.
See [the contract and reproducible command](native-reparenting.md). Cross-diagram
migration, automatic layout, live documents and full Modeler automation remain
open; this record does not narrow their scope.

## Consolidated 0.5.0-alpha.1 Windows package

The [consolidated package record](validation-consolidated-package.md) documents
ten independent MCP/native circuits against a clean extracted archive: 174
terminal native operations, 280 exited workers and 347 unchanged manifest
entries. It includes exact source/archive/transcript fingerprints and clearly
separates deliberate failure assertions from successful operations. Historical
source and earlier-package records below remain valid for their stated versions.

## Native embedded-subprocess extraction — 2026-09-08 UTC

The enriched independent MCP run `20260908-040825-01d182` passed root and
nested extraction through the installed `RefactorElementsCommand`, durable
native persistence and fresh-process reads. Its 15 terminal native operations
include 14 completed and one expected wrong-element-type failure; a stale
revision was separately rejected at MCP preflight. Inspection afterward
confirmed recovery and the unchanged original source revision.

The client authored two diagrams, root/nested subprocesses, internal and
incident external connections, Unicode names, all four RACI assignment sets,
extended text, an embedded attachment and a nested transparent image. Root
extraction `99f39533963149559686e8bd13519212` moved five descendant identities;
nested extraction `bd2528a7df324a068858d86f9e6fbddc` independently moved two
from the same original source. The tests checked attachment/image bytes,
unchanged incident flow references, native no-op saving, SVG rendering, and
the order, selected state and remapping of four persisted current-user tabs.

All 30 owned workers exited. The 178 periodic read-only samples observed no
visible worker window or foreground ownership; this is sampled evidence, not
continuous desktop tracing or independent Modeler visual compatibility.
The transcript SHA-256 is
`c54329b997410f47b05a9f0755110fda3669c7c9b11ef2d2057e0d8a355a2843`.

After the final archive-identity guards were added, the current-source real
MCP replay `20260908-041340-817091` independently extracted each ordinary
subprocess from the retained rich native model and saved each result again.
All five operations completed; 11 workers exited and 66 periodic samples
observed no visible window or foreground ownership. This replay used actual
workers and native files, not a comparison-only fixture. Its transcript is
`b79562484d6dc64363cb4e8e7f8eba4ff7f56a251412ede84ad3e6ab67a7f06a`.

An earlier diagnostic run `20260908-034749-bbb9cf` correctly failed because
the installed reader materialized a call QName absent immediately after the
native command. The adapter now initializes both native call representations
before saving. The fresh-reader comparison was not weakened to hide this
difference. Diagnostic artifacts remain private.

The source build completed with zero warnings/errors and **909 separate
unit/component tests** passed, including 13 extraction-policy cases. These
tests are not substitutes for the native acceptance above. The subsequent
general MCP circuit `20260908-041807-b9e3eb` passed with
`--native --extended --simulation --render --recovery`: 12 completed native
operations, one expected failure, one cancellation and one deliberate
host-death interruption. It covered actual XML MCP, native persistence and
edits, validation, simulation, SVG, corrupt input, cancellation/recovery,
Job Object cleanup and state-lease rejection. Its transcript SHA-256 is
`44741a05fbcda3b83b427067a19ecd55ac0690bd32150afb5a29404b9aa5f084`.

See [the extraction contract](native-refactoring.md) for interpretation and
remaining boundaries. Reusable calls are not simulator-equivalent to embedded
execution. Configured child scenarios, presentation actions, special subprocess
types and arbitrary selection refactoring remain separate work. The earlier
immutable 0.4.0-alpha.1 ZIP does not include this capability; this milestone
does not close full Modeler automation.

## Native XPDL 2.2 interchange — 2026-09-08 UTC

The enriched independent MCP circuit `20260908-032836-a1a99d` passed **13
terminal native operations**: 11 completed and two expected native failures,
plus six expected MCP preflight rejections. It uses the installed Unicode file
exporter and XPDL importer, not an XML-generated stand-in for native behavior.

The two-diagram model was authored through native MCP creation, mutation,
resource/RACI and documentation tools. It contains Unicode labels, a nested
subprocess/task, a gateway and connection, two extended definitions, populated
Unicode values and a verified 34-byte embedded attachment. The run verifies
all-diagram export, artifact import, native no-op save, selected-diagram export,
duplicate-document rejection, stale revisions, DTD/version rejection, missing
native diagram failure and a successful native inspection afterward.

Primary export `bde4a02a933242d6b91808f9580b5c85` and artifact import
`cd1969fda73648f6becbb8032273783d` completed their independent fresh-process
graph and metadata/documentation gates. All 27 owned workers exited; 154
periodic samples observed no visible worker window or foreground ownership.
Sampling is evidence about those observations, not continuous desktop proof.
The transcript SHA-256 is
`9be64494c32a67756ca3f312dbd7acf88439324fed51e7e0c60ec59463268fbb`.

The test explicitly confirms **exchange losses**, not losslessness: the
attachment byte entry disappears, original extended definitions become new
`LongText` definitions, and simulation/graphical/text differences are reported.
Unicode text and the tested four RACI assignment sets survive. The original
native source revision remains unchanged. Native path strings can be exported
as plain attribute text; raw artifacts and diagnostics remain private.

The release-source build completed with zero warnings/errors. **896
unit/component tests** passed separately, including 19 XPDL parser/difference
tests; unit success is not the native acceptance claim. The prior initial rich
run `20260908-032136-da9087` also passed. An earlier attempt rejected invalid
initial diagram labels before any native work; it is retained as diagnostic
evidence, not counted as an accepted interchange run.

The post-change general circuit `20260908-033155-0fe1f7` also passed with
`--native --extended --simulation --render --recovery`: 12 completed native
operations, one expected failure, one cancellation and one deliberate host-death
interruption. Actual XML MCP, nested/multi-diagram native persistence and edits,
validation, simulation, SVG, corrupt input, worker recovery, Job Object cleanup
and state-lease rejection ran successfully. Its transcript SHA-256 is
`76c87c8260fdd9a5051dfbe1f9513b88f802ac7419702b3291acc1053def2195`.

See [the XPDL contract](native-xpdl.md) for precise fields and remaining format
boundaries. This source capability is not part of the earlier immutable
0.4.0-alpha.1 release ZIP and does not close full Modeler automation.

## Native task/gateway conversion — 2026-09-08 UTC

The final enriched source circuit `20260908-025001-518144` passed **19 terminal
native operations**, all completed, plus three expected real MCP preflight
rejections. Both 99-element conversion batches passed fresh-reader and full
archive gates: forward `d88cd30b66e34acaafac35de6b3eab31` and reverse
`ef3bcdf104594bfda576aa2f49c65514`.

This run adds 28 standard and 28 multi-instance task loops, nondefault activity
quantities, all four RACI assignments, two native extended definitions, Unicode
text values and a byte-exact embedded file. Specialized native documentation
and metadata reads independently checked both directions; two native exports
retained the attachment's 42 original bytes. Native save updates definition
audit timestamps: acceptance validates those timestamps and compares all other
definition content, rather than demanding byte-identical audit metadata or
ignoring unknown fields. Nineteen operation receipts and the raw transcript
remain private. Release compilation had zero warnings/errors and **877
unit/component tests** passed separately.

All 31 workers exited. Their 204 periodic desktop samples observed no visible
worker window or worker foreground ownership. The transcript SHA-256 is
`ac6c10e90ca041a70f1b4ae0446762e1a4b67a10285ef3c1771c82429068943a`.

The documentation regression `20260908-025441-f40a72` also passed: 15 completed
native operations and three expected worker failures, covering all 12 native
attribute kinds, table rows, file/image attachments, exact extraction, rename,
no-op persistence, reference guards and deletion. All 29 workers exited;
184 periodic samples observed no worker-visible window or foreground ownership.
Its transcript SHA-256 is
`67d906b767953274204a9841db4eb05a6e7c124f8d0ba8cff84c0fe6ee02c7c8`.

The extended general regression `20260908-025806-c3a48a` passed actual XML MCP,
BPMN/native roundtrip, multi-diagram/nested editing, no-op comparison, vendor
validation, native simulation results and offscreen rendering. Its 15 terminal
operations comprise 12 completed, one expected worker failure, one active
cancellation and one deliberate host-death interruption. Recovery verified
Job Object child cleanup, journal restart, a subsequent successful native call
and rejection of a competing host sharing the state directory. The transcript
SHA-256 is `4cd2e4cdc5fcf76fe42fb59bfbe125738eea904d40535fdbd344f50dc7acd68d`.

The independent official SDK circuit `20260908-022443-45e5c7` passed seven
terminal native operations, including a batch of **99 directed conversions**
and the 99 reverse conversions. The matrix contains all 56 directed task pairs,
all 42 directed gateway pairs and one task inside an embedded subprocess.
Unicode descriptions, native label bounds/styles, an incident flow, fresh
readers, native no-op save, nested rendering and original-file revision checks
were included. Three additional MCP preflight checks rejected stale revisions,
stale expected types and cross-category requests. Discovery observed 35 tools;
875 unit/component tests passed separately at that baseline.

Fifteen workers exited, with 501 periodic desktop samples and no observed
worker-visible window or worker foreground ownership. These are periodic
observations, not continuous instrumentation or independent desktop GUI proof.
The retained transcript SHA-256 is
`19706e0b2f109d774d2a7ae009a14c3496e308d1d07b15744c27dc9c3439ffc8`.

Initial runs exposed exact native factory selector/default differences and
comparison-copy indentation; those were corrected without rewriting archive
XML or exempting arbitrary unknown content. A richer follow-up also exposed
a native loader failure when attachment extraction exceeded the old Windows
path boundary. The process-local worker manifest/configuration now opt into
long paths; the same native route extracted an actual 262-character attachment
path with all 42 source bytes intact. This is not a promise that every native
exporter supports arbitrary path lengths.

See the [conversion contract](native-conversions.md) for type-specific
retirement restrictions and the distinction between persistence, behavioral
equivalence and visual compatibility. This source milestone does not update
the old immutable release archive or complete full Modeler automation.

## Native typography, colors and label bounds — 2026-09-08 UTC

The final source circuit `20260908-005617-3388f3` passed **16 terminal native
operations**: 12 completed and four expected worker failures. Two additional
real MCP preflight rejections covered fractional coordinates and empty label
bounds, which must not silently reset an existing label rectangle.
The official SDK discovered **34 tools**. Release compilation had no warnings
or errors, and **847 unit/component tests** passed separately.

The native circuit enumerated actual installed fonts; created styled tasks,
an end event, gateway, embedded subprocess, annotation, nested data object,
sequence flow and association; edited an existing pool; created a styled pool,
lane and milestone; saved and reopened every change; checked no-op persistence;
cloned the diagram with exact style/label readback; verified later source edits
did not mutate cloned formatting; and checked original-source revisions.
Expected failures covered an absent font, an unsupported connector fill, an
unpersisted process style and unsupported pool text direction. Valid updates
and native reads succeeded after these failures, without replaying a write.

The installed native renderer produced real root/nested SVG and transparent
PNG artifacts. Independent assertions checked font family, measured font size,
bold/italic/underline/strikeout, opaque font/fill/outline colors, alignment and
native direction attributes. The authored external sequence-flow label retained
its requested `(230, 145, 130, 40)` rectangle in the actual SVG. A 14-unit font
rendered as 18.5941 CSS pixels; a 12-unit font rendered as 15.9378 pixels.

| Source circuit | Terminal states | Workers / periodic samples | Transcript SHA-256 |
| --- | --- | --- | --- |
| `20260908-005617-3388f3`, final source and complete rectangle intent | 12 completed, 4 expected failures | 25 / 226 | `e4fb61ae3ebd0aee4aa87b80ea11c932d2b0d7fd883210bb2515e14eb8e16fb9` |
| `20260908-004001-860345`, reinforced style/render/type corpus | 12 completed, 4 expected failures | 25 / 227 | `01b41199c96e649a7281178b8ec38558f83a427555ce5c48fed650c36c35fd6e` |
| `20260908-003334-e915e7`, initial native style lifecycle | 11 completed, 3 expected failures | 23 / 220 | `7d02231a856106441456c69a473b98f1a3ab8ba2ef4744cf936c9e407feab4a7` |
| `20260908-004427-fe2f4a`, general native/recovery regression | 12 completed, 1 expected failure, 1 cancelled, 1 interrupted | 19 / 799 | `286801195a5b8f669adbd231b692746a8f682eeb4000c8b3b0c6673ceac32792` |
| `20260908-004855-f4fa0d`, native custom artifact regression | 18 completed, 2 expected failures | 38 / 366 | `f59397775188cb873a0420708bb324f1412dde5d1bfbe267ecf2100c367fd9c3` |

The general regression retained actual MCP XML operations, native roundtrip,
stale-revision rejection, multi-diagram/nested editing, no-op fidelity, vendor
validation, simulation results, offscreen SVG, corrupt-input diagnostics,
cancellation/recovery and host-death Job Object cleanup with journal restart
and competing-state-owner rejection.
The custom-artifact regression retained real `.bca` import/export, explicit
image conversion, shared definitions, root/nested instances and native SVG
image pixels, cloning, deletion and original-preserving persistence.

No worker-visible window or worker foreground ownership was observed in those
periodic samples. This is not continuous desktop instrumentation or independent
Modeler GUI compatibility accreditation. Initial development also exposed real
constructor/reader graphical-default differences; only new objects now receive
the reader's actual defaults, without normalizing existing model observations.

Internal task labels ignored some manually persisted bounds, and the native
pool adapter does not persist arbitrary label geometry/direction/background.
Those limits are explicit in the tool contract and mutation warning. Direction
attributes alone do not accredit pixel rotation, and semitransparent rendering
or universal glyph coverage is not claimed. See [native style boundaries](native-styles.md).
This source milestone does not update an older immutable release ZIP or close
the full Modeler automation objective.

## Native custom artifact definitions, instances and `.bca` — 2026-09-07

The final source circuit `20260907-235432-948997` passed **20 terminal native
operations**: 18 completed and two expected worker failures. Two additional real
MCP preflight checks rejected deletion of a referenced definition and an
unacknowledged `.bca` identity collision.

The circuit covered model-owned definition creation; explicit native alpha
rasterization and repeated serialized-byte/pixel stability; root and nested
instances; reference changes; replacement with a genuinely different image;
shared-definition preservation through native diagram cloning; actual native
`.bca` export and import; explicit replacement of conflicting existing content;
import into an independently created model; root/nested native SVG payloads;
instance and definition deletion; no-op persistence; and unchanged originals.
Export verification used an exporter worker, a separate native importer and a
fresh native model reader. No substitute archive writer or mock engine was used.

| Source circuit | Terminal states | Workers / periodic samples | Transcript SHA-256 |
| --- | --- | --- | --- |
| `20260907-235432-948997`, final source and export-path guard | 18 completed, 2 expected failures | 38 / 358 | `180de8f163f9ae565f42a1632e7a601bff8aa43da6ce4411a36bf456e785b6d0` |
| `20260907-234838-3e6790`, reinforced image/replacement/temp-path checks | 18 completed, 2 expected failures | 38 / 565 | `b685f0cf242187f9b3eedc55fb6b44cbd98162b78e4ba61f9cdb6792be023178` |
| `20260907-234137-0c9f0e`, initial complete custom lifecycle | 16 completed, 2 expected failures | 34 / 698 | `fa987cb4c7cc93b4ca98df5e6e620024d2101b66d4da2e0f21638dd9b1025b74` |
| `20260907-235925-b7e284`, complete image/clone/SVG/Word regression | 14 completed, 4 expected failures | 29 / 623 | `ea870a630851beb0d7af893a314ec02bf1687867c8e4c2093f3b0dba3b7f7700` |
| `20260908-000426-34336f`, general extended/simulation/render/recovery regression | 12 completed, 1 expected failure, 1 cancelled, 1 interrupted | 19 / 145 | `b8b481d5adb8d8f5caf47071162f8777bf3d3568f79223f95961471be3a57945` |

The expected worker failures were unacknowledged native pixel conversion and an
unknown model-owned definition reference. The reinforced/final harness checked
their actual error causes, not merely terminal failure status. Each owned worker's
actual temporary directory was recorded and checked against its private run path.
Periodic observation detected no owned visible window or foreground acquisition;
this is not continuous proof or independent desktop GUI compatibility.

Native SVG readback verified one root and one nested embedded image, including
actual payload hashes and decoded pixels. The original source alpha PNG and its
native normalized representation had different pixel fingerprints; that change
was explicitly acknowledged and recorded. Ordinary image artifacts retain their
separate exact-pixel contract. The installed renderer labeled the custom PNG data
as `image/jpeg`; validation decoded the actual bytes instead of trusting the label.

A development failure exposed an unbound inherited
`CustomArtifactTypeManager` dependency in native persistence. Wiring the real
installed manager allowed fresh-process reopening without initializing the GUI or
loading/modifying the operator's global palette. Native bitmap stream lifetimes
were detached only after comparison with durable source payloads. Deletion removed
only the exact isolated definition file that native persistence would otherwise
leave behind; fresh readback proved it did not reappear.

The final Release build had no warnings/errors, **819 unit/component tests**
passed, and the independent SDK listed **33 MCP tools**. These counts do not
accredit full Modeler automation. The new source family is not automatically part
of the previous immutable release package. See the
[custom artifact contract](native-custom-artifacts.md) for intent, limits and
reproduction commands. Raw native research, model copies and transcripts remain
private; only sanitized results and owned source/tests are published.

After the shared image-decoding and persistence-dependency changes, the complete
image lifecycle passed again. The general regression also passed real MCP XML,
BPMN/native roundtrip, multi-diagram/nested editing, native simulation results,
offscreen rendering, stale/corrupt inputs, active cancellation, host-death child
cleanup, journal restart and state-lease checks. No owned visible/foreground
window was observed in either regression. The recorded interruption and
cancellation were deliberate recovery tests, not substituted successes.

## Native image artifacts — 2026-09-07

The source image lifecycle and reinforced native-SVG payload circuit
`20260907-225232-e04385` passed **18 terminal native operations** (14 completed,
four expected worker failures), plus a stale-source-image revision rejection at
MCP preflight. The corpus covered transparent PNG, BMP, JPEG, palette GIF,
animated GIF and multi-page TIFF. It exercised frame selection, replacement,
exact export/reuse, native clone, root/nested rendering, Word publication,
deletion, no-op persistence and unchanged original model/image revisions.

The SVG gate verified actual embedded raster bytes and decoded pixels for **four
root images and two nested images**, not only graphical IDs. Word readback found
**six pages and 11 images**, including document icons; its dimension/content
checks do not establish image-pixel or document-layout equivalence.

| Source circuit | Terminal states | Workers / periodic samples | Transcript SHA-256 |
| --- | --- | --- | --- |
| `20260907-225232-e04385`, reinforced SVG payload verification | 14 completed, 4 expected failures | 29 / 1,848 | `4cc907a0d6941c74278e464fc62716a1e0f89ba4b4363d3ececb73f4861eaaea` |
| `20260907-224718-8ce30f`, earlier complete image lifecycle | 14 completed, 4 expected failures | 29 / 417 | `7ca11530c5459b057c08d6784f16a1cdcfadfc91032a955cab59cf700cf68af9` |
| `20260907-230222-c318f9`, basic native/XML/cancellation regression | 6 completed, 1 expected failure, 1 cancelled | 11 / 125 | `f0f869aaef39fdabd9154d7046fcf8cc9ced4ad91c12678c69665b150b130c44` |
| `20260907-230605-08dfaf`, extended/simulation/render/host-recovery regression | 12 completed, 1 expected failure, 1 cancelled, 1 interrupted | 19 / 322 | `b74be33a10880144864abb1a9c473a4c417d3007f10bb087faacdb61ec51118f` |

No listed circuit observed an owned visible window or foreground takeover.
Long native publication was allowed to advance through real activity rather
than being cancelled by total duration. The image family is source-level
experimental functionality, not independent Modeler GUI accreditation or the
closure of full automation. The prior immutable package does not include it.

Release compilation completed without warnings/errors and **787 unit/component
tests** passed. The independent SDK listed **30 MCP tools**. Detailed input,
pixel, stream-lifetime and encoder contracts are in [native images](native-images.md).

The extended regression used `--native --extended --simulation --render --recovery`.
Actual markers covered multi-diagram/nested edits, native simulation, offscreen
SVG, no-op preservation, worker failures, cancellation, host-death job cleanup,
journal restart and the exclusive state-directory lease. These are independent
regressions, not substitutes for the image-specific circuit.

## Native content artifacts — 2026-09-07

The final source circuit `20260907-220740-15048c` passed **18 terminal operations**
(14 completed, four expected native worker failures). It created and changed
annotation/formatted-text content, root/nested artifacts, a diagram group and a
native header; preserved them during unrelated edits and native cloning; checked
header context remapping; updated group/header geometry; and completed clearing,
deletion, no-op persistence and original-revision verification.

The native root and subprocess renderers verified **8/8** and **2/2** graphical
identities respectively. The client also required both SVG and PNG artifacts for
the exact requested surface, not merely a successful render of another diagram.
Word readback reported **6 pages and 23 images**, including document icons.
This is not a complete rich-text-layout or independent Modeler GUI review.

Actual failed worker paths covered incident-connected artifact deletion,
artifact content on a task, process-owned group creation and a geometric change
that would silently move a root artifact to another pool. Subsequent native
requests completed; failed outputs were not adopted as the next revision.

| Source circuit | Run | Terminal states | Worker observations / periodic samples | Transcript SHA-256 |
| --- | --- | --- | --- | --- |
| Final artifact lifecycle | `20260907-220740-15048c` | 14 completed, 4 expected failures | 30 / 303 | `d4fe6190cad14ffb8495aab65ee51ba7d49a24baa9bd8e010aa5ec13ffe844c9` |
| Earlier artifact lifecycle | `20260907-215207-d6f8d3` | 14 completed, 4 expected failures | 30 / 1,025 | `c7c4ebcbd04b84ddaab03331f4642e4576eb166566f75912cca636baf79ce26a` |
| Data/activity regression | `20260907-220000-e1b918` | 16 completed, 4 expected failures | 35 / 440 | `1d4b7c20cdbc1b9e3a17688400abd986241a4e2ea859dfa543f8783f602d3098` |
| General native/recovery regression | `20260907-220513-fd80b2` | 13 completed, 2 expected failures, 1 cancelled, 1 interrupted | 21 / 130 | `3730c88724d7832ab4321aae9ed2c3377edefca78a8fae29c906f849f98b0c2a` |

No visible owned-worker window or worker foreground was observed in these
sampled checks. Long native initialization continued while real activity was
observed; total elapsed duration was not used as an automatic cancellation rule.
Release builds completed without warnings/errors and **748 unit/component tests**
passed. Those tests are separate from the native circuits above.

The first artifact development run correctly failed on native group geometry:
the installed group reader materializes an intrinsic expanded view. The adapter
now requires that actual view explicitly instead of normalizing the difference
away. A later harness failure treated an operation artifact reference as a disk
path; the client was corrected to use the native MCP revision/inspection path.
No native comparison was weakened to turn either failed run into a pass.

See the [artifact contract and remaining families](native-artifacts.md).
This source milestone is not a new release and is not present in the older ZIP
described below. Full Modeler automation remains open.

## Consolidated native data package — 2026-09-07

Five actual MCP circuits passed against the same clean, freshly extracted package
at source `3c52d54`: **106 terminal native operations**, with all **331 manifest
entries** unchanged after execution. This includes data/activity and event I/O,
event payloads, special subprocesses and general native/recovery regression.
See the [package identity, circuit counts, hashes and remaining scope](validation-native-data-package.md).
This is not a new release or completion of full Modeler automation.

## Native event definition payloads — 2026-09-07

Source circuit `20260907-193023-a3bfd1` passed **19 terminal operations**:
15 completed and four expected failures, through the independent official SDK
MCP stdio client, restricted worker pipe and installed Modeler **4.3.0.008**.
It retained **33 worker observations / 294 periodic desktop samples** with no
observed visible worker window or worker foreground. Transcript SHA-256:
`88ec142ddc1c5756a17eb0f7ddf061d77b336d82d6e4c42f8090ee76fbf0feaf`.
Periodic observation is not continuous proof or independent Modeler GUI review.

The native creation batch verified **49 requested elements**, including typed
message, timer, conditional, link, signal, error, escalation and compensation
definitions; mode-specific multiple defaults; parallel-multiple catch/boundary
events; and context-specific starts in nested event-triggered subprocesses.
Unicode names, conditional expressions, error/escalation codes, canonical cycle
text and zone-less date values survived durable native save and fresh readback.
Nonempty updates, one-field patches within a multiple event, unrelated renames,
explicit clearing and subsequent deletion/no-op save passed archive fidelity.

Compensation references survived fresh native loading, retargeting and cloning,
including nested targets. The native clone retained original diagram contents
while remapping target identities into the cloned diagram. Actual native
operations rejected referenced activity deletion, payloads on a task, an absent
definition kind and a cross-container target; later successful operations proved
recovery without adopting failed artifacts.

Offscreen native rendering and Word publication completed. Independent document
readback reported **17 pages and 76 images** (including document icons, not just
diagrams). This verifies the publication path and tested image/text requirements,
not every event payload's printed appearance or independent document layout.

Development runs exposed real issues rather than being declared successful:

- The acceptance client initially assumed one multiple-definition set for every
  mode. The installed factory's actual mode-specific sets replaced that assumption.
- Intermediate multiple events use `TriggerIntermediateMultiple`; comparison
  now recognizes only its exact native mode and ancestry.
- Link names use native NMTOKEN encoding; only that field receives the matching
  framework encoding in requested-value comparison.
- The collaboration cloner shared compensation definitions with its source.
  Native definition cloning now detaches them before remapping references.
- Clearing a condition to null produced an unstable artifact: another load/save
  materialized an empty expression. Clearing now persists the native empty
  expression immediately, and the later deletion/no-op circuit proves stability.

The source builds with zero warnings/errors and passes **648 unit/component
tests**. Those fixtures are not the native acceptance evidence. See the
[payload contract](native-event-payloads.md) for fields, timer limitations and
reference semantics. Definition collection editing, event execution, independent
GUI compatibility, a new packaged release and full automation remain separate.

The same source then passed expanded regression `20260907-193449-1a68eb`:
**17 terminal operations** (13 completed, two expected failures, one requested
cancellation and one interrupted operation), **21 worker observations / 120
periodic samples**. Transcript SHA-256:
`16770449fe70e889a90c98ef1fe304e02b022b2d1126eb1ef7fffb0f4d300a99`.
This exercised native interchange, inspection, validation, simulation, rendering,
settings contention, corrupt input, active cancellation/recovery and host-death
Job Object cleanup, journal restart and state ownership. No worker remained after
completion. The 648 unit/component tests also passed again afterward.

## Native special subprocesses — 2026-09-07

First source circuit `20260907-181529-eaf638` passed **25 terminal operations**:
16 completed and nine expected failures, using the independent SDK MCP stdio
client and installed Modeler **4.3.0.008**. The run retained **36 worker
observations / 3,676 periodic desktop samples**. Transcript SHA-256:
`bb53b6cbd8363b67a82ad5a7c9636c53f554c9af73d9080b553612d182a4d3f2`.

The native factory created ordinary, transaction, ad hoc and event-triggered
subprocesses, nested special containers, context-specific start/cancel events
and a transaction cancel boundary. Trigger flags, ad hoc ordering, Unicode
condition text, explicit clearing, unrelated renames and native clone properties
survived independent worker restarts and whole-archive fidelity comparison.
Transaction cancel references remapped through the native clone identity map.

Nine actual native operations rejected the intended incompatible context,
wrong-kind property target, illegal mixed subprocess mode, invalid cancel
context, referenced activity deletion or sequence-flow attachment. Successful
operations afterward verified recovery without adopting failed artifacts.

Five native offscreen surface requests and Word publication completed. The
independent publication reader reported **22 pages and 102 images**; this image
count includes document icons, not only diagram images. No-op native save and
final inspection passed without modifying the source. Long native rendering,
document generation and fingerprint-reading phases completed without a total
operation timeout; no clicks or foreground control substituted for the engine.

The expanded source policy suite passes **601 unit/component tests**, with no
Release-build warnings or errors. This does not make those fixtures a native
acceptance corpus. See the [special-subprocess contract](native-subprocesses.md)
for read-only fields, context guards and unsupported simulation semantics.
This remains source acceptance, not a new release-archive or GUI-equivalence claim.

Expanded circuit `20260907-183230-aa4c14` passed **33 terminal operations**:
23 completed and ten expected failures, with **50 worker observations / 1,235
periodic samples**. Transcript SHA-256:
`cd8c4a64cc15aa0884ee97fa95585484d072b96d106f41d71a056c0812ecaea8`.
It additionally verifies selected/ordered special-subprocess tabs, a two-level
expanded transaction → ad hoc → task surface, child-first container deletion,
noninterrupting escalation starts and rejection/recovery when a trigger-flag
update would leave an incident sequence flow. Native Word readback reports
24 pages / 110 total images, including document icons.

An actual connected diagnostic diagram traversed `native_simulate`. Its response
retained two input-specific `special_subprocess_simulation_unsupported` warnings,
one for the actual transaction and one for the actual ad hoc subprocess, with
their own native IDs and properties. This verifies honest diagnostics and native
output transport, **not** transactional rollback, ad hoc scheduling or condition
execution. Native simulation behavior remains subject to its separate gates.

Broad source regression `20260907-184224-fa6b6e` passed **17 terminal operations**:
13 completed, two expected failures, one cancelled and one interrupted;
**21 worker observations / 131 periodic samples**. Actual XML/native interchange,
default simulation, offscreen rendering, corrupt input, settings contention,
active cancellation, host death, owned-worker cleanup, journal restart and state
ownership were exercised. Transcript SHA-256:
`5dc910867f637bacb4205cfdbefdc09926da064f67561ad0ecbcc2119b39ae31`.
All 601 unit/component tests passed again afterward. No new distribution archive
or release is claimed by these source circuits; consolidated packaging and the
remaining full-automation families still require their own acceptance.

## Native event palette and boundary lifecycle — 2026-09-07

First native source circuit `20260907-173054-03d57d` passed **17 operations**:
12 completed and five expected failures, through the independent SDK stdio MCP
client and installed Modeler **4.3.0.008**. The run retained **28 worker
observations / 2,063 periodic samples**. Transcript SHA-256:
`fb6f8ce3ee26dd6aada84cef38984fcb4c806c682350ca271b71212d07b50869`.

The corpus creates start/end variants, catch/throw intermediate events, boundary
triggers and instantiating event gateways using the installed factory. Boundary
references survive nested containment, reattachment, interruption changes, an
unrelated rename and native diagram cloning. No-op save and independent native
readback pass the whole-archive fidelity policy. Native offscreen rendering
verifies all 44 expected identities on the main palette; Word publication also
renders its nested surfaces and verifies the durable document through a separate
reader. The earlier renderer's slow startup completed without a total timeout.

Deletion of a referenced activity, cross-container reattachment, a noninterrupting
error boundary, a non-event property target and a sequence flow entering a boundary
all failed for the specific intended reason. Subsequent successful operations
verify recovery. The expanded interruption corpus and broad regression follow.

The expanded event circuit `20260907-174347-d8444c` passed **19 operations**:
13 completed and six expected failures, **31 worker observations / 264 periodic
samples**. It additionally switches all seven interruption-capable boundary
trigger kinds, verifies a start-event property update and rejects a
noninterrupting start outside an event-triggered subprocess. Transcript SHA-256:
`bf6865e35c4e7ca85f603be25803e7bbbd542e0db00ac8d2a87fc49ef3b73974`.
The source policy suite now passes **564 unit/component tests**. Policy fixtures
for event XML and clone references were corrected to use actual native XML
ancestry; production comparison rules were not relaxed to accommodate fixtures.

The broad source regression `20260907-174726-d6a22c` also passed: **17 terminal
operations**, 13 completed, two expected failures, one cancelled and one
interrupted; **21 worker observations / 1,011 samples**. Actual XML/native
interchange, simulation, offscreen rendering, corrupt input, settings contention,
active cancellation, host death, owned-job cleanup, durable journal restart and
competing state ownership were exercised. Transcript SHA-256:
`381e552c4650c2bc7e2f8cc3f80b3e97611d0e4b44d5cee4f49e035ed21a4f44`.
Periodic desktop samples are not continuous desktop certification.

Final Release build completed with no warnings or errors, all 564 tests passed
again, and independent XML MCP run `20260907-175541-d43855` verified the current
host's explicit creation-type/mode inventory. That schema-discovery assertion is
not counted as an additional native-engine circuit.

This is **source acceptance**, not validation of a new release archive. The
[event contract](native-events.md) does not claim full event-definition payload
editing, simulator event execution, transaction/event subprocess automation or
independent Modeler GUI equivalence. The full-automation goal remains open.

## Native loops and initial connection failure classification — 2026-09-07

This source checkpoint exposes **29 MCP tools** and passes **524 independent
unit/component tests**. Three of those new component tests use real OS pipes;
they test connection/error policy, not native modeling. Release compilation
completed with no warnings or errors.

Native run `20260907-163046-36c2cc` passed **17 terminal operations**:
16 completed and one expected wrong-kind failure, with **30 worker observations
/ 270 periodic samples**. Transcript SHA-256:
`45c3f5191f389c602523fff2fc615bd9c3201bee3ffc2dd7b53ca71bb43084f6`.

The independent MCP client created native standard loops on eight task
subclasses, an embedded subprocess and a reusable call. It verified exact
maximum/counter/test-time/Unicode-condition metadata, switched to parallel and
sequential multi-instance configurations across All/One/None/Complex behavior,
checked both expression fields, removed loops and returned to explicit native
defaults. Fresh-worker inspection, no-op save, an unrelated name change,
offscreen rendering and native Word publication passed. Real simulation of a
separate single-task diagram returned input-specific loop limitations and actual
metrics. This accredits editing and diagnostics, **not iteration execution**.
See [the loop contract](native-loops.md).

Expanded run `20260907-163455-8168dd` also passed **19 terminal operations**:
18 completed and one expected failure, with **34 worker observations / 284
periodic samples**. Transcript SHA-256:
`96e520e42b25341b75ce12ccc01f08dd14b39bb364e314abdbfde3e5d53a36e6`.
It adds separate native clones of the standard-loop and multi-instance palette,
correlates every cloned activity through the actual native `Identities` map and
asserts exact loop metadata in the independent reader. Subsequent original edits,
native no-op save and four-diagram Word publication retain both clones. This
does not imply simulation of those disconnected palette diagrams.

Run `20260907-163015-aa4d50` passed an actual missing-worker-dependency failure
followed by installed-engine recovery: **one expected failure / one completion**,
with **two worker observations / five periodic samples**. Transcript SHA-256:
`eb85f7290ac7272e5a14889929407b0604ce502d1bfa26ade2e221c281be0c38`.
Only a private copied worker dependency set omitted `StreamJsonRpc.dll`; neither
the vendor installation nor source/release binaries were altered. The actual
worker error identifies the missing assembly. A three-second initial connection
deadline produced `failed`, `TimeoutException`, `requestDispatched: false` and
`operationCancellationRequested: false`, followed by verified worker exit. The
restarted host used the intact worker and completed real native initialization.
No write was replayed. This is a startup/recovery circuit, not a full modeling
acceptance by itself.

### Extracted native-loop candidate

Clean runtime source `6202fc034527186ef62de79d8dafe575d9ab4fd6` passed
[public CI](https://github.com/h0w4r/MCP-Bizagi/actions/runs/34144344027).
Its private framework-dependent Windows archive is **6,619,736 bytes**, SHA-256:
`4a00b7f42f5a156230657826021cd414e68e0ba08578d06a5d1b3fdf4b686e59`.
All **323 manifest entries / 324 files** matched before execution and after each
successful circuit against the unmodified extracted host and worker:

| Circuit | Run | Terminal operations | Worker observations / samples |
| --- | --- | --- | --- |
| Initial connection failure and actual native recovery | `20260907-164151-47f293` | 1 completed / 1 expected failure | 2 / 3 |
| Loop creation/edit/removal, two native clones and Word publication | `20260907-164218-c09d37` | 18 completed / 1 expected failure | 34 / 283 |
| Native scalar/flow semantics and honest input/result diagnostics | `20260907-164604-d29d53` | 20 completed / 4 expected failures | 40 / 290 |
| Resources, RACI, configured simulation levels 2–4 and what-if | `20260907-165009-d8bd36` | 14 completed / 2 expected failures | 25 / 131 |
| Reusable-call black-box behavior, what-if and Word/PDF | `20260907-165236-0b2a04` | 10 completed | 16 / 1,947 |
| Expanded native/XML, settings contention, active cancellation and host recovery | `20260907-170342-b92377` | 13 completed / 2 expected failures / 1 cancelled / 1 interrupted | 21 / 1,137 |

Transcript hashes, in table order:

- `b7aff628cf36aacd94fe8a3ccabdf4bdf8786da8cf74502ff1d921700a12a085`
- `14be37d46db075559925a1587c243ae79e694d8fb01dbd91da81a8c2b879fad1`
- `5d85ef90693cd1f51996928113d40f94b01c61d29d5cf3d5fc6fbe2b8a723be1`
- `0378852b64b1b4652d9e565fbadb39ddd2d867585ec49e212e793c4eeadc65e3`
- `ab4013f2d76e228514368ee3bd0858d648b1b3b9d0777f1d0c47030920114355`
- `3080cf43b4d44f20456e05e8c21932c06bc190e032eed48669bd4743634a461d`

Initial connections used the explicit 90-second operator setting; the copied
missing-dependency case deliberately used three seconds. No total native
operation deadline was imposed. Slow offscreen initialization retained actual
activity and eventually completed; periodic samples are not continuous desktop
certification or independent GUI equivalence.

The first full attempt, `20260907-165905-dc6293`, is retained as **failed**. Its
acceptance client cancelled at the new host `worker_connecting` phase and then
incorrectly demanded native-settings evidence before native initialization.
The corrected independent client waits for an actual `native_registration` or
`register:*` event and saves its cancellation intent. The successful repeat
records `native_registration` for operation `9d904e1ae3084cc2854d4c691d613f27`,
then verifies cancellation, cleanup and subsequent recovery. Only the acceptance
client changed; no extracted runtime binary or verification invariant was edited
to make this pass. The earlier failed run is not counted as a successful circuit.

This source candidate is not a replacement public release or a declaration of
full Modeler automation. Nondefault token and loop execution, additional native
element semantics, rich formats, live unsaved documents and independent desktop
compatibility remain separately tracked.

The separate strict token-quantity gate, `20260907-171032-34b98b`, **failed** as
an unsatisfied behavioral requirement, not an infrastructure error. Its actual
native end counts were 24/24/24 for verified input quantities 1/1, 2/1 and 2/3;
the intended counts were 24/12/36. The native results were neither rewritten nor
replaced by a custom evaluator. Transcript SHA-256:
`aadfdb0a5a96153d1bbad14c45aaab14372699743bb6b4ea3ec35cad85b3e7d5`.
All 323 manifest entries / 324 files still matched after this strict run, and
no MCP worker remained. A diagnostics gate passing does not close this strict
gate or establish behavior on other simulation levels or engine versions.

## Native activity properties and flow semantics — 2026-09-07

This source checkpoint exposes **29 MCP tools** and passes **487 unit tests**.
A non-incremental Release build completed without warnings or errors, followed
by the independent XML MCP circuit `20260907-160631-cef651`. Unit/XML success is
not substituted for the following installed-engine acceptance.

Native run `20260907-160107-ff15c0` passed the **editing and simulation diagnostics**
gate through independent SDK stdio MCP and installed Modeler **4.3.0.008**:
**24 terminal operations**, 20 completed and four expected failures, with
**40 worker observations / 281 periodic samples**. Transcript SHA-256:
`d7abf63f473fef69df2e601816e6d7b1245a28540c97b74d9d2d8229f95e42b6`.

The real native corpus exercises eight task subclasses, an embedded subprocess
and a reusable call, all four activity properties and all seven activity states,
gateway directions, Unicode condition text, ordered default switching and
default-source reconnection. Durable fresh-worker readback, no-op save, native
offscreen rendering and Word publication completed. Wrong activity/gateway/flow
kinds and duplicate default flows failed explicitly. Three additional actual SDK
binding errors rejected misspelled/unknown typed members before tool execution;
their original plain-text errors are retained, not counted as engine operations.

**Nondefault token execution remains unaccredited.** The installed simulator's
actual input retained the requested start/completion quantities, but the
two-branch, twelve-instance corpus produced 24 end tokens for all three settings:
1/1, 2/1 and 2/3. The intended counts were respectively 24, 12 and 36. The server
reports exact input identities/quantities separately from actual simulation
metrics and emits `nondefault_token_quantities_unaccredited` for each affected
activity. No output or native engine was changed to conceal the discrepancy.
See [the semantic contract and separate strict gate](native-semantics.md).

Failures are retained independently: `20260907-154334-71a09a` rejected a newly
created condition's serialization scaffold; the fix restores only verified
native indentation in the comparison copy, not arbitrary whitespace/content.
Runs `20260907-154605-baf12f` and `20260907-155537-dc30e3` failed intended token
behavior assertions despite correct native inputs. They are not operational
passes for token execution. Current diagnostics acceptance preserves that open
issue; it does not redefine simulation semantics to match the observed counts.

These results do not accredit complete loop/event editing, every decision
topology, independent desktop visual equivalence or full Modeler automation.
Raw archives, operator paths and proprietary research remain private.

### Extracted semantic candidate and retained startup failure

Clean source `4a65d25fac38b6f4ddd5026165ba311f6ca04129` passed
[public CI](https://github.com/h0w4r/MCP-Bizagi/actions/runs/34142139370).
The private ZIP is **6,606,220 bytes**, SHA-256:
`ee9eea2b9829ec4fd41b55b51209d3a59759349062753433d5e36577a8c8e084`.
All **321 manifest entries / 322 files** matched before and after these circuits:

| Circuit | Run | Terminal operations | Worker observations / samples |
| --- | --- | --- | --- |
| Native property editing and honest simulation diagnostics | `20260907-161324-1a4dc2` | 20 completed / 4 expected failures | 40 / 319 |
| Call black-box simulation, what-if and Word/PDF | `20260907-162146-2c8edb` | 10 completed | 16 / 863 |

Transcript hashes, in table order:

- `3635115ae0303eb2685ced464094532bcb24e1c921c721b07ac70dbef6b159dd`
- `77a55dabe42ce6dafd18bbb0042768a74c13dfe95c9221800cd595a41654ef1a`

The first call regression attempt, `20260907-161747-12c6f7`, did **not** pass:
its worker never connected within the initial 30-second deadline, and the old
journal incorrectly classified that internal deadline as operator cancellation.
Empty worker logs do not establish why startup was delayed. A fresh run used
the same immutable candidate with the operator connection setting increased to
90 seconds; this changes only the initial handshake deadline, not native
operation duration. The subsequent source correction and separate missing-
dependency circuit above accredit failure classification, not a guessed startup
root cause. The failed attempt remains retained and is not counted as a pass.

The candidate predates loop editing and the connection-classification fix.
It is not a new public release, a complete regression run or completion of the
full Modeler automation objective. Periodic desktop samples are not independent
GUI equivalence evidence.

## Native reusable-call lifecycle and black-box simulation — 2026-09-07

That source checkpoint exposed **29 MCP tools** and passed **444 unit tests**. The native
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

### Extracted native-call candidate

Clean source `4a8fdd012cf6def20abda129aa09b301464bccc0` passed
[public CI](https://github.com/h0w4r/MCP-Bizagi/actions/runs/34138079559).
Its framework-dependent Windows ZIP is **6,589,030 bytes**, SHA-256:
`af635bdfb011e2079bff21331c14dd253beaf94e88fe037fa3eb9b3812687c8e`.
All **319 immutable manifest entries / 320 files** matched before and after the
following actual MCP circuits against the unmodified extracted host and worker:

| Circuit | Run | Terminal operations | Worker observations / samples |
| --- | --- | --- | --- |
| Call lifecycle, wrong targets/kinds, deletion guards and native clone | `20260907-152450-887a36` | 17 completed / 5 expected failures | 37 / 224 |
| Configured black-box simulation, four replications and Word/PDF | `20260907-152812-67c8ce` | 10 completed | 16 / 273 |
| Expanded native/XML regression, settings contention and host recovery | `20260907-153020-44027b` | 13 completed / 2 expected failures / 1 cancelled / 1 interrupted | 21 / 108 |

Transcript hashes, in table order:

- `cd025fd0f7e0d1468658659d52b593c52ef7cd7b0bfdf6c4a09619980f897f52`
- `6a1e4ff70ccdd66857ccc43a72faa203bd38bc3e7957852fed4e5a6a26f417bb`
- `9a92928f0a2a6af345e561e7fe445baad59dd633d9fc6ce4d2f337233aa94209`

The publication circuit explicitly asserts the call name/description and target
diagram/task in the independent durable Word/PDF reader output. The regression
includes cancellation, real host death, Job Object cleanup, journal restart and
state/settings ownership. No owned MCP worker remained afterward. Desktop
observations are periodic read-only samples, not continuous visual certification.
This is a verified source candidate, not a replacement of the old public 0.4
release or completion of full Modeler automation.

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
