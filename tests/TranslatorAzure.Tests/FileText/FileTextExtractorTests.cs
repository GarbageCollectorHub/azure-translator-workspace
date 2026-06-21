using TranslatorAzure.Services.FileText;
using TranslatorAzure.Tests.TestSupport;

namespace TranslatorAzure.Tests.FileText;

// Verifies that each supported format is read predictably and rejected safely when malformed or too large.
public sealed class FileTextExtractorTests
{
    [Fact]
    public async Task PlainTextExtractor_ReadsUtf8WithBom()
    {
        var extractor = new PlainTextExtractor();

        var document = await extractor.ExtractAsync(TestData.TextStream("\u041f\u0440\u0438\u0432\u0456\u0442", withUtf8Bom: true));

        Assert.Equal("\u041f\u0440\u0438\u0432\u0456\u0442", Assert.Single(document.Sections).Text);
        Assert.Null(document.PageCount);
    }

    [Theory]
    [InlineData(".txt", true)]
    [InlineData(".TXT", true)]
    [InlineData(".pdf", false)]
    public void PlainTextExtractor_SupportsOnlyTextFiles(string extension, bool expected)
    {
        Assert.Equal(expected, new PlainTextExtractor().Supports(extension));
    }

    [Fact]
    public async Task OpenXmlWordTextExtractor_JoinsNonEmptyParagraphs()
    {
        using var file = TestData.WordDocument("First paragraph", "", "Second paragraph");

        var document = await new OpenXmlWordTextExtractor().ExtractAsync(file);

        Assert.Equal(
            $"First paragraph{Environment.NewLine}{Environment.NewLine}Second paragraph",
            Assert.Single(document.Sections).Text);
    }

    [Fact]
    public async Task PdfTextExtractor_ReportsTotalPagesAndSourcePages()
    {
        using var file = TestData.PdfDocument("First page", "Second page");

        var document = await new PdfTextExtractor(TestData.FileTextOptions()).ExtractAsync(file);

        Assert.Equal(2, document.PageCount);
        Assert.Collection(
            document.Sections,
            first =>
            {
                Assert.Contains("First page", first.Text);
                Assert.Equal(1, first.SourcePage);
            },
            second =>
            {
                Assert.Contains("Second page", second.Text);
                Assert.Equal(2, second.SourcePage);
            });
    }

    [Fact]
    public async Task PdfTextExtractor_RejectsInvalidHeader()
    {
        var exception = await Assert.ThrowsAsync<FileTextImportException>(() =>
            new PdfTextExtractor(TestData.FileTextOptions()).ExtractAsync(TestData.TextStream("not a PDF")));

        Assert.Equal("The selected file is not a valid PDF.", exception.Message);
    }

    [Fact]
    public async Task PdfTextExtractor_RejectsPdfWithoutSelectableText()
    {
        using var file = TestData.PdfDocument(string.Empty);

        var exception = await Assert.ThrowsAsync<FileTextImportException>(() =>
            new PdfTextExtractor(TestData.FileTextOptions()).ExtractAsync(file));

        Assert.Contains("No selectable text", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PdfTextExtractor_RejectsTooManyPagesBeforeExtraction()
    {
        using var file = TestData.PdfDocument("One", "Two");

        var exception = await Assert.ThrowsAsync<FileTextImportException>(() =>
            new PdfTextExtractor(TestData.FileTextOptions(maxPdfPages: 1)).ExtractAsync(file));

        Assert.Equal("PDF files are limited to 1 pages.", exception.Message);
    }

    [Fact]
    public async Task PdfTextExtractor_RejectsPdfWithTooMuchSelectableText()
    {
        using var file = TestData.PdfDocument("More than five characters");

        var exception = await Assert.ThrowsAsync<FileTextImportException>(() =>
            new PdfTextExtractor(TestData.FileTextOptions(maxExtractedCharacters: 5)).ExtractAsync(file));

        Assert.Contains("more than 5 characters", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
