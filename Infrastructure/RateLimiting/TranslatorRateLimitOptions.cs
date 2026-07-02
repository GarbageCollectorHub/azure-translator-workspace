namespace TranslatorAzure.Infrastructure.RateLimiting;

public sealed class TranslatorRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    // Per-client limits use the visitor IP; global limits cap all visitors together.
    public int TranslationPermitLimit { get; init; } = 20;

    public int TranslationWindowSeconds { get; init; } = 60;

    public int GlobalTranslationPermitLimit { get; init; } = 60;

    public int GlobalTranslationWindowSeconds { get; init; } = 60;

    public int FileImportPermitLimit { get; init; } = 6;

    public int FileImportWindowSeconds { get; init; } = 60;

    public int GlobalFileImportPermitLimit { get; init; } = 20;

    public int GlobalFileImportWindowSeconds { get; init; } = 60;

    public int SpeechSynthesisPermitLimit { get; init; } = 12;

    public int SpeechSynthesisWindowSeconds { get; init; } = 60;

    public int GlobalSpeechSynthesisPermitLimit { get; init; } = 40;

    public int GlobalSpeechSynthesisWindowSeconds { get; init; } = 60;

    public int FeedbackPermitLimit { get; init; } = 3;

    public int FeedbackWindowSeconds { get; init; } = 600;

    public int GlobalFeedbackPermitLimit { get; init; } = 20;

    public int GlobalFeedbackWindowSeconds { get; init; } = 600;
}
