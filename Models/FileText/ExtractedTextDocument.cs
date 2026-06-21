namespace TranslatorAzure.Models.FileText;

public sealed record ExtractedTextDocument(
    IReadOnlyList<ExtractedTextSection> Sections,
    int? PageCount = null);
