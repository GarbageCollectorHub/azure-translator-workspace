using TranslatorAzure.Services.Analytics.Handlers;
using TranslatorAzure.Services.Analytics.Queue;

namespace TranslatorAzure.Services.Analytics.Background;

public sealed class AnalyticsBackgroundService(
    IAnalyticsQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<AnalyticsBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var analyticsEvent in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<IAnalyticsEventHandler>();

                await handler.HandleAsync(analyticsEvent, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Analytics event processing failed.");
            }
        }
    }
}