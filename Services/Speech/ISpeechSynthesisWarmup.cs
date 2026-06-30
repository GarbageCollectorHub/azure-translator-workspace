namespace TranslatorAzure.Services.Speech;

public interface ISpeechSynthesisWarmup
{
    Task WarmUpAsync(CancellationToken cancellationToken = default);
}
