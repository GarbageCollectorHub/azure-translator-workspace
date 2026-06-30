export function createTranslatorApi({
    translateEndpoint,
    speechEndpoint,
    antiForgeryToken,
    sourceLanguage,
    targetLanguage
}) {
    function createApiError(message, response, result = {}) {
        const error = new Error(message);
        error.status = response.status;
        error.data = result.data;
        return error;
    }

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
        const result = await response.json().catch(() => ({}));

        if (!response.ok || !result.status?.isOk) {
            throw createApiError(
                result.data?.message || result.message || result.status?.phrase || 'Translation unavailable',
                response,
                result);
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

    async function requestSpeech(text, language) {
        const response = await fetch(speechEndpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': antiForgeryToken
            },
            body: JSON.stringify({ text, language })
        });

        if (!response.ok) {
            const result = await response.json().catch(() => ({}));
            throw createApiError(
                result.data?.message || result.message || result.status?.phrase || 'Audio unavailable',
                response,
                result);
        }

        return response.blob();
    }

    return {
        requestTranslation,
        requestSpeech
    };
}
