using System.Text;
using TranslatorAzure.Models.FileText;

namespace TranslatorAzure.Services.FileText;

public sealed class PlainTextExtractor : IFileTextExtractor
{
    public bool Supports(string extension) =>
        string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase);

    public async Task<ExtractedTextDocument> ExtractAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(
            source,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        var text = await reader.ReadToEndAsync(cancellationToken);
        return new ExtractedTextDocument([new ExtractedTextSection(text)]);
    }
}
