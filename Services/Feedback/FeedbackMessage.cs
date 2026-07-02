namespace TranslatorAzure.Services.Feedback;

public sealed record FeedbackMessage(
    string? Name,
    string Message,
    string? PageUrl,
    string? UserAgent,
    DateTimeOffset CreatedAtUtc);
