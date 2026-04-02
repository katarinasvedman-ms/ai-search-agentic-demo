using System.Text.Json.Serialization;

namespace SecureRagChat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RetrievalMode
{
    Traditional,
    Agentic
}