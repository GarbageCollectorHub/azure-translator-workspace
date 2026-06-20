namespace TranslatorAzure.Services.FileText;

public interface IFileTextExtractor
{
    bool Supports(string extension);

    Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default);
}
