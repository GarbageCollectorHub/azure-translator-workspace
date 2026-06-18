using Microsoft.AspNetCore.Mvc;

namespace TranslatorAzure.Models.Translator
{
    public class TranslatorFormModel
    {
        public const string AutoDetectLanguageCode = "auto";

        [FromQuery(Name = "lang-from")]
        public string LanguageFrom { get; set; } = AutoDetectLanguageCode;

        [FromQuery(Name = "lang-to")]
        public string LanguageTo { get; set; } = "uk";

        [FromQuery(Name = "text")]
        public string? Text { get; set; }

        [FromQuery(Name = "translation")]
        public string? Translation { get; set; }

        [FromQuery(Name = "detected-lang")]
        public string? DetectedLanguage { get; set; }

        [FromQuery(Name = "btn")]
        public string? Button { get; set; }
    }
}
