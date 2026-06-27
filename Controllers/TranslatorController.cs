using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using TranslatorAzure.Models.Rest;
using TranslatorAzure.Models.Translator;
using TranslatorAzure.Services.Analytics.Tracking;
using TranslatorAzure.Services.Translator;

namespace TranslatorAzure.Controllers
{
    public class TranslatorController : Controller
    {
        private const string TranslationUnavailableMessage =
            "Translation is temporarily unavailable. Please try again.";

        private readonly ITranslatorService _translatorService;
        private readonly ILogger<TranslatorController> _logger;

        private readonly IAnalyticsTracker _analyticsTracker;

        public TranslatorController(
            ITranslatorService translatorService,
            ILogger<TranslatorController> logger,
            IAnalyticsTracker analyticsTracker
            )
        {
            _translatorService = translatorService;
            _logger = logger;
            _analyticsTracker = analyticsTracker;
        }


        // GET /Translator Ч только показывает страницу
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            _analyticsTracker.TrackVisitOpened(HttpContext);

            var formModel = new TranslatorFormModel();
            NormalizeForm(formModel);

            var viewModel = await CreateViewModelAsync(formModel, cancellationToken);

            return View(viewModel);
        }

        // POST /Translator Ч переводит основную форму
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(
            [FromForm] TranslatorFormModel formModel,
            CancellationToken cancellationToken)
        {
            NormalizeForm(formModel);

            var viewModel = await CreateViewModelAsync(formModel, cancellationToken);

            if (IsSubmitted(formModel))
            {
                try
                {
                    if (formModel.Button == "switch")
                    {
                        await SwitchTranslationAsync(formModel, viewModel, cancellationToken);
                    }
                    else if (!string.IsNullOrWhiteSpace(formModel.Text))
                    {
                        if (formModel.Text.Length > TranslationRequest.MaxTextLength)
                        {
                            viewModel.ErrorMessage =
                                $"Enter up to {TranslationRequest.MaxTextLength:N0} characters to translate.";
                            return View(viewModel);
                        }

                        await TranslateFormAsync(formModel, viewModel, cancellationToken);

                        if (viewModel.ErrorMessage == null && !string.IsNullOrWhiteSpace(viewModel.Translation))
                        {
                            _analyticsTracker.TrackMainTranslationCompleted(HttpContext);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Azure Translator failed to translate text.");
                    viewModel.ErrorMessage = TranslationUnavailableMessage;
                }
            }

            return View(viewModel);
        }

        private async Task<TranslatorViewModel> CreateViewModelAsync(
            TranslatorFormModel formModel,
            CancellationToken cancellationToken)
        {
            return new TranslatorViewModel
            {
                FormModel = formModel,
                TranslatorLanguages = await _translatorService.GetLanguagesAsync(cancellationToken)
            };
        }

        // POST /Translator/Translate Ч остаЄтс€ дл€ selected text / PDF selection
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Translate(
            [FromBody] TranslationRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(CreateTranslationErrorResponse(
                    $"Choose between 2 and {TranslationRequest.MaxTextLength:N0} characters to translate.",
                    StatusCodes.Status400BadRequest));
            }

            var formModel = new TranslatorFormModel
            {
                LanguageFrom = request.LanguageFrom,
                LanguageTo = request.LanguageTo,
                Text = request.Text
            };

            NormalizeForm(formModel);

            try
            {
                var result = await _translatorService.TranslateAsync(formModel, cancellationToken);

                if (string.IsNullOrWhiteSpace(result.TranslatedText))
                {
                    return StatusCode(
                        StatusCodes.Status503ServiceUnavailable,
                        CreateTranslationErrorResponse());
                }

                var isPdfSelection = string.Equals(
                                        request.Source,
                                        "pdf-selection",
                                        StringComparison.OrdinalIgnoreCase
                                        );

                _analyticsTracker.TrackSelectionTranslationCompleted(HttpContext, isPdfSelection);

                return Ok(new RestResponse
                {
                    Meta = new()
                    {
                        Service = "TranslatorAzure",
                        Method = "POST",
                        Action = "Text Translation",
                        Time = DateTime.Now.Ticks,
                        DataType = "translation",
                        Params = new()
                        {
                            { "lang-from", formModel.LanguageFrom },
                            { "lang-to", formModel.LanguageTo }
                        }
                    },
                    Data = new
                    {
                        text = result.TranslatedText,
                        transliteration = result.TransliterationTo,
                        detectedLanguage = result.DetectedLanguage,
                        detectionScore = result.DetectionScore
                    }
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Azure Translator API request failed.");
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    CreateTranslationErrorResponse());
            }
        }

        private static void NormalizeForm(TranslatorFormModel formModel)
        {
            formModel.LanguageFrom = string.IsNullOrWhiteSpace(formModel.LanguageFrom)
                ? TranslatorFormModel.AutoDetectLanguageCode
                : formModel.LanguageFrom;

            formModel.LanguageTo = string.IsNullOrWhiteSpace(formModel.LanguageTo)
                ? "uk"
                : formModel.LanguageTo;

            formModel.Text ??= string.Empty;
        }

        private static bool IsSubmitted(TranslatorFormModel formModel)
        {
            return formModel.Button is "translate" or "switch";
        }

        private async Task TranslateFormAsync(
            TranslatorFormModel formModel,
            TranslatorViewModel viewModel,
            CancellationToken cancellationToken)
        {
            var result = await _translatorService.TranslateAsync(formModel, cancellationToken);
            ApplyTranslationResult(formModel, viewModel, result);
        }

        private async Task SwitchTranslationAsync(
            TranslatorFormModel formModel,
            TranslatorViewModel viewModel,
            CancellationToken cancellationToken)
        {
            var originalFrom = formModel.LanguageFrom;
            var originalTo = formModel.LanguageTo;
            var originalText = formModel.Text ?? string.Empty;
            var translatedText = formModel.Translation;
            TranslatorResult? result = null;

            if (string.IsNullOrWhiteSpace(translatedText) && !string.IsNullOrWhiteSpace(originalText))
            {
                result = await _translatorService.TranslateAsync(formModel, cancellationToken);
                translatedText = result.TranslatedText;
            }

            if (string.IsNullOrWhiteSpace(translatedText))
            {
                viewModel.ErrorMessage = TranslationUnavailableMessage;
                return;
            }

            if (originalFrom != TranslatorFormModel.AutoDetectLanguageCode || !string.IsNullOrWhiteSpace(translatedText))
            {
                formModel.LanguageFrom = originalTo;
                formModel.LanguageTo = ResolveLanguageAfterAutoDetect(originalFrom, formModel.DetectedLanguage, result?.DetectedLanguage);
            }

            if (!string.IsNullOrWhiteSpace(translatedText))
            {
                formModel.Text = translatedText;
                formModel.Translation = originalText;
                viewModel.Translation = originalText;
                viewModel.TransliterationFrom = result?.TransliterationTo;
                viewModel.TransliterationTo = result?.TransliterationFrom;
            }
        }

        private static void ApplyTranslationResult(
            TranslatorFormModel formModel,
            TranslatorViewModel viewModel,
            TranslatorResult result)
        {
            if (string.IsNullOrWhiteSpace(result.TranslatedText))
            {
                viewModel.ErrorMessage = TranslationUnavailableMessage;
                return;
            }

            formModel.DetectedLanguage = result.DetectedLanguage;
            viewModel.Translation = result.TranslatedText;
            viewModel.TransliterationFrom = result.TransliterationFrom;
            viewModel.TransliterationTo = result.TransliterationTo;
            viewModel.DetectedLanguage = result.DetectedLanguage;
            viewModel.DetectionScore = result.DetectionScore;
        }

        private static string ResolveLanguageAfterAutoDetect(params string?[] languageCodes)
        {
            return languageCodes.FirstOrDefault(code =>
                    !string.IsNullOrWhiteSpace(code) &&
                    code != TranslatorFormModel.AutoDetectLanguageCode)
                ?? "en";
        }

        private static RestResponse CreateTranslationErrorResponse(
            string? message = null,
            int statusCode = StatusCodes.Status503ServiceUnavailable)
        {
            message ??= TranslationUnavailableMessage;

            return new RestResponse
            {
                Status = new()
                {
                    IsOk = false,
                    Code = statusCode,
                    Phrase = message
                },
                Meta = new()
                {
                    Service = "TranslatorAzure",
                    Method = "POST",
                    Action = "Text Translation",
                    Time = DateTime.Now.Ticks,
                    DataType = "error"
                },
                Data = new
                {
                    message
                }
            };
        }
    }
}
