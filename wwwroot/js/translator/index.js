import { createTranslatorApi } from './api.js';
import { readTranslatorPageConfig } from './config.js';
import { getTranslatorElements } from './dom.js';
import { createTranslationHistory } from './history.js';
import { createWorkspaceResize } from './workspaceResize.js';

const pageConfig = readTranslatorPageConfig();

(() => {
    const {
        historyKey = 'azure-translator-history-v1',
        historyLimit = 20,
        maxSelectionLength = 0,
        maxUploadBytes = 0,
        maxUploadSizeLabel = '',
        translateEndpoint = '',
        speechEndpoint = '',
        fileImportEndpoint = '',
        pdfJsModuleUrl = '',
        pdfWorkerUrl = '',
        initialInput = '',
        initialOutput = '',
        detectedLanguage = null
    } = pageConfig;
    const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    const {
        form,
        submitAction,
        translateButton,
        sourceLanguage,
        targetLanguage,
        sourceText,
        speakSource,
        speakTarget,
        sampleText,
        fileText,
        defaultSampleText,
        translateSelected,
        replaceSelected,
        pasteText,
        clearText,
        selectAllText,
        undoReplacement,
        redoReplacement,
        resetSample,
        selectionStatus,
        selectionTranslationPanel,
        selectionTranslationHeader,
        selectionTranslationTime,
        selectionTranslationResult,
        copySelectionTranslation,
        resetSelectionTranslationPanel,
        closeSelectionTranslationPanel,
        workspaceGrid,
        workspaceResizeHandle,
        historyPanel,
        historyToggle,
        historyClose,
        historyList,
        clearHistory,
        translatedText,
        copyTranslation,
        fileInput,
        fileSelectionName,
        fileDropZone,
        importFile,
        fileUpload,
        fileEditor,
        fileImportMessage,
        importedFileName,
        previousSegment,
        nextSegment,
        segmentPosition,
        clearFile,
        fileViewToggle,
        fileViewButtons,
        pdfPreview,
        previousPdfPage,
        nextPdfPage,
        pdfPagePosition,
        pdfPreviewStatus,
        pdfCanvasShell,
        pdfPageStage,
        pdfCanvas,
        pdfTextLayer,
        pdfSelectionPopover,
        pdfSelectionActions,
        pdfSelectionCardTitle,
        translatePdfSelectionButton,
        copyPdfSelectionButton,
        expandPdfSelectionButton,
        resetPdfSelectionLayoutButton,
        closePdfSelectionButton,
        pdfSelectionSource,
        pdfSelectionResult
    } = getTranslatorElements();

    let activeWorkspace = 'sample';
    let selectionTimer = 0;
    let lastSelectionKey = '';
    let importedSegments = [];
    let importedPageCount = null;
    let currentSegmentIndex = 0;
    let selectedFile = null;
    let fileDropDepth = 0;
    let isApplyingProgrammaticTextChange = false;
    let importedPdfFile = null;
    let pdfDocument = null;
    let pdfLoadingTask = null;
    let currentPdfPage = 1;
    let isPdfPreviewActive = false;
    let pdfRenderId = 0;
    let pdfRenderTask = null;
    let pdfResizeTimer = 0;
    let pdfJs = null;
    let activePdfSelectionText = '';
    let isPdfPointerSelectionActive = false;
    let pdfPointerSelectionFrame = 0;
    let pdfPointerSelectionPosition = null;
    let pdfPointerSelectionAnchor = null;
    let activePdfSelectionRect = null;
    let activePdfTranslationText = '';
    let pdfSelectionCardDrag = null;
    let currentSourceDetectedLanguage = detectedLanguage;
    let currentTargetTranslationState = initialOutput
        ? {
            sourceText: initialInput,
            sourceLanguage: sourceLanguage.value,
            targetLanguage: targetLanguage.value,
            translatedText: initialOutput
        }
        : null;
    let speechAudio = null;
    let speechObjectUrl = '';
    let activeSpeechButton = null;
    let activeSelectionTranslationText = '';
    let selectionTranslationPanelDrag = null;

    const replacementHistoryLimit = 20;
    const languagePreferencesKey = 'azure-translator-language-preferences-v1';
    const translationTextareaHeightKey = 'azure-translator-translation-textarea-height-v2';
    const translationTextareaMinimumHeight = 200;
    const translationTextareaMaximumHeight = 560;
    const selectionTranslationPanelLayoutKey = 'azure-translator-selection-translation-panel-layout-v2';
    const selectionTranslationPanelViewportMargin = 16;
    const selectionTranslationPanelDefaultWidth = 520;
    const selectionTranslationPanelDefaultHeight = 260;
    const selectionTranslationPanelMinimumWidth = 360;
    const selectionTranslationPanelMinimumHeight = 190;
    const pdfSelectionPointerTolerance = 48;
    const pdfSelectionCardLayoutKey = 'azure-translator-pdf-selection-card-layout-v1';
    const pdfSelectionCardViewportMargin = 16;
    const pdfSelectionCardDefaultWidth = 500;
    const pdfSelectionCardDefaultHeight = 320;
    const pdfSelectionCardMinimumWidth = 360;
    const pdfSelectionCardMinimumHeight = 220;
    const replacementHistories = new Map();

    const { requestTranslation, requestSpeech } = createTranslatorApi({
        translateEndpoint,
        speechEndpoint,
        antiForgeryToken,
        sourceLanguage,
        targetLanguage
    });

    const {
        addHistory,
        clearHistory: clearTranslationHistory,
        renderHistory
    } = createTranslationHistory({
        historyKey,
        historyLimit,
        historyList
    });

    const {
        beginWorkspaceResize,
        endWorkspaceResize,
        expandImportedWorkspace,
        finishWorkspaceResize,
        handleWorkspaceResizeLostPointerCapture,
        resizeWorkspaceFromKeyboard,
        resizeWorkspaceFromPointer,
        updateWorkspaceResizeValue
    } = createWorkspaceResize({
        workspaceGrid,
        workspaceResizeHandle
    });

    function clampSelectionTranslationPanelValue(value, minimum, maximum) {
        return Math.min(Math.max(value, minimum), maximum);
    }

    function getStoredSelectionTranslationPanelLayout() {
        try {
            const saved = JSON.parse(sessionStorage.getItem(selectionTranslationPanelLayoutKey) || 'null');
            if (!saved || ![saved.leftRatio, saved.topRatio, saved.width, saved.height].every(Number.isFinite)) {
                return null;
            }

            return saved;
        } catch {
            return null;
        }
    }

    function saveSelectionTranslationPanelLayout() {
        if (selectionTranslationPanel.hidden || window.innerWidth <= 900) {
            return;
        }

        const rect = selectionTranslationPanel.getBoundingClientRect();
        try {
            sessionStorage.setItem(selectionTranslationPanelLayoutKey, JSON.stringify({
                leftRatio: rect.left / window.innerWidth,
                topRatio: rect.top / window.innerHeight,
                width: Math.round(rect.width),
                height: Math.round(rect.height)
            }));
        } catch {
            // Layout memory is optional; keep the panel usable without storage.
        }
    }

    function applySelectionTranslationPanelLayout(layout) {
        const maximumWidth = Math.max(
            selectionTranslationPanelMinimumWidth,
            window.innerWidth - (selectionTranslationPanelViewportMargin * 2));
        const maximumHeight = Math.max(
            selectionTranslationPanelMinimumHeight,
            window.innerHeight - (selectionTranslationPanelViewportMargin * 2));
        const width = clampSelectionTranslationPanelValue(
            layout.width,
            selectionTranslationPanelMinimumWidth,
            maximumWidth);
        const height = clampSelectionTranslationPanelValue(
            layout.height,
            selectionTranslationPanelMinimumHeight,
            maximumHeight);
        const left = clampSelectionTranslationPanelValue(
            layout.leftRatio * window.innerWidth,
            selectionTranslationPanelViewportMargin,
            window.innerWidth - width - selectionTranslationPanelViewportMargin);
        const top = clampSelectionTranslationPanelValue(
            layout.topRatio * window.innerHeight,
            selectionTranslationPanelViewportMargin,
            window.innerHeight - height - selectionTranslationPanelViewportMargin);

        selectionTranslationPanel.style.width = `${Math.round(width)}px`;
        selectionTranslationPanel.style.height = `${Math.round(height)}px`;
        selectionTranslationPanel.style.left = `${Math.round(left)}px`;
        selectionTranslationPanel.style.top = `${Math.round(top)}px`;
    }

    function positionDefaultSelectionTranslationPanel() {
        const maximumWidth = Math.max(
            selectionTranslationPanelMinimumWidth,
            window.innerWidth - (selectionTranslationPanelViewportMargin * 2));
        const maximumHeight = Math.max(
            selectionTranslationPanelMinimumHeight,
            window.innerHeight - (selectionTranslationPanelViewportMargin * 2));
        const width = Math.min(selectionTranslationPanelDefaultWidth, maximumWidth);
        const height = Math.min(selectionTranslationPanelDefaultHeight, maximumHeight);
        const left = clampSelectionTranslationPanelValue(
            (window.innerWidth - width) / 2,
            selectionTranslationPanelViewportMargin,
            window.innerWidth - width - selectionTranslationPanelViewportMargin);
        const top = clampSelectionTranslationPanelValue(
            88,
            selectionTranslationPanelViewportMargin,
            window.innerHeight - height - selectionTranslationPanelViewportMargin);

        selectionTranslationPanel.style.width = `${width}px`;
        selectionTranslationPanel.style.height = `${height}px`;
        selectionTranslationPanel.style.left = `${Math.round(left)}px`;
        selectionTranslationPanel.style.top = `${Math.round(top)}px`;
    }

    function clampSelectionTranslationPanelToViewport() {
        const rect = selectionTranslationPanel.getBoundingClientRect();
        applySelectionTranslationPanelLayout({
            leftRatio: rect.left / window.innerWidth,
            topRatio: rect.top / window.innerHeight,
            width: rect.width,
            height: rect.height
        });
    }

    function showSelectionTranslationPanel(translatedTextValue) {
        activeSelectionTranslationText = translatedTextValue;
        selectionTranslationResult.textContent = translatedTextValue;
        selectionTranslationTime.textContent = new Date().toLocaleTimeString();
        const wasHidden = selectionTranslationPanel.hidden;
        selectionTranslationPanel.hidden = false;

        window.requestAnimationFrame(() => {
            if (window.innerWidth <= 900) {
                return;
            }

            if (!wasHidden) {
                clampSelectionTranslationPanelToViewport();
                return;
            }

            const savedLayout = getStoredSelectionTranslationPanelLayout();
            if (savedLayout) {
                applySelectionTranslationPanelLayout(savedLayout);
            } else {
                positionDefaultSelectionTranslationPanel();
            }
        });
    }

    function hideSelectionTranslationPanel() {
        selectionTranslationPanel.hidden = true;
        selectionTranslationPanelDrag = null;
    }

    function resetSelectionTranslationPanelLayout() {
        try {
            sessionStorage.removeItem(selectionTranslationPanelLayoutKey);
        } catch {
            // Ignore storage failures; the visible panel can still be reset.
        }

        if (!selectionTranslationPanel.hidden && window.innerWidth > 900) {
            positionDefaultSelectionTranslationPanel();
        }
    }

    function beginSelectionTranslationPanelDrag(event) {
        if (event.button !== 0 || event.target.closest('button') || window.innerWidth <= 900) {
            return;
        }

        const rect = selectionTranslationPanel.getBoundingClientRect();
        selectionTranslationPanelDrag = {
            pointerId: event.pointerId,
            startClientX: event.clientX,
            startClientY: event.clientY,
            startLeft: rect.left,
            startTop: rect.top
        };
        selectionTranslationHeader.setPointerCapture(event.pointerId);
        event.preventDefault();
    }

    function dragSelectionTranslationPanel(event) {
        if (!selectionTranslationPanelDrag || event.pointerId !== selectionTranslationPanelDrag.pointerId) {
            return;
        }

        const rect = selectionTranslationPanel.getBoundingClientRect();
        const left = clampSelectionTranslationPanelValue(
            selectionTranslationPanelDrag.startLeft + event.clientX - selectionTranslationPanelDrag.startClientX,
            selectionTranslationPanelViewportMargin,
            window.innerWidth - rect.width - selectionTranslationPanelViewportMargin);
        const top = clampSelectionTranslationPanelValue(
            selectionTranslationPanelDrag.startTop + event.clientY - selectionTranslationPanelDrag.startClientY,
            selectionTranslationPanelViewportMargin,
            window.innerHeight - rect.height - selectionTranslationPanelViewportMargin);

        selectionTranslationPanel.style.left = `${Math.round(left)}px`;
        selectionTranslationPanel.style.top = `${Math.round(top)}px`;
    }

    function finishSelectionTranslationPanelDrag(
        pointerId = selectionTranslationPanelDrag?.pointerId,
        releaseCapture = true) {
        if (!selectionTranslationPanelDrag) {
            return;
        }

        if (releaseCapture && pointerId !== undefined && selectionTranslationHeader.hasPointerCapture(pointerId)) {
            selectionTranslationHeader.releasePointerCapture(pointerId);
        }

        selectionTranslationPanelDrag = null;
        saveSelectionTranslationPanelLayout();
    }

    function endSelectionTranslationPanelDrag(event) {
        if (!selectionTranslationPanelDrag || event.pointerId !== selectionTranslationPanelDrag.pointerId) {
            return;
        }

        finishSelectionTranslationPanelDrag(event.pointerId);
    }

    function showNotification(text, type = 'error') {
        const toastEl = document.getElementById('notification-toast');
        const iconEl = document.getElementById('notification-toast-icon');
        const titleEl = document.getElementById('notification-toast-title');

        toastEl.classList.toggle('notification-toast--error', type === 'error');
        toastEl.classList.toggle('notification-toast--warning', type === 'warning');
        iconEl.innerHTML = type === 'warning'
            ? '<i class="bi bi-exclamation-triangle"></i>'
            : '<i class="bi bi-exclamation-circle"></i>';
        titleEl.textContent = type === 'warning' ? 'Attention' : 'Audio';
        document.getElementById('notification-toast-body').textContent = text;
        document.getElementById('notification-toast-time').textContent = new Date().toLocaleTimeString();
        bootstrap.Toast.getOrCreateInstance(toastEl, { autohide: true, delay: 6000 }).show();
    }

    function setSpeechButtonIcon(button, iconClass) {
        button.innerHTML = `<i class="bi ${iconClass}"></i>`;
    }

    function setSpeechButtonLoading(button, isLoading) {
        button.disabled = isLoading;
        button.innerHTML = isLoading
            ? '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span>'
            : '<i class="bi bi-volume-up"></i>';
    }

    function stopSpeech() {
        if (speechAudio) {
            speechAudio.pause();
            speechAudio = null;
        }

        if (speechObjectUrl) {
            URL.revokeObjectURL(speechObjectUrl);
            speechObjectUrl = '';
        }

        if (activeSpeechButton) {
            activeSpeechButton.classList.remove('is-playing');
            setSpeechButtonIcon(activeSpeechButton, 'bi-volume-up');
            activeSpeechButton = null;
        }
    }

    function getLanguageLabel(select, languageCode) {
        if (!languageCode) {
            return 'this language';
        }

        const option = Array.from(select.options)
            .find(item => item.value === languageCode);

        return option?.textContent?.trim() || languageCode;
    }

    function resolveSpeechPayload(target) {
        if (target === 'source') {
            const language = sourceLanguage.value === 'auto'
                ? currentSourceDetectedLanguage
                : sourceLanguage.value;

            return {
                button: speakSource,
                text: sourceText.value.trim(),
                language,
                languageLabel: getLanguageLabel(sourceLanguage, language),
                missingLanguageMessage: 'Translate once before listening to auto-detected text.'
            };
        }

        return {
            button: speakTarget,
            text: translatedText.value.trim(),
            language: targetLanguage.value,
            languageLabel: getLanguageLabel(targetLanguage, targetLanguage.value),
            missingLanguageMessage: 'Choose a target language first.'
        };
    }

    function isTargetTranslationCurrent() {
        return currentTargetTranslationState !== null
            && currentTargetTranslationState.sourceText === sourceText.value
            && currentTargetTranslationState.sourceLanguage === sourceLanguage.value
            && currentTargetTranslationState.targetLanguage === targetLanguage.value
            && currentTargetTranslationState.translatedText === translatedText.value;
    }

    function isSpeechButtonLoading(button) {
        return Boolean(button.querySelector('.spinner-border'));
    }

    function getSourceSpeechTitle(hasSpeechEndpoint, hasSourceText, sourceNeedsDetection) {
        if (!hasSpeechEndpoint) {
            return 'Audio is temporarily unavailable';
        }

        if (!hasSourceText) {
            return 'Enter text to listen';
        }

        return sourceNeedsDetection
            ? 'Translate once before listening to auto-detected text'
            : 'Listen to source';
    }

    function getTargetSpeechTitle(hasSpeechEndpoint, hasTargetText, targetNeedsRefresh) {
        if (!hasSpeechEndpoint) {
            return 'Audio is temporarily unavailable';
        }

        if (!hasTargetText) {
            return 'Translate text before listening';
        }

        return targetNeedsRefresh
            ? 'Translate again before listening to this result'
            : 'Listen to translation';
    }

    function updateSpeechButtons() {
        const hasSpeechEndpoint = Boolean(speechEndpoint);
        const hasSourceText = Boolean(sourceText.value.trim());
        const hasTargetText = Boolean(translatedText.value.trim());
        const sourceNeedsDetection = sourceLanguage.value === 'auto' && !currentSourceDetectedLanguage;
        const targetNeedsRefresh = hasTargetText && !isTargetTranslationCurrent();

        speakSource.disabled = isSpeechButtonLoading(speakSource);
        speakTarget.disabled = isSpeechButtonLoading(speakTarget);

        speakSource.title = getSourceSpeechTitle(hasSpeechEndpoint, hasSourceText, sourceNeedsDetection);
        speakTarget.title = getTargetSpeechTitle(hasSpeechEndpoint, hasTargetText, targetNeedsRefresh);
    }

    function resetSourceDetectedLanguage() {
        if (sourceLanguage.value === 'auto') {
            currentSourceDetectedLanguage = null;
        }
    }

    async function playSpeech(target) {
        const { button, text, language, languageLabel, missingLanguageMessage } = resolveSpeechPayload(target);

        if (speechAudio && activeSpeechButton === button && !speechAudio.paused) {
            stopSpeech();
            updateSpeechButtons();
            return;
        }

        if (!speechEndpoint) {
            showNotification('Audio is temporarily unavailable. Please try again later.');
            return;
        }

        if (!text) {
            showNotification(
                target === 'source' ? 'Enter text to listen.' : 'Translate text before listening.',
                'warning');
            return;
        }

        if (!language || language === 'auto') {
            showNotification(missingLanguageMessage, 'warning');
            return;
        }

        if (target === 'target' && !isTargetTranslationCurrent()) {
            showNotification('Translate again before listening to this result.', 'warning');
            return;
        }

        stopSpeech();
        setSpeechButtonLoading(button, true);

        try {
            const audioBlob = await requestSpeech(text, language);
            speechObjectUrl = URL.createObjectURL(audioBlob);
            speechAudio = new Audio(speechObjectUrl);
            activeSpeechButton = button;
            button.classList.add('is-playing');
            setSpeechButtonIcon(button, 'bi-stop-fill');
            button.disabled = false;

            speechAudio.addEventListener('ended', () => {
                stopSpeech();
                updateSpeechButtons();
            }, { once: true });

            await speechAudio.play();
        } catch (error) {
            stopSpeech();
            const message = error?.status === 404
                ? `Audio is not supported for ${languageLabel} yet.`
                : error instanceof Error ? error.message : 'Audio unavailable';

            showNotification(message, error?.status === 404 ? 'warning' : 'error');
            updateSpeechButtons();
        } finally {
            if (activeSpeechButton !== button) {
                setSpeechButtonLoading(button, false);
            }
        }
    }

    function languageOptionExists(select, value) {
        return Boolean(value) && Array.from(select.options).some(option => option.value === value);
    }

    function loadLanguagePreferences() {
        try {
            return JSON.parse(localStorage.getItem(languagePreferencesKey) || 'null');
        } catch {
            return null;
        }
    }

    function saveLanguagePreferences() {
        try {
            localStorage.setItem(languagePreferencesKey, JSON.stringify({
                sourceLanguage: sourceLanguage.value,
                targetLanguage: targetLanguage.value
            }));
        } catch {
            // Language preferences are a convenience only; the translator should keep working without storage.
        }
    }

    function syncLanguageTextDirection(select, textArea) {
        const selectedOption = select.selectedOptions[0];
        const isRtl = selectedOption?.classList.contains('rtl-text') ?? false;

        textArea.classList.toggle('rtl-text', isRtl);
        textArea.classList.toggle('ltr-text', !isRtl);
    }

    function syncLanguageTextDirections() {
        syncLanguageTextDirection(sourceLanguage, form.elements.text);
        syncLanguageTextDirection(targetLanguage, translatedText);
    }

    function restoreLanguagePreferences() {
        const preferences = loadLanguagePreferences();
        if (!preferences || initialOutput) {
            return;
        }

        const defaultSourceLanguage = sourceLanguage.options[0]?.value ?? 'auto';
        const isDefaultLanguagePair = sourceLanguage.value === defaultSourceLanguage
            && targetLanguage.value === 'uk';

        if (!isDefaultLanguagePair) {
            return;
        }

        if (languageOptionExists(sourceLanguage, preferences.sourceLanguage)) {
            sourceLanguage.value = preferences.sourceLanguage;
        }

        if (languageOptionExists(targetLanguage, preferences.targetLanguage)) {
            targetLanguage.value = preferences.targetLanguage;
        }
    }

    function loadTranslationTextareaHeight() {
        try {
            const value = Number(sessionStorage.getItem(translationTextareaHeightKey));
            return Number.isFinite(value) ? value : null;
        } catch {
            return null;
        }
    }

    function saveTranslationTextareaHeight(height) {
        try {
            sessionStorage.setItem(translationTextareaHeightKey, String(Math.round(height)));
        } catch {
            // Layout preference is optional; keep the translator usable without storage.
        }
    }

    function clampTranslationTextareaHeight(height) {
        return Math.min(
            Math.max(height, translationTextareaMinimumHeight),
            translationTextareaMaximumHeight
        );
    }

    function setTranslationTextareaHeight(height, persist = true) {
        const nextHeight = clampTranslationTextareaHeight(height);
        sourceText.style.height = `${nextHeight}px`;
        translatedText.style.height = `${nextHeight}px`;

        if (persist) {
            saveTranslationTextareaHeight(nextHeight);
        }
    }

    function syncTranslationTextareaHeight(persist = true) {
        setTranslationTextareaHeight(sourceText.getBoundingClientRect().height, persist);
    }

    function initializeTranslationTextareaResize() {
        const savedHeight = loadTranslationTextareaHeight();

        if (savedHeight != null) {
            setTranslationTextareaHeight(savedHeight, false);
        }

        let resizeFrame = 0;
        const scheduleTextareaHeightSync = (persist = true) => {
            cancelAnimationFrame(resizeFrame);
            resizeFrame = requestAnimationFrame(() => syncTranslationTextareaHeight(persist));
        };

        if ('ResizeObserver' in window) {
            const observer = new ResizeObserver(() => {
                scheduleTextareaHeightSync();
            });

            observer.observe(sourceText);
        }

        sourceText.addEventListener('input', () => scheduleTextareaHeightSync(false));
        sourceText.addEventListener('keyup', () => scheduleTextareaHeightSync(false));
        sourceText.addEventListener('mouseup', () => scheduleTextareaHeightSync());
        sourceText.addEventListener('pointerup', () => scheduleTextareaHeightSync());
        sourceText.addEventListener('touchend', () => scheduleTextareaHeightSync());
        window.addEventListener('mouseup', () => scheduleTextareaHeightSync());
    }

    function activeTextArea() {
        return activeWorkspace === 'file' ? fileText : sampleText;
    }

    function activeReplacementHistoryKey() {
        return activeWorkspace === 'file'
            ? `file:${currentSegmentIndex}`
            : 'sample';
    }

    function currentReplacementHistory() {
        const key = activeReplacementHistoryKey();
        if (!replacementHistories.has(key)) {
            replacementHistories.set(key, { undo: [], redo: [] });
        }

        return replacementHistories.get(key);
    }

    function updateUndoRedoControls() {
        const history = currentReplacementHistory();
        undoReplacement.disabled = history.undo.length === 0;
        redoReplacement.disabled = history.redo.length === 0;
    }

    function syncCurrentFileSegment() {
        const currentSegment = importedSegments[currentSegmentIndex];
        if (activeWorkspace === 'file' && currentSegment) {
            currentSegment.text = fileText.value;
        }
    }

    function clearCurrentReplacementHistory() {
        replacementHistories.delete(activeReplacementHistoryKey());
        updateUndoRedoControls();
    }

    function clearReplacementHistories() {
        replacementHistories.clear();
        updateUndoRedoControls();
    }

    function recordReplacement(beforeText, afterText) {
        if (beforeText === afterText) {
            return;
        }

        const history = currentReplacementHistory();
        history.undo.push({ beforeText, afterText });
        if (history.undo.length > replacementHistoryLimit) {
            history.undo.shift();
        }

        history.redo = [];
        updateUndoRedoControls();
    }

    function applyReplacementHistory(direction) {
        const history = currentReplacementHistory();
        const source = direction === 'undo' ? history.undo : history.redo;
        const target = direction === 'undo' ? history.redo : history.undo;
        const change = source.pop();

        if (!change) {
            updateUndoRedoControls();
            return false;
        }

        const textArea = activeTextArea();
        const value = direction === 'undo' ? change.beforeText : change.afterText;

        isApplyingProgrammaticTextChange = true;
        try {
            textArea.value = value;
        } finally {
            isApplyingProgrammaticTextChange = false;
        }
        syncCurrentFileSegment();

        const caretPosition = value.length;
        textArea.focus();
        textArea.setSelectionRange(caretPosition, caretPosition);
        target.push(change);
        updateUndoRedoControls();
        resetSelectionState();
        return true;
    }

    function handleWorkspaceTextInput() {
        syncCurrentFileSegment();

        if (!isApplyingProgrammaticTextChange) {
            clearCurrentReplacementHistory();
        }
    }

    async function pasteFromClipboard() {
        if (!navigator.clipboard?.readText) {
            setSelectionStatus('Clipboard access is unavailable. Use Ctrl+V.');
            return;
        }

        try {
            const pastedText = await navigator.clipboard.readText();
            if (!pastedText) {
                return;
            }

            const textArea = activeTextArea();
            const beforeText = textArea.value;
            const shouldReplaceDefaultSample = activeWorkspace === 'sample'
                && textArea.value === defaultSampleText
                && textArea.selectionStart === textArea.selectionEnd;

            if (shouldReplaceDefaultSample) {
                textArea.select();
            }

            textArea.setRangeText(pastedText, textArea.selectionStart, textArea.selectionEnd, 'end');
            syncCurrentFileSegment();
            recordReplacement(beforeText, textArea.value);
            resetSelectionState();
            textArea.focus();
        } catch {
            setSelectionStatus('Clipboard access is unavailable. Use Ctrl+V.');
        }
    }

    function clearCurrentText() {
        const textArea = activeTextArea();
        const beforeText = textArea.value;

        if (!beforeText) {
            textArea.focus();
            return;
        }

        textArea.value = '';
        syncCurrentFileSegment();
        recordReplacement(beforeText, '');
        resetSelectionState();
        updateSpeechButtons();
        hideSelectionTranslationPanel();
        textArea.focus();
    }

    function setSelectionStatus(message) {
        selectionStatus.textContent = message;
    }

    async function translateSelection(force = false) {
        const textArea = activeTextArea();
        if (!translateSelected.checked || (!force && document.activeElement !== textArea)) {
            return;
        }

        const start = textArea.selectionStart;
        const end = textArea.selectionEnd;
        const selection = textArea.value.substring(start, end);
        const selectedText = selection.trim();

        if (selectedText.length < 2) {
            return;
        }

        if (selectedText.length > maxSelectionLength) {
            setSelectionStatus(`Select up to ${maxSelectionLength.toLocaleString()} characters at a time.`);
            return;
        }

        const selectionKey = `${activeWorkspace}:${start}:${end}:${selectedText}:${sourceLanguage.value}:${targetLanguage.value}:${replaceSelected.checked}`;
        if (selectionKey === lastSelectionKey) {
            return;
        }
        lastSelectionKey = selectionKey;

        setSelectionStatus('Translating...');

        try {
            const result = await requestTranslation(selectedText, 'selection');
            const output = result.text;
            if (replaceSelected.checked) {
                const beforeText = textArea.value;
                const leadingWhitespace = selection.match(/^\s*/)?.[0] ?? '';
                const trailingWhitespace = selection.match(/\s*$/)?.[0] ?? '';
                isApplyingProgrammaticTextChange = true;
                try {
                    textArea.setRangeText(`${leadingWhitespace}${output}${trailingWhitespace}`, start, end, 'end');
                } finally {
                    isApplyingProgrammaticTextChange = false;
                }
                syncCurrentFileSegment();
                recordReplacement(beforeText, textArea.value);
                hideSelectionTranslationPanel();
            } else {
                showSelectionTranslationPanel(output);
            }

            addHistory(selectedText, output, result.detectedLanguage);
            updateSpeechButtons();
            setSelectionStatus('');
        } catch (error) {
            setSelectionStatus(error instanceof Error ? error.message : 'Translation unavailable');
        }
    }

    function scheduleSelectionTranslation() {
        clearTimeout(selectionTimer);
        selectionTimer = window.setTimeout(() => translateSelection(), 650);
    }

    function resetSelectionState() {
        lastSelectionKey = '';
        setSelectionStatus('');
    }

    function setActiveWorkspace(workspace) {
        activeWorkspace = workspace;
        document.getElementById('sample-workspace').hidden = workspace !== 'sample';
        document.getElementById('file-workspace').hidden = workspace !== 'file';
        resetSample.hidden = workspace !== 'sample';

        document.querySelectorAll('[data-workspace-tab]').forEach(button => {
            const isActive = button.dataset.workspaceTab === workspace;
            button.classList.toggle('is-active', isActive);
            button.setAttribute('aria-selected', String(isActive));
        });

        resetSelectionState();
        updateUndoRedoControls();
    }

    function setHistoryOpen(isOpen) {
        historyPanel.hidden = !isOpen;
        workspaceGrid.classList.toggle('is-history-open', isOpen);
        historyToggle.setAttribute('aria-expanded', String(isOpen));

        window.requestAnimationFrame(() => {
            if (fileEditor.classList.contains('is-split-view') && !canUseSplitFileView()) {
                setFileView('text');
            }
        });
    }

    function showFileImportMessage(message, isError = false) {
        fileImportMessage.textContent = message;
        fileImportMessage.hidden = !message;
        fileImportMessage.classList.toggle('is-error', Boolean(message) && isError);
    }

    function renderCurrentSegment() {
        const segment = importedSegments[currentSegmentIndex];
        if (!segment) {
            return;
        }

        fileText.value = segment.text;
        if (Number.isInteger(segment.sourcePage)) {
            const pageCountSuffix = Number.isInteger(importedPageCount)
                ? ` of ${importedPageCount}`
                : '';
            const partSuffix = segment.partCount > 1
                ? `, part ${segment.partNumber} of ${segment.partCount}`
                : '';
            segmentPosition.textContent = `Page ${segment.sourcePage}${pageCountSuffix}${partSuffix}`;
        } else {
            segmentPosition.textContent = `Section ${currentSegmentIndex + 1} of ${importedSegments.length}`;
        }
        previousSegment.disabled = currentSegmentIndex === 0;
        nextSegment.disabled = currentSegmentIndex === importedSegments.length - 1;
        resetSelectionState();
        updateUndoRedoControls();

        if (isPdfPreviewActive && Number.isInteger(segment.sourcePage)) {
            void renderPdfPage(segment.sourcePage);
        }
    }

    function resetPendingFile() {
        selectedFile = null;
        fileInput.value = '';
        fileSelectionName.textContent = '';
        fileSelectionName.hidden = true;
        importFile.disabled = true;
    }

    function getFileExtension(fileName) {
        const separator = fileName.lastIndexOf('.');
        return separator === -1 ? '' : fileName.substring(separator + 1).toLowerCase();
    }

    function isPdfFile(file) {
        return getFileExtension(file?.name ?? '') === 'pdf';
    }

    function setFileView(view) {
        const canSplitFileView = canUseSplitFileView();
        const selectedView = view === 'split' && !canSplitFileView ? 'text' : view;
        const shouldShowPreview = (selectedView === 'preview' || selectedView === 'split') && importedPdfFile !== null;
        const shouldShowSplit = selectedView === 'split' && shouldShowPreview;
        isPdfPreviewActive = shouldShowPreview;
        fileEditor.classList.toggle('is-split-view', shouldShowSplit);
        fileText.hidden = shouldShowPreview && !shouldShowSplit;
        pdfPreview.hidden = !shouldShowPreview;

        fileViewButtons.forEach(button => {
            const isActive = button.dataset.fileView === (shouldShowPreview ? selectedView : 'text');
            button.classList.toggle('is-active', isActive);
            button.setAttribute('aria-selected', String(isActive));
        });

        if (shouldShowPreview) {
            const segment = importedSegments[currentSegmentIndex];
            void renderPdfPage(segment?.sourcePage ?? currentPdfPage);
        }
    }

    function canUseSplitFileView() {
        return window.matchMedia('(min-width: 1200px)').matches && fileEditor.clientWidth >= 980;
    }

    function resetPdfPreview() {
        pdfRenderId++;
        hidePdfSelectionPopover();

        try {
            pdfRenderTask?.cancel();
        } catch {
            // The render task may already be complete.
        }

        try {
            pdfLoadingTask?.destroy();
        } catch {
            // The loading task may already be settled.
        }

        try {
            pdfDocument?.destroy();
        } catch {
            // The document may already be released.
        }

        importedPdfFile = null;
        pdfDocument = null;
        pdfLoadingTask = null;
        pdfRenderTask = null;
        currentPdfPage = 1;
        isPdfPreviewActive = false;
        pdfPreview.hidden = true;
        pdfPreviewStatus.textContent = '';
        pdfPagePosition.textContent = 'Page 1';
        previousPdfPage.disabled = true;
        nextPdfPage.disabled = true;
        pdfCanvas.width = 0;
        pdfCanvas.height = 0;
        pdfCanvas.style.width = '';
        pdfCanvas.style.height = '';
        pdfPageStage.style.width = '';
        pdfPageStage.style.height = '';
        pdfTextLayer.replaceChildren();
    }

    async function loadPdfJs() {
        if (!pdfJs) {
            pdfJs = await import(pdfJsModuleUrl);
            pdfJs.GlobalWorkerOptions.workerSrc = pdfWorkerUrl;
        }

        return pdfJs;
    }

    async function loadPdfDocument() {
        if (pdfDocument) {
            return pdfDocument;
        }

        if (!importedPdfFile) {
            return null;
        }

        try {
            const pdfjs = await loadPdfJs();
            const fileData = new Uint8Array(await importedPdfFile.arrayBuffer());
            pdfLoadingTask = pdfjs.getDocument({ data: fileData });
            pdfDocument = await pdfLoadingTask.promise;
            return pdfDocument;
        } catch {
            pdfPreviewStatus.textContent = 'Preview is unavailable for this PDF.';
            return null;
        }
    }

    function isPdfTextNode(node) {
        const element = node?.nodeType === Node.ELEMENT_NODE ? node : node?.parentElement;
        return Boolean(element && pdfTextLayer.contains(element));
    }

    function getPdfTextSpan(node) {
        const element = node?.nodeType === Node.ELEMENT_NODE ? node : node?.parentElement;
        return element?.closest?.('.pdf-text-layer span:not(.markedContent)') ?? null;
    }

    function beginPdfPointerSelection(event) {
        if (event.button !== 0) {
            return;
        }

        const anchorSpan = getPdfTextSpan(event.target)
            ?? findClosestPdfTextSpan(event.clientX, event.clientY);
        const anchorNode = anchorSpan?.firstChild;
        const selection = window.getSelection();

        if (!anchorNode || !selection || typeof selection.setBaseAndExtent !== 'function') {
            isPdfPointerSelectionActive = false;
            pdfPointerSelectionAnchor = null;
            return;
        }

        // PDF.js spans have gaps between glyphs. Anchor the drag to the nearest text span
        // so a small miss beside a line cannot reuse an old browser selection range.
        event.preventDefault();
        isPdfPointerSelectionActive = true;
        pdfPointerSelectionAnchor = {
            node: anchorNode,
            offset: getPdfTextOffsetAtPointer(anchorSpan, event.clientX, event.clientY)
        };
        selection.removeAllRanges();
        selection.setBaseAndExtent(
            pdfPointerSelectionAnchor.node,
            pdfPointerSelectionAnchor.offset,
            pdfPointerSelectionAnchor.node,
            pdfPointerSelectionAnchor.offset);

        // A compact helper follows the current selection. The expanded card is user-owned
        // and must stay open while a new PDF fragment is being selected.
        if (!isPdfSelectionCardExpanded()) {
            hidePdfSelectionPopover();
        }
    }

    function distanceToPdfTextRect(rect, clientX, clientY) {
        const horizontalDistance = Math.max(rect.left - clientX, 0, clientX - rect.right);
        const verticalDistance = Math.max(rect.top - clientY, 0, clientY - rect.bottom);
        return Math.hypot(horizontalDistance, verticalDistance);
    }

    function findClosestPdfTextSpan(clientX, clientY) {
        let closest = null;

        for (const span of pdfTextLayer.querySelectorAll('span:not(.markedContent)')) {
            if (!span.textContent?.trim()) {
                continue;
            }

            const distance = distanceToPdfTextRect(span.getBoundingClientRect(), clientX, clientY);
            if (!closest || distance < closest.distance) {
                closest = { span, distance };
            }
        }

        return closest?.distance <= pdfSelectionPointerTolerance ? closest.span : null;
    }

    function getPdfTextOffsetAtPointer(span, clientX, clientY) {
        const caretPosition = document.caretPositionFromPoint?.(clientX, clientY);
        if (caretPosition?.offsetNode && span.contains(caretPosition.offsetNode)) {
            return caretPosition.offset;
        }

        const caretRange = document.caretRangeFromPoint?.(clientX, clientY);
        if (caretRange?.startContainer && span.contains(caretRange.startContainer)) {
            return caretRange.startOffset;
        }

        const rect = span.getBoundingClientRect();
        return clientX < rect.left + rect.width / 2 ? 0 : span.textContent.length;
    }

    function normalisePdfSelectionEndpoint(event) {
        if (!isPdfPointerSelectionActive || !pdfPointerSelectionAnchor) {
            return false;
        }

        const selection = window.getSelection();
        if (!selection || typeof selection.setBaseAndExtent !== 'function') {
            return false;
        }

        const endpointSpan = findClosestPdfTextSpan(event.clientX, event.clientY);
        const endpointNode = endpointSpan?.firstChild;
        if (!endpointNode) {
            return false;
        }

        selection.setBaseAndExtent(
            pdfPointerSelectionAnchor.node,
            pdfPointerSelectionAnchor.offset,
            endpointNode,
            getPdfTextOffsetAtPointer(endpointSpan, event.clientX, event.clientY));
        return true;
    }

    function updatePdfPointerSelection(event) {
        if (!isPdfPointerSelectionActive || (event.buttons & 1) === 0) {
            return;
        }

        pdfPointerSelectionPosition = {
            clientX: event.clientX,
            clientY: event.clientY
        };

        if (pdfPointerSelectionFrame) {
            return;
        }

        pdfPointerSelectionFrame = window.requestAnimationFrame(() => {
            pdfPointerSelectionFrame = 0;

            if (!isPdfPointerSelectionActive || !pdfPointerSelectionPosition) {
                return;
            }

            // Browser selection can fall through absolutely positioned PDF spans into distant DOM text.
            // Keep the endpoint beside the actual pointer while the user is dragging.
            normalisePdfSelectionEndpoint(pdfPointerSelectionPosition);
        });
    }

    function finishPdfPointerSelection(event) {
        const releasedInsidePopover = pdfSelectionPopover.contains(event.target);
        const didNormalise = normalisePdfSelectionEndpoint(event);
        isPdfPointerSelectionActive = false;
        pdfPointerSelectionPosition = null;
        pdfPointerSelectionAnchor = null;

        if (pdfPointerSelectionFrame) {
            window.cancelAnimationFrame(pdfPointerSelectionFrame);
            pdfPointerSelectionFrame = 0;
        }

        if (!releasedInsidePopover && (didNormalise || getPdfTextSelection())) {
            schedulePdfSelectionPopover();
        }
    }

    function getPdfTextSelection() {
        const selection = window.getSelection();
        if (!selection || selection.isCollapsed || selection.rangeCount === 0
            || !isPdfTextNode(selection.anchorNode) || !isPdfTextNode(selection.focusNode)) {
            return null;
        }

        const text = selection.toString().replace(/\s+/g, ' ').trim();
        if (text.length < 2) {
            return null;
        }

        const range = selection.getRangeAt(0);
        const rect = Array.from(range.getClientRects()).find(candidate => candidate.width > 0 && candidate.height > 0)
            ?? range.getBoundingClientRect();

        return rect.width > 0 && rect.height > 0 ? { text, rect } : null;
    }

    function hidePdfSelectionPopover(clearBrowserSelection = false, force = false) {
        if (isPdfSelectionCardExpanded() && !force) {
            if (clearBrowserSelection) {
                window.getSelection()?.removeAllRanges();
            }
            return;
        }

        activePdfSelectionText = '';
        activePdfTranslationText = '';
        activePdfSelectionRect = null;
        resetPdfSelectionCardPresentation();
        pdfSelectionPopover.hidden = true;
        pdfSelectionResult.hidden = true;
        pdfSelectionResult.textContent = '';

        if (clearBrowserSelection) {
            window.getSelection()?.removeAllRanges();
        }
    }

    function showPdfSelectionPopover(selection) {
        const keepExpandedCard = isPdfSelectionCardExpanded() && !pdfSelectionPopover.hidden;

        if (!keepExpandedCard) {
            resetPdfSelectionCardPresentation();
        }

        activePdfSelectionText = selection.text;
        activePdfTranslationText = '';
        activePdfSelectionRect = selection.rect;
        pdfSelectionSource.textContent = selection.text;
        pdfSelectionResult.hidden = !keepExpandedCard;
        pdfSelectionResult.textContent = keepExpandedCard
            ? 'New selection is ready. Choose Translate to update this card.'
            : '';
        updatePdfSelectionCopyButton();
        updatePdfSelectionExpandButton();
        pdfSelectionPopover.hidden = false;

        if (!keepExpandedCard) {
            positionPdfSelectionPopover();
        }
    }

    function isPdfSelectionCardExpanded() {
        return pdfSelectionPopover.classList.contains('is-expanded');
    }

    function resetPdfSelectionCardPresentation() {
        pdfSelectionPopover.classList.remove('is-expanded');
        pdfSelectionPopover.style.width = '';
        pdfSelectionPopover.style.height = '';
        pdfSelectionCardDrag = null;
        pdfSelectionCardTitle.hidden = true;
        pdfSelectionSource.hidden = true;
        resetPdfSelectionLayoutButton.hidden = true;
        updatePdfSelectionExpandButton();
    }

    function updatePdfSelectionCopyButton() {
        const copiesTranslation = Boolean(activePdfTranslationText);
        const label = copiesTranslation ? 'Copy translation' : 'Copy selected text';
        copyPdfSelectionButton.title = label;
        copyPdfSelectionButton.setAttribute('aria-label', label);
    }

    function updatePdfSelectionExpandButton() {
        const expanded = isPdfSelectionCardExpanded();
        const showLabel = !expanded && Boolean(activePdfTranslationText);
        const label = expanded ? 'Collapse translation' : 'Expand translation';

        expandPdfSelectionButton.title = label;
        expandPdfSelectionButton.setAttribute('aria-label', label);
        expandPdfSelectionButton.setAttribute('aria-expanded', String(expanded));
        expandPdfSelectionButton.classList.toggle('has-label', showLabel);
        expandPdfSelectionButton.innerHTML = expanded
            ? '<i class="bi bi-arrows-angle-contract" aria-hidden="true"></i>'
            : `<i class="bi bi-arrows-angle-expand" aria-hidden="true"></i>${showLabel ? '<span>Expand</span>' : ''}`;
    }

    function clampPdfSelectionCardValue(value, minimum, maximum) {
        return Math.min(Math.max(value, minimum), maximum);
    }

    function getStoredPdfSelectionCardLayout() {
        try {
            const saved = JSON.parse(localStorage.getItem(pdfSelectionCardLayoutKey) || 'null');
            if (!saved || ![saved.leftRatio, saved.topRatio, saved.width, saved.height].every(Number.isFinite)) {
                return null;
            }

            return saved;
        } catch {
            return null;
        }
    }

    function savePdfSelectionCardLayout() {
        if (!isPdfSelectionCardExpanded() || pdfSelectionPopover.hidden || window.innerWidth <= 900) {
            return;
        }

        const rect = pdfSelectionPopover.getBoundingClientRect();
        localStorage.setItem(pdfSelectionCardLayoutKey, JSON.stringify({
            leftRatio: rect.left / window.innerWidth,
            topRatio: rect.top / window.innerHeight,
            width: Math.round(rect.width),
            height: Math.round(rect.height)
        }));
    }

    function applyPdfSelectionCardLayout(layout) {
        const maximumWidth = Math.max(pdfSelectionCardMinimumWidth, window.innerWidth - (pdfSelectionCardViewportMargin * 2));
        const maximumHeight = Math.max(pdfSelectionCardMinimumHeight, window.innerHeight - (pdfSelectionCardViewportMargin * 2));
        const width = clampPdfSelectionCardValue(layout.width, pdfSelectionCardMinimumWidth, maximumWidth);
        const height = clampPdfSelectionCardValue(layout.height, pdfSelectionCardMinimumHeight, maximumHeight);
        const left = clampPdfSelectionCardValue(
            layout.leftRatio * window.innerWidth,
            pdfSelectionCardViewportMargin,
            window.innerWidth - width - pdfSelectionCardViewportMargin);
        const top = clampPdfSelectionCardValue(
            layout.topRatio * window.innerHeight,
            pdfSelectionCardViewportMargin,
            window.innerHeight - height - pdfSelectionCardViewportMargin);

        pdfSelectionPopover.style.width = `${Math.round(width)}px`;
        pdfSelectionPopover.style.height = `${Math.round(height)}px`;
        pdfSelectionPopover.style.left = `${Math.round(left)}px`;
        pdfSelectionPopover.style.top = `${Math.round(top)}px`;
    }

    function clampPdfSelectionCardToViewport() {
        const rect = pdfSelectionPopover.getBoundingClientRect();
        applyPdfSelectionCardLayout({
            leftRatio: rect.left / window.innerWidth,
            topRatio: rect.top / window.innerHeight,
            width: rect.width,
            height: rect.height
        });
    }

    function positionExpandedPdfSelectionCard() {
        const maximumWidth = Math.max(pdfSelectionCardMinimumWidth, window.innerWidth - (pdfSelectionCardViewportMargin * 2));
        const maximumHeight = Math.max(pdfSelectionCardMinimumHeight, window.innerHeight - (pdfSelectionCardViewportMargin * 2));
        const width = Math.min(pdfSelectionCardDefaultWidth, maximumWidth);
        const height = Math.min(pdfSelectionCardDefaultHeight, maximumHeight);
        const sourceRect = activePdfSelectionRect ?? {
            left: pdfSelectionCardViewportMargin,
            bottom: pdfSelectionCardViewportMargin
        };
        const left = clampPdfSelectionCardValue(
            sourceRect.left,
            pdfSelectionCardViewportMargin,
            window.innerWidth - width - pdfSelectionCardViewportMargin);
        const top = clampPdfSelectionCardValue(
            sourceRect.bottom + 10,
            pdfSelectionCardViewportMargin,
            window.innerHeight - height - pdfSelectionCardViewportMargin);

        pdfSelectionPopover.style.width = `${width}px`;
        pdfSelectionPopover.style.height = `${height}px`;
        pdfSelectionPopover.style.left = `${Math.round(left)}px`;
        pdfSelectionPopover.style.top = `${Math.round(top)}px`;
    }

    function expandPdfSelectionCard() {
        if (pdfSelectionPopover.hidden || isPdfSelectionCardExpanded()) {
            return;
        }

        pdfSelectionPopover.classList.add('is-expanded');
        pdfSelectionCardTitle.hidden = false;
        pdfSelectionSource.hidden = false;
        resetPdfSelectionLayoutButton.hidden = false;
        updatePdfSelectionExpandButton();

        window.requestAnimationFrame(() => {
            const savedLayout = getStoredPdfSelectionCardLayout();
            if (savedLayout && window.innerWidth > 900) {
                applyPdfSelectionCardLayout(savedLayout);
            } else {
                positionExpandedPdfSelectionCard();
            }
        });
    }

    function collapsePdfSelectionCard() {
        if (!isPdfSelectionCardExpanded()) {
            return;
        }

        savePdfSelectionCardLayout();
        resetPdfSelectionCardPresentation();
        positionPdfSelectionPopover();
    }

    function resetPdfSelectionCardLayout() {
        localStorage.removeItem(pdfSelectionCardLayoutKey);
        if (isPdfSelectionCardExpanded()) {
            positionExpandedPdfSelectionCard();
        }
    }

    function beginPdfSelectionCardDrag(event) {
        if (!isPdfSelectionCardExpanded() || event.button !== 0 || event.target.closest('button') || window.innerWidth <= 900) {
            return;
        }

        const rect = pdfSelectionPopover.getBoundingClientRect();
        pdfSelectionCardDrag = {
            pointerId: event.pointerId,
            startClientX: event.clientX,
            startClientY: event.clientY,
            startLeft: rect.left,
            startTop: rect.top
        };
        pdfSelectionActions.setPointerCapture(event.pointerId);
        event.preventDefault();
    }

    function dragPdfSelectionCard(event) {
        if (!pdfSelectionCardDrag || event.pointerId !== pdfSelectionCardDrag.pointerId) {
            return;
        }

        const rect = pdfSelectionPopover.getBoundingClientRect();
        const left = clampPdfSelectionCardValue(
            pdfSelectionCardDrag.startLeft + event.clientX - pdfSelectionCardDrag.startClientX,
            pdfSelectionCardViewportMargin,
            window.innerWidth - rect.width - pdfSelectionCardViewportMargin);
        const top = clampPdfSelectionCardValue(
            pdfSelectionCardDrag.startTop + event.clientY - pdfSelectionCardDrag.startClientY,
            pdfSelectionCardViewportMargin,
            window.innerHeight - rect.height - pdfSelectionCardViewportMargin);

        pdfSelectionPopover.style.left = `${Math.round(left)}px`;
        pdfSelectionPopover.style.top = `${Math.round(top)}px`;
    }

    function finishPdfSelectionCardDrag(pointerId = pdfSelectionCardDrag?.pointerId, releaseCapture = true) {
        if (!pdfSelectionCardDrag) {
            return;
        }

        if (releaseCapture && pointerId !== undefined && pdfSelectionActions.hasPointerCapture(pointerId)) {
            pdfSelectionActions.releasePointerCapture(pointerId);
        }

        pdfSelectionCardDrag = null;
        savePdfSelectionCardLayout();
    }

    function endPdfSelectionCardDrag(event) {
        if (!pdfSelectionCardDrag || event.pointerId !== pdfSelectionCardDrag.pointerId) {
            return;
        }

        finishPdfSelectionCardDrag(event.pointerId);
    }

    function positionPdfSelectionPopover() {
        if (!activePdfSelectionRect || pdfSelectionPopover.hidden || isPdfSelectionCardExpanded()) {
            return;
        }

        const margin = 12;
        const preferredTop = activePdfSelectionRect.bottom + 10;
        const maxLeft = window.innerWidth - pdfSelectionPopover.offsetWidth - margin;
        const maxTop = window.innerHeight - pdfSelectionPopover.offsetHeight - margin;
        const topAbove = activePdfSelectionRect.top - pdfSelectionPopover.offsetHeight - 10;
        const left = Math.max(margin, Math.min(activePdfSelectionRect.left, maxLeft));
        const top = preferredTop <= maxTop
            ? preferredTop
            : Math.max(margin, topAbove);

        pdfSelectionPopover.style.left = `${left}px`;
        pdfSelectionPopover.style.top = `${top}px`;
    }

    function schedulePdfSelectionPopover() {
        window.setTimeout(() => {
            const selection = getPdfTextSelection();
            if (!selection) {
                hidePdfSelectionPopover();
                return;
            }

            if (selection.text.length > maxSelectionLength) {
                hidePdfSelectionPopover();
                pdfPreviewStatus.textContent = `Select up to ${maxSelectionLength.toLocaleString()} characters at a time.`;
                return;
            }

            pdfPreviewStatus.textContent = '';
            showPdfSelectionPopover(selection);
        }, 0);
    }

    async function copyText(text) {
        if (!text) {
            return false;
        }

        try {
            if (!navigator.clipboard?.writeText) {
                throw new Error('Clipboard API is unavailable.');
            }

            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            const fallback = document.createElement('textarea');
            fallback.value = text;
            fallback.setAttribute('readonly', '');
            fallback.style.position = 'fixed';
            fallback.style.opacity = '0';
            document.body.appendChild(fallback);
            fallback.select();
            const copied = document.execCommand('copy');
            fallback.remove();
            return copied;
        }
    }

    async function translatePdfSelection() {
        if (!activePdfSelectionText || activePdfSelectionText.length > maxSelectionLength) {
            return;
        }

        translatePdfSelectionButton.disabled = true;
        pdfSelectionResult.hidden = false;
        pdfSelectionResult.textContent = 'Translating...';
        positionPdfSelectionPopover();

        try {
            const result = await requestTranslation(activePdfSelectionText, 'pdf-selection');
            pdfSelectionResult.textContent = result.text;
            activePdfTranslationText = result.text;
            updatePdfSelectionCopyButton();
            updatePdfSelectionExpandButton();
            addHistory(activePdfSelectionText, result.text, result.detectedLanguage);
            updateSpeechButtons();
        } catch (error) {
            pdfSelectionResult.textContent = error instanceof Error ? error.message : 'Translation unavailable';
        } finally {
            translatePdfSelectionButton.disabled = false;
            positionPdfSelectionPopover();
        }
    }

    async function renderPdfTextLayer(page, viewport, renderId) {
        pdfTextLayer.replaceChildren();
        const textContent = await page.getTextContent();

        if (renderId !== pdfRenderId) {
            return false;
        }

        if (!textContent.items.some(item => item.str?.trim())) {
            pdfPreviewStatus.textContent = 'No selectable text found on this page.';
            return false;
        }

        const pdfjs = await loadPdfJs();
        const textLayer = new pdfjs.TextLayer({
            textContentSource: textContent,
            container: pdfTextLayer,
            viewport
        });
        await textLayer.render();
        return renderId === pdfRenderId;
    }

    async function renderPdfPage(pageNumber) {
        if (!isPdfPreviewActive) {
            return;
        }

        const renderId = ++pdfRenderId;
        try {
            pdfRenderTask?.cancel();
        } catch {
            // The render task may already be complete.
        }
        pdfPreviewStatus.textContent = 'Loading preview...';
        const documentProxy = await loadPdfDocument();
        if (!documentProxy || renderId !== pdfRenderId) {
            return;
        }

        const normalisedPage = Math.min(Math.max(Number(pageNumber) || 1, 1), documentProxy.numPages);

        try {
            const page = await documentProxy.getPage(normalisedPage);
            if (renderId !== pdfRenderId) {
                return;
            }

            const baseViewport = page.getViewport({ scale: 1 });
            const availableWidth = Math.max(pdfCanvasShell.clientWidth - 36, 280);
            const displayScale = Math.min(1.75, Math.max(0.7, availableWidth / baseViewport.width));
            const outputScale = Math.min(window.devicePixelRatio || 1, 2);
            const displayViewport = page.getViewport({ scale: displayScale });
            const displayWidth = Math.floor(displayViewport.width);
            const displayHeight = Math.floor(displayViewport.height);
            const context = pdfCanvas.getContext('2d', { alpha: false });

            if (!context) {
                throw new Error('Canvas is unavailable.');
            }

            pdfCanvas.width = Math.ceil(displayViewport.width * outputScale);
            pdfCanvas.height = Math.ceil(displayViewport.height * outputScale);
            pdfCanvas.style.width = `${displayWidth}px`;
            pdfCanvas.style.height = `${displayHeight}px`;
            pdfPageStage.style.width = `${displayWidth}px`;
            pdfPageStage.style.height = `${displayHeight}px`;
            pdfTextLayer.style.width = `${displayWidth}px`;
            pdfTextLayer.style.height = `${displayHeight}px`;
            pdfTextLayer.style.setProperty('--total-scale-factor', String(displayViewport.scale));
            pdfTextLayer.replaceChildren();
            // Canvas uses a high-DPI transform while TextLayer keeps the same viewport as the visible page.
            // This keeps text selection coordinates aligned with the rendered PDF at every display scale.
            const renderTask = page.render({
                canvasContext: context,
                transform: outputScale === 1 ? null : [outputScale, 0, 0, outputScale, 0, 0],
                viewport: displayViewport
            });
            pdfRenderTask = renderTask;
            await renderTask.promise;

            if (renderId !== pdfRenderId) {
                return;
            }

            pdfRenderTask = null;
            currentPdfPage = normalisedPage;
            pdfPagePosition.textContent = `Page ${normalisedPage} of ${documentProxy.numPages}`;
            previousPdfPage.disabled = normalisedPage <= 1;
            nextPdfPage.disabled = normalisedPage >= documentProxy.numPages;
            try {
                const renderedTextLayer = await renderPdfTextLayer(page, displayViewport, renderId);
                if (renderedTextLayer) {
                    pdfPreviewStatus.textContent = 'Select text in the preview to translate it.';
                }
            } catch {
                pdfTextLayer.replaceChildren();
                pdfPreviewStatus.textContent = 'Preview loaded, but selectable text is unavailable on this page.';
            }
        } catch {
            if (renderId === pdfRenderId) {
                pdfRenderTask = null;
                pdfPreviewStatus.textContent = 'Preview is unavailable for this PDF.';
            }
        }
    }

    function goToPdfPage(pageNumber) {
        if (!isPdfPreviewActive) {
            return;
        }

        const segmentIndex = importedSegments.findIndex(segment => segment.sourcePage === pageNumber);
        if (segmentIndex >= 0 && segmentIndex !== currentSegmentIndex) {
            syncCurrentFileSegment();
            currentSegmentIndex = segmentIndex;
            renderCurrentSegment();
            return;
        }

        void renderPdfPage(pageNumber);
    }

    function setSelectedFile(file) {
        resetPendingFile();

        if (!file) {
            return;
        }

        const extension = getFileExtension(file.name);
        if (extension !== 'txt' && extension !== 'docx' && extension !== 'pdf') {
            showFileImportMessage('Supported file types: TXT, DOCX, and PDF.', true);
            return;
        }

        if (file.size > maxUploadBytes) {
            showFileImportMessage(`The file is larger than the ${maxUploadSizeLabel} import limit.`, true);
            return;
        }

        selectedFile = file;
        fileSelectionName.textContent = file.name;
        fileSelectionName.hidden = false;
        importFile.disabled = false;
        showFileImportMessage('');
    }

    function removeDropState() {
        fileDropDepth = 0;
        fileDropZone.classList.remove('is-dragging');
    }

    function resetImportedFile() {
        resetPendingFile();
        removeDropState();
        resetPdfPreview();
        importedSegments = [];
        importedPageCount = null;
        currentSegmentIndex = 0;
        clearReplacementHistories();
        fileText.value = '';
        fileText.hidden = false;
        fileEditor.classList.remove('is-split-view');
        fileViewToggle.hidden = true;
        fileEditor.hidden = true;
        fileUpload.hidden = false;
        showFileImportMessage('');
        resetSelectionState();
    }

    async function openSelectedFile() {
        const file = selectedFile;
        if (!file) {
            return;
        }

        importFile.disabled = true;
        importFile.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span>';
        showFileImportMessage('');

        try {
            const body = new FormData();
            body.append('file', file);

            const response = await fetch(fileImportEndpoint, {
                method: 'POST',
                headers: { RequestVerificationToken: antiForgeryToken },
                body
            });
            const result = await response.json().catch(() => ({}));

            if (!response.ok || !Array.isArray(result.segments) || result.segments.length === 0) {
                const fallbackMessage = response.status === 413
                    ? `The file is larger than the ${maxUploadSizeLabel} import limit.`
                    : response.status === 429
                        ? 'Too many requests. Please wait a minute and try again.'
                        : 'The file could not be imported.';
                showFileImportMessage(result.message || fallbackMessage, true);
                return;
            }

            clearReplacementHistories();
            importedSegments = result.segments;
            importedPageCount = Number.isInteger(result.pageCount) ? result.pageCount : null;
            currentSegmentIndex = 0;
            importedFileName.textContent = result.fileName || file.name;
            resetPdfPreview();
            importedPdfFile = isPdfFile(file) ? file : null;
            fileViewToggle.hidden = importedPdfFile === null;
            fileText.hidden = false;
            resetPendingFile();
            fileUpload.hidden = true;
            fileEditor.hidden = false;
            expandImportedWorkspace();
            setFileView(importedPdfFile && canUseSplitFileView() ? 'split' : 'text');
            renderCurrentSegment();
        } catch {
            showFileImportMessage('The file could not be imported. Please try again.', true);
        } finally {
            importFile.disabled = !selectedFile;
            importFile.innerHTML = '<i class="bi bi-file-earmark-text me-2"></i>Open text';
        }
    }

    function selectDroppedFile(event) {
        event.preventDefault();
        removeDropState();

        const files = Array.from(event.dataTransfer?.files ?? []);
        if (files.length !== 1) {
            resetPendingFile();
            showFileImportMessage('Drop one TXT, DOCX, or PDF file at a time.', true);
            return;
        }

        setSelectedFile(files[0]);
    }

    function handleFileInputChange() {
        setSelectedFile(fileInput.files?.[0] ?? null);
    }

    function beginDropState(event) {
        event.preventDefault();
        fileDropDepth++;
        fileDropZone.classList.add('is-dragging');
    }

    function updateDropState(event) {
        event.preventDefault();
        event.dataTransfer.dropEffect = 'copy';
        fileDropZone.classList.add('is-dragging');
    }

    function leaveDropZone(event) {
        event.preventDefault();
        fileDropDepth--;
        if (fileDropDepth <= 0) {
            removeDropState();
        }
    }

    function preventFileDrop(event) {
        event.preventDefault();
    }

    function handleUndoRedoShortcut(event) {
        if (document.activeElement !== activeTextArea() || event.altKey || !(event.ctrlKey || event.metaKey)) {
            return;
        }

        const key = event.key.toLowerCase();
        const direction = key === 'y' || (key === 'z' && event.shiftKey)
            ? 'redo'
            : key === 'z'
                ? 'undo'
                : null;

        if (direction && applyReplacementHistory(direction)) {
            event.preventDefault();
        }
    }

    document.querySelectorAll('[data-submit-action]').forEach(button => {
        button.addEventListener('click', () => {
            submitAction.value = button.dataset.submitAction;
        });
    });

    document.querySelectorAll('[data-workspace-tab]').forEach(button => {
        button.addEventListener('click', () => setActiveWorkspace(button.dataset.workspaceTab));
    });

    fileViewButtons.forEach(button => {
        button.addEventListener('click', () => {
            hidePdfSelectionPopover(true, true);
            setFileView(button.dataset.fileView);
        });
    });

    workspaceResizeHandle.addEventListener('pointerdown', beginWorkspaceResize);
    workspaceResizeHandle.addEventListener('pointermove', resizeWorkspaceFromPointer);
    workspaceResizeHandle.addEventListener('pointerup', endWorkspaceResize);
    workspaceResizeHandle.addEventListener('pointercancel', endWorkspaceResize);
    workspaceResizeHandle.addEventListener('lostpointercapture', handleWorkspaceResizeLostPointerCapture);
    workspaceResizeHandle.addEventListener('keydown', resizeWorkspaceFromKeyboard);
    window.addEventListener('blur', () => finishWorkspaceResize());
    updateWorkspaceResizeValue(workspaceGrid.getBoundingClientRect().height);

    historyToggle.addEventListener('click', () => setHistoryOpen(historyPanel.hidden));
    historyClose.addEventListener('click', () => setHistoryOpen(false));

    initializeTranslationTextareaResize();

    restoreLanguagePreferences();
    syncLanguageTextDirections();
    saveLanguagePreferences();
    sourceLanguage.addEventListener('change', () => {
        syncLanguageTextDirection(sourceLanguage, form.elements.text);
        resetSourceDetectedLanguage();
        saveLanguagePreferences();
        updateSpeechButtons();
    });
    targetLanguage.addEventListener('change', () => {
        syncLanguageTextDirection(targetLanguage, translatedText);
        saveLanguagePreferences();
        updateSpeechButtons();
    });
    sourceText.addEventListener('input', () => {
        resetSourceDetectedLanguage();
        updateSpeechButtons();
    });
    speakSource.addEventListener('click', () => void playSpeech('source'));
    speakTarget.addEventListener('click', () => void playSpeech('target'));
    updateSpeechButtons();

    form?.addEventListener('submit', event => {
        saveLanguagePreferences();
        stopSpeech();
        syncTranslationTextareaHeight();
        submitAction.value = event.submitter?.dataset.submitAction || submitAction.value || 'translate';
        translateButton.disabled = true;
        translateButton.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span>';
    });

    [sampleText, fileText].forEach(textArea => {
        textArea.addEventListener('select', scheduleSelectionTranslation);
        textArea.addEventListener('keyup', scheduleSelectionTranslation);
        textArea.addEventListener('mouseup', scheduleSelectionTranslation);
        textArea.addEventListener('input', handleWorkspaceTextInput);
    });

    selectAllText.addEventListener('click', () => {
        const textArea = activeTextArea();
        if (!textArea.value.trim()) {
            setSelectionStatus(activeWorkspace === 'file' ? 'Choose a file first.' : 'No text to select.');
            return;
        }

        textArea.focus();
        textArea.select();
        translateSelection(true);
    });

    pasteText.addEventListener('click', pasteFromClipboard);
    clearText.addEventListener('click', clearCurrentText);
    resetSample.addEventListener('click', () => {
        sampleText.value = defaultSampleText;
        clearCurrentReplacementHistory();
        resetSelectionState();
        hideSelectionTranslationPanel();
        updateSpeechButtons();
    });

    fileInput.addEventListener('change', handleFileInputChange);
    fileDropZone.addEventListener('dragenter', beginDropState);
    fileDropZone.addEventListener('dragover', updateDropState);
    fileDropZone.addEventListener('dragleave', leaveDropZone);
    fileDropZone.addEventListener('drop', selectDroppedFile);
    fileDropZone.addEventListener('dragend', removeDropState);
    document.addEventListener('dragover', preventFileDrop);
    document.addEventListener('drop', preventFileDrop);
    document.addEventListener('keydown', handleUndoRedoShortcut);
    importFile.addEventListener('click', openSelectedFile);
    undoReplacement.addEventListener('click', () => applyReplacementHistory('undo'));
    redoReplacement.addEventListener('click', () => applyReplacementHistory('redo'));
    previousSegment.addEventListener('click', () => {
        if (currentSegmentIndex > 0) {
            syncCurrentFileSegment();
            currentSegmentIndex--;
            renderCurrentSegment();
        }
    });
    nextSegment.addEventListener('click', () => {
        if (currentSegmentIndex < importedSegments.length - 1) {
            syncCurrentFileSegment();
            currentSegmentIndex++;
            renderCurrentSegment();
        }
    });
    clearFile.addEventListener('click', resetImportedFile);
    previousPdfPage.addEventListener('click', () => goToPdfPage(currentPdfPage - 1));
    nextPdfPage.addEventListener('click', () => goToPdfPage(currentPdfPage + 1));
    pdfTextLayer.addEventListener('mousedown', beginPdfPointerSelection);
    document.addEventListener('mousemove', updatePdfPointerSelection);
    pdfTextLayer.addEventListener('keyup', event => {
        if (event.key === 'Shift' || event.key.startsWith('Arrow')) {
            schedulePdfSelectionPopover();
        }
    });
    translatePdfSelectionButton.addEventListener('click', translatePdfSelection);
    expandPdfSelectionButton.addEventListener('click', () => {
        if (isPdfSelectionCardExpanded()) {
            collapsePdfSelectionCard();
        } else {
            expandPdfSelectionCard();
        }
    });
    resetPdfSelectionLayoutButton.addEventListener('click', resetPdfSelectionCardLayout);
    pdfSelectionActions.addEventListener('pointerdown', beginPdfSelectionCardDrag);
    pdfSelectionActions.addEventListener('pointermove', dragPdfSelectionCard);
    pdfSelectionActions.addEventListener('pointerup', endPdfSelectionCardDrag);
    pdfSelectionActions.addEventListener('pointercancel', endPdfSelectionCardDrag);
    pdfSelectionActions.addEventListener('lostpointercapture', event => {
        if (pdfSelectionCardDrag?.pointerId === event.pointerId) {
            finishPdfSelectionCardDrag(event.pointerId, false);
        }
    });
    copyPdfSelectionButton.addEventListener('click', async () => {
        const copiedText = activePdfTranslationText || activePdfSelectionText;
        const copied = await copyText(copiedText);
        pdfPreviewStatus.textContent = copied
            ? activePdfTranslationText ? 'Translation copied.' : 'Selected text copied.'
            : 'Copy is unavailable. Select the text and use Ctrl+C.';
    });
    closePdfSelectionButton.addEventListener('click', () => hidePdfSelectionPopover(true, true));
    if ('ResizeObserver' in window) {
        new ResizeObserver(() => savePdfSelectionCardLayout()).observe(pdfSelectionPopover);
    }
    pdfCanvasShell.addEventListener('scroll', () => {
        if (!isPdfSelectionCardExpanded()) {
            hidePdfSelectionPopover();
        }
    });
    document.addEventListener('selectionchange', () => {
        const hasPopoverFocus = pdfSelectionPopover.contains(document.activeElement);
        if (!isPdfSelectionCardExpanded() && !getPdfTextSelection() && !hasPopoverFocus) {
            hidePdfSelectionPopover();
        }
    });
    document.addEventListener('mouseup', finishPdfPointerSelection);
    window.addEventListener('blur', () => {
        finishSelectionTranslationPanelDrag();
        finishPdfSelectionCardDrag();
    });
    window.addEventListener('resize', () => {
        if (!selectionTranslationPanel.hidden && window.innerWidth > 900) {
            clampSelectionTranslationPanelToViewport();
            saveSelectionTranslationPanelLayout();
        }

        if (isPdfSelectionCardExpanded()) {
            clampPdfSelectionCardToViewport();
            savePdfSelectionCardLayout();
        } else {
            hidePdfSelectionPopover();
        }
        window.clearTimeout(pdfResizeTimer);
        if (fileEditor.classList.contains('is-split-view') && !canUseSplitFileView()) {
            setFileView('text');
        }
        if (!isPdfPreviewActive) {
            return;
        }

        pdfResizeTimer = window.setTimeout(() => void renderPdfPage(currentPdfPage), 160);
    });

    clearHistory.addEventListener('click', clearTranslationHistory);

    selectionTranslationHeader.addEventListener('pointerdown', beginSelectionTranslationPanelDrag);
    selectionTranslationHeader.addEventListener('pointermove', dragSelectionTranslationPanel);
    selectionTranslationHeader.addEventListener('pointerup', endSelectionTranslationPanelDrag);
    selectionTranslationHeader.addEventListener('pointercancel', endSelectionTranslationPanelDrag);
    selectionTranslationHeader.addEventListener('lostpointercapture', event => {
        if (selectionTranslationPanelDrag?.pointerId === event.pointerId) {
            finishSelectionTranslationPanelDrag(event.pointerId, false);
        }
    });
    if ('ResizeObserver' in window) {
        new ResizeObserver(() => saveSelectionTranslationPanelLayout()).observe(selectionTranslationPanel);
    }
    closeSelectionTranslationPanel.addEventListener('click', hideSelectionTranslationPanel);
    resetSelectionTranslationPanel.addEventListener('click', resetSelectionTranslationPanelLayout);
    copySelectionTranslation.addEventListener('click', async () => {
        const copied = await copyText(activeSelectionTranslationText);
        setSelectionStatus(copied ? 'Translation copied.' : 'Copy is unavailable. Select the text and use Ctrl+C.');
    });

    copyTranslation.addEventListener('click', () => {
        if (translatedText.value.trim()) {
            navigator.clipboard?.writeText(translatedText.value);
        }
    });

    if (initialInput && initialOutput) {
        addHistory(initialInput, initialOutput, detectedLanguage);
    }

    resetSample.hidden = false;
    setHistoryOpen(false);
    updateUndoRedoControls();
    renderHistory();
})();
