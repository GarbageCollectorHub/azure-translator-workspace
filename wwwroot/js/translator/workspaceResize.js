export function createWorkspaceResize({
    workspaceGrid,
    workspaceResizeHandle
}) {
    let workspaceResizeState = null;

    function workspaceResizeBounds() {
        const minimum = 440;
        const maximum = Math.max(minimum + 260, Math.floor(window.innerHeight * 1.55));
        return { minimum, maximum };
    }

    const workspaceResizeDragMultiplier = 0.98;
    const workspaceResizeEdgeSize = 120;
    const workspaceResizeEdgeMaxScrollPerSecond = 610;

    function updateWorkspaceResizeValue(height) {
        const { minimum, maximum } = workspaceResizeBounds();
        workspaceResizeHandle.setAttribute('aria-valuemin', String(minimum));
        workspaceResizeHandle.setAttribute('aria-valuemax', String(maximum));
        workspaceResizeHandle.setAttribute('aria-valuenow', String(Math.round(height)));
    }

    function resizeWorkspace(height) {
        const { minimum, maximum } = workspaceResizeBounds();
        const nextHeight = Math.min(Math.max(Math.round(height), minimum), maximum);
        workspaceGrid.style.height = `${nextHeight}px`;
        updateWorkspaceResizeValue(nextHeight);
        return nextHeight;
    }

    function workspaceResizeEdgeRatio(clientY) {
        if (clientY > window.innerHeight - workspaceResizeEdgeSize) {
            return (workspaceResizeEdgeSize - (window.innerHeight - clientY)) / workspaceResizeEdgeSize;
        }

        if (clientY < workspaceResizeEdgeSize) {
            return -((workspaceResizeEdgeSize - clientY) / workspaceResizeEdgeSize);
        }

        return 0;
    }

    function resizeWorkspaceFromClientY(clientY) {
        if (!workspaceResizeState) {
            return;
        }

        workspaceResizeState.lastClientY = clientY;
        const dragDelta = window.scrollY + clientY - workspaceResizeState.startPageY;
        resizeWorkspace(workspaceResizeState.startHeight + (dragDelta * workspaceResizeDragMultiplier));
    }

    function runWorkspaceResizeAutoScroll() {
        if (!workspaceResizeState) {
            return;
        }

        const now = window.performance.now();
        const elapsedSeconds = Math.min((now - workspaceResizeState.lastFrameTime) / 1000, 0.05);
        workspaceResizeState.lastFrameTime = now;

        const edgeRatio = workspaceResizeEdgeRatio(workspaceResizeState.lastClientY);
        if (edgeRatio !== 0) {
            const scrollStep = edgeRatio * workspaceResizeEdgeMaxScrollPerSecond * elapsedSeconds;
            window.scrollBy(0, scrollStep);
            resizeWorkspaceFromClientY(workspaceResizeState.lastClientY);
        }

        workspaceResizeState.autoScrollFrame = window.requestAnimationFrame(runWorkspaceResizeAutoScroll);
    }

    function stopWorkspaceResizeAutoScroll() {
        if (workspaceResizeState?.autoScrollFrame) {
            window.cancelAnimationFrame(workspaceResizeState.autoScrollFrame);
            workspaceResizeState.autoScrollFrame = null;
        }
    }

    function finishWorkspaceResize(pointerId = workspaceResizeState?.pointerId, releaseCapture = true) {
        if (!workspaceResizeState) {
            return;
        }

        if (releaseCapture && pointerId !== undefined && workspaceResizeHandle.hasPointerCapture(pointerId)) {
            workspaceResizeHandle.releasePointerCapture(pointerId);
        }

        stopWorkspaceResizeAutoScroll();
        document.body.classList.remove('is-resizing-workspace');
        workspaceResizeState = null;
    }

    function expandImportedWorkspace() {
        if (!window.matchMedia('(min-width: 901px) and (min-height: 820px)').matches || workspaceGrid.style.height) {
            return;
        }

        resizeWorkspace(window.innerHeight * 0.68);
    }

    function beginWorkspaceResize(event) {
        if (event.button !== undefined && event.button !== 0) {
            return;
        }

        event.preventDefault();
        workspaceResizeState = {
            pointerId: event.pointerId,
            startHeight: workspaceGrid.getBoundingClientRect().height,
            startPageY: window.scrollY + event.clientY,
            lastClientY: event.clientY,
            lastFrameTime: window.performance.now(),
            autoScrollFrame: null
        };
        workspaceResizeHandle.setPointerCapture(event.pointerId);
        document.body.classList.add('is-resizing-workspace');
        workspaceResizeState.autoScrollFrame = window.requestAnimationFrame(runWorkspaceResizeAutoScroll);
    }

    function resizeWorkspaceFromPointer(event) {
        if (!workspaceResizeState || workspaceResizeState.pointerId !== event.pointerId) {
            return;
        }

        resizeWorkspaceFromClientY(event.clientY);
    }

    function endWorkspaceResize(event) {
        if (!workspaceResizeState || workspaceResizeState.pointerId !== event.pointerId) {
            return;
        }

        finishWorkspaceResize(event.pointerId);
    }

    function handleWorkspaceResizeLostPointerCapture(event) {
        if (workspaceResizeState?.pointerId === event.pointerId) {
            finishWorkspaceResize(event.pointerId, false);
        }
    }

    function resizeWorkspaceFromKeyboard(event) {
        const currentHeight = workspaceGrid.getBoundingClientRect().height;
        if (event.key === 'ArrowUp') {
            event.preventDefault();
            resizeWorkspace(currentHeight - 48);
        } else if (event.key === 'ArrowDown') {
            event.preventDefault();
            resizeWorkspace(currentHeight + 48);
        } else if (event.key === 'Home') {
            event.preventDefault();
            resizeWorkspace(workspaceResizeBounds().minimum);
        } else if (event.key === 'End') {
            event.preventDefault();
            resizeWorkspace(workspaceResizeBounds().maximum);
        }
    }

    return {
        beginWorkspaceResize,
        endWorkspaceResize,
        expandImportedWorkspace,
        finishWorkspaceResize,
        handleWorkspaceResizeLostPointerCapture,
        resizeWorkspaceFromKeyboard,
        resizeWorkspaceFromPointer,
        updateWorkspaceResizeValue
    };
}
