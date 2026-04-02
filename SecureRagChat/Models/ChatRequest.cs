using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SecureRagChat.Models;

public sealed class ChatRequest
{
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public required string Query { get; set; }

    public string? ConversationId { get; set; }

    public bool? PreferEntitledContent { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RetrievalMode Mode { get; set; } = RetrievalMode.Traditional;
}
