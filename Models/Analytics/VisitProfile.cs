using Newtonsoft.Json;

namespace TranslatorAzure.Models.Analytics;

public sealed class VisitProfile
{
    public const string GeoPending = "Pending";
    public const string GeoCompleted = "Completed";
    public const string GeoUnavailable = "Unavailable";

    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    [JsonProperty("visitorId")]
    public string VisitorId { get; set; } = default!;

    [JsonProperty("firstSeenAtUtc")]
    public DateTime FirstSeenAtUtc { get; set; }

    [JsonProperty("lastSeenAtUtc")]
    public DateTime LastSeenAtUtc { get; set; }

    [JsonProperty("visitCount")]
    public int VisitCount { get; set; }

    [JsonProperty("lastVisitsUtc")]
    public List<DateTime> LastVisitsUtc { get; set; } = [];

    [JsonProperty("ipHash")]
    public string? IpHash { get; set; }

    [JsonProperty("country")]
    public string? Country { get; set; }

    [JsonProperty("region")]
    public string? Region { get; set; }

    [JsonProperty("city")]
    public string? City { get; set; }

    [JsonProperty("geoStatus")]
    public string? GeoStatus { get; set; }

    [JsonProperty("userAgent")]
    public string? UserAgent { get; set; }

    [JsonProperty("browser")]
    public string? Browser { get; set; }

    [JsonProperty("operatingSystem")]
    public string? OperatingSystem { get; set; }

    [JsonProperty("deviceType")]
    public string? DeviceType { get; set; }

    [JsonProperty("acceptLanguage")]
    public string? AcceptLanguage { get; set; }

    [JsonProperty("mainTranslationCount")]
    public int MainTranslationCount { get; set; }

    [JsonProperty("selectionTranslationCount")]
    public int SelectionTranslationCount { get; set; }

    [JsonProperty("pdfSelectionTranslationCount")]
    public int PdfSelectionTranslationCount { get; set; }

    [JsonProperty("fileImportCount")]
    public int FileImportCount { get; set; }
}