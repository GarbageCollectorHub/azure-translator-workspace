namespace TranslatorAzure.Tests.Frontend;

public sealed class FrontendContractTests
{
    [Fact]
    public void TranslatorWorkspace_KeepsSelectedTextPauseAndImportedFileHeaderContracts()
    {
        var page = ReadWorkspaceFile("Views", "Translator", "Index.cshtml");
        var domScript = ReadWorkspaceFile("wwwroot", "js", "translator", "dom.js");
        var indexScript = ReadWorkspaceFile("wwwroot", "js", "translator", "index.js");

        Assert.Contains("id=\"pause-selected\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"pause\"", page, StringComparison.Ordinal);
        Assert.Contains("selection-mode-option--pause", page, StringComparison.Ordinal);
        Assert.DoesNotContain("selection-pause-toggle", page, StringComparison.Ordinal);
        Assert.Contains("Pause selected text actions", page, StringComparison.Ordinal);
        Assert.Contains("id=\"imported-file-meta\"", page, StringComparison.Ordinal);
        Assert.Contains("file-close-button", page, StringComparison.Ordinal);

        Assert.Contains("pauseSelected: byId('pause-selected')", domScript, StringComparison.Ordinal);
        Assert.Contains("importedFileMeta: byId('imported-file-meta')", domScript, StringComparison.Ordinal);

        Assert.Contains("pauseSelected.checked", indexScript, StringComparison.Ordinal);
        Assert.Contains("updateImportedFileSummary", indexScript, StringComparison.Ordinal);
        Assert.Contains("clearImportedFileSummary", indexScript, StringComparison.Ordinal);
        Assert.Contains("importedFileMeta.textContent", indexScript, StringComparison.Ordinal);
    }

    private static string ReadWorkspaceFile(params string[] relativePathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TranslatorAzure.csproj")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var pathParts = new string[relativePathParts.Length + 1];
        pathParts[0] = directory!.FullName;
        Array.Copy(relativePathParts, 0, pathParts, 1, relativePathParts.Length);

        return File.ReadAllText(Path.Combine(pathParts));
    }
}
