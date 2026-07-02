namespace TranslatorAzure.Services.Feedback;

public sealed class FeedbackSendException : Exception
{
    public FeedbackSendException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
