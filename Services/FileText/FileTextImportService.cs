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
            throw new FileTextImportException("Choose a non-empty TXT or DOCX file.");
        }

        if (fileLength > _options.MaxUploadBytes)
        {
            throw new FileTextImportException("The file is larger than the 2 MB import limit.");
        }

        var extension = Path.GetExtension(fileName);
        var extractor = _extractors.FirstOrDefault(candidate => candidate.Supports(extension));

        if (extractor is null)
        {
            throw new FileTextImportException("Supported file types: TXT and DOCX.");
        }

        string extractedText;
        try
        {
            extractedText = await extractor.ExtractAsync(source, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new FileTextImportException("The file could not be read. Choose a valid TXT or DOCX file.");
        }

        var segments = SplitIntoSegments(extractedText).ToList();
        if (segments.Count == 0)
        {
            throw new FileTextImportException("No editable text was found in this file.");
        }

        return new ImportedTextDocument(Path.GetFileName(fileName), segments);
    }

    private IEnumerable<FileTextSegment> SplitIntoSegments(string text)
    {
        var normalizedText = text.Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            yield break;
        }

        var section = new StringBuilder();
        var number = 1;

        foreach (var paragraph in normalizedText.Split("\n", StringSplitOptions.None))
        {
            var delimiterLength = section.Length == 0 ? 0 : Environment.NewLine.Length;

            if (paragraph.Length + delimiterLength > _options.MaxSegmentCharacters)
            {
                if (section.Length > 0)
                {
                    yield return new FileTextSegment(number++, section.ToString());
                    section.Clear();
                }

                foreach (var part in SplitLongText(paragraph))
                {
                    yield return new FileTextSegment(number++, part);
                }

                continue;
            }

            if (section.Length + paragraph.Length + delimiterLength > _options.MaxSegmentCharacters)
            {
                yield return new FileTextSegment(number++, section.ToString());
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
            yield return new FileTextSegment(number, section.ToString());
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
