using SecureRagChat.Models;

namespace SecureRagChat.Services;

public interface IAgenticRetrievalService
{
    Task<RetrievalResult> RetrieveAsync(string query, RetrievalPlane plane, string? userToken, CancellationToken ct = default);
}