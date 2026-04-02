namespace SecureRagChat.Configuration;

public sealed class BingSearchOptions
{
    public const string SectionName = "BingSearch";

    public bool Enabled { get; set; }
    public required string Endpoint { get; set; }
    public required string ApiKey { get; set; }
    public string Market { get; set; } = "en-US";
    public int Count { get; set; } = 5;
}