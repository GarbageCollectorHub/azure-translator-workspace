using TranslatorAzure.Models.Translator;

namespace TranslatorAzure.Services.FileText;

public sealed class FileTextImportOptions
{
    public const string SectionName = "FileText";
    public const int DefaultMaxUploadBytes = 2 * 1024 * 1024;
    public const int MultipartEnvelopeBytes = 16 * 1024;
    public const int MaximumRequestBytes = DefaultMaxUploadBytes + MultipartEnvelopeBytes;

    public int MaxUploadBytes { get; init; } = DefaultMaxUploadBytes;

    public int MaxSegmentCharacters => TranslationLimits.MaxInteractiveTextLength;

    public string MaxUploadSizeLabel =>
        $"{MaxUploadBytes / (1024d * 1024d):0.#} MB";
}
