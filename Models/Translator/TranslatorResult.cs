namespace TranslatorAzure.Models.Translator
{
    public class TranslatorResult
    {
        public string SourceText { get; set; } = string.Empty;
        public string TranslatedText { get; set; } = string.Empty;
        public string TargetLanguage { get; set; } = string.Empty;
        public string? DetectedLanguage { get; set; }
        public double? DetectionScore { get; set; }
        public string? TransliterationFrom { get; set; }
        public string? TransliterationTo { get; set; }
    }
}
