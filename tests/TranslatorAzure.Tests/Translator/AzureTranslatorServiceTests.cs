using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TranslatorAzure.Models.Translator;
using TranslatorAzure.Services.Translator;
using TranslatorAzure.Tests.TestSupport;

namespace TranslatorAzure.Tests.Translator;

// Covers Azure request formation, cached languages, transliteration, and upstream failure behavior.
public sealed class AzureTranslatorServiceTests
{
    [Fact]
    public async Task GetLanguagesAsync_CachesSuccessfulAzureResponse()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(RecordingHttpMessageHandler.Json(LanguagesJson())));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(handler, cache);

        var first = await service.GetLanguagesAsync();
        var second = await service.GetLanguagesAsync();

        Assert.Same(first, second);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
    }

    [Fact]
    public async Task GetLanguagesAsync_UsesFallbackWhenAzureIsUnavailable()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(handler, cache);

        var languages = await service.GetLanguagesAsync();
        var second = await service.GetLanguagesAsync();

        Assert.Contains("en", languages.translation.Keys);
        Assert.Contains("uk", languages.translation.Keys);
        Assert.Same(languages, second);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task TranslateAsync_ReturnsEmptyResultWithoutCallingAzureForBlankText()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(RecordingHttpMessageHandler.Json("{}")));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(handler, cache);

        var result = await service.TranslateAsync(new TranslatorFormModel { Text = "   " });

        Assert.Equal(string.Empty, result.TranslatedText);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TranslateAsync_AutoDetectOmitsFromParameterAndUsesRequiredHeaders()
    {
        var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            var json = request.Method == HttpMethod.Get
                ? LanguagesJson()
                : """[{"detectedLanguage":{"language":"en","score":0.99},"translations":[{"text":"\u041f\u0440\u0438\u0432\u0456\u0442","to":"uk"}]}]""";
            return Task.FromResult(RecordingHttpMessageHandler.Json(json));
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(handler, cache);

        var result = await service.TranslateAsync(new TranslatorFormModel
        {
            LanguageFrom = TranslatorFormModel.AutoDetectLanguageCode,
            LanguageTo = "uk",
            Text = "Hello"
        });

        Assert.Equal("\u041f\u0440\u0438\u0432\u0456\u0442", result.TranslatedText);
        Assert.Equal("en", result.DetectedLanguage);
        Assert.Equal(0.99, result.DetectionScore!.Value);
        var translationRequest = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.DoesNotContain("from=", translationRequest.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("to=uk", translationRequest.RequestUri.Query, StringComparison.Ordinal);
        Assert.Contains("Hello", translationRequest.Body, StringComparison.Ordinal);
        Assert.Equal("test-key", translationRequest.Headers["Ocp-Apim-Subscription-Key"]);
        Assert.Equal("global", translationRequest.Headers["Ocp-Apim-Subscription-Region"]);
    }

    [Fact]
    public async Task TranslateAsync_ExplicitSourceIncludesFromParameter()
    {
        var handler = new RecordingHttpMessageHandler((request, _) =>
            Task.FromResult(RecordingHttpMessageHandler.Json(
                request.Method == HttpMethod.Get
                    ? LanguagesJson()
                    : """[{"translations":[{"text":"Hallo","to":"de"}]}]""")));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(handler, cache);

        var result = await service.TranslateAsync(new TranslatorFormModel
        {
            LanguageFrom = "en",
            LanguageTo = "de",
            Text = "Hello"
        });

        Assert.Equal("Hallo", result.TranslatedText);
        var request = Assert.Single(handler.Requests, item => item.Method == HttpMethod.Post);
        Assert.Contains("from=en", request.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("to=de", request.RequestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TranslateAsync_TransliteratesWhenLanguageSupportsLatinOutput()
    {
        var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(RecordingHttpMessageHandler.Json(LanguagesJson(includeJapaneseTransliteration: true)));
            }

            if (request.RequestUri!.AbsolutePath.Contains("transliterate", StringComparison.Ordinal))
            {
                return Task.FromResult(RecordingHttpMessageHandler.Json("""[{"text":"Konnichiwa","script":"Latn"}]"""));
            }

            return Task.FromResult(RecordingHttpMessageHandler.Json("""[{"translations":[{"text":"Hello","to":"en"}]}]"""));
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(handler, cache);

        var result = await service.TranslateAsync(new TranslatorFormModel
        {
            LanguageFrom = "ja",
            LanguageTo = "en",
            Text = "\u3053\u3093\u306b\u3061\u306f"
        });

        Assert.Equal("Konnichiwa", result.TransliterationFrom);
        Assert.Null(result.TransliterationTo);
        var transliterationRequest = Assert.Single(handler.Requests, request =>
            request.RequestUri!.AbsolutePath.Contains("transliterate", StringComparison.Ordinal));
        Assert.Contains("language=ja", transliterationRequest.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("fromScript=Jpan", transliterationRequest.RequestUri.Query, StringComparison.Ordinal);
        Assert.Contains("toScript=Latn", transliterationRequest.RequestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TranslateAsync_ThrowsForFailedAzureResponse()
    {
        var handler = new RecordingHttpMessageHandler((request, _) =>
            Task.FromResult(request.Method == HttpMethod.Get
                ? RecordingHttpMessageHandler.Json(LanguagesJson())
                : RecordingHttpMessageHandler.Json("{\"error\":{}}", HttpStatusCode.TooManyRequests)));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(handler, cache);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.TranslateAsync(new TranslatorFormModel { Text = "Hello", LanguageTo = "uk" }));
    }

    private static AzureTranslatorService CreateService(
        HttpMessageHandler handler,
        IMemoryCache cache) =>
        new(
            new TestHttpClientFactory(handler),
            cache,
            Options.Create(new TranslatorOptions
            {
                Key = "test-key",
                Endpoint = "https://translator.test",
                Location = "global",
                Route = "/translate?api-version=3.0",
                RouteTransliterate = "/transliterate?api-version=3.0",
                LanguagesUrl = "https://translator.test/languages?api-version=3.0"
            }));

    private static string LanguagesJson(bool includeJapaneseTransliteration = false) =>
        includeJapaneseTransliteration
            ? """
              {"translation":{"en":{"name":"English","nativeName":"English","dir":"ltr"},"ja":{"name":"Japanese","nativeName":"\u65e5\u672c\u8a9e","dir":"ltr"}},"transliteration":{"ja":{"name":"Japanese","nativeName":"\u65e5\u672c\u8a9e","scripts":[{"code":"Jpan","toScripts":[{"code":"Latn"}]}]}}}
              """
            : """
              {"translation":{"en":{"name":"English","nativeName":"English","dir":"ltr"},"uk":{"name":"Ukrainian","nativeName":"\u0423\u043a\u0440\u0430\u0457\u043d\u0441\u044c\u043a\u0430","dir":"ltr"},"de":{"name":"German","nativeName":"Deutsch","dir":"ltr"}},"transliteration":{}}
              """;
}
