namespace SecureRagChat.Configuration;

public sealed class AzureSearchOptions
{
    public const string SectionName = "AzureSearch";

    public required string Endpoint { get; set; }
    public required string PublicIndex { get; set; }
    public required string EntitledIndex { get; set; }
    public string ApiVersion { get; set; } = "2025-11-01-preview";
    public bool EnableSemanticRanking { get; set; } = true;
    public string SemanticConfiguration { get; set; } = "default";
    public string? ApiKey { get; set; }
    public bool UseLoggedInDeveloperIdentityForUserToken { get; set; }
}
