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
}
