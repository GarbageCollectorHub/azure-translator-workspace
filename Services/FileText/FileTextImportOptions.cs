using TranslatorAzure.Models.Translator;

namespace TranslatorAzure.Services.FileText;

public sealed class FileTextImportOptions
{
    public const string SectionName = "FileText";
    public const int DefaultMaxUploadBytes = 2 * 1024 * 1024;
    public const int MultipartEnvelopeBytes = 16 * 1024;
    public const int MaximumRequestBytes = DefaultMaxUploadBytes + MultipartEnvelopeBytes;
    public const int DefaultMaxPdfPages = 50;
    public const int DefaultMaxExtractedCharacters = 250_000;

    public int MaxUploadBytes { get; init; } = DefaultMaxUploadBytes;

    public int MaxSegmentCharacters => TranslationLimits.MaxInteractiveTextLength;

    public int MaxPdfPages { get; init; } = DefaultMaxPdfPages;

    public int MaxExtractedCharacters { get; init; } = DefaultMaxExtractedCharacters;

    public string MaxUploadSizeLabel =>
        $"{MaxUploadBytes / (1024d * 1024d):0.#} MB";
}
