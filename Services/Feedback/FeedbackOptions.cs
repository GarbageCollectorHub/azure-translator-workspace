using System.ComponentModel.DataAnnotations;

namespace TranslatorAzure.Services.Feedback;

public sealed class FeedbackOptions
{
    public const string SectionName = "Feedback";

    public bool Enabled { get; init; }

    public string SmtpHost { get; init; } = "smtp.gmail.com";

    [Range(1, 65535)]
    public int SmtpPort { get; init; } = 587;

    public bool UseStartTls { get; init; } = true;

    public string SenderEmail { get; init; } = string.Empty;

    public string SenderName { get; init; } = "Azure Translator Workspace";

    public string RecipientEmail { get; init; } = string.Empty;

    public string SmtpUserName { get; init; } = string.Empty;

    public string SmtpPassword { get; init; } = string.Empty;

    public string Subject { get; init; } = "Azure Translator Workspace feedback";

    [Range(1, 200)]
    public int MinMessageLength { get; init; } = 5;

    [Range(200, 5000)]
    public int MaxMessageLength { get; init; } = 2000;

    [Range(1, 30)]
    public int SendTimeoutSeconds { get; init; } = 10;

    [Range(1, 1000)]
    public int QueueCapacity { get; init; } = 100;

    public string EffectiveRecipientEmail =>
        string.IsNullOrWhiteSpace(RecipientEmail) ? SenderEmail : RecipientEmail;

    public string EffectiveSmtpUserName =>
        string.IsNullOrWhiteSpace(SmtpUserName) ? SenderEmail : SmtpUserName;

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(SmtpHost)
        && SmtpPort is > 0 and <= 65535
        && !string.IsNullOrWhiteSpace(SenderEmail)
        && !string.IsNullOrWhiteSpace(EffectiveRecipientEmail)
        && !string.IsNullOrWhiteSpace(EffectiveSmtpUserName)
        && !string.IsNullOrWhiteSpace(SmtpPassword);
}
