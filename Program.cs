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
    .ValidateOnStart();
builder.Services.AddSingleton<IFileTextExtractor, PlainTextExtractor>();
builder.Services.AddSingleton<IFileTextExtractor, OpenXmlWordTextExtractor>();
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
