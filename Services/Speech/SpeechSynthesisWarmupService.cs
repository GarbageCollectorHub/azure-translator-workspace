namespace TranslatorAzure.Services.Speech;

public sealed class SpeechSynthesisWarmupService(
    IServiceScopeFactory scopeFactory,
    ILogger<SpeechSynthesisWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var warmup = scope.ServiceProvider.GetRequiredService<ISpeechSynthesisWarmup>();

            await warmup.WarmUpAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application is stopping.
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Azure Speech warmup failed.");
        }
    }
}
