using Microsoft.AspNetCore.Http;
using TranslatorAzure.Services.Analytics.Tracking;

namespace TranslatorAzure.Tests.TestSupport;

public sealed class TestAnalyticsTracker : IAnalyticsTracker
{
    public int VisitOpenedCalls { get; private set; }

    public int MainTranslationCompletedCalls { get; private set; }

    public int SelectionTranslationCompletedCalls { get; private set; }

    public int PdfSelectionTranslationCompletedCalls { get; private set; }

    public int FileImportedCalls { get; private set; }

    public int SpeechSynthesisCompletedCalls { get; private set; }

    public int SpeechCharacterCount { get; private set; }

    public void TrackVisitOpened(HttpContext httpContext)
    {
        VisitOpenedCalls++;
    }

    public void TrackMainTranslationCompleted(HttpContext httpContext)
    {
        MainTranslationCompletedCalls++;
    }

    public void TrackSelectionTranslationCompleted(HttpContext httpContext, bool isPdfSelection)
    {
        if (isPdfSelection)
        {
            PdfSelectionTranslationCompletedCalls++;
            return;
        }

        SelectionTranslationCompletedCalls++;
    }

    public void TrackFileImported(HttpContext httpContext)
    {
        FileImportedCalls++;
    }

    public void TrackSpeechSynthesisCompleted(HttpContext httpContext, int characterCount)
    {
        SpeechSynthesisCompletedCalls++;
        SpeechCharacterCount += characterCount;
    }
}
