using TranslatorAzure.Services.Analytics.Events;

namespace TranslatorAzure.Services.Analytics.Queue;

public interface IAnalyticsQueue
{
    bool TryEnqueue(AnalyticsEvent analyticsEvent);

    IAsyncEnumerable<AnalyticsEvent> ReadAllAsync(CancellationToken cancellationToken);
}