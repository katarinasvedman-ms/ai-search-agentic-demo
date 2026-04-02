using System.Text.Json.Serialization;

namespace SecureRagChat.Models;

public sealed class RetrievedChunk
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("snippet")]
    public required string Snippet { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
