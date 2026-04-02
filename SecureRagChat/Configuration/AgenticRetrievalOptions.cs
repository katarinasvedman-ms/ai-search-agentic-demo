namespace SecureRagChat.Configuration;

public sealed class AgenticRetrievalOptions
{
    public const string SectionName = "AgenticRetrieval";

    public required string Endpoint { get; set; }
    public required string KnowledgeBaseName { get; set; }
    public string ApiVersion { get; set; } = "2025-11-01-preview";
    public string RetrievalReasoningEffort { get; set; } = "low";
    public string OutputMode { get; set; } = "answerSynthesis";
    public bool IncludeActivity { get; set; } = true;
    public int MaxOutputSize { get; set; } = 6000;
    public int MaxRuntimeInSeconds { get; set; } = 30;
    public string? ApiKey { get; set; }
}