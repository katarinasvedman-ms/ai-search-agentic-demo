namespace SecureRagChat.Models;

public sealed class GenerationResult
{
    public required string Answer { get; set; }
    public required Citation[] Citations { get; set; }
}
