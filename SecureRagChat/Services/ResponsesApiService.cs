using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Microsoft.Extensions.Options;
using SecureRagChat.Configuration;
using SecureRagChat.Models;

namespace SecureRagChat.Services;

public sealed class ResponsesApiService : IResponsesApiService
{
    private static readonly TokenRequestContext OpenAITokenContext =
        new(["https://cognitiveservices.azure.com/.default"]);

    private const string SystemPrompt = """
        You are a helpful assistant. Answer the user's question using ONLY the provided context below.
        If the answer is not found in the context, respond with "I don't know based on the available information."

        Rules:
        - Use ONLY the provided context to answer.
        - Include citations as [Source N] where N corresponds to the source number.
        - Never make up information not present in the context.
                - Prefer clear, decision-ready structure over raw excerpts.
                - For recommendation/planning questions, use this structure:
                    1) Recommendation summary
                    2) Tradeoffs (coverage, timing/fronthaul, installation, operations risk)
                    3) Phased deployment plan (Phase 1, Phase 2, Phase 3)
                - Keep each claim grounded with citations.
        """;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureOpenAIOptions _options;
    private readonly TokenCredential _credential;
    private readonly ILogger<ResponsesApiService> _logger;

    public ResponsesApiService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAIOptions> options,
        TokenCredential credential,
        ILogger<ResponsesApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _credential = credential;
        _logger = logger;
    }

    public async Task<GenerationResult> GenerateAsync(string query, RetrievedChunk[] chunks, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating response with {ChunkCount} chunks", chunks.Length);

        var token = await _credential.GetTokenAsync(OpenAITokenContext, ct);

        using var client = _httpClientFactory.CreateClient("AzureOpenAI");
        var requestUrl = BuildResponsesUrl(_options.Endpoint);

        var contextBlock = BuildContextBlock(chunks);

        var requestBody = new ResponsesApiRequest
        {
            Model = _options.Model,
            Input = [
                new ResponsesMessage { Role = "system", Content = SystemPrompt },
                new ResponsesMessage { Role = "user", Content = $"Context:\n{contextBlock}\n\nQuestion: {query}" }
            ],
            Temperature = 0
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody, ResponsesSerializerContext.Default.ResponsesApiRequest),
            Encoding.UTF8,
            "application/json");

        var startTime = DateTime.UtcNow;
        using var response = await client.SendAsync(request, ct);
        var elapsed = DateTime.UtcNow - startTime;

        _logger.LogInformation("Responses API call completed in {ElapsedMs}ms, status={StatusCode}",
            elapsed.TotalMilliseconds, (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Responses API returned {StatusCode}: {Body}", (int)response.StatusCode, errorBody);
            throw new HttpRequestException($"Azure OpenAI Responses API returned {(int)response.StatusCode}");
        }

        var responseStream = await response.Content.ReadAsStreamAsync(ct);
        var apiResponse = await JsonSerializer.DeserializeAsync(
            responseStream,
            ResponsesSerializerContext.Default.ResponsesApiResponse,
            ct);

        var answerText = apiResponse?.Output?
            .Where(o => o.Type == "message")
            .SelectMany(o => o.Content ?? [])
            .Where(c => c.Type == "output_text")
            .Select(c => c.Text)
            .FirstOrDefault() ?? "No response generated.";

        var citations = ExtractCitations(answerText, chunks);

        return new GenerationResult
        {
            Answer = answerText,
            Citations = citations
        };
    }

    private static string BuildResponsesUrl(string endpoint)
    {
        var trimmed = endpoint.TrimEnd('/');

        if (trimmed.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            return $"{trimmed}/responses";
        }

        return $"{trimmed}/openai/v1/responses";
    }

    private static string BuildContextBlock(RetrievedChunk[] chunks)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < chunks.Length; i++)
        {
            var chunk = chunks[i];
            sb.AppendLine($"[Source {i + 1}] Title: {chunk.Title}");
            if (!string.IsNullOrEmpty(chunk.Url))
                sb.AppendLine($"URL: {chunk.Url}");
            sb.AppendLine(string.IsNullOrWhiteSpace(chunk.Content) ? chunk.Snippet : chunk.Content);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static Citation[] ExtractCitations(string answer, RetrievedChunk[] chunks)
    {
        var citations = new List<Citation>();
        for (int i = 0; i < chunks.Length; i++)
        {
            var marker = $"[Source {i + 1}]";
            if (answer.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                citations.Add(new Citation
                {
                    Title = chunks[i].Title,
                    Url = chunks[i].Url,
                    SourceIndex = i + 1
                });
            }
        }
        return citations.ToArray();
    }
}

// Internal DTOs for Azure OpenAI Responses API

internal sealed class ResponsesApiRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; set; }

    [JsonPropertyName("input")]
    public required ResponsesMessage[] Input { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }
}

internal sealed class ResponsesMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("content")]
    public required string Content { get; set; }
}

internal sealed class ResponsesApiResponse
{
    [JsonPropertyName("output")]
    public ResponsesOutputItem[]? Output { get; set; }
}

internal sealed class ResponsesOutputItem
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("content")]
    public ResponsesContentItem[]? Content { get; set; }
}

internal sealed class ResponsesContentItem
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

[JsonSerializable(typeof(ResponsesApiRequest))]
[JsonSerializable(typeof(ResponsesApiResponse))]
internal partial class ResponsesSerializerContext : JsonSerializerContext;
