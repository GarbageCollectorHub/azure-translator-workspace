using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TranslatorAzure.Models.FileText;

namespace TranslatorAzure.Services.FileText;

public sealed class OpenXmlWordTextExtractor : IFileTextExtractor
{
    public bool Supports(string extension) =>
        string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase);

    public Task<ExtractedTextDocument> ExtractAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var document = WordprocessingDocument.Open(source, isEditable: false);

        var paragraphs = document.MainDocumentPart?.Document?.Body?
            .Descendants<Paragraph>()
            .Select(paragraph => paragraph.InnerText.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            ?? [];

        var text = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        return Task.FromResult(new ExtractedTextDocument([new ExtractedTextSection(text)]));
    }
}
