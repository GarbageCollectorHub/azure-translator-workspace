using TranslatorAzure.Services.FileText;
using TranslatorAzure.Services.Translator;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Configuration.AddJsonFile("azuresettings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.Configure<TranslatorOptions>(builder.Configuration.GetSection("translator"));
builder.Services.AddScoped<ITranslatorService, AzureTranslatorService>();
builder.Services
    .AddOptions<FileTextImportOptions>()
    .Bind(builder.Configuration.GetSection(FileTextImportOptions.SectionName))
    .Validate(options => options.MaxUploadBytes > 0, "The file import size limit must be positive.")
    .Validate(options => options.MaxUploadBytes <= FileTextImportOptions.DefaultMaxUploadBytes,
        $"The file import size limit cannot exceed {FileTextImportOptions.DefaultMaxUploadBytes} bytes.")
    .Validate(options => options.MaxPdfPages > 0 && options.MaxPdfPages <= FileTextImportOptions.DefaultMaxPdfPages,
        $"The PDF page limit must be between 1 and {FileTextImportOptions.DefaultMaxPdfPages}.")
    .Validate(options => options.MaxExtractedCharacters > 0
        && options.MaxExtractedCharacters <= FileTextImportOptions.DefaultMaxExtractedCharacters,
        $"The extracted text limit cannot exceed {FileTextImportOptions.DefaultMaxExtractedCharacters} characters.")
    .ValidateOnStart();
builder.Services.AddSingleton<IFileTextExtractor, PlainTextExtractor>();
builder.Services.AddSingleton<IFileTextExtractor, OpenXmlWordTextExtractor>();
builder.Services.AddSingleton<IFileTextExtractor, PdfTextExtractor>();
builder.Services.AddScoped<IFileTextImportService, FileTextImportService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
