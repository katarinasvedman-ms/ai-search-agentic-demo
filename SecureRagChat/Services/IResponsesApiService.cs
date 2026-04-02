using SecureRagChat.Models;

namespace SecureRagChat.Services;

public interface IResponsesApiService
{
    Task<GenerationResult> GenerateAsync(string query, RetrievedChunk[] chunks, CancellationToken ct = default);
}
