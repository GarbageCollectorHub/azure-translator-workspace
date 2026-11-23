using TranslatorAzure.Infrastructure.RateLimiting;
using TranslatorAzure.Services.Analytics;
using TranslatorAzure.Services.Feedback;
using TranslatorAzure.Services.FileText;
using TranslatorAzure.Services.Speech;
using TranslatorAzure.Services.Translator;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

builder.Configuration.AddJsonFile("azuresettings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.Configure<TranslatorOptions>(builder.Configuration.GetSection("translator"));
builder.Services.AddScoped<ITranslatorService, AzureTranslatorService>();
builder.Services.AddTranslatorRateLimiting(builder.Configuration);
builder.Services.AddSpeechSynthesis(builder.Configuration);
builder.Services.AddFileTextImport(builder.Configuration);
builder.Services.AddFeedback(builder.Configuration);
builder.Services.AddAnalytics(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
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
