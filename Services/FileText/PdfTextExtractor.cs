using System.Text;
using Microsoft.Extensions.Options;
using TranslatorAzure.Models.FileText;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace TranslatorAzure.Services.FileText;

public sealed class PdfTextExtractor(IOptions<FileTextImportOptions> options) : IFileTextExtractor
{
    private readonly FileTextImportOptions _options = options.Value;

    public bool Supports(string extension) =>
        string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<ExtractedTextDocument> ExtractAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        await ValidatePdfHeaderAsync(source, cancellationToken);

        try
        {
            using var document = PdfDocument.Open(source);
            if (document.NumberOfPages > _options.MaxPdfPages)
            {
                throw new FileTextImportException(
                    $"PDF files are limited to {_options.MaxPdfPages} pages.");
            }

            var sections = new List<ExtractedTextSection>();
            var totalCharacters = 0;

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var text = ContentOrderTextExtractor.GetText(page).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                totalCharacters += text.Length;
                if (totalCharacters > _options.MaxExtractedCharacters)
                {
                    throw new FileTextImportException(
                        $"The PDF contains more than {_options.MaxExtractedCharacters:N0} characters of selectable text.");
                }

                sections.Add(new ExtractedTextSection(text, page.Number));
            }

            if (sections.Count == 0)
            {
                throw new FileTextImportException(
                    "No selectable text was found. Scanned or image-only PDFs are not supported.");
            }

            return new ExtractedTextDocument(sections, document.NumberOfPages);
        }
        catch (PdfDocumentEncryptedException)
        {
            throw new FileTextImportException("Password-protected PDFs are not supported.");
        }
    }

    private static async Task ValidatePdfHeaderAsync(Stream source, CancellationToken cancellationToken)
    {
        if (!source.CanSeek)
        {
            return;
        }

        var startPosition = source.Position;
        var length = (int)Math.Min(source.Length - source.Position, 1_024);
        var header = new byte[length];

        try
        {
            var bytesRead = await source.ReadAsync(header.AsMemory(), cancellationToken);
            var signature = Encoding.ASCII.GetString(header, 0, bytesRead);
            if (!signature.Contains("%PDF-", StringComparison.Ordinal))
            {
                throw new FileTextImportException("The selected file is not a valid PDF.");
            }
        }
        finally
        {
            source.Position = startPosition;
        }
    }
}
