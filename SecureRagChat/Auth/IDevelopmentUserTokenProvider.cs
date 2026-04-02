namespace SecureRagChat.Auth;

public interface IDevelopmentUserTokenProvider
{
    Task<string?> TryGetUserTokenAsync(CancellationToken ct = default);
}
