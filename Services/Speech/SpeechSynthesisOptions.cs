namespace TranslatorAzure.Services.Speech;

public sealed class SpeechSynthesisOptions
{
    public const string SectionName = "SpeechSynthesis";

    public bool Enabled { get; init; } = true;

    public string Key { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public string OutputFormat { get; init; } = "audio-24khz-96kbitrate-mono-mp3";

    public int MaxTextLength { get; init; } = 5000;

    public int ClientMonthlyCharacterLimit { get; init; } = 50000;

    public int GlobalMonthlyCharacterLimit { get; init; }
}
