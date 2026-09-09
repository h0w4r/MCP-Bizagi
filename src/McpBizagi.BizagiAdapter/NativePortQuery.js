// Read-only adapter around the installed layouter service. No vendor code is
// copied, patched or reimplemented, and no command-stack operation is issued.
(() => {
    const host = document.querySelector('mod-bpmn-canvas'), context = host && host.__ngContext__;
    const views = Array.isArray(context) ? [context, ...context.filter(Array.isArray)] : [];
    const diagrams = [...new Set(views.flat().filter(v => v && v.processDiagram).map(v => v.processDiagram))];
    if (diagrams.length !== 1) throw new Error('No unique installed diagram service');
    const diagram = diagrams[0], registry = diagram.elementRegistry, layouter = diagram.diagram.get('layouter');
    if (!layouter || typeof layouter.layoutConnection !== 'function') throw new Error('Installed layouter unavailable');
    const snapshot = () => JSON.stringify(registry.getDiagramElements().map(e => ({
        id: e.id, type: e.elementType, subtype: e.elementSubtype, parent: e.parent && e.parent.id,
        host: e.host && e.host.id, x: e.x, y: e.y, width: e.width, height: e.height,
        source: e.source && e.source.id, target: e.target && e.target.id,
        points: e.waypoints && e.waypoints.map(p => ({ x: p.x, y: p.y })), properties: e.graphicalElementProperties
    })));
    const before = snapshot();
    window.__mcpPortQueryReceipt = { RegistryUnchanged: false, Observations: [] };
    window.__mcpQueryNativePort = query => {
        const original = registry.get(query.ConnectionId);
        if (!original || !original.source || !original.target || original.source.id !== query.SourceId || original.target.id !== query.TargetId)
            throw new Error('Connection identity differs from native registry');
        if (original.source.host || original.target.host) throw new Error('Host-relative docking is not a rectangle port query');
        // Proposed coordinates affect only detached query envelopes. Endpoint
        // identities, prototypes and native shape metadata come from the registry.
        const endpoint = (e, b) => Object.assign(Object.create(Object.getPrototypeOf(e)), e,
            { x: b.X, y: b.Y, width: b.Width, height: b.Height });
        const start = { x: query.SourcePoint.X, y: query.SourcePoint.Y }, end = { x: query.TargetPoint.X, y: query.TargetPoint.Y };
        const edge = Object.assign(Object.create(Object.getPrototypeOf(original)), original, {
            source: endpoint(original.source, query.SourceBounds), target: endpoint(original.target, query.TargetBounds),
            waypoints: [{ ...start }, { ...end }],
            graphicalElementProperties: { ...original.graphicalElementProperties, sourcePort: true, targetPort: true }
        });
        const errors = [], log = console.error; let route;
        console.error = (...args) => { errors.push(args.map(a => String(a)).join(' ')); log.apply(console, args); };
        try { route = layouter.layoutConnection(edge, { connectionStart: { ...start }, connectionEnd: { ...end }, recalculate: true }); }
        finally { console.error = log; }
        if (snapshot() !== before) throw new Error('Installed port query mutated the registry');
        window.__mcpPortQueryReceipt.Observations.push({ Query: query,
            SourcePort: String(edge.graphicalElementProperties.sourcePort), TargetPort: String(edge.graphicalElementProperties.targetPort),
            Route: (route || []).map(p => ({ X: p.x, Y: p.y })), Errors: errors });
        window.__mcpPortQueryReceipt.RegistryUnchanged = true;
        return true;
    };
})();
