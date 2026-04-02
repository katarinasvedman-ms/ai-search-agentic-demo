using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Microsoft.Extensions.Options;
using SecureRagChat.Configuration;
using SecureRagChat.Models;

namespace SecureRagChat.Services;

public sealed class AzureSearchRetrievalService : IRetrievalService
{
    private static readonly TokenRequestContext SearchTokenContext =
        new(["https://search.azure.com/.default"]);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureSearchOptions _options;
    private readonly TokenCredential _credential;
    private readonly ILogger<AzureSearchRetrievalService> _logger;

    public AzureSearchRetrievalService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureSearchOptions> options,
        TokenCredential credential,
        ILogger<AzureSearchRetrievalService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _credential = credential;
        _logger = logger;
    }

    public async Task<RetrievalResult> RetrieveAsync(string query, string? userToken, CancellationToken ct = default)
    {
        var plane = userToken is not null ? RetrievalPlane.Entitled : RetrievalPlane.Public;
        var indexName = plane == RetrievalPlane.Entitled ? _options.EntitledIndex : _options.PublicIndex;

        _logger.LogInformation("Retrieving from {Plane} plane, index={Index}", plane, indexName);

        using var client = _httpClientFactory.CreateClient("AzureSearch");
        var requestUrl = $"{_options.Endpoint}/indexes/{indexName}/docs/search?api-version={_options.ApiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Add("api-key", _options.ApiKey);
            _logger.LogDebug("Using Azure AI Search API key authentication for local retrieval.");
        }
        else
        {
            var serviceToken = await _credential.GetTokenAsync(SearchTokenContext, ct);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken.Token);
        }

        if (userToken is not null)
        {
            request.Headers.Add("x-ms-query-source-authorization", $"Bearer {userToken}");
            _logger.LogDebug("Added x-ms-query-source-authorization header for entitled retrieval");
        }

        var searchBody = new SearchRequest
        {
            Search = query,
            Top = 10,
            Select = "id,title,url,snippet"
        };

        var requestPayload = JsonSerializer.Serialize(searchBody, SearchSerializerContext.Default.SearchRequest);
        _logger.LogInformation(
            "Traditional retrieval constructed Azure AI Search request. Plane={Plane}, Index={Index}, Filters={Filters}, Request={Request}",
            plane,
            indexName,
            "none",
            requestPayload);

        request.Content = new StringContent(
            requestPayload,
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Azure Search returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new HttpRequestException($"Azure AI Search returned {(int)response.StatusCode}");
        }

        var responseStream = await response.Content.ReadAsStreamAsync(ct);
        var searchResult = await JsonSerializer.DeserializeAsync(
            responseStream,
            SearchSerializerContext.Default.SearchResponse,
            ct);

        var chunks = searchResult?.Value?
            .Select(v => new RetrievedChunk
            {
                Id = v.Id ?? "",
                Title = v.Title ?? "",
                Url = v.Url,
                Snippet = v.Snippet ?? ""
            })
            .ToArray() ?? [];

        _logger.LogInformation("Traditional retrieval returned {ChunkCount} chunks from {Plane} plane", chunks.Length, plane);

        return new RetrievalResult
        {
            Chunks = chunks,
            Plane = plane,
            Source = RetrievalSource.AzureSearch
        };
    }
}

// Internal DTOs for Azure AI Search request/response

internal sealed class SearchRequest
{
    [JsonPropertyName("search")]
    public required string Search { get; set; }

    [JsonPropertyName("top")]
    public int Top { get; set; } = 10;

    [JsonPropertyName("select")]
    public string? Select { get; set; }
}

internal sealed class SearchResponse
{
    [JsonPropertyName("value")]
    public SearchResultItem[]? Value { get; set; }
}

internal sealed class SearchResultItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("snippet")]
    public string? Snippet { get; set; }
}

[JsonSerializable(typeof(SearchRequest))]
[JsonSerializable(typeof(SearchResponse))]
internal partial class SearchSerializerContext : JsonSerializerContext;
