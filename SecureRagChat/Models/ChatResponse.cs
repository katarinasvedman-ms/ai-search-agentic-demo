using System.Text.Json.Serialization;

namespace SecureRagChat.Models;

public sealed class ChatResponse
{
    public required string Answer { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required RetrievalPlane RetrievalPlane { get; set; }

    public required Citation[] Citations { get; set; }
    public required ChatDiagnostics Diagnostics { get; set; }
    public RetrievalDetails? RetrievalDetails { get; set; }
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

public sealed class RetrievalDetails
{
    public required string Mode { get; set; }
    public string? RetrievalStyle { get; set; }
    public string? Query { get; set; }
    public string? Filters { get; set; }
    public int ResultsCount { get; set; }
    public string? Authorization { get; set; }
    public bool? KnowledgeBaseUsed { get; set; }
    public string? QueryConstruction { get; set; }
}
