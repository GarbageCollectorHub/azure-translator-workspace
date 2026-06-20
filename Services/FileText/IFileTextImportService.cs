using TranslatorAzure.Models.FileText;

namespace TranslatorAzure.Services.FileText;

public interface IFileTextImportService
{
    Task<ImportedTextDocument> ImportAsync(
        string fileName,
        long fileLength,
        Stream source,
        CancellationToken cancellationToken = default);
}
