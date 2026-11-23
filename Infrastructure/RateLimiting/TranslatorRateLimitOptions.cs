using System.ComponentModel.DataAnnotations;

namespace TranslatorAzure.Infrastructure.RateLimiting;

public sealed class TranslatorRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    // Per-client limits use the visitor IP; global limits cap all visitors together.
    [Range(1, int.MaxValue)]
    public int TranslationPermitLimit { get; init; } = 20;

    [Range(1, int.MaxValue)]
    public int TranslationWindowSeconds { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int GlobalTranslationPermitLimit { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int GlobalTranslationWindowSeconds { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int FileImportPermitLimit { get; init; } = 6;

    [Range(1, int.MaxValue)]
    public int FileImportWindowSeconds { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int GlobalFileImportPermitLimit { get; init; } = 20;

    [Range(1, int.MaxValue)]
    public int GlobalFileImportWindowSeconds { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int SpeechSynthesisPermitLimit { get; init; } = 12;

    [Range(1, int.MaxValue)]
    public int SpeechSynthesisWindowSeconds { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int GlobalSpeechSynthesisPermitLimit { get; init; } = 40;

    [Range(1, int.MaxValue)]
    public int GlobalSpeechSynthesisWindowSeconds { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int FeedbackPermitLimit { get; init; } = 3;

    [Range(1, int.MaxValue)]
    public int FeedbackWindowSeconds { get; init; } = 600;

    [Range(1, int.MaxValue)]
    public int GlobalFeedbackPermitLimit { get; init; } = 20;

    [Range(1, int.MaxValue)]
    public int GlobalFeedbackWindowSeconds { get; init; } = 600;
}
