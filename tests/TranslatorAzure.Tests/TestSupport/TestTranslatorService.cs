using TranslatorAzure.Models.Translator;
using TranslatorAzure.Services.Translator;

namespace TranslatorAzure.Tests.TestSupport;

// Deterministic translator fake used where a test must control Azure outcomes.
public sealed class TestTranslatorService : ITranslatorService
{
    public TranslatorLanguagesResponse Languages { get; set; } = TestData.CreateLanguages();

    public TranslatorResult Result { get; set; } = new()
    {
        SourceText = "Hello",
        TranslatedText = "\u041f\u0440\u0438\u0432\u0456\u0442",
        TargetLanguage = "uk",
        DetectedLanguage = "en",
        DetectionScore = 1
    };

    public Exception? GetLanguagesException { get; set; }

    public Exception? TranslateException { get; set; }

    public List<TranslatorFormModel> TranslateCalls { get; } = [];

    public Task<TranslatorLanguagesResponse> GetLanguagesAsync(CancellationToken cancellationToken = default)
    {
        if (GetLanguagesException is not null)
        {
            throw GetLanguagesException;
        }

        return Task.FromResult(Languages);
    }

    public Task<TranslatorResult> TranslateAsync(
        TranslatorFormModel formModel,
        CancellationToken cancellationToken = default)
    {
        if (TranslateException is not null)
        {
            throw TranslateException;
        }

        TranslateCalls.Add(new TranslatorFormModel
        {
            LanguageFrom = formModel.LanguageFrom,
            LanguageTo = formModel.LanguageTo,
            Text = formModel.Text,
            Translation = formModel.Translation,
            DetectedLanguage = formModel.DetectedLanguage,
            Button = formModel.Button
        });

        return Task.FromResult(Result);
    }
}
