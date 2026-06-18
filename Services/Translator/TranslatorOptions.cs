namespace TranslatorAzure.Services.Translator
{
    public class TranslatorOptions
    {
        public string Key { get; set; } = string.Empty;
        public string Endpoint { get; set; } = "https://api.cognitive.microsofttranslator.com";
        public string Location { get; set; } = string.Empty;
        public string Route { get; set; } = "/translate?api-version=3.0";
        public string RouteTransliterate { get; set; } = "/transliterate?api-version=3.0";
        public string LanguagesUrl { get; set; } = "https://api.cognitive.microsofttranslator.com/languages?api-version=3.0";
    }
}
