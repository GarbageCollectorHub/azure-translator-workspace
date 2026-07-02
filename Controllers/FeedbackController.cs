using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TranslatorAzure.Infrastructure.RateLimiting;
using TranslatorAzure.Models.Feedback;
using TranslatorAzure.Services.Feedback;
using TranslatorAzure.Services.Feedback.Queue;

namespace TranslatorAzure.Controllers;

[Route("feedback")]
public sealed class FeedbackController(
    IFeedbackEmailSender feedbackEmailSender,
    IFeedbackQueue feedbackQueue,
    IOptions<FeedbackOptions> options,
    ILogger<FeedbackController> logger) : Controller
{
    private const int MaxNameLength = 120;

    [HttpPost("send")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(TranslatorRateLimitPolicies.Feedback)]
    public IActionResult Send([FromForm] FeedbackRequest request)
    {
        var feedbackOptions = options.Value;

        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return Ok(CreateResponse("Thanks. Your message was received."));
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

        if (!feedbackQueue.TryEnqueue(feedbackMessage))
        {
            logger.LogWarning("Feedback form was submitted, but the background queue is full.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateResponse("Feedback is temporarily unavailable. Please try again later.", false));
        }

        return Ok(CreateResponse("Thanks. Your message was received."));
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
