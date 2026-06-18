using System.Text.Json.Serialization;

namespace TranslatorAzure.Models.Translator
{
    public class TransliterationResponseModel
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("script")]
        public string Script { get; set; } = string.Empty;
    }
}
