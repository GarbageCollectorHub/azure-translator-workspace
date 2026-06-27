namespace TranslatorAzure.Services.Analytics.Tracking;

public interface IAnalyticsTracker
{
    void TrackVisitOpened(HttpContext httpContext);

    void TrackMainTranslationCompleted(HttpContext httpContext);

    void TrackSelectionTranslationCompleted(HttpContext httpContext, bool isPdfSelection);

    void TrackFileImported(HttpContext httpContext);
}