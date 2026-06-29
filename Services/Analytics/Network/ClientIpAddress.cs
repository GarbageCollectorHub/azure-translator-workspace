using System.Net;
using System.Net.Sockets;

namespace TranslatorAzure.Services.Analytics.Network;

public static class ClientIpAddress
{
    private static readonly string[] HeaderNames =
    [
        "CF-Connecting-IP",
        "True-Client-IP",
        "X-Azure-ClientIP",
        "X-Client-IP",
        "X-Real-IP",
        "X-Forwarded-For"
    ];

    public static string? FromHttpContext(HttpContext httpContext)
    {
        var candidates = HeaderNames
            .SelectMany(headerName => SplitHeader(httpContext.Request.Headers[headerName].ToString()))
            .Select(TryParse)
            .Where(ipAddress => ipAddress != null)
            .Cast<IPAddress>()
            .ToList();

        if (httpContext.Connection.RemoteIpAddress != null)
        {
            candidates.Add(Normalize(httpContext.Connection.RemoteIpAddress));
        }

        return candidates.FirstOrDefault(IsPublic)?.ToString()
            ?? candidates.FirstOrDefault()?.ToString();
    }

    public static bool IsPublic(string? value)
    {
        var ipAddress = TryParse(value);
        return ipAddress != null && IsPublic(ipAddress);
    }

    public static bool IsPublic(IPAddress ipAddress)
    {
        ipAddress = Normalize(ipAddress);

        if (IPAddress.IsLoopback(ipAddress))
        {
            return false;
        }

        return ipAddress.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicIPv4(ipAddress),
            AddressFamily.InterNetworkV6 => IsPublicIPv6(ipAddress),
            _ => false
        };
    }

    private static IEnumerable<string> SplitHeader(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IPAddress? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim().Trim('"');

        if (value.StartsWith('['))
        {
            var bracketEnd = value.IndexOf(']');
            if (bracketEnd > 0)
            {
                value = value[1..bracketEnd];
            }
        }

        if (IPAddress.TryParse(value, out var ipAddress))
        {
            return Normalize(ipAddress);
        }

        var colonCount = value.Count(character => character == ':');
        var portSeparator = value.LastIndexOf(':');

        if (colonCount == 1
            && portSeparator > 0
            && IPAddress.TryParse(value[..portSeparator], out ipAddress))
        {
            return Normalize(ipAddress);
        }

        return null;
    }

    private static IPAddress Normalize(IPAddress ipAddress)
    {
        return ipAddress.IsIPv4MappedToIPv6
            ? ipAddress.MapToIPv4()
            : ipAddress;
    }

    private static bool IsPublicIPv4(IPAddress ipAddress)
    {
        var bytes = ipAddress.GetAddressBytes();

        return bytes[0] switch
        {
            0 => false,
            10 => false,
            100 when bytes[1] is >= 64 and <= 127 => false,
            127 => false,
            169 when bytes[1] == 254 => false,
            172 when bytes[1] is >= 16 and <= 31 => false,
            192 when bytes[1] == 168 => false,
            192 when bytes[1] == 0 && bytes[2] == 2 => false,
            198 when bytes[1] is 18 or 19 => false,
            198 when bytes[1] == 51 && bytes[2] == 100 => false,
            203 when bytes[1] == 0 && bytes[2] == 113 => false,
            >= 224 => false,
            _ => true
        };
    }

    private static bool IsPublicIPv6(IPAddress ipAddress)
    {
        if (ipAddress.IsIPv6LinkLocal
            || ipAddress.IsIPv6Multicast
            || ipAddress.IsIPv6SiteLocal)
        {
            return false;
        }

        var bytes = ipAddress.GetAddressBytes();

        return bytes[0] switch
        {
            0 => false,
            0xfc or 0xfd => false,
            0x20 when bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8 => false,
            _ => true
        };
    }
}
