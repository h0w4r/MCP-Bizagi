# Capability ledger

This ledger distinguishes implementation from operational accreditation.

The [0.6.0-alpha.1 extracted-package record](validation-package-0.6.md)
consolidates twenty real MCP/native circuits. "After 0.4" below identifies the
source generation of a feature, not its absence from the newer package.
It does not imply that every combination or unsaved GUI workflow is verified.

| Family | Status | Evidence required for broader claims |
| --- | --- | --- |
| XML create/read/name edits | Locally verified through MCP | Unicode, stale revision and original backup checks |
| XML structural checks | Implemented, bounded | Complete standards validation is separate |
| Native bootstrap | Experimental diagnostic | Actual native request, not assembly presence |
| Native import/save/reload/export | Locally verified, experimental | Basic and two-diagram cases; warnings expose normalization |
| Native XPDL 2.2 interchange | Experimental current source | Installed file serializer/importer, Unicode, multi-diagram selection, native restart and explicit metadata/attachment losses; no lossless or GUI claim; [contract](native-xpdl.md) |
| Native Visio VDX interchange | Experimental; 0.6 package verified | Installed mapper, explicit page/kind/label loss reports and strict native no-op/readback; separate nested-body pages with source-page receipts and tested internal flows; not reconstructed hierarchy, complete Visio support or a native backup; [contract](native-visio.md) |
| Native container preservation | Partial corpus, explicit policy | Unknown-content checks are implemented; broad attributes/attachments/scenario corpora remain pending |
| Existing `.bpm` inspection | Locally verified, copy-only | Source byte equality and actual native identities |
| Blank native model creation | Experimental current source | Native constructor/domain defaults, exact durable diagram identities, ordered tabs and no-op stability; [contract](native-models.md) and [execution evidence](validation.md) |
| Native workspace adoption and save-as | Experimental current source | Exact source bytes, real engine preflight, guarded replacement/backup, durable receipts, cancellation and host-death reconciliation; [contract](native-commit.md) |
| `.bpm` name batches | Locally verified, copy-only | Task/event batch and task nested two subprocess levels; full archive checked on tested inputs |
| Native connector port metadata | Locally verified source after 0.6 | SequenceFlow, Association and MessageFlow explicit port lifecycle, fresh reader and independent archive checks; not all port/shape combinations or global layout; [contract](native-connector-ports.md) |
| Native no-op save and comparison | Basic and documented rich corpora locally verified | Unknown/binary changes rejected; untested combinations remain subject to the same fidelity gate |
| Worker cancellation and recovery | Locally verified | Active registration cancellation, exit evidence, subsequent native call |
| Host death and state ownership | Locally verified | Actual host termination, Job Object worker cleanup, interrupted journal, restart, competing-host rejection |
| Native validator | Real invocation locally verified | Exhaustive invalid-model categories still pending |
| Native documentation | Excel/Word/PDF locally verified on tested inputs | Installed generators, separate reader, actual text/image evidence; current source reports native empty-pool Excel omissions and verifies populated row IDs; custom templates, other formats and broad attachment corpus remain open |
| Native Web publication | Experimental source after 0.6; tested MCP/native circuit verified | Selected/all root and nested pages, native search projection, PNG/attachment bytes and cancellation recovery; desktop functional journey verified, accessibility/mobile gate not accredited; [contract](native-web-publication.md) |
| Native simulation | Defaults and configured levels 2–4 locally verified | Quantitative timing, cost, resource and calendar checks; four what-if replications; broader distribution/behavioral corpus remains open |
| Native resources and activity RACI | Locally verified, copy-only | Role/Entity catalog edits, assignment replacement and clearing; referenced deletion rejected; global catalogs and process-level assignment editing remain open |
| Native scenario configuration | Locally verified, copy-only | Complete diagram BPSim replacement, fresh-worker readback, explicit result discard; [details](native-simulation.md) |
| Native presentation action definitions | Experimental source after 0.6 | Per-owner text/link/none/file/image actions, native description and attribute references, exact payload bytes and fresh-reader fidelity; explicit action migration through reparenting; not playback or live sessions; [contract](native-presentation.md) |
| Native saved simulation results | Experimental source after 0.6 | Actual single-scenario result-copy persistence, historical native metrics, independent readback and other-result preservation; what-if history and GUI compatibility remain open; [contract](native-saved-simulation.md) |
| Cross-diagram scenario parameters | Experimental source after 0.6 | Explicit correspondence, context/dependency guards, native parameter moves and level-3/4 before/after metrics; explicit native saved-result retirement; inherited execution unaccredited; [contract](native-scenario-migration.md) |
| Native offscreen SVG/PNG | Basic and two-level expanded corpus locally verified | Native child completion, all-level graphical IDs, expanded bounds and transparent corners; rich visual corpus still pending |
| Structural/geometry edits | Partial native acceptance | 22 task/event/gateway variants created/deleted; UserTask/SequenceFlow insertion, reconnection, bounds, colors and descriptions verified; automatic layout remains open |
| Native typography and label styling | Experimental current source | Installed-font inventory, sparse native font/graphics fields, independent persistence/clone comparison, root/nested SVG text properties and external flow-label geometry; internal labels, pool fields and visual semantics remain distinct; [contract](native-styles.md) |
| Native task/gateway type conversion | Experimental current source | All directed pairs among eight tasks and seven gateways plus a nested task, native command and three-process fidelity checks; nondefault content retirement, events and subprocess refactoring remain separate; [contract](native-conversions.md) |
| Native event kind conversion | Experimental source after 0.6 | Same-role start/end/catch/throw/boundary kind changes, installed native command, guarded neutral definitions, context/interruption/attachment protection and independent archive/readback checks. Role conversion, configured payload migration and simulator equivalence remain separate; [contract](native-conversions.md#events-preserve-the-role-replace-neutral-definitions) |
| Task / unbound-call conversion | Experimental source after 0.6 | Eight task types plus a nested task, installed commands, whole-archive checks, explicit attribute applicability, native I/O/event references and call binding/clearing. Reverse call-to-task conversion requires an explicitly unbound source and guarded layout; not extraction, reverse inlining or simulator equivalence; [contract](native-conversions.md#task-to-an-unbound-call) |
| Native containers | Experimental source after 0.4 | Explicit pool/process IDs, stable lanes, milestones, nested embedded subprocess lifecycle and separate expanded bounds; [contract and open families](native-containers.md) |
| Local reusable subprocess calls | Experimental source after 0.4 | Native call targets, nested incoming-reference protection, clone remapping, configured black-box simulation and what-if; [contract](native-calls.md) |
| Embedded-to-reusable subprocess extraction | Experimental current source | Native command at root/nested levels, preserved child identities, rich content relocation and current-user tab remapping; scenario/action migration remains separate; [contract](native-refactoring.md) |
| Local reusable-body inlining | Experimental source after 0.6 | Native call-to-container command plus closed body copy, original shared process/other callers retained, separate conversion/copy archive gates; not recursive expansion, simulation migration or live editing; [contract](native-inlining.md) |
| Explicit native element reparenting | Experimental; same-diagram in 0.6, cross-diagram in later source | Explicit process/subprocess collections, reference closure, native values/files/tab migration, optional root position and whole-archive preservation; explicit mapped scenario migration is available in later source; presentation-action migration requires explicit consent; auto-layout remains open; [contract](native-reparenting.md) |
| Native activity/flow properties | Experimental source after 0.4 | Typed properties, conditional/default flows and derived quantity/reference fidelity; nondefault simulator quantity execution remains unaccredited; [contract](native-semantics.md) |
| Native activity loops | Experimental source after 0.4 | Typed standard/multi-instance configuration, removal and native restart verification; no implied native iteration support; [contract and evidence](native-loops.md) |
| Native events and boundary lifecycle | Experimental source after 0.4 | Explicit catch/throw/boundary creation, interruption, reattachment, guarded activity deletion and native clone remapping; payload editing and simulation behavior remain separate; [contract](native-events.md) |
| Native special subprocesses | Experimental source after 0.4 | Transaction, ad hoc and event-triggered lifecycle, typed properties and context-specific events; read-only native observations are not writable capabilities; [contract](native-subprocesses.md) |
| Native event definition payloads | Experimental source after 0.4 | Unique existing-kind text/code/timer patches and compensation references with exact native fidelity; collection editing and runtime execution remain separate; [contract](native-event-payloads.md) |
| Native data and flow-node I/O | Experimental source after 0.4 | Typed objects/stores, shared references, association-derived activity/event bindings, nested-loader adjustments and clone identity checks; not a general I/O editor or GUI accreditation; [contract](native-data.md) |
| Native content artifacts | Experimental source after 0.4 | Annotation/formatted text, diagram groups, headers and associations with typed native persistence, cloning and explicit geometry; not every artifact or rich-text editor behavior; [contract](native-artifacts.md) |
| Native image artifacts | Experimental, source and 0.6 package verified | Explicit raster/frame inputs, native files and decoded pixels, clone/extraction/deletion; broad rendering and GUI equivalence remain separate; [contract](native-images.md) |
| Native custom artifacts | Experimental, source and 0.6 package verified | Model-owned definitions/instances, nested clone/rendering, `.bca` exchange and native restart; broader catalogs/options remain separate; [contract](native-custom-artifacts.md) |
| Diagram lifecycle and persisted tabs | Experimental source after 0.4 | Creation, name/derived-header change, native cloning with checked ID maps, deletion and ordered/selected diagram/subprocess tabs; [contract and remaining corpus](native-diagrams.md) |
| Extended attributes/attachments editing | Experimental source acceptance after 0.4 | 12 definition/value kinds, two-row table, embedded file/image, lifecycle, rich no-op and unrelated edit; [contract and remaining corpus](native-attributes.md) |
| Visual compatibility | Not accredited | Independent verification inside Modeler |
| Native selection alignment and distribution | Experimental; 0.6 root/nested and rich package circuits verified | Eight modes, actual CEF Promise acknowledgment, independent readback, archive fidelity, no-op, nested attachment/manual-label preservation, active-CEF cancellation and recovery. Source after 0.6 also verifies host-attached boundary translation, manual labels and callback ports. Broader combinations remain separate; not global auto-layout; [contract](native-layout.md) |
| Automatic native surface placement | Experimental source after 0.6; closed root/embedded circuit locally verified | Server-side MSAGL positions and actual grouped native move/router commands, cycles/self-loops/parallel routes, attached timer and manual-label envelopes, fresh-reader and archive gates. Partitions, expanded/cross-owner layout and ambiguous boundary origins reject; global route quality and independent desktop compatibility remain open; [contract](native-surface-layout.md) |
| Automatic represented-diagram layout | Experimental source after 0.6 | Complete selected-diagram MSAGL placement/routing, preserved pool/lane/milestone membership, nested expanded sizes and native resized-host anchors, graphical group membership/margins, manual labels, cardinal/native-verified observed offset ports and cross-pool messages; blank/single/rich native corpus, cancellation and recovery; not every surface, rounded curve or independent desktop compatibility; [contract](native-diagram-layout.md) |
| Live open/unsaved desktop documents | Experimental managed source workflow | Dedicated native editor, independent owner, synchronized property batches, native undo/redo, checkpoint/publication, clean close and recovery; not arbitrary existing-window attachment; [contract](live-sessions.md) |
| Native selection copying | Experimental; 0.6 package verified | Closed selections, explicit destination/position, source-derived closure, exact native record/payload comparison and fresh-process readback; arbitrary selection families and live editing remain separate; [contract](native-selection-copy.md) |
| Newer Modeler versions | Not accredited | Full version-specific contract rerun |

The first implementation includes diagnostics to discover native integration
failures without substituting UI clicks or pretending that XML-only behavior is
native support. Consult operation results; a capability declaration does not
override a failed run.

The local baseline is documented in [validation](validation.md). It is not a
promise about all installations, files, or newer builds. Broad rich preservation,
all native engine-save cancellation boundaries, additional documentation
formats, broader simulation cases and independent visual compatibility still require their own circuits.
Full Modeler automation is **not** complete, and there is no invented overall
completion percentage.
