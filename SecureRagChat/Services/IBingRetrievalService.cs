using SecureRagChat.Models;

namespace SecureRagChat.Services;

public interface IBingRetrievalService
{
    Task<RetrievalResult> RetrieveAsync(string query, CancellationToken ct = default);
}