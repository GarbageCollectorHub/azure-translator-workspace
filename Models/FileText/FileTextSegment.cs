namespace TranslatorAzure.Models.FileText;

public sealed record FileTextSegment(
    int Number,
    string Text,
    int? SourcePage,
    int PartNumber,
    int PartCount);
