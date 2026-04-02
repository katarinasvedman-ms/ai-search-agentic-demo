using Azure.Core;
using Azure.Identity;

namespace SecureRagChat.Auth;

public sealed class DevelopmentAzureCliUserTokenProvider : IDevelopmentUserTokenProvider
{
    private static readonly TokenRequestContext SearchTokenContext =
        new(["https://search.azure.com/.default"]);

    private readonly AzureCliCredential _credential = new();
    private readonly ILogger<DevelopmentAzureCliUserTokenProvider> _logger;

    public DevelopmentAzureCliUserTokenProvider(ILogger<DevelopmentAzureCliUserTokenProvider> logger)
    {
        _logger = logger;
    }

    public async Task<string?> TryGetUserTokenAsync(CancellationToken ct = default)
    {
        try
        {
            var token = await _credential.GetTokenAsync(SearchTokenContext, ct);
            return token.Token;
        }
        catch (CredentialUnavailableException ex)
        {
            _logger.LogWarning(ex, "Azure CLI credential unavailable. Run 'az login' to use development user token fallback.");
            return null;
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogWarning(ex, "Azure CLI authentication failed while retrieving development user token fallback.");
            return null;
        }
    }
}
