namespace TranslatorAzure.Services.Feedback.Queue;

public interface IFeedbackQueue
{
    bool TryEnqueue(FeedbackMessage feedbackMessage);

    IAsyncEnumerable<FeedbackMessage> ReadAllAsync(CancellationToken cancellationToken);
}
