using TranslatorAzure.Services.Analytics.Network;

namespace TranslatorAzure.Infrastructure.RateLimiting;

public static class TranslatorRateLimitPartitionKeys
{
    public static string Client(HttpContext httpContext)
    {
        return ClientIpAddress.FromHttpContext(httpContext)
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown-client";
    }

    public static string GlobalTranslation(HttpContext httpContext)
    {
        return IsTranslationRequest(httpContext)
            ? "global-translation"
            : "not-rate-limited";
    }

    public static string GlobalFileImport(HttpContext httpContext)
    {
        return IsFileImportRequest(httpContext)
            ? "global-file-import"
            : "not-rate-limited";
    }

    public static string GlobalSpeechSynthesis(HttpContext httpContext)
    {
        return IsSpeechSynthesisRequest(httpContext)
            ? "global-speech-synthesis"
            : "not-rate-limited";
    }

    public static bool IsTranslationRequest(HttpContext httpContext)
    {
        if (!HttpMethods.IsPost(httpContext.Request.Method))
        {
            return false;
        }

        return httpContext.Request.Path.Equals("/Translator", StringComparison.OrdinalIgnoreCase)
            || httpContext.Request.Path.Equals("/Translator/Translate", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFileImportRequest(HttpContext httpContext)
    {
        return HttpMethods.IsPost(httpContext.Request.Method)
            && httpContext.Request.Path.Equals("/file-text/import", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSpeechSynthesisRequest(HttpContext httpContext)
    {
        return HttpMethods.IsPost(httpContext.Request.Method)
            && httpContext.Request.Path.Equals("/Translator/Speech", StringComparison.OrdinalIgnoreCase);
    }
}
