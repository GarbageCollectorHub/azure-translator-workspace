using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TranslatorAzure.Services.FileText;

public sealed class OpenXmlWordTextExtractor : IFileTextExtractor
{
    public bool Supports(string extension) =>
        string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase);

    public Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default)
    {
        using var document = WordprocessingDocument.Open(source, isEditable: false);

        var paragraphs = document.MainDocumentPart?.Document?.Body?
            .Descendants<Paragraph>()
            .Select(paragraph => paragraph.InnerText.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            ?? [];

        return Task.FromResult(string.Join(Environment.NewLine + Environment.NewLine, paragraphs));
    }
}
