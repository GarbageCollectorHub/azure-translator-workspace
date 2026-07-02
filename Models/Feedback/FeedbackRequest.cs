namespace TranslatorAzure.Models.Feedback;

public sealed class FeedbackRequest
{
    public string? Name { get; init; }

    public string? Message { get; init; }

    public string? Website { get; init; }
}
