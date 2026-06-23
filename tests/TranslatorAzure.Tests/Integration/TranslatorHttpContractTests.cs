using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using TranslatorAzure.Tests.TestSupport;

namespace TranslatorAzure.Tests.Integration;

// Verifies routes, antiforgery, validation, serialization, and file import through the real HTTP pipeline.
public sealed class TranslatorHttpContractTests : IClassFixture<TranslatorApplicationFactory>
{
    private readonly TranslatorApplicationFactory _factory;

    public TranslatorHttpContractTests(TranslatorApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TranslateEndpoint_ReturnsTranslationForValidAntiforgeryRequest()
    {
        _factory.ResetTranslator();
        using var client = await CreateAntiforgeryClientAsync();

        var response = await client.PostAsJsonAsync("/Translator/Translate", new
        {
            text = "Hello",
            languageFrom = "auto",
            languageTo = "uk"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("\u041f\u0440\u0438\u0432\u0456\u0442", payload.RootElement.GetProperty("data").GetProperty("text").GetString());
        Assert.Single(_factory.Translator.TranslateCalls);
    }

    [Fact]
    public async Task TranslateEndpoint_RejectsRequestWithoutAntiforgeryToken()
    {
        _factory.ResetTranslator();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/Translator/Translate", new
        {
            text = "Hello",
            languageFrom = "auto",
            languageTo = "uk"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Translator.TranslateCalls);
    }

    [Fact]
    public async Task TranslateEndpoint_RejectsTextOutsideInputContract()
    {
        _factory.ResetTranslator();
        using var client = await CreateAntiforgeryClientAsync();

        var response = await client.PostAsJsonAsync("/Translator/Translate", new
        {
            text = "x",
            languageFrom = "en",
            languageTo = "uk"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Translator.TranslateCalls);
    }

    [Fact]
    public async Task TranslateEndpoint_HidesTranslatorFailureBehindControlledResponse()
    {
        _factory.ResetTranslator();
        _factory.Translator.TranslateException = new HttpRequestException("upstream failure");
        using var client = await CreateAntiforgeryClientAsync();

        var response = await client.PostAsJsonAsync("/Translator/Translate", new
        {
            text = "Hello",
            languageFrom = "en",
            languageTo = "uk"
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("temporarily unavailable", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("upstream failure", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FileImportEndpoint_ImportsTxtUsingActualPipeline()
    {
        _factory.ResetTranslator();
        using var client = await CreateAntiforgeryClientAsync();
        using var form = CreateFileForm("notes.txt", "First paragraph\nSecond paragraph");

        var response = await client.PostAsync("/file-text/import", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("notes.txt", payload.RootElement.GetProperty("fileName").GetString());
        Assert.Equal(
            $"First paragraph{Environment.NewLine}Second paragraph",
            payload.RootElement.GetProperty("segments")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task FileImportEndpoint_ImportsPdfAndExposesTotalPages()
    {
        _factory.ResetTranslator();
        using var client = await CreateAntiforgeryClientAsync();
        using var pdf = TestData.PdfDocument("First page", "Second page");
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(pdf), "file", "two-pages.pdf");

        var response = await client.PostAsync("/file-text/import", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, payload.RootElement.GetProperty("pageCount").GetInt32());
        Assert.Equal(1, payload.RootElement.GetProperty("segments")[0].GetProperty("sourcePage").GetInt32());
    }

    [Fact]
    public async Task TranslatorPage_ProvidesThePdfPreviewWorkspace()
    {
        _factory.ResetTranslator();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/Translator");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"pdf-preview\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"pdf-text-layer\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"pdf-selection-popover\"", html, StringComparison.Ordinal);
        Assert.Contains("/lib/pdfjs/pdf.min.mjs", html, StringComparison.Ordinal);
        Assert.Contains("Original PDF", html, StringComparison.Ordinal);
        Assert.Contains("data-file-view=\"split\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"workspace-resize-handle\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/lib/pdfjs/pdf.min.mjs")]
    [InlineData("/lib/pdfjs/pdf.worker.min.mjs")]
    public async Task PdfPreviewAssets_AreServedByTheApplication(string assetPath)
    {
        _factory.ResetTranslator();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(assetPath);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task FileImportEndpoint_RejectsUnsupportedFileType()
    {
        _factory.ResetTranslator();
        using var client = await CreateAntiforgeryClientAsync();
        using var form = CreateFileForm("notes.md", "# Heading");

        var response = await client.PostAsync("/file-text/import", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Supported file types", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileImportEndpoint_RequiresAFile()
    {
        _factory.ResetTranslator();
        using var client = await CreateAntiforgeryClientAsync();
        using var form = new MultipartFormDataContent();

        var response = await client.PostAsync("/file-text/import", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Choose a TXT", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private async Task<HttpClient> CreateAntiforgeryClientAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        var page = await client.GetAsync("/Translator");
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();
        var token = Regex.Match(
                html,
                "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")
            .Groups[1].Value;

        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Add("RequestVerificationToken", WebUtility.HtmlDecode(token));
        return client;
    }

    private static MultipartFormDataContent CreateFileForm(string fileName, string content)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", fileName);
        return form;
    }
}
