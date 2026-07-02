using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TranslatorAzure.Infrastructure.RateLimiting;
using TranslatorAzure.Models.Feedback;
using TranslatorAzure.Services.Feedback;

namespace TranslatorAzure.Controllers;

[Route("feedback")]
public sealed class FeedbackController(
    IFeedbackEmailSender feedbackEmailSender,
    IOptions<FeedbackOptions> options,
    ILogger<FeedbackController> logger) : Controller
{
    private const int MaxNameLength = 120;

    [HttpPost("send")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(TranslatorRateLimitPolicies.Feedback)]
    public async Task<IActionResult> Send(
        [FromForm] FeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var feedbackOptions = options.Value;

        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return Ok(CreateResponse("Thanks. Your message was sent."));
        }

        var message = request.Message?.Trim() ?? string.Empty;
        var name = request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(CreateResponse("Write a message before sending.", false));
        }

        if (message.Length < feedbackOptions.MinMessageLength)
        {
            return BadRequest(CreateResponse(
                $"Write at least {feedbackOptions.MinMessageLength:N0} characters.",
                false));
        }

        if (message.Length > feedbackOptions.MaxMessageLength)
        {
            return BadRequest(CreateResponse(
                $"Keep feedback under {feedbackOptions.MaxMessageLength:N0} characters.",
                false));
        }

        if (name?.Length > MaxNameLength)
        {
            return BadRequest(CreateResponse($"Keep name under {MaxNameLength:N0} characters.", false));
        }

        if (!feedbackEmailSender.IsConfigured)
        {
            logger.LogWarning("Feedback form was submitted, but feedback email is not configured.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateResponse("Feedback is temporarily unavailable. Please try again later.", false));
        }

        var feedbackMessage = new FeedbackMessage(
            name,
            message,
            ReadHeader("Referer"),
            ReadHeader("User-Agent"),
            DateTimeOffset.UtcNow);

        try
        {
            await feedbackEmailSender.SendAsync(feedbackMessage, cancellationToken);
            return Ok(CreateResponse("Thanks. Your message was sent."));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Feedback email is not configured.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateResponse("Feedback is temporarily unavailable. Please try again later.", false));
        }
        catch (FeedbackSendException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateResponse("Feedback is temporarily unavailable. Please try again later.", false));
        }
    }

    private string? ReadHeader(string headerName)
    {
        return Request.Headers.TryGetValue(headerName, out var value)
            ? value.ToString()
            : null;
    }

    private static FeedbackResponse CreateResponse(string message, bool isOk = true)
    {
        return new FeedbackResponse(isOk, message);
    }
}
