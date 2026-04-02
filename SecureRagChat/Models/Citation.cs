namespace SecureRagChat.Models;

public sealed class Citation
{
    public required string Title { get; set; }
    public string? Url { get; set; }
    public int SourceIndex { get; set; }
}
