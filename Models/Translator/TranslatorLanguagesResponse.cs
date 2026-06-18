namespace TranslatorAzure.Models.Translator
{
    public class TranslatorLanguagesResponse
    {
        public Dictionary<string, LanguageInfo> translation { get; set; } = [];
        public Dictionary<string, LanguageInfo> transliteration { get; set; } = [];
    }

    public class LanguageInfo
    {
        public string? code { get; set; }
        public string name { get; set; } = string.Empty;
        public string nativeName { get; set; } = string.Empty;
        public string? dir { get; set; }
        public LanguageInfo[]? scripts { get; set; }
        public LanguageInfo[]? toScripts { get; set; }
    }
}
