using System.ComponentModel.DataAnnotations;

namespace TranslatorAzure.Models.Speech;

public sealed class SpeechSynthesisRequest
{
    public const int MaxTextLength = 5000;

    [Required]
    [StringLength(MaxTextLength, MinimumLength = 1)]
    public string Text { get; init; } = string.Empty;

    [Required]
    public string Language { get; init; } = string.Empty;
}
