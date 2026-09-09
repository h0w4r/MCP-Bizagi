// Version-pinned instrumentation for the real installed editor. It never replaces
// native editing, simulates input, or exposes a caller-supplied JavaScript endpoint.
(() => {
    if (window.__mcpLiveSynchronizationV1) return window.__mcpLiveSynchronizationV1.begin();
    const host = document.querySelector('mod-bpmn-canvas');
    const context = host && host.__ngContext__;
    const views = Array.isArray(context) ? [context, ...context.filter(Array.isArray)] : [];
    const diagrams = [...new Set(views.flat().filter(v => v && v.processDiagram).map(v => v.processDiagram))];
    if (diagrams.length !== 1 || !diagrams[0].diagram) throw new Error('No unique native live diagram.');
    const diagram = diagrams[0].diagram;
    const direct = diagram.get('directEditing');
    const provider = diagram.get('desktopData')._cefsharpProvider;
    if (!provider || !direct || typeof window.syncPendingChanges !== 'function')
        throw new Error('Native live synchronization contract is unavailable.');
    const state = { version: 1, pending: 0, started: 0, completed: 0, failed: 0, epoch: 0, expected: [] };
    const wrappers = [];
    for (const [group, methods] of [
        ['diagramEditorElementsHandler', ['createElementShape', 'updateElementShape', 'deleteElementShape', 'createSwimlanes', 'deleteSwimlanes', 'fixInvalidElements']],
        ['diagramEditorKeyboardCommandHandler', ['undoChange', 'redoChange']]
    ]) {
        const target = provider[group];
        if (!target) throw new Error('Native Chromium binding is absent: ' + group);
        for (const method of methods) {
            const original = target[method];
            if (typeof original !== 'function') throw new Error('Native Chromium method is absent: ' + method);
            const wrapper = function (...args) {
                state.pending++; state.started++;
                let result;
                try { result = original.apply(target, args); }
                catch (error) { state.pending--; state.failed++; throw error; }
                if (!result || typeof result.then !== 'function') {
                    state.pending--; state.failed++;
                    return result; // Preserve native return semantics, but reject our synchronization evidence.
                }
                result.then(() => { state.pending--; state.completed++; }, () => { state.pending--; state.failed++; });
                return result;
            };
            wrappers.push({ target, method, wrapper, original });
        }
    }
    // Preflight all contracts before changing any binding; roll back a partial
    // installation instead of stacking invisible wrappers on the next attempt.
    try {
        for (const item of wrappers) {
            item.target[item.method] = item.wrapper;
            if (item.target[item.method] !== item.wrapper) throw new Error('Native callback binding is not writable.');
        }
    } catch (error) {
        for (const item of wrappers) if (item.target[item.method] === item.wrapper) item.target[item.method] = item.original;
        throw error;
    }
    diagram.get('eventBus').on('commandStack.changed', () => { state.epoch++; });
    const status = () => {
        if (wrappers.some(w => w.target[w.method] !== w.wrapper)) throw new Error('Native live binding changed during synchronization.');
        return JSON.stringify({ ...state, editing: direct.isActive() });
    };
    window.__mcpLiveSynchronizationV1 = {
        status,
        begin: () => {
            state.expected = [];
            if (direct.isActive()) {
                const active = direct._active;
                const element = active && (active.parentElement || active.element);
                const text = direct.getValue();
                if (!element || typeof element.id !== 'string' || typeof text !== 'string')
                    throw new Error('Pending native label identity cannot be determined.');
                state.expected.push({ id: element.id, name: text.trim() ? text : '' });
            }
            window.syncPendingChanges();
            return status();
        }
    };
    return window.__mcpLiveSynchronizationV1.begin();
})()
