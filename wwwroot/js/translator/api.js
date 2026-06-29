export function createTranslatorApi({
    translateEndpoint,
    antiForgeryToken,
    sourceLanguage,
    targetLanguage
}) {
    async function requestTranslation(text, source = 'selection') {
        const response = await fetch(translateEndpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': antiForgeryToken
            },
            body: JSON.stringify({
                text,
                languageFrom: sourceLanguage.value,
                languageTo: targetLanguage.value,
                source
            })
        });
        const result = await response.json();

        if (!response.ok || !result.status?.isOk) {
            throw new Error(result.data?.message || result.status?.phrase || 'Translation unavailable');
        }

        const output = result.data?.text ?? '';
        if (!output) {
            throw new Error('Translation unavailable');
        }

        return {
            text: output,
            detectedLanguage: result.data?.detectedLanguage
        };
    }

    return {
        requestTranslation
    };
}
