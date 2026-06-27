using TranslatorAzure.Infrastructure.Cosmos;
using TranslatorAzure.Services.Analytics.Background;
using TranslatorAzure.Services.Analytics.Configuration;
using TranslatorAzure.Services.Analytics.GeoIp;
using TranslatorAzure.Services.Analytics.Handlers;
using TranslatorAzure.Services.Analytics.Queue;
using TranslatorAzure.Services.Analytics.Repositories;
using TranslatorAzure.Services.Analytics.Security;
using TranslatorAzure.Services.Analytics.Tracking;
using TranslatorAzure.Services.Analytics.UserAgent;

namespace TranslatorAzure.Services.Analytics;

public static class AnalyticsServiceCollectionExtensions
{
    public static IServiceCollection AddAnalytics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AnalyticsOptions>()
            .Bind(configuration.GetSection(AnalyticsOptions.SectionName))
            .Validate(options => options.VisitCooldownMinutes > 0, "Visit cooldown must be positive.")
            .Validate(options => options.VisitorCookieDays > 0, "Visitor cookie lifetime must be positive.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.IpHashSalt),
                "Analytics IP hash salt is required when analytics is enabled.")
            .ValidateOnStart();

        services.AddSingleton<ICosmosContainerProvider, CosmosContainerProvider>();

        services.AddSingleton<IAnalyticsQueue, AnalyticsQueue>();
        services.AddScoped<IAnalyticsTracker, AnalyticsTracker>();
        services.AddScoped<IAnalyticsEventHandler, AnalyticsEventHandler>();
        services.AddScoped<IVisitAnalyticsRepository, CosmosVisitAnalyticsRepository>();

        services.AddScoped<IpAddressHasher>();
        services.AddScoped<UserAgentParser>();

        services.AddHttpClient<IGeoIpService, IpApiGeoIpService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        services.AddHostedService<AnalyticsBackgroundService>();

        return services;
    }
}