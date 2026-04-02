namespace SecureRagChat.Configuration;

public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public required string Endpoint { get; set; }
    public required string Model { get; set; }
}
