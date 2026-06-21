using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Options;
using TranslatorAzure.Models.FileText;
using TranslatorAzure.Models.Translator;
using TranslatorAzure.Services.FileText;

namespace TranslatorAzure.Tests.TestSupport;

// Small in-memory fixtures shared by import, controller, and HTTP contract tests.
internal static class TestData
{
    public static TranslatorLanguagesResponse CreateLanguages() => new()
    {
        translation = new Dictionary<string, LanguageInfo>
        {
            ["en"] = new() { name = "English", nativeName = "English", dir = "ltr" },
            ["uk"] = new() { name = "Ukrainian", nativeName = "\u0423\u043a\u0440\u0430\u0457\u043d\u0441\u044c\u043a\u0430", dir = "ltr" },
            ["de"] = new() { name = "German", nativeName = "Deutsch", dir = "ltr" }
        },
        transliteration = new Dictionary<string, LanguageInfo>()
    };

    public static IOptions<FileTextImportOptions> FileTextOptions(
        int maxUploadBytes = FileTextImportOptions.DefaultMaxUploadBytes,
        int maxPdfPages = FileTextImportOptions.DefaultMaxPdfPages,
        int maxExtractedCharacters = FileTextImportOptions.DefaultMaxExtractedCharacters) =>
        Options.Create(new FileTextImportOptions
        {
            MaxUploadBytes = maxUploadBytes,
            MaxPdfPages = maxPdfPages,
            MaxExtractedCharacters = maxExtractedCharacters
        });

    public static MemoryStream TextStream(string text, bool withUtf8Bom = false)
    {
        var encoding = new UTF8Encoding(withUtf8Bom);
        return new MemoryStream(encoding.GetBytes(text));
    }

    public static MemoryStream WordDocument(params string[] paragraphs)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
                  <Default Extension="xml" ContentType="application/xml" />
                  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml" />
                </Types>
                """);
            AddEntry(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml" />
                </Relationships>
                """);

            var documentXml = new StringBuilder("""
                <?xml version="1.0" encoding="UTF-8"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>
                """);
            foreach (var paragraph in paragraphs)
            {
                documentXml.Append("<w:p><w:r><w:t>")
                    .Append(System.Security.SecurityElement.Escape(paragraph))
                    .Append("</w:t></w:r></w:p>");
            }

            documentXml.Append("<w:sectPr /></w:body></w:document>");
            AddEntry(archive, "word/document.xml", documentXml.ToString());
        }

        stream.Position = 0;
        return stream;
    }

    public static MemoryStream PdfDocument(params string?[] pages)
    {
        var objects = new List<string>();
        var pageObjectIds = Enumerable.Range(0, pages.Length).Select(index => 4 + (index * 2)).ToArray();

        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add($"<< /Type /Pages /Kids [{string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pages.Length} >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        for (var index = 0; index < pages.Length; index++)
        {
            var contentId = pageObjectIds[index] + 1;
            var escapedText = EscapePdfText(pages[index] ?? string.Empty);
            var content = $"BT /F1 14 Tf 72 720 Td ({escapedText}) Tj ET";
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentId} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }

        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(index + 1).Append(" 0 obj\n")
                .Append(objects[index]).Append("\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 ").Append(objects.Count + 1).Append("\n")
            .Append("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            builder.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }

        builder.Append("trailer\n<< /Size ").Append(objects.Count + 1)
            .Append(" /Root 1 0 R >>\nstartxref\n")
            .Append(xrefOffset)
            .Append("\n%%EOF");

        return new MemoryStream(Encoding.ASCII.GetBytes(builder.ToString()));
    }

    private static void AddEntry(ZipArchive archive, string path, string contents)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(contents);
    }

    private static string EscapePdfText(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
