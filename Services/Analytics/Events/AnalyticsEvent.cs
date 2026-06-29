namespace TranslatorAzure.Services.Analytics.Events;

public sealed class AnalyticsEvent
{
    public required string VisitorId { get; init; }

    public required AnalyticsEventType Type { get; init; }

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string? AcceptLanguage { get; init; }

    public string? Source { get; init; }

    public string? PagePath { get; init; }

    public string? Referrer { get; init; }
}
