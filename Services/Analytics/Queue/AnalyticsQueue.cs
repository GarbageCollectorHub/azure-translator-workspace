using System.Threading.Channels;
using TranslatorAzure.Services.Analytics.Events;

namespace TranslatorAzure.Services.Analytics.Queue;

public sealed class AnalyticsQueue : IAnalyticsQueue
{
    private readonly Channel<AnalyticsEvent> _channel = Channel.CreateBounded<AnalyticsEvent>(
        new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryEnqueue(AnalyticsEvent analyticsEvent)
    {
        return _channel.Writer.TryWrite(analyticsEvent);
    }

    public IAsyncEnumerable<AnalyticsEvent> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}