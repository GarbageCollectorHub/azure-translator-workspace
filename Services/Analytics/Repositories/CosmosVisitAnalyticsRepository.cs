using Microsoft.Azure.Cosmos;
using System.Net;
using TranslatorAzure.Infrastructure.Cosmos;
using TranslatorAzure.Models.Analytics;
using TranslatorAzure.Services.Analytics.Events;
using TranslatorAzure.Services.Analytics.GeoIp;
using TranslatorAzure.Services.Analytics.UserAgent;

namespace TranslatorAzure.Services.Analytics.Repositories;

public sealed class CosmosVisitAnalyticsRepository(ICosmosContainerProvider containerProvider) : IVisitAnalyticsRepository
{
    private const int MaxLastVisits = 10;

    public async Task<bool> UpsertVisitAsync(
        AnalyticsEvent analyticsEvent,
        string? ipHash,
        BrowserInfo browserInfo,
        CancellationToken cancellationToken)
    {
        var id = CreateDocumentId(analyticsEvent.VisitorId);
        var container = await containerProvider.GetContainerAsync(cancellationToken);

        try
        {
            var response = await container.ReadItemAsync<VisitProfile>(
                id,
                new PartitionKey(id),
                cancellationToken: cancellationToken);

            var profile = response.Resource;
            var previousIpHash = profile.IpHash;
            var countAsVisit = analyticsEvent.CountAsVisit
                || profile.VisitCount == 0
                || profile.LastVisitsUtc.Count == 0;

            profile.LastSeenAtUtc = analyticsEvent.CreatedAtUtc;

            if (countAsVisit)
            {
                profile.VisitCount += 1;
                profile.LastVisitsUtc.Insert(0, analyticsEvent.CreatedAtUtc);
                profile.LastVisitsUtc = profile.LastVisitsUtc
                    .Distinct()
                    .OrderByDescending(date => date)
                    .Take(MaxLastVisits)
                    .ToList();
            }

            profile.IpHash = ipHash ?? profile.IpHash;
            profile.UserAgent = analyticsEvent.UserAgent ?? profile.UserAgent;
            profile.AcceptLanguage = analyticsEvent.AcceptLanguage ?? profile.AcceptLanguage;
            profile.Browser = browserInfo.Browser ?? profile.Browser;
            profile.OperatingSystem = browserInfo.OperatingSystem ?? profile.OperatingSystem;
            profile.DeviceType = browserInfo.DeviceType ?? profile.DeviceType;
            profile.LastPagePath = analyticsEvent.PagePath ?? profile.LastPagePath;
            profile.LastReferrer = analyticsEvent.Referrer ?? profile.LastReferrer;

            if (!string.IsNullOrWhiteSpace(analyticsEvent.Source))
            {
                profile.FirstSource ??= analyticsEvent.Source;
                profile.LastSource = analyticsEvent.Source;
            }

            var shouldLookupGeo = ShouldLookupGeo(
                profile,
                analyticsEvent.IpAddress,
                ipHash,
                previousIpHash);

            if (shouldLookupGeo)
            {
                profile.GeoStatus = VisitProfile.GeoPending;
            }

            await container.ReplaceItemAsync(
                profile,
                profile.Id,
                new PartitionKey(profile.Id),
                cancellationToken: cancellationToken);

            return shouldLookupGeo;
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            var shouldLookupGeo = !string.IsNullOrWhiteSpace(analyticsEvent.IpAddress);

            var profile = new VisitProfile
            {
                Id = id,
                VisitorId = analyticsEvent.VisitorId,
                FirstSeenAtUtc = analyticsEvent.CreatedAtUtc,
                LastSeenAtUtc = analyticsEvent.CreatedAtUtc,
                VisitCount = 1,
                LastVisitsUtc = [analyticsEvent.CreatedAtUtc],
                IpHash = ipHash,
                UserAgent = analyticsEvent.UserAgent,
                AcceptLanguage = analyticsEvent.AcceptLanguage,
                Browser = browserInfo.Browser,
                OperatingSystem = browserInfo.OperatingSystem,
                DeviceType = browserInfo.DeviceType,
                FirstSource = analyticsEvent.Source,
                LastSource = analyticsEvent.Source,
                LastPagePath = analyticsEvent.PagePath,
                LastReferrer = analyticsEvent.Referrer,
                GeoStatus = shouldLookupGeo ? VisitProfile.GeoPending : VisitProfile.GeoUnavailable
            };

            try
            {
                await container.CreateItemAsync(
                    profile,
                    new PartitionKey(profile.Id),
                    cancellationToken: cancellationToken);

                return shouldLookupGeo;
            }
            catch (CosmosException conflictException) when (conflictException.StatusCode == HttpStatusCode.Conflict)
            {
                return false;
            }
        }
    }

    public async Task UpdateGeoAsync(string visitorId, GeoIpInfo geoInfo, CancellationToken cancellationToken)
    {
        var id = CreateDocumentId(visitorId);
        var container = await containerProvider.GetContainerAsync(cancellationToken);

        await PatchIfExistsAsync(
            container,
            id,
            [
                PatchOperation.Set("/country", geoInfo.Country),
                PatchOperation.Set("/region", geoInfo.Region),
                PatchOperation.Set("/city", geoInfo.City),
                PatchOperation.Set("/geoStatus", VisitProfile.GeoCompleted)
            ],
            cancellationToken);
    }

    public async Task UpdateGeoStatusAsync(string visitorId, string geoStatus, CancellationToken cancellationToken)
    {
        var id = CreateDocumentId(visitorId);
        var container = await containerProvider.GetContainerAsync(cancellationToken);

        await PatchIfExistsAsync(
            container,
            id,
            [PatchOperation.Set("/geoStatus", geoStatus)],
            cancellationToken);
    }

    public Task IncrementMainTranslationCountAsync(string visitorId, CancellationToken cancellationToken)
    {
        return IncrementCounterAsync(visitorId, "/mainTranslationCount", cancellationToken);
    }

    public Task IncrementSelectionTranslationCountAsync(string visitorId, CancellationToken cancellationToken)
    {
        return IncrementCounterAsync(visitorId, "/selectionTranslationCount", cancellationToken);
    }

    public Task IncrementPdfSelectionTranslationCountAsync(string visitorId, CancellationToken cancellationToken)
    {
        return IncrementCounterAsync(visitorId, "/pdfSelectionTranslationCount", cancellationToken);
    }

    public Task IncrementFileImportCountAsync(string visitorId, CancellationToken cancellationToken)
    {
        return IncrementCounterAsync(visitorId, "/fileImportCount", cancellationToken);
    }

    public Task IncrementSpeechSynthesisUsageAsync(
        string visitorId,
        int characterCount,
        CancellationToken cancellationToken)
    {
        return IncrementSpeechSynthesisUsageCoreAsync(
            visitorId,
            Math.Max(0, characterCount),
            cancellationToken);
    }

    private async Task IncrementCounterAsync(
        string visitorId,
        string counterPath,
        CancellationToken cancellationToken)
    {
        var id = CreateDocumentId(visitorId);
        var container = await containerProvider.GetContainerAsync(cancellationToken);

        try
        {
            await container.PatchItemAsync<VisitProfile>(
                id,
                new PartitionKey(id),
                [
                    PatchOperation.Increment(counterPath, 1),
                    PatchOperation.Set("/lastSeenAtUtc", DateTime.UtcNow)
                ],
                cancellationToken: cancellationToken);
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            var profile = CreateEmptyProfile(visitorId, id, DateTime.UtcNow);

            ApplyCounter(profile, counterPath);

            try
            {
                await container.CreateItemAsync(
                    profile,
                    new PartitionKey(profile.Id),
                    cancellationToken: cancellationToken);
            }
            catch (CosmosException conflictException) when (conflictException.StatusCode == HttpStatusCode.Conflict)
            {
                await container.PatchItemAsync<VisitProfile>(
                    id,
                    new PartitionKey(id),
                    [PatchOperation.Increment(counterPath, 1)],
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task IncrementSpeechSynthesisUsageCoreAsync(
        string visitorId,
        int characterCount,
        CancellationToken cancellationToken)
    {
        var id = CreateDocumentId(visitorId);
        var container = await containerProvider.GetContainerAsync(cancellationToken);

        try
        {
            await container.PatchItemAsync<VisitProfile>(
                id,
                new PartitionKey(id),
                [
                    PatchOperation.Increment("/speechSynthesisCount", 1),
                    PatchOperation.Increment("/speechCharacterCount", characterCount),
                    PatchOperation.Set("/lastSeenAtUtc", DateTime.UtcNow)
                ],
                cancellationToken: cancellationToken);
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            var profile = CreateEmptyProfile(visitorId, id, DateTime.UtcNow);
            profile.SpeechSynthesisCount = 1;
            profile.SpeechCharacterCount = characterCount;

            try
            {
                await container.CreateItemAsync(
                    profile,
                    new PartitionKey(profile.Id),
                    cancellationToken: cancellationToken);
            }
            catch (CosmosException conflictException) when (conflictException.StatusCode == HttpStatusCode.Conflict)
            {
                await container.PatchItemAsync<VisitProfile>(
                    id,
                    new PartitionKey(id),
                    [
                        PatchOperation.Increment("/speechSynthesisCount", 1),
                        PatchOperation.Increment("/speechCharacterCount", characterCount)
                    ],
                    cancellationToken: cancellationToken);
            }
        }
    }

    private static async Task PatchIfExistsAsync(
        Container container,
        string id,
        IReadOnlyList<PatchOperation> operations,
        CancellationToken cancellationToken)
    {
        try
        {
            await container.PatchItemAsync<VisitProfile>(
                id,
                new PartitionKey(id),
                operations,
                cancellationToken: cancellationToken);
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // Analytics document was not created yet or was deleted. Ignore.
        }
    }

    private static VisitProfile CreateEmptyProfile(string visitorId, string id, DateTime now)
    {
        return new VisitProfile
        {
            Id = id,
            VisitorId = visitorId,
            FirstSeenAtUtc = now,
            LastSeenAtUtc = now,
            VisitCount = 0,
            LastVisitsUtc = [],
            GeoStatus = VisitProfile.GeoUnavailable
        };
    }

    private static void ApplyCounter(VisitProfile profile, string counterPath)
    {
        switch (counterPath)
        {
            case "/mainTranslationCount":
                profile.MainTranslationCount += 1;
                break;

            case "/selectionTranslationCount":
                profile.SelectionTranslationCount += 1;
                break;

            case "/pdfSelectionTranslationCount":
                profile.PdfSelectionTranslationCount += 1;
                break;

            case "/fileImportCount":
                profile.FileImportCount += 1;
                break;
        }
    }

    private static bool ShouldLookupGeo(
        VisitProfile profile,
        string? ipAddress,
        string? currentIpHash,
        string? previousIpHash)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || !string.IsNullOrWhiteSpace(profile.Country))
        {
            return false;
        }

        if (profile.GeoStatus == VisitProfile.GeoCompleted)
        {
            return false;
        }

        if (profile.GeoStatus == VisitProfile.GeoNonPublic)
        {
            return !string.IsNullOrWhiteSpace(currentIpHash)
                && currentIpHash != previousIpHash;
        }

        return true;
    }

    private static string CreateDocumentId(string visitorId)
    {
        return $"visitor_{visitorId}";
    }
}
