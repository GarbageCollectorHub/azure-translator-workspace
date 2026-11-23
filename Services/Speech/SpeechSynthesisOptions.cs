using System.ComponentModel.DataAnnotations;
using TranslatorAzure.Models.Speech;

namespace TranslatorAzure.Services.Speech;

public sealed class SpeechSynthesisOptions
{
    public const string SectionName = "SpeechSynthesis";

    public bool Enabled { get; init; } = true;

    public string Key { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public string OutputFormat { get; init; } = "audio-24khz-96kbitrate-mono-mp3";

    [Range(1, SpeechSynthesisRequest.MaxTextLength)]
    public int MaxTextLength { get; init; } = SpeechSynthesisRequest.MaxTextLength;

    [Range(0, int.MaxValue)]
    public int ClientMonthlyCharacterLimit { get; init; } = 50000;

    [Range(0, int.MaxValue)]
    public int GlobalMonthlyCharacterLimit { get; init; }
}
