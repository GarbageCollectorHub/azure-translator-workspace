namespace TranslatorAzure.Services.FileText;

public static class FileTextServiceCollectionExtensions
{
    public static IServiceCollection AddFileTextImport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FileTextImportOptions>()
            .Bind(configuration.GetSection(FileTextImportOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IFileTextExtractor, PlainTextExtractor>();
        services.AddSingleton<IFileTextExtractor, OpenXmlWordTextExtractor>();
        services.AddSingleton<IFileTextExtractor, PdfTextExtractor>();
        services.AddScoped<IFileTextImportService, FileTextImportService>();
        return services;
    }
}
