using System.Diagnostics;
using System.Net;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace TranslatorAzure.Services.Speech;

public sealed class AzureSpeechSynthesisService(
    HttpClient httpClient,
    IMemoryCache cache,
    ISpeechSynthesisQuota quota,
    IOptions<SpeechSynthesisOptions> options,
    ILogger<AzureSpeechSynthesisService> logger) : ISpeechSynthesisService, ISpeechSynthesisWarmup
{
    private const int MaxLoggedErrorBodyLength = 500;
    private const string VoicesCacheKey = "azure-speech-voices";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SpeechSynthesisOptions _options = options.Value;

    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled
            || string.IsNullOrWhiteSpace(_options.Key)
            || !TryResolveSpeechEndpoint(out _))
        {
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        var voices = await GetVoicesAsync(cancellationToken);

        logger.LogInformation(
            "Azure Speech voices cache warmed with {VoiceCount} voices in {ElapsedMilliseconds} ms.",
            voices.Count,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
    }

    public async Task<SpeechSynthesisResult> SynthesizeAsync(
        string text,
        string language,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var normalizedText = text.Trim();
        var normalizedLanguage = language.Trim();

        if (!_options.Enabled
            || string.IsNullOrWhiteSpace(_options.Key)
            || !TryResolveSpeechEndpoint(out var speechEndpoint))
        {
            throw new SpeechSynthesisException(
                "Audio is temporarily unavailable. Please try again later.",
                StatusCodes.Status503ServiceUnavailable,
                "Audio is not configured. Add SpeechSynthesis:Key and SpeechSynthesis:Region or SpeechSynthesis:Endpoint.");
        }

        if (normalizedText.Length == 0 || normalizedText.Length > _options.MaxTextLength)
        {
            throw new SpeechSynthesisException(
                $"Enter up to {_options.MaxTextLength:N0} characters to listen.",
                StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(normalizedLanguage)
            || string.Equals(normalizedLanguage, "auto", StringComparison.OrdinalIgnoreCase))
        {
            throw new SpeechSynthesisException(
                "Translate once before listening to auto-detected text.",
                StatusCodes.Status400BadRequest);
        }

        var voice = await ResolveVoiceAsync(normalizedLanguage, cancellationToken);
        var voiceResolvedAt = Stopwatch.GetTimestamp();
        if (voice is null)
        {
            throw new SpeechSynthesisException(
                "Audio is not supported for this language yet.",
                StatusCodes.Status404NotFound,
                $"No Azure Speech voice matched language '{normalizedLanguage}'.");
        }

        var reservation = await quota.TryReserveAsync(normalizedText.Length, cancellationToken);
        var quotaReservedAt = Stopwatch.GetTimestamp();
        if (reservation is null)
        {
            throw new SpeechSynthesisException(
                "Monthly audio limit reached. Please try again later.",
                StatusCodes.Status429TooManyRequests);
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                CreateSpeechUri(speechEndpoint, "cognitiveservices/v1"));

            request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", _options.Key);
            request.Headers.TryAddWithoutValidation("X-Microsoft-OutputFormat", _options.OutputFormat);
            request.Headers.TryAddWithoutValidation("User-Agent", "TranslatorAzure");
            request.Content = new StringContent(
                CreateSsml(normalizedText, voice.Locale, voice.ShortName),
                Encoding.UTF8,
                "application/ssml+xml");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await ReadShortErrorBodyAsync(response, cancellationToken);
                logger.LogWarning(
                    "Azure Speech synthesis failed with status {StatusCode} {ReasonPhrase}. Body: {Body}",
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    errorBody);

                throw CreateServiceException(response.StatusCode, errorBody);
            }

            var audio = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var completedAt = Stopwatch.GetTimestamp();
            if (audio.Length == 0)
            {
                throw new SpeechSynthesisException(
                    "Audio is temporarily unavailable. Please try again.",
                    StatusCodes.Status503ServiceUnavailable);
            }

            logger.LogInformation(
                "Azure Speech synthesized {CharacterCount} characters with {VoiceName} in {ElapsedMilliseconds} ms. Voice lookup: {VoiceLookupMilliseconds} ms, quota: {QuotaMilliseconds} ms, Azure request: {AzureRequestMilliseconds} ms.",
                normalizedText.Length,
                voice.ShortName,
                Stopwatch.GetElapsedTime(startedAt, completedAt).TotalMilliseconds,
                Stopwatch.GetElapsedTime(startedAt, voiceResolvedAt).TotalMilliseconds,
                Stopwatch.GetElapsedTime(voiceResolvedAt, quotaReservedAt).TotalMilliseconds,
                Stopwatch.GetElapsedTime(quotaReservedAt, completedAt).TotalMilliseconds);

            return new SpeechSynthesisResult(audio, "audio/mpeg", voice.ShortName);
        }
        catch
        {
            await quota.ReleaseAsync(reservation, cancellationToken);
            throw;
        }
    }

    private async Task<SpeechVoice?> ResolveVoiceAsync(string language, CancellationToken cancellationToken)
    {
        var voices = await GetVoicesAsync(cancellationToken);
        var languageCandidates = SpeechLanguageCode.GetSpeechLocaleCandidates(language);
        return voices
            .Select(voice => new
            {
                Voice = voice,
                MatchScore = voice.GetMatchScore(languageCandidates)
            })
            .Where(match => match.MatchScore > 0)
            .OrderByDescending(match => match.MatchScore)
            .ThenByDescending(match => match.Voice.IsNeural)
            .ThenBy(match => match.Voice.Locale.Length)
            .ThenBy(match => match.Voice.ShortName)
            .Select(match => match.Voice)
            .FirstOrDefault();
    }

    private async Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync(CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(VoicesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            return await LoadVoicesAsync(cancellationToken);
        }) ?? [];
    }

    private async Task<List<SpeechVoice>> LoadVoicesAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Key)
            || !TryResolveSpeechEndpoint(out var speechEndpoint))
        {
            return [];
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            CreateSpeechUri(speechEndpoint, "cognitiveservices/voices/list"));

        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", _options.Key);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await ReadShortErrorBodyAsync(response, cancellationToken);
            logger.LogWarning(
                "Azure Speech voices list failed with status {StatusCode} {ReasonPhrase}. Body: {Body}",
                (int)response.StatusCode,
                response.ReasonPhrase,
                errorBody);
            throw CreateServiceException(response.StatusCode, errorBody);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<List<SpeechVoice>>(json, JsonOptions) ?? [];
    }

    private static SpeechSynthesisException CreateServiceException(HttpStatusCode statusCode, string? responseBody)
    {
        var developerMessage = string.IsNullOrWhiteSpace(responseBody)
            ? $"Azure Speech returned {(int)statusCode} {statusCode}."
            : $"Azure Speech returned {(int)statusCode} {statusCode}: {responseBody}";

        return statusCode is HttpStatusCode.TooManyRequests
            ? new SpeechSynthesisException(
                "Audio limit is currently unavailable. Please try again later.",
                StatusCodes.Status429TooManyRequests,
                developerMessage)
            : new SpeechSynthesisException(
                "Audio is temporarily unavailable. Please try again.",
                StatusCodes.Status503ServiceUnavailable,
                developerMessage);
    }

    private static async Task<string?> ReadShortErrorBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var normalizedBody = body.ReplaceLineEndings(" ").Trim();
        return normalizedBody.Length <= MaxLoggedErrorBodyLength
            ? normalizedBody
            : normalizedBody[..MaxLoggedErrorBodyLength];
    }

    private bool TryResolveSpeechEndpoint(out Uri speechEndpoint)
    {
        speechEndpoint = null!;

        if (!string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            return TryResolveConfiguredEndpoint(_options.Endpoint, out speechEndpoint);
        }

        if (string.IsNullOrWhiteSpace(_options.Region))
        {
            return false;
        }

        var resolved = Uri.TryCreate(
            $"https://{_options.Region.Trim()}.tts.speech.microsoft.com/",
            UriKind.Absolute,
            out var regionalEndpoint);
        speechEndpoint = regionalEndpoint!;
        return resolved;
    }

    private static bool TryResolveConfiguredEndpoint(string endpoint, out Uri speechEndpoint)
    {
        speechEndpoint = null!;

        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var configuredEndpoint))
        {
            return false;
        }

        if (TryResolvePortalEndpoint(configuredEndpoint, out speechEndpoint))
        {
            return true;
        }

        var root = configuredEndpoint.GetLeftPart(UriPartial.Authority);
        var resolved = Uri.TryCreate($"{root}/", UriKind.Absolute, out var configuredSpeechEndpoint);
        speechEndpoint = configuredSpeechEndpoint!;
        return resolved;
    }

    private static bool TryResolvePortalEndpoint(Uri endpoint, out Uri speechEndpoint)
    {
        speechEndpoint = null!;

        const string portalHostSuffix = ".api.cognitive.microsoft.com";
        if (!endpoint.Host.EndsWith(portalHostSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var region = endpoint.Host[..^portalHostSuffix.Length];
        if (string.IsNullOrWhiteSpace(region))
        {
            return false;
        }

        var resolved = Uri.TryCreate(
            $"{endpoint.Scheme}://{region}.tts.speech.microsoft.com/",
            UriKind.Absolute,
            out var portalSpeechEndpoint);
        speechEndpoint = portalSpeechEndpoint!;
        return resolved;
    }

    private static Uri CreateSpeechUri(Uri speechEndpoint, string relativePath)
    {
        return new Uri(speechEndpoint, relativePath);
    }

    private static string CreateSsml(string text, string locale, string voiceName)
    {
        return $"""
            <speak version="1.0" xml:lang="{XmlEscape(locale)}" xmlns="http://www.w3.org/2001/10/synthesis">
              <voice name="{XmlEscape(voiceName)}">{XmlEscape(text)}</voice>
            </speak>
            """;
    }

    private static string XmlEscape(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }

    private sealed class SpeechVoice
    {
        public string ShortName { get; init; } = string.Empty;

        public string Locale { get; init; } = string.Empty;

        public string VoiceType { get; init; } = string.Empty;

        public IReadOnlyList<string> SecondaryLocaleList { get; init; } = [];

        public bool IsNeural => VoiceType.Contains("Neural", StringComparison.OrdinalIgnoreCase);

        public int GetMatchScore(IReadOnlyList<string> languageCandidates)
        {
            for (var index = 0; index < languageCandidates.Count; index++)
            {
                var language = languageCandidates[index];
                var priorityOffset = index * 4;

                if (Locale.Equals(language, StringComparison.OrdinalIgnoreCase))
                {
                    return 100 - priorityOffset;
                }

                if (Locale.StartsWith($"{language}-", StringComparison.OrdinalIgnoreCase))
                {
                    return 90 - priorityOffset;
                }

                if (SecondaryLocaleList.Any(locale =>
                        locale.Equals(language, StringComparison.OrdinalIgnoreCase)
                        || locale.StartsWith($"{language}-", StringComparison.OrdinalIgnoreCase)))
                {
                    return 80 - priorityOffset;
                }
            }

            return 0;
        }
    }
}
