export function readTranslatorPageConfig() {
    const configElement = document.getElementById('translator-page-config');

    if (!configElement?.textContent) {
        return {};
    }

    try {
        return JSON.parse(configElement.textContent);
    } catch {
        return {};
    }
}
