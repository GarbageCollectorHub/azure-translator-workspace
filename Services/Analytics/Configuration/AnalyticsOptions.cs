namespace TranslatorAzure.Services.Analytics.Configuration;

public sealed class AnalyticsOptions
{
    public const string SectionName = "analytics";

    public bool Enabled { get; set; } = false;      // Enabled = false по умолчанию — специально. Если нет azuresettings.json, приложение не должно падать.

    public string? IpHashSalt { get; set; }

    public string? IgnoreToken { get; set; }

    public int VisitCooldownMinutes { get; set; } = 30;

    public int VisitorCookieDays { get; set; } = 180;

    public bool GeoIpEnabled { get; set; } = true;
}
