namespace TranslatorAzure.Services.Speech;

public interface ISpeechSynthesisService
{
    Task<SpeechSynthesisResult> SynthesizeAsync(
        string text,
        string language,
        CancellationToken cancellationToken = default);
}
