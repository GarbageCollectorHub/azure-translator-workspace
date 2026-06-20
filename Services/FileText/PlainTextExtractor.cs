using System.Text;

namespace TranslatorAzure.Services.FileText;

public sealed class PlainTextExtractor : IFileTextExtractor
{
    public bool Supports(string extension) =>
        string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase);

    public async Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(
            source,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        return await reader.ReadToEndAsync(cancellationToken);
    }
}
