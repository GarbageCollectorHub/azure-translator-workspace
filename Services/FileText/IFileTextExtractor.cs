using TranslatorAzure.Models.FileText;

namespace TranslatorAzure.Services.FileText;

public interface IFileTextExtractor
{
    bool Supports(string extension);

    Task<ExtractedTextDocument> ExtractAsync(
        Stream source,
        CancellationToken cancellationToken = default);
}
