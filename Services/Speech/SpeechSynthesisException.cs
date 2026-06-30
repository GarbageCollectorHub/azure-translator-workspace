namespace TranslatorAzure.Services.Speech;

public sealed class SpeechSynthesisException : Exception
{
    public SpeechSynthesisException(string message, int statusCode, string? developerMessage = null)
        : base(message)
    {
        StatusCode = statusCode;
        DeveloperMessage = developerMessage;
    }

    public int StatusCode { get; }

    public string? DeveloperMessage { get; }
}
