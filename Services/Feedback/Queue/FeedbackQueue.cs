using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace TranslatorAzure.Services.Feedback.Queue;

public sealed class FeedbackQueue : IFeedbackQueue
{
    private readonly Channel<FeedbackMessage> _channel;

    public FeedbackQueue(IOptions<FeedbackOptions> options)
    {
        _channel = Channel.CreateBounded<FeedbackMessage>(
            new BoundedChannelOptions(options.Value.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    }

    public bool TryEnqueue(FeedbackMessage feedbackMessage)
    {
        return _channel.Writer.TryWrite(feedbackMessage);
    }

    public IAsyncEnumerable<FeedbackMessage> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
