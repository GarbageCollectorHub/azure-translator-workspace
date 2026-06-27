using TranslatorAzure.Services.Analytics.Events;

namespace TranslatorAzure.Services.Analytics.Handlers;

public interface IAnalyticsEventHandler
{
    Task HandleAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken);
}