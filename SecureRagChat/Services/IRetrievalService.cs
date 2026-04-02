using SecureRagChat.Models;

namespace SecureRagChat.Services;

public interface IRetrievalService
{
    Task<RetrievalResult> RetrieveAsync(string query, string? userToken, CancellationToken ct = default);
}
