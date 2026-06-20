using System.ComponentModel.DataAnnotations;

namespace TranslatorAzure.Models.Translator;

public sealed class TranslationRequest
{
    public const int MaxTextLength = TranslationLimits.MaxInteractiveTextLength;

    [Required]
    [StringLength(MaxTextLength, MinimumLength = 2)]
    public string Text { get; init; } = string.Empty;

    public string LanguageFrom { get; init; } = TranslatorFormModel.AutoDetectLanguageCode;

    public string LanguageTo { get; init; } = "uk";
}
