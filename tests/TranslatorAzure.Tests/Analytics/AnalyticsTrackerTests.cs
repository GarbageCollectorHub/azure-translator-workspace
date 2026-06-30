using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TranslatorAzure.Services.Analytics.Configuration;
using TranslatorAzure.Services.Analytics.Events;
using TranslatorAzure.Services.Analytics.Queue;
using TranslatorAzure.Services.Analytics.Tracking;

namespace TranslatorAzure.Tests.Analytics;

public sealed class AnalyticsTrackerTests
{
    [Fact]
    public void TrackVisitOpened_SkipsEventAndVisitorCookie_WhenIgnoreQueryTokenMatches()
    {
        var queue = new TestAnalyticsQueue();
        var tracker = CreateTracker(queue, ignoreToken: "codex-secret");
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?analytics_ignore=codex-secret");

        tracker.TrackVisitOpened(context);

        Assert.Empty(queue.Events);
        Assert.DoesNotContain(
            context.Response.Headers.SetCookie,
            value => value?.StartsWith("translator_visitor_id=", StringComparison.Ordinal) == true);
        Assert.Contains(
            context.Response.Headers.SetCookie,
            value => value?.StartsWith("translator_analytics_ignore=", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void TrackVisitOpened_TracksEvent_WhenIgnoreQueryTokenDoesNotMatch()
    {
        var queue = new TestAnalyticsQueue();
        var tracker = CreateTracker(queue, ignoreToken: "codex-secret");
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?analytics_ignore=wrong-secret");

        tracker.TrackVisitOpened(context);

        var analyticsEvent = Assert.Single(queue.Events);
        Assert.Equal(AnalyticsEventType.VisitOpened, analyticsEvent.Type);
        Assert.Contains(
            context.Response.Headers.SetCookie,
            value => value?.StartsWith("translator_visitor_id=", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void TrackCounter_SkipsEvent_WhenIgnoreCookieTokenMatches()
    {
        var queue = new TestAnalyticsQueue();
        var tracker = CreateTracker(queue, ignoreToken: "codex-secret");
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "translator_analytics_ignore=codex-secret";

        tracker.TrackMainTranslationCompleted(context);

        Assert.Empty(queue.Events);
    }

    [Fact]
    public void TrackSpeechSynthesisCompleted_TracksCharacterCount()
    {
        var queue = new TestAnalyticsQueue();
        var tracker = CreateTracker(queue);
        var context = new DefaultHttpContext();

        tracker.TrackSpeechSynthesisCompleted(context, 42);

        var analyticsEvent = Assert.Single(queue.Events);
        Assert.Equal(AnalyticsEventType.SpeechSynthesisCompleted, analyticsEvent.Type);
        Assert.Equal(42, analyticsEvent.CharacterCount);
    }

    private static AnalyticsTracker CreateTracker(TestAnalyticsQueue queue, string? ignoreToken = null)
    {
        return new AnalyticsTracker(
            queue,
            Options.Create(new AnalyticsOptions
            {
                Enabled = true,
                IgnoreToken = ignoreToken,
                IpHashSalt = "test-salt"
            }));
    }

    private sealed class TestAnalyticsQueue : IAnalyticsQueue
    {
        public List<AnalyticsEvent> Events { get; } = [];

        public bool TryEnqueue(AnalyticsEvent analyticsEvent)
        {
            Events.Add(analyticsEvent);
            return true;
        }

        public IAsyncEnumerable<AnalyticsEvent> ReadAllAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
