using System.Text;
using TranslatorAzure.Models.FileText;
using Microsoft.Extensions.Options;

namespace TranslatorAzure.Services.FileText;

public sealed class FileTextImportService(
    IEnumerable<IFileTextExtractor> extractors,
    IOptions<FileTextImportOptions> options) : IFileTextImportService
{
    private readonly IReadOnlyList<IFileTextExtractor> _extractors = extractors.ToList();
    private readonly FileTextImportOptions _options = options.Value;

    public async Task<ImportedTextDocument> ImportAsync(
        string fileName,
        long fileLength,
        Stream source,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileLength <= 0)
        {
            throw new FileTextImportException("Choose a non-empty TXT, DOCX, or PDF file.");
        }

        if (fileLength > _options.MaxUploadBytes)
        {
            throw new FileTextImportException($"The file is larger than the {_options.MaxUploadSizeLabel} import limit.");
        }

        var extension = Path.GetExtension(fileName);
        var extractor = _extractors.FirstOrDefault(candidate => candidate.Supports(extension));

        if (extractor is null)
        {
            throw new FileTextImportException("Supported file types: TXT, DOCX, and PDF.");
        }

        ExtractedTextDocument extractedDocument;
        try
        {
            extractedDocument = await extractor.ExtractAsync(source, cancellationToken);
        }
        catch (FileTextImportException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new FileTextImportException("The file could not be read. Choose a valid TXT, DOCX, or PDF file.");
        }

        var segments = SplitIntoSegments(extractedDocument.Sections).ToList();
        if (segments.Count == 0)
        {
            throw new FileTextImportException("No editable text was found in this file.");
        }

        return new ImportedTextDocument(
            Path.GetFileName(fileName),
            segments,
            extractedDocument.PageCount);
    }

    private IEnumerable<FileTextSegment> SplitIntoSegments(IReadOnlyList<ExtractedTextSection> sections)
    {
        var number = 1;

        foreach (var section in sections)
        {
            var parts = SplitText(section.Text).ToList();
            for (var index = 0; index < parts.Count; index++)
            {
                yield return new FileTextSegment(
                    number++,
                    parts[index],
                    section.SourcePage,
                    index + 1,
                    parts.Count);
            }
        }
    }

    private IEnumerable<string> SplitText(string text)
    {
        var normalizedText = text.Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            yield break;
        }

        var section = new StringBuilder();
        foreach (var paragraph in normalizedText.Split("\n", StringSplitOptions.None))
        {
            var delimiterLength = section.Length == 0 ? 0 : Environment.NewLine.Length;

            if (paragraph.Length + delimiterLength > _options.MaxSegmentCharacters)
            {
                if (section.Length > 0)
                {
                    yield return section.ToString();
                    section.Clear();
                }

                foreach (var part in SplitLongText(paragraph))
                {
                    yield return part;
                }

                continue;
            }

            if (section.Length + paragraph.Length + delimiterLength > _options.MaxSegmentCharacters)
            {
                yield return section.ToString();
                section.Clear();
            }

            if (section.Length > 0)
            {
                section.AppendLine();
            }

            section.Append(paragraph);
        }

        if (section.Length > 0)
        {
            yield return section.ToString();
        }
    }

    private IEnumerable<string> SplitLongText(string text)
    {
        for (var offset = 0; offset < text.Length; offset += _options.MaxSegmentCharacters)
        {
            var length = Math.Min(_options.MaxSegmentCharacters, text.Length - offset);
            yield return text.Substring(offset, length);
        }
    }
}
