using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TranslatorAzure.Infrastructure.RateLimiting;
using TranslatorAzure.Services.FileText;
using TranslatorAzure.Services.Translator;
using TranslatorAzure.Services.Analytics;

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
var rateLimitOptions = builder.Configuration
    .GetSection(TranslatorRateLimitOptions.SectionName)
    .Get<TranslatorRateLimitOptions>() ?? new TranslatorRateLimitOptions();
builder.Services
    .AddOptions<TranslatorRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(TranslatorRateLimitOptions.SectionName))
    .Validate(options => options.TranslationPermitLimit > 0, "Translation rate limit must be positive.")
    .Validate(options => options.TranslationWindowSeconds > 0, "Translation rate limit window must be positive.")
    .Validate(options => options.GlobalTranslationPermitLimit > 0, "Global translation rate limit must be positive.")
    .Validate(options => options.GlobalTranslationWindowSeconds > 0, "Global translation rate limit window must be positive.")
    .Validate(options => options.FileImportPermitLimit > 0, "File import rate limit must be positive.")
    .Validate(options => options.FileImportWindowSeconds > 0, "File import rate limit window must be positive.")
    .Validate(options => options.GlobalFileImportPermitLimit > 0, "Global file import rate limit must be positive.")
    .Validate(options => options.GlobalFileImportWindowSeconds > 0, "Global file import rate limit window must be positive.")
    .ValidateOnStart();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        const string message = "Too many requests. Please wait a minute and try again.";

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString();
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new { message }, cancellationToken);
    };

    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            TranslatorRateLimitPartitionKeys.IsTranslationRequest(httpContext)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    TranslatorRateLimitPartitionKeys.GlobalTranslation(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = rateLimitOptions.GlobalTranslationPermitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.GlobalTranslationWindowSeconds)
                    })
                : RateLimitPartition.GetNoLimiter("not-rate-limited")),
        PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            TranslatorRateLimitPartitionKeys.IsFileImportRequest(httpContext)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    TranslatorRateLimitPartitionKeys.GlobalFileImport(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = rateLimitOptions.GlobalFileImportPermitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.GlobalFileImportWindowSeconds)
                    })
                : RateLimitPartition.GetNoLimiter("not-rate-limited")));

    options.AddPolicy(TranslatorRateLimitPolicies.Translation, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            TranslatorRateLimitPartitionKeys.Client(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rateLimitOptions.TranslationPermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(rateLimitOptions.TranslationWindowSeconds)
            }));

    options.AddPolicy(TranslatorRateLimitPolicies.FileImport, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            TranslatorRateLimitPartitionKeys.Client(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rateLimitOptions.FileImportPermitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(rateLimitOptions.FileImportWindowSeconds)
            }));
});
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

// Analytics Services
builder.Services.AddAnalytics(builder.Configuration);


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

app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program
{
}
