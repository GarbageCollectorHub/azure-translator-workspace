namespace TranslatorAzure.Services.Speech;

public interface ISpeechSynthesisQuota
{
    Task<SpeechSynthesisQuotaReservation?> TryReserveAsync(
        int characterCount,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        SpeechSynthesisQuotaReservation reservation,
        CancellationToken cancellationToken = default);
}
