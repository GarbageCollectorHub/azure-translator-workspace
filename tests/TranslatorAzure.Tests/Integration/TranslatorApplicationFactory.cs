using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TranslatorAzure.Models.Translator;
using TranslatorAzure.Services.Translator;
using TranslatorAzure.Tests.TestSupport;

namespace TranslatorAzure.Tests.Integration;

// Hosts the real ASP.NET Core pipeline while replacing only the external Azure translator dependency.
public sealed class TranslatorApplicationFactory : WebApplicationFactory<Program>
{
    public TestTranslatorService Translator { get; } = new();

    public void ResetTranslator()
    {
        Translator.Result = new TranslatorResult
        {
            SourceText = "Hello",
            TranslatedText = "\u041f\u0440\u0438\u0432\u0456\u0442",
            TargetLanguage = "uk",
            DetectedLanguage = "en",
            DetectionScore = 1
        };
        Translator.GetLanguagesException = null;
        Translator.TranslateException = null;
        Translator.TranslateCalls.Clear();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["analytics:enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITranslatorService>();
            services.AddSingleton<ITranslatorService>(Translator);
        });
    }
}
