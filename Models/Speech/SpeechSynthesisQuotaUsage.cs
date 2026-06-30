using Newtonsoft.Json;

namespace TranslatorAzure.Models.Speech;

public sealed class SpeechSynthesisQuotaUsage
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    [JsonProperty("documentType")]
    public string DocumentType { get; set; } = "speechQuota";

    [JsonProperty("scope")]
    public string Scope { get; set; } = default!;

    [JsonProperty("clientKeyHash")]
    public string? ClientKeyHash { get; set; }

    [JsonProperty("month")]
    public string Month { get; set; } = default!;

    [JsonProperty("usedCharacters")]
    public int UsedCharacters { get; set; }

    [JsonProperty("limitCharacters")]
    public int LimitCharacters { get; set; }

    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [JsonProperty("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }
}
