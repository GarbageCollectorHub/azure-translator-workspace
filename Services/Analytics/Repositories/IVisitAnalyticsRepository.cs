using TranslatorAzure.Services.Analytics.Events;
using TranslatorAzure.Services.Analytics.GeoIp;
using TranslatorAzure.Services.Analytics.UserAgent;

namespace TranslatorAzure.Services.Analytics.Repositories;

public interface IVisitAnalyticsRepository
{
    Task<bool> UpsertVisitAsync(
        AnalyticsEvent analyticsEvent,
        string? ipHash,
        BrowserInfo browserInfo,
        CancellationToken cancellationToken);

    Task UpdateGeoAsync(string visitorId, GeoIpInfo geoInfo, CancellationToken cancellationToken);

    Task UpdateGeoStatusAsync(string visitorId, string geoStatus, CancellationToken cancellationToken);

    Task IncrementMainTranslationCountAsync(string visitorId, CancellationToken cancellationToken);

    Task IncrementSelectionTranslationCountAsync(string visitorId, CancellationToken cancellationToken);

    Task IncrementPdfSelectionTranslationCountAsync(string visitorId, CancellationToken cancellationToken);

    Task IncrementFileImportCountAsync(string visitorId, CancellationToken cancellationToken);

    Task IncrementSpeechSynthesisUsageAsync(
        string visitorId,
        int characterCount,
        CancellationToken cancellationToken);
}
