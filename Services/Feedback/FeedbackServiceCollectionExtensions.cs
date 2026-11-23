using TranslatorAzure.Services.Feedback.Background;
using TranslatorAzure.Services.Feedback.Queue;

namespace TranslatorAzure.Services.Feedback;

public static class FeedbackServiceCollectionExtensions
{
    public static IServiceCollection AddFeedback(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FeedbackOptions>()
            .Bind(configuration.GetSection(FeedbackOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IFeedbackQueue, FeedbackQueue>();
        services.AddScoped<IFeedbackEmailSender, SmtpFeedbackEmailSender>();
        services.AddHostedService<FeedbackBackgroundService>();
        return services;
    }
}
