namespace TranslatorAzure.Models.Translator
{
    public class TranslatorViewModel
    {
        public TranslatorLanguagesResponse TranslatorLanguages { get; set; } = null!;
        public TranslatorFormModel? FormModel { get; set; }
        public string? Translation { get; set; }
        public string? TransliterationFrom { get; set; }
        public string? TransliterationTo { get; set; }
        public string? DetectedLanguage { get; set; }
        public double? DetectionScore { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
