using Microsoft.Extensions.Options;
using TranslatorAzure.Models.Analytics;
using TranslatorAzure.Services.Analytics.Configuration;
using TranslatorAzure.Services.Analytics.Events;
using TranslatorAzure.Services.Analytics.GeoIp;
using TranslatorAzure.Services.Analytics.Network;
using TranslatorAzure.Services.Analytics.Repositories;
using TranslatorAzure.Services.Analytics.Security;
using TranslatorAzure.Services.Analytics.UserAgent;

namespace TranslatorAzure.Services.Analytics.Handlers;

public sealed class AnalyticsEventHandler(
    IVisitAnalyticsRepository repository,
    IpAddressHasher ipAddressHasher,
    UserAgentParser userAgentParser,
    IGeoIpService geoIpService,
    IOptions<AnalyticsOptions> options) : IAnalyticsEventHandler
{
    private readonly AnalyticsOptions _options = options.Value;

    public async Task HandleAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        switch (analyticsEvent.Type)
        {
            case AnalyticsEventType.VisitOpened:
                await HandleVisitOpenedAsync(analyticsEvent, cancellationToken);
                break;

            case AnalyticsEventType.MainTranslationCompleted:
                await repository.IncrementMainTranslationCountAsync(analyticsEvent.VisitorId, cancellationToken);
                break;

            case AnalyticsEventType.SelectionTranslationCompleted:
                await repository.IncrementSelectionTranslationCountAsync(analyticsEvent.VisitorId, cancellationToken);
                break;

            case AnalyticsEventType.PdfSelectionTranslationCompleted:
                await repository.IncrementPdfSelectionTranslationCountAsync(analyticsEvent.VisitorId, cancellationToken);
                break;

            case AnalyticsEventType.FileImported:
                await repository.IncrementFileImportCountAsync(analyticsEvent.VisitorId, cancellationToken);
                break;

            case AnalyticsEventType.SpeechSynthesisCompleted:
                await repository.IncrementSpeechSynthesisUsageAsync(
                    analyticsEvent.VisitorId,
                    analyticsEvent.CharacterCount,
                    cancellationToken);
                break;
        }
    }

    private async Task HandleVisitOpenedAsync(
        AnalyticsEvent analyticsEvent,
        CancellationToken cancellationToken)
    {
        var ipHash = ipAddressHasher.Hash(analyticsEvent.IpAddress);
        var browserInfo = userAgentParser.Parse(analyticsEvent.UserAgent);

        var shouldLookupGeo = await repository.UpsertVisitAsync(
            analyticsEvent,
            ipHash,
            browserInfo,
            cancellationToken);

        if (!_options.GeoIpEnabled || !shouldLookupGeo || string.IsNullOrWhiteSpace(analyticsEvent.IpAddress))
        {
            return;
        }

        if (!ClientIpAddress.IsPublic(analyticsEvent.IpAddress))
        {
            await repository.UpdateGeoStatusAsync(
                analyticsEvent.VisitorId,
                VisitProfile.GeoNonPublic,
                cancellationToken);

            return;
        }

        var geoInfo = await geoIpService.GetGeoInfoAsync(
            analyticsEvent.IpAddress,
            cancellationToken);

        if (geoInfo == null)
        {
            await repository.UpdateGeoStatusAsync(
                analyticsEvent.VisitorId,
                VisitProfile.GeoUnavailable,
                cancellationToken);

            return;
        }

        await repository.UpdateGeoAsync(
            analyticsEvent.VisitorId,
            geoInfo,
            cancellationToken);
    }
}
