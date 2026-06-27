using System.Net.Http.Json;

namespace TranslatorAzure.Services.Analytics.GeoIp;

public sealed class IpApiGeoIpService(HttpClient httpClient, ILogger<IpApiGeoIpService> logger) : IGeoIpService
{
    public async Task<GeoIpInfo?> GetGeoInfoAsync(string ipAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        var url = $"http://ip-api.com/json/{Uri.EscapeDataString(ipAddress)}?fields=status,country,regionName,city";

        try
        {
            var response = await httpClient.GetFromJsonAsync<IpApiResponse>(url, cancellationToken);

            if (response?.Status != "success")
            {
                return null;
            }

            return new GeoIpInfo
            {
                Country = response.Country,
                Region = response.RegionName,
                City = response.City
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "GeoIP lookup failed.");
            return null;
        }
    }

    private sealed class IpApiResponse
    {
        public string? Status { get; init; }

        public string? Country { get; init; }

        public string? RegionName { get; init; }

        public string? City { get; init; }
    }
}