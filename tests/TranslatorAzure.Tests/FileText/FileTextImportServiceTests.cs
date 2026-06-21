using TranslatorAzure.Models.FileText;
using TranslatorAzure.Models.Translator;
using TranslatorAzure.Services.FileText;
using TranslatorAzure.Tests.TestSupport;

namespace TranslatorAzure.Tests.FileText;

// Protects the import contract: validation, safe limits, and translation-ready segmentation.
public sealed class FileTextImportServiceTests
{
    [Fact]
    public async Task ImportAsync_RejectsMissingOrEmptyFile()
    {
        var service = CreateService(new StubExtractor(".txt", []));

        var exception = await Assert.ThrowsAsync<FileTextImportException>(() =>
            service.ImportAsync("", 0, Stream.Null));

        Assert.Equal("Choose a non-empty TXT, DOCX, or PDF file.", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_RejectsFileLargerThanConfiguredLimit()
    {
        var service = CreateService(
            new StubExtractor(".txt", [new ExtractedTextSection("text")]),
            maxUploadBytes: 8);

        var exception = await Assert.ThrowsAsync<FileTextImportException>(() =>
            service.ImportAsync("notes.txt", 9, TestData.TextStream("text")));

        Assert.Contains("larger", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("notes.md")]
    [InlineData("archive.zip")]
    [InlineData("no-extension")]
    public async Task ImportAsync_RejectsUnsupportedFileType(string fileName)
    {
        var service = CreateService(new StubExtractor(".txt", [new ExtractedTextSection("text")]));

        var exception = await Assert.ThrowsAsync<FileTextImportException>(() =>
            service.ImportAsync(fileName, 4, TestData.TextStream("text")));

        Assert.Equal("Supported file types: TXT, DOCX, and PDF.", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_PreservesPageMetadataAndSectionOrder()
    {
        var service = CreateService(new StubExtractor(
            ".txt",
            [new ExtractedTextSection("First section", 2), new ExtractedTextSection("Second section", 3)],
            pageCount: 3));

        var result = await service.ImportAsync("My Notes.TXT", 10, TestData.TextStream("ignored"));

        Assert.Equal("My Notes.TXT", result.FileName);
        Assert.Equal(3, result.PageCount);
        Assert.Collection(
            result.Segments,
            first =>
            {
                Assert.Equal("First section", first.Text);
                Assert.Equal(2, first.SourcePage);
                Assert.Equal(1, first.Number);
            },
            second =>
            {
                Assert.Equal("Second section", second.Text);
                Assert.Equal(3, second.SourcePage);
                Assert.Equal(2, second.Number);
            });
    }

    [Fact]
    public async Task ImportAsync_SplitsLongSectionIntoTranslatorSafeSegments()
    {
        var text = new string('a', TranslationLimits.MaxInteractiveTextLength + 1);
        var service = CreateService(new StubExtractor(".txt", [new ExtractedTextSection(text, 1)]));

        var result = await service.ImportAsync("long.txt", text.Length, TestData.TextStream(text));

        Assert.Collection(
            result.Segments,
            first =>
            {
                Assert.Equal(TranslationLimits.MaxInteractiveTextLength, first.Text.Length);
                Assert.Equal(1, first.PartNumber);
                Assert.Equal(2, first.PartCount);
                Assert.Equal(1, first.SourcePage);
            },
            second =>
            {
                Assert.Equal(1, second.Text.Length);
                Assert.Equal(2, second.PartNumber);
                Assert.Equal(2, second.PartCount);
            });
    }

    [Fact]
    public async Task ImportAsync_PreservesParagraphBoundariesWhenTextFits()
    {
        var service = CreateService(new StubExtractor(
            ".txt",
            [new ExtractedTextSection("First paragraph\r\nSecond paragraph")]));

        var result = await service.ImportAsync("notes.txt", 10, TestData.TextStream("ignored"));

        Assert.Equal(
            $"First paragraph{Environment.NewLine}Second paragraph",
            Assert.Single(result.Segments).Text);
    }

    [Fact]
    public async Task ImportAsync_RejectsDocumentWithoutEditableText()
    {
        var service = CreateService(new StubExtractor(".txt", [new ExtractedTextSection("  \r\n  ")]));

        var exception = await Assert.ThrowsAsync<FileTextImportException>(() =>
            service.ImportAsync("blank.txt", 4, TestData.TextStream("ignored")));

        Assert.Equal("No editable text was found in this file.", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_MapsUnexpectedExtractorFailureToSafeMessage()
    {
        var service = CreateService(new StubExtractor(
            ".txt",
            [],
            exception: new InvalidOperationException("internal parser detail")));

        var exception = await Assert.ThrowsAsync<FileTextImportException>(() =>
            service.ImportAsync("broken.txt", 4, TestData.TextStream("text")));

        Assert.Equal("The file could not be read. Choose a valid TXT, DOCX, or PDF file.", exception.Message);
    }

    private static FileTextImportService CreateService(
        IFileTextExtractor extractor,
        int maxUploadBytes = FileTextImportOptions.DefaultMaxUploadBytes) =>
        new([extractor], TestData.FileTextOptions(maxUploadBytes));

    private sealed class StubExtractor(
        string extension,
        IReadOnlyList<ExtractedTextSection> sections,
        int? pageCount = null,
        Exception? exception = null) : IFileTextExtractor
    {
        public bool Supports(string candidate) =>
            string.Equals(extension, candidate, StringComparison.OrdinalIgnoreCase);

        public Task<ExtractedTextDocument> ExtractAsync(
            Stream source,
            CancellationToken cancellationToken = default) =>
            exception is null
                ? Task.FromResult(new ExtractedTextDocument(sections, pageCount))
                : Task.FromException<ExtractedTextDocument>(exception);
    }
}
