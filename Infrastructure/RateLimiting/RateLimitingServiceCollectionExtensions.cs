using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace TranslatorAzure.Infrastructure.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddTranslatorRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TranslatorRateLimitOptions>()
            .Bind(configuration.GetSection(TranslatorRateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter();
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<TranslatorRateLimitOptions>>((options, settings) => Configure(options, settings.Value));
        return services;
    }

    private static void Configure(RateLimiterOptions options, TranslatorRateLimitOptions limits)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = WriteRejectionAsync;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (TranslatorRateLimitPartitionKeys.IsTranslationRequest(context))
                return Window(TranslatorRateLimitPolicies.Translation, limits.GlobalTranslationPermitLimit, limits.GlobalTranslationWindowSeconds);
            if (TranslatorRateLimitPartitionKeys.IsFileImportRequest(context))
                return Window(TranslatorRateLimitPolicies.FileImport, limits.GlobalFileImportPermitLimit, limits.GlobalFileImportWindowSeconds);
            if (TranslatorRateLimitPartitionKeys.IsSpeechSynthesisRequest(context))
                return Window(TranslatorRateLimitPolicies.SpeechSynthesis, limits.GlobalSpeechSynthesisPermitLimit, limits.GlobalSpeechSynthesisWindowSeconds);
            if (TranslatorRateLimitPartitionKeys.IsFeedbackRequest(context))
                return Window(TranslatorRateLimitPolicies.Feedback, limits.GlobalFeedbackPermitLimit, limits.GlobalFeedbackWindowSeconds);

            return RateLimitPartition.GetNoLimiter("not-rate-limited");
        });

        AddClientPolicy(options, TranslatorRateLimitPolicies.Translation, limits.TranslationPermitLimit, limits.TranslationWindowSeconds);
        AddClientPolicy(options, TranslatorRateLimitPolicies.FileImport, limits.FileImportPermitLimit, limits.FileImportWindowSeconds);
        AddClientPolicy(options, TranslatorRateLimitPolicies.SpeechSynthesis, limits.SpeechSynthesisPermitLimit, limits.SpeechSynthesisWindowSeconds);
        AddClientPolicy(options, TranslatorRateLimitPolicies.Feedback, limits.FeedbackPermitLimit, limits.FeedbackWindowSeconds);
    }

    private static void AddClientPolicy(RateLimiterOptions options, string policy, int permits, int seconds) =>
        options.AddPolicy(policy, context => Window(TranslatorRateLimitPartitionKeys.Client(context), permits, seconds));

    private static RateLimitPartition<string> Window(string key, int permits, int seconds) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromSeconds(seconds),
            QueueLimit = 0,
            AutoReplenishment = true
        });

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        await response.WriteAsJsonAsync(new { message = "Too many requests. Please wait a minute and try again." }, cancellationToken);
    }
}
