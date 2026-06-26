using Microsoft.AspNetCore.Mvc;

namespace TranslatorAzure.Models.Translator
{
    public class TranslatorFormModel
    {
        public const string AutoDetectLanguageCode = "auto";

        [FromForm(Name = "lang-from")]
        public string LanguageFrom { get; set; } = AutoDetectLanguageCode;

        [FromForm(Name = "lang-to")]
        public string LanguageTo { get; set; } = "uk";

        [FromForm(Name = "text")]
        public string? Text { get; set; }

        [FromForm(Name = "translation")]
        public string? Translation { get; set; }

        [FromForm(Name = "detected-lang")]
        public string? DetectedLanguage { get; set; }

        [FromForm(Name = "btn")]
        public string? Button { get; set; }
    }
}