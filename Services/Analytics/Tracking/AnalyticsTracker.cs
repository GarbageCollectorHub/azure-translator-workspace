using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
    private const string IgnoreCookieName = "translator_analytics_ignore";
    private const string IgnoreQueryName = "analytics_ignore";
    private const string IgnoreHeaderName = "X-Analytics-Ignore";

    private readonly AnalyticsOptions _options = options.Value;

    public void TrackVisitOpened(HttpContext httpContext)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var now = DateTime.UtcNow;

        if (ShouldIgnoreAnalytics(httpContext, now))
        {
            return;
        }

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
            AcceptLanguage = Truncate(httpContext.Request.Headers["Accept-Language"].ToString(), 200),
            Source = GetTrackingSource(httpContext),
            PagePath = GetPagePath(httpContext),
            Referrer = Truncate(httpContext.Request.Headers["Referer"].ToString(), 500)
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

        var now = DateTime.UtcNow;

        if (ShouldIgnoreAnalytics(httpContext, now))
        {
            return;
        }

        var visitorId = GetOrCreateVisitorId(httpContext, now);

        queue.TryEnqueue(new AnalyticsEvent
        {
            VisitorId = visitorId,
            Type = eventType,
            CreatedAtUtc = now
        });
    }

    private bool ShouldIgnoreAnalytics(HttpContext httpContext, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(_options.IgnoreToken))
        {
            return false;
        }

        var ignoreToken = _options.IgnoreToken.Trim();

        if (IsIgnoreTokenMatch(httpContext.Request.Query[IgnoreQueryName].FirstOrDefault(), ignoreToken))
        {
            SetIgnoreCookie(httpContext, now, ignoreToken);
            return true;
        }

        if (IsIgnoreTokenMatch(httpContext.Request.Headers[IgnoreHeaderName].ToString(), ignoreToken))
        {
            return true;
        }

        return httpContext.Request.Cookies.TryGetValue(IgnoreCookieName, out var cookieValue)
            && IsIgnoreTokenMatch(cookieValue, ignoreToken);
    }

    private void SetIgnoreCookie(HttpContext httpContext, DateTime now, string ignoreToken)
    {
        httpContext.Response.Cookies.Append(
            IgnoreCookieName,
            ignoreToken,
            CreateCookieOptions(httpContext, now));
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

    private static string? GetTrackingSource(HttpContext httpContext)
    {
        foreach (var key in new[] { "source", "utm_source" })
        {
            if (httpContext.Request.Query.TryGetValue(key, out var values))
            {
                var value = NormalizeTrackingValue(values.FirstOrDefault(), 80);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static string? GetPagePath(HttpContext httpContext)
    {
        var path = string.Concat(httpContext.Request.PathBase, httpContext.Request.Path);
        return Truncate(path, 300);
    }

    private static string? NormalizeTrackingValue(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = new string(value
            .Trim()
            .Where(character => !char.IsControl(character))
            .ToArray());

        return Truncate(cleaned, maxLength);
    }

    private static bool IsIgnoreTokenMatch(string? value, string ignoreToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var valueBytes = Encoding.UTF8.GetBytes(value.Trim());
        var tokenBytes = Encoding.UTF8.GetBytes(ignoreToken);

        return valueBytes.Length == tokenBytes.Length
            && CryptographicOperations.FixedTimeEquals(valueBytes, tokenBytes);
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
