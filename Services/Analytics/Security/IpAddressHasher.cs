using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TranslatorAzure.Services.Analytics.Configuration;

namespace TranslatorAzure.Services.Analytics.Security;

public sealed class IpAddressHasher(IOptions<AnalyticsOptions> options)
{
    private readonly AnalyticsOptions _options = options.Value;

    public string? Hash(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(_options.IpHashSalt))
        {
            return null;
        }

        var input = $"{ipAddress}:{_options.IpHashSalt}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}