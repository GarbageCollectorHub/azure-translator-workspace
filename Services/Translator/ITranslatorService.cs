using TranslatorAzure.Models.Translator;

namespace TranslatorAzure.Services.Translator
{
    public interface ITranslatorService
    {
        Task<TranslatorLanguagesResponse> GetLanguagesAsync(CancellationToken cancellationToken = default);
        Task<TranslatorResult> TranslateAsync(TranslatorFormModel formModel, CancellationToken cancellationToken = default);
    }
}
