using TranslatorAzure.Services.Speech;

namespace TranslatorAzure.Tests.Speech;

public sealed class SpeechLanguageCodeTests
{
    [Theory]
    [InlineData("zh-Hans", "zh-CN")]
    [InlineData("zh-Hant", "zh-TW")]
    [InlineData("sr-Cyrl", "sr-RS")]
    [InlineData("fil", "fil-PH")]
    public void GetSpeechLocaleCandidates_UsesKnownSpeechLocaleOverrides(
        string language,
        string expectedLocale)
    {
        var candidates = SpeechLanguageCode.GetSpeechLocaleCandidates(language);

        Assert.Equal(expectedLocale, candidates[0]);
    }

    [Fact]
    public void GetSpeechLocaleCandidates_AddsParentFallbackForScriptSpecificCodes()
    {
        var candidates = SpeechLanguageCode.GetSpeechLocaleCandidates("sr-Cyrl");

        Assert.Contains("sr", candidates);
    }
}
