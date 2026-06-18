using System.Text.Json;
using System.Text.Json.Serialization;

namespace TranslatorAzure.Models.Translator
{
    public class TranslatorResponseModel
    {
        [JsonPropertyName("detectedLanguage")]
        public DetectedLanguage? DetectedLanguage { get; set; }

        [JsonPropertyName("translations")]
        public Translation[] Translations { get; set; } = [];

        public static TranslatorResponseModel[] FromJson(string json)
        {
            return JsonSerializer.Deserialize<TranslatorResponseModel[]>(json) ?? [];
        }
    }

    public class DetectedLanguage
    {
        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }

    public class Translation
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;
    }
}
