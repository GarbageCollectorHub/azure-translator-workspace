namespace TranslatorAzure.Services.Analytics.GeoIp;

public interface IGeoIpService
{
    Task<GeoIpInfo?> GetGeoInfoAsync(string ipAddress, CancellationToken cancellationToken);
}