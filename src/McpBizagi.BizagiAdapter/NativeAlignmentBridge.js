// Version-pinned adapter for the unchanged installed Modeler editor, not a replacement layout engine.
(() => {
    const host = document.querySelector('mod-bpmn-canvas');
    const context = host && host.__ngContext__;
    const views = Array.isArray(context) ? [context, ...context.filter(Array.isArray)] : [];
    const components = views.flat().filter(value => value && value.processDiagram);
    const diagrams = [...new Set(components.map(value => value.processDiagram))];
    if (diagrams.length !== 1 || !diagrams[0].eventBus) {
        throw new Error('Unsupported native editor component contract: no unique process diagram.');
    }
    const eventBus = diagrams[0].eventBus;
    // The editor maps zero-participant subprocess DTOs to a transient canvas pool.
    // Retain the actual pre-command alias instead of mistaking it for a .bpm ID.
    window.__mcpLayoutInitial = diagrams[0].elementRegistry.getDiagramElements().map(element => ({
        id: element.id, participantId: element.participantId,
        sourceId: element.source && element.source.id, targetId: element.target && element.target.id
    }));
    window.__mcpLayoutPolicy = { installed: true, suppressedInsertions: [], preservedSubprocessEndpoints: [], acknowledged: false };

    // Embedded surfaces use the native zero participant identity. The normal drag
    // behavior otherwise assigns a root-canvas participant from absolute coordinates.
    // Reuse the editor's own move hint to preserve this explicit surface ownership.
    eventBus.on('commandStack.elements.move.preExecute', 2000, event => {
        if (window.__mcpAlignmentActive && window.__mcpAlignmentSubProcess) {
            event.context.hints = { ...event.context.hints, avoidUpdateParent: true };
        }
    });

    // The installed move helper treats a sequence flow whose target is on the
    // root canvas as a request to dock to that canvas. Embedded surfaces put all
    // their direct children there. Preserve the actual target when that specific
    // native hint is emitted, before the native reconnect/router consumes it.
    eventBus.on('commandStack.connection.updateWaypoints.preExecute', 2000, event => {
        if (!window.__mcpAlignmentActive || !window.__mcpAlignmentSubProcess) return;
        const context = event.context, connection = context.connection, hints = context.hints || {};
        if (connection.target && hints.target === connection.target.parent && hints.source === connection.source) {
            window.__mcpLayoutPolicy.preservedSubprocessEndpoints.push({
                connectionId: connection.id, sourceId: connection.source.id, targetId: connection.target.id
            });
            hints.target = connection.target;
        }
    });

    // Native insertion sets targetFlow during elements.move.preExecute at priority 1000.
    // A later extension hook clears that optional intent for this geometry-only command.
    // Native alignment, movement and routing remain unchanged; callbacks still undergo
    // independent semantic/archive validation. No vendor asset is modified.
    eventBus.on('commandStack.elements.move.preExecute', 500, event => {
        if (!window.__mcpAlignmentActive || !event.context.targetFlow) return;
        const context = event.context;
        window.__mcpLayoutPolicy.suppressedInsertions.push({
            shapeIds: context.shapes.map(shape => shape.id), flowId: context.targetFlow.id
        });
        delete context.targetFlow;
    });

    // Observe fulfillment of the actual CEF Task, not merely its .NET completion.
    const handler = window.diagramEditorElementsHandler;
    const update = handler.updateElementShape.bind(handler);
    handler.updateElementShape = (...args) => update(...args).then(value => {
        window.__mcpLayoutPolicy.acknowledged = true;
        return value;
    });
})();
