# Capability ledger

This ledger distinguishes implementation from operational accreditation.

| Family | Status | Evidence required for broader claims |
| --- | --- | --- |
| XML create/read/name edits | Implemented | Independent MCP client and durable readback |
| XML structural checks | Implemented, bounded | Complete standards validation is separate |
| Native bootstrap | Experimental diagnostic | Actual native request, not assembly presence |
| Native import/save/reload/export | Experimental diagnostic | Fresh-worker roundtrip and semantic comparisons |
| Native rich-model preservation | Not accredited | Multi-diagram models, nested subprocesses, attributes, attachments, simulation data |
| `.bpm` editing | Not released | Revision handling plus loss-aware native mutation |
| Native documentation | Investigated only | Real publisher and readable output |
| Native simulation | Investigated only | Actual engine, scenarios, outputs and repeatability |
| Visual compatibility | Not accredited | Independent verification inside Modeler |
| Newer Modeler versions | Not accredited | Full version-specific contract rerun |

The first implementation includes diagnostics to discover native integration
failures without substituting UI clicks or pretending that XML-only behavior is
native support. Consult operation results; a capability declaration does not
override a failed run.
