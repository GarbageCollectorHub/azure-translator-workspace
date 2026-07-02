using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TranslatorAzure.Services.Feedback;

public sealed class SmtpFeedbackEmailSender(
    IOptionsMonitor<FeedbackOptions> options,
    ILogger<SmtpFeedbackEmailSender> logger) : IFeedbackEmailSender
{
    public bool IsConfigured => options.CurrentValue.IsConfigured;

    public async Task SendAsync(FeedbackMessage feedbackMessage, CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;
        if (!currentOptions.IsConfigured)
        {
            throw new InvalidOperationException("Feedback email is not configured.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, currentOptions.SendTimeoutSeconds)));

        try
        {
            using var smtpClient = new SmtpClient
            {
                Timeout = Math.Max(1, currentOptions.SendTimeoutSeconds) * 1000
            };

            var socketOptions = currentOptions.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            var email = CreateEmail(currentOptions, feedbackMessage);

            await smtpClient.ConnectAsync(
                currentOptions.SmtpHost,
                currentOptions.SmtpPort,
                socketOptions,
                timeoutCts.Token);
            await smtpClient.AuthenticateAsync(
                currentOptions.EffectiveSmtpUserName,
                currentOptions.SmtpPassword,
                timeoutCts.Token);
            await smtpClient.SendAsync(email, timeoutCts.Token);
            await smtpClient.DisconnectAsync(true, timeoutCts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Feedback email could not be sent.");
            throw new FeedbackSendException("Feedback email could not be sent.", ex);
        }
    }

    private static MimeMessage CreateEmail(FeedbackOptions options, FeedbackMessage feedbackMessage)
    {
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(options.SenderName, options.SenderEmail));
        email.To.Add(MailboxAddress.Parse(options.EffectiveRecipientEmail));
        email.Subject = options.Subject;
        email.Body = new TextPart("plain")
        {
            Text = BuildPlainTextBody(feedbackMessage)
        };

        return email;
    }

    private static string BuildPlainTextBody(FeedbackMessage feedbackMessage)
    {
        var name = string.IsNullOrWhiteSpace(feedbackMessage.Name)
            ? "Not provided"
            : feedbackMessage.Name.Trim();
        var pageUrl = string.IsNullOrWhiteSpace(feedbackMessage.PageUrl)
            ? "Not provided"
            : feedbackMessage.PageUrl.Trim();
        var userAgent = string.IsNullOrWhiteSpace(feedbackMessage.UserAgent)
            ? "Not provided"
            : feedbackMessage.UserAgent.Trim();

        return $"""
        New feedback message for Azure Translator Workspace.

        Name: {name}
        Page: {pageUrl}
        User agent: {userAgent}
        Created at: {feedbackMessage.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC

        Message:
        {feedbackMessage.Message.Trim()}
        """;
    }
}
