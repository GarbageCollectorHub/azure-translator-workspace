namespace TranslatorAzure.Services.Speech;

public sealed record SpeechSynthesisQuotaReservation(
    int CharacterCount,
    string? ClientDocumentId,
    string? GlobalDocumentId,
    int RemainingCharacters);
