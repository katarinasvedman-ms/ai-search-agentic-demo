namespace SecureRagChat.Models;

public sealed class RetrievalResult
{
    public required RetrievedChunk[] Chunks { get; set; }
    public required RetrievalPlane Plane { get; set; }
    public required RetrievalSource Source { get; set; }
    public Citation[] Citations { get; set; } = [];
    public int ChunkCount => Chunks.Length;
}
