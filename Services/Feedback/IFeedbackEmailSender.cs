namespace TranslatorAzure.Services.Feedback;

public interface IFeedbackEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(FeedbackMessage message, CancellationToken cancellationToken);
}
