namespace TranslatorAzure.Services.Speech;

public static class SpeechServiceCollectionExtensions
{
    public static IServiceCollection AddSpeechSynthesis(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SpeechSynthesisOptions>()
            .Bind(configuration.GetSection(SpeechSynthesisOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ISpeechSynthesisQuota, SpeechSynthesisQuota>();
        services.AddHttpClient<AzureSpeechSynthesisService>();
        services.AddTransient<ISpeechSynthesisService>(provider => provider.GetRequiredService<AzureSpeechSynthesisService>());
        services.AddTransient<ISpeechSynthesisWarmup>(provider => provider.GetRequiredService<AzureSpeechSynthesisService>());
        services.AddHostedService<SpeechSynthesisWarmupService>();
        return services;
    }
}
