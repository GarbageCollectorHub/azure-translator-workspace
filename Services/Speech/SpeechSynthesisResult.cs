namespace TranslatorAzure.Services.Speech;

public sealed record SpeechSynthesisResult(
    byte[] Audio,
    string ContentType,
    string VoiceName);
