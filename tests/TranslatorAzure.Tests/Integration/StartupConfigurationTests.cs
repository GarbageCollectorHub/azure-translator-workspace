using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace TranslatorAzure.Tests.Integration;

public sealed class StartupConfigurationTests : IClassFixture<TranslatorApplicationFactory>
{
    private readonly TranslatorApplicationFactory _factory;

    public StartupConfigurationTests(TranslatorApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("FileText:MaxPdfPages", "201")]
    [InlineData("SpeechSynthesis:ClientMonthlyCharacterLimit", "-1")]
    [InlineData("Feedback:SmtpPort", "0")]
    [InlineData("RateLimiting:FeedbackWindowSeconds", "0")]
    public void InvalidOptionPreventsStartup(string key, string value)
    {
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })));

        var error = Assert.Throws<OptionsValidationException>(() => app.CreateClient());
        Assert.Contains(key.Split(':')[1], error.Message);
    }

    [Theory]
    [InlineData("RateLimiting:TranslationPermitLimit")]
    [InlineData("RateLimiting:GlobalTranslationPermitLimit")]
    public async Task TranslationLimitIsSharedAcrossTranslationRoutesButNotWithFileImport(string limit)
    {
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [limit] = "1" })));
        using var client = app.CreateClient();

        // Tokenless requests exercise the middleware without calling a translation provider.
        using var first = await client.PostAsync("/Translator", null);
        using var rejected = await client.PostAsync("/Translator/Translate", null);
        using var separatePolicy = await client.PostAsync("/file-text/import", null);

        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        using var body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("Too many requests. Please wait a minute and try again.", body.RootElement.GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, separatePolicy.StatusCode);
    }
}
