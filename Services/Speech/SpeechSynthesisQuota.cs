using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using TranslatorAzure.Infrastructure.Cosmos;
using TranslatorAzure.Models.Speech;
using TranslatorAzure.Services.Analytics.Configuration;
using TranslatorAzure.Services.Analytics.Network;

namespace TranslatorAzure.Services.Speech;

public sealed class SpeechSynthesisQuota(
    ICosmosContainerProvider containerProvider,
    IHttpContextAccessor httpContextAccessor,
    IOptions<SpeechSynthesisOptions> speechOptions,
    IOptions<AnalyticsOptions> analyticsOptions) : ISpeechSynthesisQuota
{
    private const int MaxConcurrencyAttempts = 5;
    private readonly AnalyticsOptions _analyticsOptions = analyticsOptions.Value;
    private readonly SpeechSynthesisOptions _speechOptions = speechOptions.Value;

    public async Task<SpeechSynthesisQuotaReservation?> TryReserveAsync(
        int characterCount,
        CancellationToken cancellationToken = default)
    {
        var month = DateTimeOffset.UtcNow.ToString("yyyy-MM");
        var clientDocumentId = _speechOptions.ClientMonthlyCharacterLimit > 0
            ? CreateClientDocumentId(month, ResolveClientKeyHash())
            : null;
        var globalDocumentId = _speechOptions.GlobalMonthlyCharacterLimit > 0
            ? CreateGlobalDocumentId(month)
            : null;

        if (clientDocumentId is null && globalDocumentId is null)
        {
            return new SpeechSynthesisQuotaReservation(characterCount, null, null, int.MaxValue);
        }

        var container = await containerProvider.GetContainerAsync(cancellationToken);
        var clientReservation = clientDocumentId is null
            ? QuotaReserveResult.Success(int.MaxValue)
            : await TryReserveDocumentAsync(
                container,
                clientDocumentId,
                "client",
                ResolveClientKeyHash(),
                month,
                _speechOptions.ClientMonthlyCharacterLimit,
                characterCount,
                cancellationToken);

        if (!clientReservation.IsReserved)
        {
            return null;
        }

        var globalReservation = QuotaReserveResult.Success(int.MaxValue);
        if (globalDocumentId is not null)
        {
            globalReservation = await TryReserveDocumentAsync(
                container,
                globalDocumentId,
                "global",
                null,
                month,
                _speechOptions.GlobalMonthlyCharacterLimit,
                characterCount,
                cancellationToken);

            if (!globalReservation.IsReserved)
            {
                await ReleaseDocumentAsync(container, clientDocumentId, characterCount, cancellationToken);
                return null;
            }
        }

        var remainingCharacters = Math.Min(
            clientReservation.RemainingCharacters,
            globalReservation.RemainingCharacters);

        return new SpeechSynthesisQuotaReservation(
            characterCount,
            clientDocumentId,
            globalDocumentId,
            remainingCharacters);
    }

    public async Task ReleaseAsync(
        SpeechSynthesisQuotaReservation reservation,
        CancellationToken cancellationToken = default)
    {
        var container = await containerProvider.GetContainerAsync(cancellationToken);

        await ReleaseDocumentAsync(
            container,
            reservation.ClientDocumentId,
            reservation.CharacterCount,
            cancellationToken);
        await ReleaseDocumentAsync(
            container,
            reservation.GlobalDocumentId,
            reservation.CharacterCount,
            cancellationToken);
    }

    private async Task<QuotaReserveResult> TryReserveDocumentAsync(
        Container container,
        string documentId,
        string scope,
        string? clientKeyHash,
        string month,
        int limitCharacters,
        int characterCount,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxConcurrencyAttempts; attempt++)
        {
            var now = DateTime.UtcNow;

            try
            {
                var response = await container.ReadItemAsync<SpeechSynthesisQuotaUsage>(
                    documentId,
                    new PartitionKey(documentId),
                    cancellationToken: cancellationToken);
                var usage = response.Resource;

                if (usage.UsedCharacters + characterCount > limitCharacters)
                {
                    return QuotaReserveResult.Rejected(Math.Max(0, limitCharacters - usage.UsedCharacters));
                }

                usage.UsedCharacters += characterCount;
                usage.LimitCharacters = limitCharacters;
                usage.UpdatedAtUtc = now;

                await container.ReplaceItemAsync(
                    usage,
                    usage.Id,
                    new PartitionKey(usage.Id),
                    new ItemRequestOptions { IfMatchEtag = response.ETag },
                    cancellationToken);

                return QuotaReserveResult.Success(Math.Max(0, limitCharacters - usage.UsedCharacters));
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                if (characterCount > limitCharacters)
                {
                    return QuotaReserveResult.Rejected(limitCharacters);
                }

                var usage = new SpeechSynthesisQuotaUsage
                {
                    Id = documentId,
                    Scope = scope,
                    ClientKeyHash = clientKeyHash,
                    Month = month,
                    UsedCharacters = characterCount,
                    LimitCharacters = limitCharacters,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                try
                {
                    await container.CreateItemAsync(
                        usage,
                        new PartitionKey(usage.Id),
                        cancellationToken: cancellationToken);

                    return QuotaReserveResult.Success(Math.Max(0, limitCharacters - usage.UsedCharacters));
                }
                catch (CosmosException conflictException) when (conflictException.StatusCode == HttpStatusCode.Conflict)
                {
                    // Another request created the monthly usage document first. Retry with ETag protection.
                }
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // Concurrent request updated the same usage document first. Retry with the new ETag.
            }
        }

        return QuotaReserveResult.Rejected(0);
    }

    private static async Task ReleaseDocumentAsync(
        Container container,
        string? documentId,
        int characterCount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return;
        }

        for (var attempt = 0; attempt < MaxConcurrencyAttempts; attempt++)
        {
            try
            {
                var response = await container.ReadItemAsync<SpeechSynthesisQuotaUsage>(
                    documentId,
                    new PartitionKey(documentId),
                    cancellationToken: cancellationToken);
                var usage = response.Resource;
                usage.UsedCharacters = Math.Max(0, usage.UsedCharacters - characterCount);
                usage.UpdatedAtUtc = DateTime.UtcNow;

                await container.ReplaceItemAsync(
                    usage,
                    usage.Id,
                    new PartitionKey(usage.Id),
                    new ItemRequestOptions { IfMatchEtag = response.ETag },
                    cancellationToken);

                return;
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // Retry with the new ETag.
            }
        }
    }

    private string ResolveClientKeyHash()
    {
        var httpContext = httpContextAccessor.HttpContext;
        var clientKey = httpContext is null
            ? "unknown-client"
            : ClientIpAddress.FromHttpContext(httpContext)
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown-client";

        var hasSalt = !string.IsNullOrWhiteSpace(_analyticsOptions.IpHashSalt);
        if (!hasSalt)
        {
            clientKey = "unknown-client";
        }

        var salt = hasSalt ? _analyticsOptions.IpHashSalt! : "missing-salt";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{clientKey}:{salt}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string CreateClientDocumentId(string month, string clientKeyHash)
    {
        return $"speech_quota_client_{month}_{clientKeyHash}";
    }

    private static string CreateGlobalDocumentId(string month)
    {
        return $"speech_quota_global_{month}";
    }

    private sealed record QuotaReserveResult(bool IsReserved, int RemainingCharacters)
    {
        public static QuotaReserveResult Success(int remainingCharacters)
        {
            return new QuotaReserveResult(true, remainingCharacters);
        }

        public static QuotaReserveResult Rejected(int remainingCharacters)
        {
            return new QuotaReserveResult(false, remainingCharacters);
        }
    }
}
