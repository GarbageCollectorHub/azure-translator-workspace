export function createTranslationHistory({
    historyKey,
    historyLimit,
    historyList
}) {
    function loadHistory() {
        try {
            return JSON.parse(localStorage.getItem(historyKey) || '[]');
        } catch {
            return [];
        }
    }

    function saveHistory(items) {
        localStorage.setItem(historyKey, JSON.stringify(items.slice(0, historyLimit)));
    }

    function addHistory(input, output, detectedLanguage) {
        if (!input?.trim() || !output?.trim()) {
            return;
        }

        const item = {
            input: input.trim(),
            output: output.trim(),
            detectedLanguage,
            time: new Date().toISOString()
        };

        const items = loadHistory()
            .filter(x => !(x.input === item.input && x.output === item.output));

        items.unshift(item);
        saveHistory(items);
        renderHistory();
    }

    function renderHistory() {
        const items = loadHistory();
        historyList.innerHTML = '';

        if (items.length === 0) {
            const empty = document.createElement('p');
            empty.className = 'history-empty';
            empty.textContent = 'No translations yet';
            historyList.appendChild(empty);
            return;
        }

        for (const item of items) {
            const row = document.createElement('article');
            row.className = 'history-item';

            const input = document.createElement('p');
            input.className = 'history-input';
            input.textContent = item.input;

            const output = document.createElement('p');
            output.className = 'history-output';
            output.textContent = item.output;

            const footer = document.createElement('div');
            footer.className = 'history-footer';

            const time = document.createElement('span');
            time.textContent = new Date(item.time).toLocaleString();

            const copy = document.createElement('button');
            copy.className = 'btn btn-sm btn-outline-secondary icon-button';
            copy.type = 'button';
            copy.title = 'Copy';
            copy.setAttribute('aria-label', 'Copy');
            copy.innerHTML = '<i class="bi bi-copy"></i>';
            copy.addEventListener('click', () => navigator.clipboard?.writeText(item.output));

            footer.append(time, copy);
            row.append(input, output, footer);
            historyList.appendChild(row);
        }
    }

    function clearHistory() {
        localStorage.removeItem(historyKey);
        renderHistory();
    }

    return {
        addHistory,
        clearHistory,
        renderHistory
    };
}
