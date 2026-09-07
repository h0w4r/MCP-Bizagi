# Capability ledger

This ledger distinguishes implementation from operational accreditation.

| Family | Status | Evidence required for broader claims |
| --- | --- | --- |
| XML create/read/name edits | Locally verified through MCP | Unicode, stale revision and original backup checks |
| XML structural checks | Implemented, bounded | Complete standards validation is separate |
| Native bootstrap | Experimental diagnostic | Actual native request, not assembly presence |
| Native import/save/reload/export | Locally verified, experimental | Basic process only; warnings expose normalization |
| Native rich-model preservation | Not accredited | Multi-diagram models, nested subprocesses, attributes, attachments, simulation data |
| Existing `.bpm` inspection | Locally verified, copy-only | Source byte equality and actual native identities |
| `.bpm` name batches | Locally verified, copy-only | Task/event batch, source revision and fresh-reader verification |
| Worker cancellation and recovery | Locally verified | Active registration cancellation, exit evidence, subsequent native call |
| Native documentation | Investigated only | Real publisher and readable output |
| Native simulation | Investigated only | Actual engine, scenarios, outputs and repeatability |
| Visual compatibility | Not accredited | Independent verification inside Modeler |
| Newer Modeler versions | Not accredited | Full version-specific contract rerun |

The first implementation includes diagnostics to discover native integration
failures without substituting UI clicks or pretending that XML-only behavior is
native support. Consult operation results; a capability declaration does not
override a failed run.

The local baseline is documented in [validation](validation.md). It is not a
promise about all installations, files, or newer builds. Rich native preservation,
in-flight save cancellation, unexpected-host-exit recovery, and independent visual
compatibility still require their own acceptance circuits.
