namespace TranslatorAzure.Services.Speech;

public static class SpeechLanguageCode
{
    private static readonly IReadOnlyDictionary<string, string[]> SpeechLocaleOverrides =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh-Hans"] = ["zh-CN"],
            ["zh-CHS"] = ["zh-CN"],
            ["zh-Hant"] = ["zh-TW", "zh-HK"],
            ["zh-CHT"] = ["zh-TW", "zh-HK"],
            ["sr-Cyrl"] = ["sr-RS"],
            ["sr-Latn"] = ["sr-Latn-RS", "sr-RS"],
            ["fil"] = ["fil-PH"],
            ["yue"] = ["yue-CN"]
        };

    public static IReadOnlyList<string> GetSpeechLocaleCandidates(string language)
    {
        var normalizedLanguage = language.Trim();
        if (string.IsNullOrWhiteSpace(normalizedLanguage))
        {
            return [];
        }

        var candidates = new List<string>();
        if (SpeechLocaleOverrides.TryGetValue(normalizedLanguage, out var overrides))
        {
            foreach (var locale in overrides)
            {
                AddCandidate(candidates, locale);
            }
        }

        AddCandidate(candidates, normalizedLanguage);

        var parentSeparatorIndex = normalizedLanguage.IndexOf('-');
        if (parentSeparatorIndex > 0)
        {
            AddCandidate(candidates, normalizedLanguage[..parentSeparatorIndex]);
        }

        return candidates;
    }

    private static void AddCandidate(List<string> candidates, string candidate)
    {
        if (!candidates.Contains(candidate, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(candidate);
        }
    }
}
