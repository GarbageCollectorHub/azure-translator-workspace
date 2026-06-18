namespace TranslatorAzure.Models.Rest
{
    public class RestResponse
    {
        public RestStatus Status { get; set; } = new();
        public RestMeta Meta { get; set; } = new();
        public object? Data { get; set; }
    }

    public class RestMeta
    {
        public string Service { get; set; } = "TranslatorAzure";
        public string Method { get; set; } = "GET";
        public string Action { get; set; } = "Read";
        public string DataType { get; set; } = "null";
        public long Time { get; set; }
        public Dictionary<string, object> Params { get; set; } = [];
    }

    public class RestStatus
    {
        public bool IsOk { get; set; } = true;
        public int Code { get; set; } = 200;
        public string Phrase { get; set; } = "Ok";
    }
}
