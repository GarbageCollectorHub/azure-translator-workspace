using TranslatorAzure.Services.Feedback.Queue;

namespace TranslatorAzure.Services.Feedback.Background;

public sealed class FeedbackBackgroundService(
    IFeedbackQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<FeedbackBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var feedbackMessage in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IFeedbackEmailSender>();

                await sender.SendAsync(feedbackMessage, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Feedback background sending failed.");
            }
        }
    }
}
