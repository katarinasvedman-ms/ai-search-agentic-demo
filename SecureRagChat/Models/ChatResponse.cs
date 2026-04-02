using System.Text.Json.Serialization;

namespace SecureRagChat.Models;

public sealed class ChatResponse
{
    public required string Answer { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required RetrievalPlane RetrievalPlane { get; set; }

    public required Citation[] Citations { get; set; }
    public required ChatDiagnostics Diagnostics { get; set; }
}

public sealed class ChatDiagnostics
{
    public int ChunkCount { get; set; }
    public bool IsAuthenticated { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RetrievalMode RetrievalMode { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RetrievalSource RetrievalSource { get; set; }
}
