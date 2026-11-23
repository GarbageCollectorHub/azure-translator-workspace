using System.ComponentModel.DataAnnotations;
using TranslatorAzure.Models.Translator;

namespace TranslatorAzure.Services.FileText;

public sealed class FileTextImportOptions
{
    public const string SectionName = "FileText";
    public const int DefaultMaxUploadBytes = 10 * 1024 * 1024;
    public const int MultipartEnvelopeBytes = 16 * 1024;
    public const int MaximumRequestBytes = DefaultMaxUploadBytes + MultipartEnvelopeBytes;
    public const int DefaultMaxPdfPages = 200;
    public const int DefaultMaxExtractedCharacters = 250_000;

    [Range(1, DefaultMaxUploadBytes)]
    public int MaxUploadBytes { get; init; } = DefaultMaxUploadBytes;

    public int MaxSegmentCharacters => TranslationLimits.MaxInteractiveTextLength;

    [Range(1, DefaultMaxPdfPages)]
    public int MaxPdfPages { get; init; } = DefaultMaxPdfPages;

    [Range(1, DefaultMaxExtractedCharacters)]
    public int MaxExtractedCharacters { get; init; } = DefaultMaxExtractedCharacters;

    public string MaxUploadSizeLabel =>
        $"{MaxUploadBytes / (1024d * 1024d):0.#} MB";
}
