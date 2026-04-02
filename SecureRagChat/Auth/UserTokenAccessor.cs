using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SecureRagChat.Configuration;

namespace SecureRagChat.Auth;

/// <summary>
/// Extracts the incoming user bearer token from the HTTP request.
/// Returns null for anonymous requests.
/// </summary>
public sealed class UserTokenAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDevelopmentUserTokenProvider _developmentUserTokenProvider;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly AzureSearchOptions _azureSearchOptions;
    private readonly ILogger<UserTokenAccessor> _logger;

    public UserTokenAccessor(
        IHttpContextAccessor httpContextAccessor,
        IDevelopmentUserTokenProvider developmentUserTokenProvider,
        IHostEnvironment hostEnvironment,
        IOptions<AzureSearchOptions> azureSearchOptions,
        ILogger<UserTokenAccessor> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _developmentUserTokenProvider = developmentUserTokenProvider;
        _hostEnvironment = hostEnvironment;
        _azureSearchOptions = azureSearchOptions.Value;
        _logger = logger;
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    /// <summary>
    /// Returns the raw bearer token from the Authorization header, or null if absent/anonymous.
    /// </summary>
    public async Task<string?> GetUserTokenAsync(CancellationToken ct = default)
    {
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            if (_hostEnvironment.IsDevelopment() && _azureSearchOptions.UseLoggedInDeveloperIdentityForUserToken)
            {
                _logger.LogDebug("No incoming user bearer token. Attempting development Azure CLI user token fallback.");
                return await _developmentUserTokenProvider.TryGetUserTokenAsync(ct);
            }

            return null;
        }

        // Expect "Bearer <token>"
        const string bearerPrefix = "Bearer ";
        if (authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            return authHeader[bearerPrefix.Length..].Trim();

        return null;
    }
}
