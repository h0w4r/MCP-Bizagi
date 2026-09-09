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
    // Planning-only native resize: the worker applies real commands to a disposable
    // model. Only independently checked anchor positions can enter a final plan.
    window.__mcpPreviewResizedAnchors = request => {
        const shape = diagrams[0].elementRegistry.get(request.HostId);
        const modeling = diagrams[0].diagram.get('modeling');
        if (!shape || !shape.attachers || !shape.attachers.length || !modeling || typeof modeling.resizeShape !== 'function')
            throw new Error('Installed native anchor resize API or host is unavailable.');
        window.__mcpNativeAnchorPreview = true;
        window.__mcpPreviewHostId = request.HostId;
        try {
            modeling.resizeShape(shape, { x: shape.x, y: shape.y, width: request.Size.Width, height: request.Size.Height },
                undefined, { autoResize: false });
        } finally { window.__mcpNativeAnchorPreview = false; window.__mcpPreviewHostId = null; }
    };
    // On the pinned editor, expanded-size bookkeeping runs at priority 500 and
    // participant/lane/neighbor reflow at 400. The latter may move an attachment
    // twice and detach it. A planning-only resize needs native attachSupport,
    // not a second layout of the surrounding diagram. Stop this lower-priority
    // post-execution phase only for the requested disposable preview host.
    eventBus.on('commandStack.shape.resize.postExecuted', 450, event => {
        if (!window.__mcpAlignmentActive || !window.__mcpNativeAnchorPreview || event.context.shape.id !== window.__mcpPreviewHostId) return;
        window.__mcpLayoutPolicy.suppressedPreviewReflow = { hostId: event.context.shape.id, priority: 450 };
        event.stopPropagation();
    });
    // One installed elements.align command groups all calculated moves and their
    // dependent native routes into the normal single UpdateElementShape callback.
    // This adapter entry point is private to the worker, not an arbitrary script tool.
    window.__mcpApplyCalculatedLayout = positions => {
        const modeling = diagrams[0].diagram.get('modeling');
        if (!modeling || typeof modeling.alignElements !== 'function')
            throw new Error('Installed calculated-move transaction API is unavailable.');
        const changes = positions.map(position => {
            const shape = diagrams[0].elementRegistry.get(position.ElementId);
            if (!shape || shape.host || shape.waypoints || !Number.isFinite(shape.x) || !Number.isFinite(shape.y))
                throw new Error('Calculated layout references an unsupported native node.');
            return { shape, delta: { x: position.X - shape.x, y: position.Y - shape.y } };
        });
        window.__mcpCalculatedLayout = true;
        try { modeling.alignElements(changes); }
        finally { window.__mcpCalculatedLayout = false; }
    };
    // The editor maps zero-participant subprocess DTOs to a transient canvas pool.
    // Retain the actual pre-command alias instead of mistaking it for a .bpm ID.
    window.__mcpLayoutInitial = diagrams[0].elementRegistry.getDiagramElements().map(element => ({
        id: element.id, participantId: element.participantId,
        sourceId: element.source && element.source.id, targetId: element.target && element.target.id,
        hostId: element.host && element.host.id,
        bounds: { x: element.x, y: element.y, width: element.width, height: element.height }
    }));
    window.__mcpLayoutPolicy = { installed: true, suppressedInsertions: [], preservedSubprocessEndpoints: [], preservedBoundaryAttachments: [], acknowledged: false };
    window.__mcpLayoutPolicy.preservedCalculatedOwnership = [];
    window.__mcpLayoutPolicy.calculatedBoundaryDocking = [];
    const boundarySides = new Map();
    for (const element of diagrams[0].elementRegistry.getDiagramElements()) {
        if (!element.host) continue;
        const x = element.x + element.width / 2, y = element.y + element.height / 2, host = element.host;
        const sides = [['top', Math.abs(y - host.y)], ['bottom', Math.abs(y - host.y - host.height)],
            ['left', Math.abs(x - host.x)], ['right', Math.abs(x - host.x - host.width)]].filter(side => side[1] < 0.001);
        if (sides.length === 1) boundarySides.set(element.id, sides[0][0]);
    }
    window.__mcpLayoutPolicy.attachmentTrace = [];
    window.__mcpLayoutPolicy.attachmentTraceObserved = 0;
    window.__mcpLayoutPolicy.attachmentTraceTruncated = false;
    // Keep bounded command evidence for native host/attachment transitions. Reading
    // this context does not issue a command or modify the installed editor.
    for (const command of ['elements.move', 'shape.move', 'element.updateAttachment']) {
        for (const phase of ['preExecute', 'executed', 'postExecuted']) {
            eventBus.on(`commandStack.${command}.${phase}`, 2001, event => {
                if (!window.__mcpAlignmentActive) return;
                const c = event.context;
                window.__mcpLayoutPolicy.attachmentTraceObserved++;
                if (window.__mcpLayoutPolicy.attachmentTrace.length >= 4096) {
                    window.__mcpLayoutPolicy.attachmentTraceTruncated = true;
                    return; // Bounded diagnostics never abort an otherwise valid live operation.
                }
                window.__mcpLayoutPolicy.attachmentTrace.push({ command, phase,
                    shape: c.shape && c.shape.id, host: c.shape && c.shape.host && c.shape.host.id,
                    newHost: c.newHost && c.newHost.id, newParent: c.newParent && c.newParent.id,
                    shapes: c.shapes && c.shapes.map(shape => shape.id), delta: c.delta,
                    bounds: c.shape && { x: c.shape.x, y: c.shape.y, width: c.shape.width, height: c.shape.height }
                });
            });
        }
    }

    // Embedded surfaces use the native zero participant identity. The normal drag
    // behavior otherwise assigns a root-canvas participant from absolute coordinates.
    // Reuse the editor's own move hint to preserve this explicit surface ownership.
    eventBus.on('commandStack.elements.move.preExecute', 2000, event => {
        if (window.__mcpAlignmentActive && (window.__mcpAlignmentSubProcess || window.__mcpCalculatedLayout || window.__mcpNativeAnchorPreview)) {
            event.context.hints = { ...event.context.hints, avoidUpdateParent: true };
        }
    });

    // During grouped calculated moves the native route may temporarily cross a
    // pool while the other endpoint is still at its old position. Its normal
    // geometry-based parent inference is inappropriate for a closed, same-owner
    // transaction. Use the installed hint, not a callback/output ownership fixup.
    for (const command of ['connection.layout', 'connection.updateWaypoints', 'connection.move']) {
        eventBus.on(`commandStack.${command}.preExecute`, 2100, event => {
            if (!window.__mcpAlignmentActive || (!window.__mcpCalculatedLayout && !window.__mcpNativeAnchorPreview)) return;
            event.context.hints = { ...event.context.hints, avoidUpdateParent: true };
            window.__mcpLayoutPolicy.preservedCalculatedOwnership.push({ command, connectionId: event.context.connection.id });
        });
    }

    eventBus.on('commandStack.connection.layout.preExecute', 2050, event => {
        if (!window.__mcpAlignmentActive || (!window.__mcpCalculatedLayout && !window.__mcpNativeAnchorPreview)) return;
        const c = event.context, source = c.connection.source;
        if (!source || !source.host) return;
        const side = boundarySides.get(source.id);
        if (!side) throw new Error('Calculated boundary docking has no unambiguous original host side.');
        // The normal move helper can supply the bottom/right corner of a moved
        // boundary as its docking hint. Feed the native router the actual outward
        // event origin instead. Do not rewrite its returned callback or saved route.
        const point = { x: source.x + source.width / 2, y: source.y + source.height / 2 };
        if (side === 'top') point.y = source.y;
        if (side === 'bottom') point.y = source.y + source.height;
        if (side === 'left') point.x = source.x;
        if (side === 'right') point.x = source.x + source.width;
        c.hints = { ...c.hints, connectionStart: point };
        window.__mcpLayoutPolicy.calculatedBoundaryDocking.push({ connectionId: c.connection.id, side, point });
    });

    // The installed move helper treats a sequence flow whose target is on the
    // root canvas as a request to dock to that canvas. Embedded surfaces put all
    // their direct children there. Preserve the actual target when that specific
    // native hint is emitted, before the native reconnect/router consumes it.
    eventBus.on('commandStack.connection.updateWaypoints.preExecute', 2000, event => {
        if (!window.__mcpAlignmentActive) return;
        const context = event.context, connection = context.connection, hints = context.hints || {};
        if (window.__mcpAlignmentSubProcess && connection.target && hints.target === connection.target.parent && hints.source === connection.source) {
            window.__mcpLayoutPolicy.preservedSubprocessEndpoints.push({
                connectionId: connection.id, sourceId: connection.source.id, targetId: connection.target.id
            });
            hints.target = connection.target;
        }
        // The installed waypoint handler treats an undirected reconnect from a
        // boundary event as a request to undock and recenter that event. Alignment
        // changes neither endpoint nor attachment. Reuse its target-docking hint
        // to keep the existing source anchor while the native router updates points.
        if (connection.source && connection.source.host && hints.source === connection.source && hints.target === connection.target) {
            window.__mcpLayoutPolicy.preservedBoundaryAttachments.push({
                connectionId: connection.id, boundaryId: connection.source.id, hostId: connection.source.host.id
            });
            hints.docking = 'target';
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
