using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Azure.Core;
using Microsoft.Extensions.Options;
using SecureRagChat.Configuration;
using SecureRagChat.Models;

namespace SecureRagChat.Services;

public sealed class AgenticRetrievalService : IAgenticRetrievalService
{
    private const string FallbackOutputMode = "answerSynthesis";

    private static readonly TokenRequestContext SearchTokenContext =
        new(["https://search.azure.com/.default"]);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AgenticRetrievalOptions _options;
    private readonly TokenCredential _credential;
    private readonly ILogger<AgenticRetrievalService> _logger;

    public AgenticRetrievalService(
        IHttpClientFactory httpClientFactory,
        IOptions<AgenticRetrievalOptions> options,
        TokenCredential credential,
        ILogger<AgenticRetrievalService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _credential = credential;
        _logger = logger;
    }

    public async Task<RetrievalResult> RetrieveAsync(string query, RetrievalPlane plane, string? userToken, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Agentic retrieval starting. KnowledgeBase={KnowledgeBase}, Plane={Plane}, QueryConstruction={QueryConstruction}",
            _options.KnowledgeBaseName,
            plane,
            "KnowledgeBase retrieve call only");

        using var client = _httpClientFactory.CreateClient("AgenticRetrieval");
        var requestUrl = $"{_options.Endpoint.TrimEnd('/')}/knowledgebases/{_options.KnowledgeBaseName}/retrieve?api-version={_options.ApiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Add("api-key", _options.ApiKey);
            _logger.LogDebug("Agentic retrieval is using Azure AI Search API key authentication.");
        }
        else
        {
            var serviceToken = await _credential.GetTokenAsync(SearchTokenContext, ct);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken.Token);
            _logger.LogDebug("Agentic retrieval is using Azure RBAC authentication.");
        }

        if (plane == RetrievalPlane.Entitled && !string.IsNullOrWhiteSpace(userToken))
        {
            request.Headers.Add("x-ms-query-source-authorization", userToken);
            _logger.LogInformation("Passing caller token to the knowledge base retrieve API for entitled agentic retrieval.");
        }
        else
        {
            _logger.LogInformation("Calling knowledge base retrieve API without caller token; public content only.");
        }

        var configuredOutputMode = NormalizeOutputMode(_options.OutputMode);
        var requestBody = new AgenticRetrievalRequest
        {
            Messages =
            [
                new AgenticMessage
                {
                    Role = "user",
                    Content = [new AgenticMessageContent { Type = "text", Text = query }]
                }
            ],
            RetrievalReasoningEffort = new AgenticReasoningEffort
            {
                Kind = _options.RetrievalReasoningEffort
            },
            OutputMode = configuredOutputMode,
            IncludeActivity = _options.IncludeActivity,
            MaxOutputSize = _options.MaxOutputSize,
            MaxRuntimeInSeconds = _options.MaxRuntimeInSeconds
        };

        var requestPayload = JsonSerializer.Serialize(requestBody, AgenticSerializerContext.Default.AgenticRetrievalRequest);
        _logger.LogInformation("Agentic retrieval request prepared without manual search query construction. Request={Request}", requestPayload);

        request.Content = new StringContent(requestPayload, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode &&
            string.Equals(configuredOutputMode, "extractedData", StringComparison.OrdinalIgnoreCase))
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            if (MentionsInvalidOutputMode(errorBody, configuredOutputMode))
            {
                _logger.LogWarning(
                    "Knowledge base retrieve rejected outputMode '{OutputMode}'. Retrying with '{FallbackOutputMode}'.",
                    configuredOutputMode,
                    FallbackOutputMode);

                return await RetryWithOutputModeAsync(
                    client,
                    requestUrl,
                    query,
                    plane,
                    userToken,
                    configuredOutputMode,
                    FallbackOutputMode,
                    ct);
            }

            _logger.LogError("Knowledge base retrieve returned {StatusCode}: {Body}", (int)response.StatusCode, errorBody);
            throw new HttpRequestException($"Knowledge base retrieve returned {(int)response.StatusCode}");
        }

        if (response.StatusCode is not HttpStatusCode.OK and not HttpStatusCode.PartialContent)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Knowledge base retrieve returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new HttpRequestException($"Knowledge base retrieve returned {(int)response.StatusCode}");
        }

        var responseStream = await response.Content.ReadAsStreamAsync(ct);
        var apiResponse = await JsonSerializer.DeserializeAsync(
            responseStream,
            AgenticSerializerContext.Default.AgenticRetrievalResponse,
            ct);

        var chunks = ExtractChunks(apiResponse);
        var citations = ExtractCitations(apiResponse, chunks);

        LogActivity(apiResponse);
        _logger.LogInformation(
            "Agentic retrieval completed. KnowledgeBase={KnowledgeBase}, ChunkCount={ChunkCount}, CitationCount={CitationCount}",
            _options.KnowledgeBaseName,
            chunks.Length,
            citations.Length);

        return new RetrievalResult
        {
            Chunks = chunks,
            Plane = plane,
            Source = RetrievalSource.KnowledgeBase,
            Citations = citations
        };
    }

    private async Task<RetrievalResult> RetryWithOutputModeAsync(
        HttpClient client,
        string requestUrl,
        string query,
        RetrievalPlane plane,
        string? userToken,
        string originalOutputMode,
        string retryOutputMode,
        CancellationToken ct)
    {
        using var retryRequest = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            retryRequest.Headers.Add("api-key", _options.ApiKey);
        }
        else
        {
            var serviceToken = await _credential.GetTokenAsync(SearchTokenContext, ct);
            retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken.Token);
        }

        if (plane == RetrievalPlane.Entitled && !string.IsNullOrWhiteSpace(userToken))
        {
            retryRequest.Headers.Add("x-ms-query-source-authorization", userToken);
        }

        var retryBody = new AgenticRetrievalRequest
        {
            Messages =
            [
                new AgenticMessage
                {
                    Role = "user",
                    Content = [new AgenticMessageContent { Type = "text", Text = query }]
                }
            ],
            RetrievalReasoningEffort = new AgenticReasoningEffort
            {
                Kind = _options.RetrievalReasoningEffort
            },
            OutputMode = retryOutputMode,
            IncludeActivity = _options.IncludeActivity,
            MaxOutputSize = _options.MaxOutputSize,
            MaxRuntimeInSeconds = _options.MaxRuntimeInSeconds
        };

        retryRequest.Content = new StringContent(
            JsonSerializer.Serialize(retryBody, AgenticSerializerContext.Default.AgenticRetrievalRequest),
            Encoding.UTF8,
            "application/json");

        using var retryResponse = await client.SendAsync(retryRequest, ct);
        if (retryResponse.StatusCode is not HttpStatusCode.OK and not HttpStatusCode.PartialContent)
        {
            var retryErrorBody = await retryResponse.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Knowledge base retrieve retry returned {StatusCode}. OriginalOutputMode={OriginalOutputMode}, RetryOutputMode={RetryOutputMode}, Body={Body}",
                (int)retryResponse.StatusCode,
                originalOutputMode,
                retryOutputMode,
                retryErrorBody);
            throw new HttpRequestException($"Knowledge base retrieve returned {(int)retryResponse.StatusCode}");
        }

        var retryResponseStream = await retryResponse.Content.ReadAsStreamAsync(ct);
        var apiResponse = await JsonSerializer.DeserializeAsync(
            retryResponseStream,
            AgenticSerializerContext.Default.AgenticRetrievalResponse,
            ct);

        var chunks = ExtractChunks(apiResponse);
        var citations = ExtractCitations(apiResponse, chunks);

        LogActivity(apiResponse);
        _logger.LogInformation(
            "Agentic retrieval completed after output mode fallback. KnowledgeBase={KnowledgeBase}, ChunkCount={ChunkCount}, CitationCount={CitationCount}",
            _options.KnowledgeBaseName,
            chunks.Length,
            citations.Length);

        return new RetrievalResult
        {
            Chunks = chunks,
            Plane = plane,
            Source = RetrievalSource.KnowledgeBase,
            Citations = citations
        };
    }

    private static string NormalizeOutputMode(string? outputMode)
    {
        return string.IsNullOrWhiteSpace(outputMode) ? FallbackOutputMode : outputMode.Trim();
    }

    private static bool MentionsInvalidOutputMode(string errorBody, string outputMode)
    {
        if (string.IsNullOrWhiteSpace(errorBody))
        {
            return false;
        }

        return errorBody.Contains("Requested value", StringComparison.OrdinalIgnoreCase)
            && errorBody.Contains(outputMode, StringComparison.OrdinalIgnoreCase);
    }

    private void LogActivity(AgenticRetrievalResponse? apiResponse)
    {
        if (apiResponse?.Activity is null || apiResponse.Activity.Length == 0)
        {
            _logger.LogInformation("Agentic retrieval activity log is empty.");
            return;
        }

        foreach (var activity in apiResponse.Activity)
        {
            if (activity.Type.Equals("searchIndex", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Agentic retrieval delegated query execution. KnowledgeSource={KnowledgeSource}, Query={Query}, Filter={Filter}, Count={Count}",
                    activity.KnowledgeSourceName ?? "unknown",
                    activity.SearchIndexArguments?.Search ?? "n/a",
                    activity.SearchIndexArguments?.Filter ?? "none",
                    activity.Count);
            }
            else
            {
                _logger.LogInformation("Agentic retrieval activity step: Type={Type}, Count={Count}", activity.Type, activity.Count);
            }
        }
    }

    private static RetrievedChunk[] ExtractChunks(AgenticRetrievalResponse? apiResponse)
    {
        var contentText = apiResponse?.Response?
            .SelectMany(message => message.Content ?? [])
            .FirstOrDefault(content => string.Equals(content.Type, "text", StringComparison.OrdinalIgnoreCase))?
            .Text;

        if (string.IsNullOrWhiteSpace(contentText))
        {
            return [];
        }

        try
        {
            var chunkData = JsonSerializer.Deserialize(contentText, AgenticSerializerContext.Default.JsonArray);
            if (chunkData is null)
            {
                return [];
            }

            return chunkData
                .OfType<JsonObject>()
                .Select((item, index) => CreateChunk(item, index))
                .Where(chunk => chunk is not null)
                .Select(chunk => chunk!)
                .ToArray();
        }
        catch (JsonException)
        {
            return
            [
                new RetrievedChunk
                {
                    Id = "kb-response-0",
                    Title = "Knowledge base result",
                    Snippet = contentText.Trim()
                }
            ];
        }
    }

    private static RetrievedChunk? CreateChunk(JsonObject item, int index)
    {
        var refId = item["ref_id"]?.GetValue<int?>()?.ToString() ?? index.ToString();
        var title = item["title"]?.GetValue<string>()
            ?? item["terms"]?.GetValue<string>()
            ?? $"Knowledge base source {index + 1}";
        var content = item["content"]?.GetValue<string>()
            ?? item["text"]?.GetValue<string>()
            ?? item.ToJsonString();
        var url = item["url"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return new RetrievedChunk
        {
            Id = refId,
            Title = title,
            Url = url,
            Snippet = content
        };
    }

    private static Citation[] ExtractCitations(AgenticRetrievalResponse? apiResponse, RetrievedChunk[] chunks)
    {
        if (apiResponse?.References is null || apiResponse.References.Length == 0)
        {
            return [];
        }

        var citations = new List<Citation>();

        foreach (var reference in apiResponse.References)
        {
            var sourceIndex = ParseSourceIndex(reference.Id, chunks);
            var matchingChunk = sourceIndex > 0 && sourceIndex <= chunks.Length ? chunks[sourceIndex - 1] : null;
            var referenceDocId = ExtractDocumentId(reference.SourceData) ?? NormalizeDocumentId(reference.DocKey);
            var hasPlaceholderChunkTitle = string.Equals(
                matchingChunk?.Title,
                "Knowledge base result",
                StringComparison.OrdinalIgnoreCase);

            var citationTitle = !string.IsNullOrWhiteSpace(reference.DocKey)
                ? reference.DocKey
                : !string.IsNullOrWhiteSpace(referenceDocId)
                    ? referenceDocId
                : hasPlaceholderChunkTitle
                    ? $"Knowledge base source {reference.Id ?? (citations.Count + 1).ToString()}"
                    : matchingChunk?.Title ?? $"Knowledge base source {reference.Id}";

            citations.Add(new Citation
            {
                Title = citationTitle,
                Url = ExtractUrl(reference.SourceData, reference.DocKey, matchingChunk) ?? matchingChunk?.Url,
                SourceIndex = sourceIndex > 0 ? sourceIndex : citations.Count + 1
            });
        }

        return citations.ToArray();
    }

    private static int ParseSourceIndex(string? referenceId, RetrievedChunk[] chunks)
    {
        if (!int.TryParse(referenceId, out var zeroBasedId))
        {
            return 0;
        }

        var sourceIndex = zeroBasedId + 1;
        return sourceIndex <= chunks.Length ? sourceIndex : 0;
    }

    private static string? ExtractUrl(JsonObject? sourceData, string? docKey, RetrievedChunk? matchingChunk)
    {
        var directUrl = ExtractFirstStringByKeys(
            sourceData,
            ["url", "sourceUrl", "uri", "documentUrl", "docUrl", "path"]);

        if (!string.IsNullOrWhiteSpace(directUrl))
        {
            return directUrl;
        }

        var docId = ExtractDocumentId(sourceData)
            ?? NormalizeDocumentId(docKey)
            ?? NormalizeDocumentId(matchingChunk?.Id)
            ?? NormalizeDocumentId(matchingChunk?.Title);

        return string.IsNullOrWhiteSpace(docId)
            ? null
            : $"/api/demo-docs/{docId}";
    }

    private static string? ExtractDocumentId(JsonObject? sourceData)
    {
        return NormalizeDocumentId(
            ExtractFirstStringByKeys(
                sourceData,
                ["id", "docId", "documentId", "key", "docKey", "sourceId", "referenceId"]));
    }

    private static string? ExtractFirstStringByKeys(JsonNode? node, string[] keys)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in keys)
            {
                if (obj.TryGetPropertyValue(key, out var value) && value is JsonValue jsonValue)
                {
                    var stringValue = jsonValue.GetValue<string?>();
                    if (!string.IsNullOrWhiteSpace(stringValue))
                    {
                        return stringValue;
                    }
                }
            }

            foreach (var property in obj)
            {
                var nestedValue = ExtractFirstStringByKeys(property.Value, keys);
                if (!string.IsNullOrWhiteSpace(nestedValue))
                {
                    return nestedValue;
                }
            }

            return null;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                var nestedValue = ExtractFirstStringByKeys(item, keys);
                if (!string.IsNullOrWhiteSpace(nestedValue))
                {
                    return nestedValue;
                }
            }
        }

        return null;
    }

    private static string? NormalizeDocumentId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith("/api/demo-docs/", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        }

        if (trimmed.StartsWith("ent-", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var marker = "ent-";
        var markerIndex = trimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var suffix = trimmed[markerIndex..];
        var id = new string(suffix.TakeWhile(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}

internal sealed class AgenticRetrievalRequest
{
    [JsonPropertyName("messages")]
    public required AgenticMessage[] Messages { get; set; }

    [JsonPropertyName("retrievalReasoningEffort")]
    public required AgenticReasoningEffort RetrievalReasoningEffort { get; set; }

    [JsonPropertyName("outputMode")]
    public required string OutputMode { get; set; }

    [JsonPropertyName("includeActivity")]
    public bool IncludeActivity { get; set; }

    [JsonPropertyName("maxOutputSize")]
    public int MaxOutputSize { get; set; }

    [JsonPropertyName("maxRuntimeInSeconds")]
    public int MaxRuntimeInSeconds { get; set; }
}

internal sealed class AgenticMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("content")]
    public required AgenticMessageContent[] Content { get; set; }
}

internal sealed class AgenticMessageContent
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("text")]
    public required string Text { get; set; }
}

internal sealed class AgenticReasoningEffort
{
    [JsonPropertyName("kind")]
    public required string Kind { get; set; }
}

internal sealed class AgenticRetrievalResponse
{
    [JsonPropertyName("response")]
    public AgenticResponseMessage[]? Response { get; set; }

    [JsonPropertyName("activity")]
    public AgenticActivity[]? Activity { get; set; }

    [JsonPropertyName("references")]
    public AgenticReference[]? References { get; set; }
}

internal sealed class AgenticResponseMessage
{
    [JsonPropertyName("content")]
    public AgenticResponseContent[]? Content { get; set; }
}

internal sealed class AgenticResponseContent
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

internal sealed class AgenticActivity
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("knowledgeSourceName")]
    public string? KnowledgeSourceName { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("searchIndexArguments")]
    public AgenticSearchIndexArguments? SearchIndexArguments { get; set; }
}

internal sealed class AgenticSearchIndexArguments
{
    [JsonPropertyName("search")]
    public string? Search { get; set; }

    [JsonPropertyName("filter")]
    public string? Filter { get; set; }
}

internal sealed class AgenticReference
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("docKey")]
    public string? DocKey { get; set; }

    [JsonPropertyName("sourceData")]
    public JsonObject? SourceData { get; set; }
}

[JsonSerializable(typeof(AgenticRetrievalRequest))]
[JsonSerializable(typeof(AgenticRetrievalResponse))]
[JsonSerializable(typeof(JsonArray))]
internal partial class AgenticSerializerContext : JsonSerializerContext;