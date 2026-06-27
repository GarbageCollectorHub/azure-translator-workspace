using System.Globalization;
using Microsoft.Extensions.Options;
using TranslatorAzure.Services.Analytics.Configuration;
using TranslatorAzure.Services.Analytics.Events;
using TranslatorAzure.Services.Analytics.Queue;

namespace TranslatorAzure.Services.Analytics.Tracking;

public sealed class AnalyticsTracker(
    IAnalyticsQueue queue,
    IOptions<AnalyticsOptions> options) : IAnalyticsTracker
{
    private const string VisitorCookieName = "translator_visitor_id";
    private const string LastVisitCookieName = "translator_last_visit_utc";

    private readonly AnalyticsOptions _options = options.Value;

    public void TrackVisitOpened(HttpContext httpContext)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var visitorId = GetOrCreateVisitorId(httpContext, now);

        if (!ShouldTrackNewVisit(httpContext, now))
        {
            return;
        }

        SetLastVisitCookie(httpContext, now);

        queue.TryEnqueue(new AnalyticsEvent
        {
            VisitorId = visitorId,
            Type = AnalyticsEventType.VisitOpened,
            CreatedAtUtc = now,
            IpAddress = GetClientIpAddress(httpContext),
            UserAgent = Truncate(httpContext.Request.Headers["User-Agent"].ToString(), 500),
            AcceptLanguage = Truncate(httpContext.Request.Headers["Accept-Language"].ToString(), 200)
        });
    }

    public void TrackMainTranslationCompleted(HttpContext httpContext)
    {
        TrackCounter(httpContext, AnalyticsEventType.MainTranslationCompleted);
    }

    public void TrackSelectionTranslationCompleted(HttpContext httpContext, bool isPdfSelection)
    {
        TrackCounter(
            httpContext,
            isPdfSelection
                ? AnalyticsEventType.PdfSelectionTranslationCompleted
                : AnalyticsEventType.SelectionTranslationCompleted);
    }

    public void TrackFileImported(HttpContext httpContext)
    {
        TrackCounter(httpContext, AnalyticsEventType.FileImported);
    }

    private void TrackCounter(HttpContext httpContext, AnalyticsEventType eventType)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var visitorId = GetOrCreateVisitorId(httpContext, DateTime.UtcNow);

        queue.TryEnqueue(new AnalyticsEvent
        {
            VisitorId = visitorId,
            Type = eventType,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private string GetOrCreateVisitorId(HttpContext httpContext, DateTime now)
    {
        if (httpContext.Request.Cookies.TryGetValue(VisitorCookieName, out var existingVisitorId)
            && Guid.TryParseExact(existingVisitorId, "N", out _))
        {
            return existingVisitorId;
        }

        var visitorId = Guid.NewGuid().ToString("N");

        httpContext.Response.Cookies.Append(
            VisitorCookieName,
            visitorId,
            CreateCookieOptions(httpContext, now));

        return visitorId;
    }

    private bool ShouldTrackNewVisit(HttpContext httpContext, DateTime now)
    {
        if (!httpContext.Request.Cookies.TryGetValue(LastVisitCookieName, out var rawLastVisit))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(
                rawLastVisit,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var lastVisit))
        {
            return true;
        }

        return now - lastVisit.UtcDateTime >= TimeSpan.FromMinutes(_options.VisitCooldownMinutes);
    }

    private void SetLastVisitCookie(HttpContext httpContext, DateTime now)
    {
        httpContext.Response.Cookies.Append(
            LastVisitCookieName,
            now.ToString("O", CultureInfo.InvariantCulture),
            CreateCookieOptions(httpContext, now));
    }

    private CookieOptions CreateCookieOptions(HttpContext httpContext, DateTime now)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = now.AddDays(_options.VisitorCookieDays)
        };
    }

    private static string? GetClientIpAddress(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();

        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}