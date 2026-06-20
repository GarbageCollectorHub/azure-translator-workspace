using TranslatorAzure.Services.FileText;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace TranslatorAzure.Controllers;

[Route("file-text")]
public sealed class FileTextController(
    IFileTextImportService fileTextImportService,
    IOptions<FileTextImportOptions> fileTextOptions,
    ILogger<FileTextController> logger) : Controller
{
    private readonly FileTextImportOptions _options = fileTextOptions.Value;

    [HttpPost("import")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(FileTextImportOptions.MaximumRequestBytes)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            if (Request.ContentLength > _options.MaxUploadBytes)
            {
                return BadRequest(new { message = $"The file is larger than the {_options.MaxUploadSizeLabel} import limit." });
            }

            return BadRequest(new { message = "Choose a TXT or DOCX file first." });
        }

        try
        {
            await using var source = file.OpenReadStream();
            var document = await fileTextImportService.ImportAsync(
                file.FileName,
                file.Length,
                source,
                cancellationToken);

            return Ok(new
            {
                document.FileName,
                segments = document.Segments.Select(segment => new
                {
                    segment.Number,
                    segment.Text
                })
            });
        }
        catch (FileTextImportException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Text import failed for {FileName}.", file.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "The file could not be imported. Please try again."
            });
        }
    }
}
