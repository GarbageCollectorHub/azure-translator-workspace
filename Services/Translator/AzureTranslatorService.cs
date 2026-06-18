using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TranslatorAzure.Models.Translator;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace TranslatorAzure.Services.Translator
{
    public class AzureTranslatorService : ITranslatorService
    {
        private const string LanguagesCacheKey = "translator-languages";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly TranslatorOptions _options;

        public AzureTranslatorService(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IOptions<TranslatorOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _options = options.Value;
        }

        public async Task<TranslatorLanguagesResponse> GetLanguagesAsync(CancellationToken cancellationToken = default)
        {
            return (await _cache.GetOrCreateAsync(LanguagesCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);

                try
                {
                    var client = _httpClientFactory.CreateClient();
                    var json = await client.GetStringAsync(_options.LanguagesUrl, cancellationToken);

                    return JsonSerializer.Deserialize<TranslatorLanguagesResponse>(json)
                        ?? CreateFallbackLanguages();
                }
                catch
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                    return CreateFallbackLanguages();
                }
            }))!;
        }

        public async Task<TranslatorResult> TranslateAsync(TranslatorFormModel formModel, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(formModel.Text))
            {
                return new TranslatorResult();
            }

            var languages = await GetLanguagesAsync(cancellationToken);
            var sourceLanguage = NormalizeSourceLanguage(formModel.LanguageFrom);
            var targetLanguage = string.IsNullOrWhiteSpace(formModel.LanguageTo) ? "uk" : formModel.LanguageTo;

            var translateUrl = BuildTranslateUrl(sourceLanguage, targetLanguage);
            var responseItems = await SendJsonAsync<TranslatorResponseModel[]>(
                translateUrl,
                new[] { new { Text = formModel.Text } },
                cancellationToken);

            var response = responseItems.FirstOrDefault();
            var translation = response?.Translations.FirstOrDefault();
            var translatedText = translation?.Text ?? string.Empty;
            var detectedLanguage = response?.DetectedLanguage?.Language;

            var languageForSourceTransliteration = sourceLanguage ?? detectedLanguage;

            return new TranslatorResult
            {
                SourceText = formModel.Text,
                TranslatedText = translatedText,
                TargetLanguage = targetLanguage,
                DetectedLanguage = detectedLanguage,
                DetectionScore = response?.DetectedLanguage?.Score,
                TransliterationFrom = await TryTransliterateAsync(
                    formModel.Text,
                    languageForSourceTransliteration,
                    languages,
                    cancellationToken),
                TransliterationTo = await TryTransliterateAsync(
                    translatedText,
                    targetLanguage,
                    languages,
                    cancellationToken)
            };
        }

        private string BuildTranslateUrl(string? sourceLanguage, string targetLanguage)
        {
            var url = $"{_options.Endpoint}{_options.Route}&to={Uri.EscapeDataString(targetLanguage)}";

            if (!string.IsNullOrWhiteSpace(sourceLanguage))
            {
                url += $"&from={Uri.EscapeDataString(sourceLanguage)}";
            }

            return url;
        }

        private async Task<string?> TryTransliterateAsync(
            string? text,
            string? language,
            TranslatorLanguagesResponse languages,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(language))
            {
                return null;
            }

            if (!languages.transliteration.TryGetValue(language, out var languageInfo))
            {
                return null;
            }

            var script = languageInfo.scripts?.FirstOrDefault(s => s.toScripts?.Length > 0);
            var targetScript = script?.toScripts?.FirstOrDefault(s => s.code == "Latn")
                ?? script?.toScripts?.FirstOrDefault();

            if (script?.code == null || targetScript?.code == null)
            {
                return null;
            }

            // language/fromScript/toScript are request-level parameters for the transliteration endpoint.
            var route = $"{_options.Endpoint}{_options.RouteTransliterate}" +
                $"&language={Uri.EscapeDataString(language)}" +
                $"&fromScript={Uri.EscapeDataString(script.code)}" +
                $"&toScript={Uri.EscapeDataString(targetScript.code)}";

            var response = await SendJsonAsync<TransliterationResponseModel[]>(
                route,
                new[] { new { Text = text } },
                cancellationToken);

            return response.FirstOrDefault()?.Text;
        }

        private async Task<T> SendJsonAsync<T>(string url, object body, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Ocp-Apim-Subscription-Key", _options.Key);
            request.Headers.Add("Ocp-Apim-Subscription-Region", _options.Location);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            response.EnsureSuccessStatusCode();

            return JsonSerializer.Deserialize<T>(json)
                ?? throw new InvalidOperationException("Azure Translator returned an empty response.");
        }

        private static string? NormalizeSourceLanguage(string? sourceLanguage)
        {
            return sourceLanguage == TranslatorFormModel.AutoDetectLanguageCode
                ? null
                : sourceLanguage;
        }

        private static TranslatorLanguagesResponse CreateFallbackLanguages()
        {
            return new TranslatorLanguagesResponse
            {
                translation = new Dictionary<string, LanguageInfo>
                {
                    ["en"] = new() { name = "English", nativeName = "English", dir = "ltr" },
                    ["uk"] = new() { name = "Ukrainian", nativeName = "Українська", dir = "ltr" },
                    ["es"] = new() { name = "Spanish", nativeName = "Español", dir = "ltr" },
                    ["de"] = new() { name = "German", nativeName = "Deutsch", dir = "ltr" },
                    ["fr"] = new() { name = "French", nativeName = "Français", dir = "ltr" },
                    ["pl"] = new() { name = "Polish", nativeName = "Polski", dir = "ltr" },
                    ["ja"] = new() { name = "Japanese", nativeName = "日本語", dir = "ltr" },
                    ["ko"] = new() { name = "Korean", nativeName = "한국어", dir = "ltr" },
                    ["zh-Hans"] = new() { name = "Chinese Simplified", nativeName = "中文（简体）", dir = "ltr" },
                    ["ar"] = new() { name = "Arabic", nativeName = "العربية", dir = "rtl" }
                },
                transliteration = []
            };
        }
    }
}
