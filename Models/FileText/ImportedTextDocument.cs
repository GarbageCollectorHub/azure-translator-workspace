namespace TranslatorAzure.Models.FileText;

public sealed record ImportedTextDocument(
    string FileName,
    IReadOnlyList<FileTextSegment> Segments);
