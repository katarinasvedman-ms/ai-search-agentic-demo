using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SecureRagChat.Configuration;
using SecureRagChat.Models;

namespace SecureRagChat.Services;

public sealed class BingRetrievalService : IBingRetrievalService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BingSearchOptions _options;
    private readonly ILogger<BingRetrievalService> _logger;

    public BingRetrievalService(
        IHttpClientFactory httpClientFactory,
        IOptions<BingSearchOptions> options,
        ILogger<BingRetrievalService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RetrievalResult> RetrieveAsync(string query, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Bing retrieval is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("BingSearch:ApiKey must be configured when Bing retrieval is enabled.");
        }

        using var client = _httpClientFactory.CreateClient("BingSearch");
        var requestUrl = $"{_options.Endpoint}?q={Uri.EscapeDataString(query)}&count={_options.Count}&mkt={Uri.EscapeDataString(_options.Market)}&textFormat=Raw";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _logger.LogInformation("Retrieving anonymous public content from Bing. Count={Count}, Market={Market}",
            _options.Count, _options.Market);

        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Bing Search returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new HttpRequestException($"Bing Search returned {(int)response.StatusCode}");
        }

        var responseStream = await response.Content.ReadAsStreamAsync(ct);
        var searchResponse = await JsonSerializer.DeserializeAsync(
            responseStream,
            BingSerializerContext.Default.BingSearchResponse,
            ct);

        var chunks = searchResponse?.WebPages?.Value?
            .Where(result => !string.IsNullOrWhiteSpace(result.Url) && !string.IsNullOrWhiteSpace(result.Name))
            .Select(result => new RetrievedChunk
            {
                Id = result.Id ?? result.Url ?? Guid.NewGuid().ToString("N"),
                Title = result.Name ?? result.Url ?? "Untitled result",
                Url = result.Url,
                Snippet = result.Snippet ?? string.Empty
            })
            .ToArray() ?? [];

        _logger.LogInformation("Retrieved {ChunkCount} chunks from Bing", chunks.Length);

        return new RetrievalResult
        {
            Chunks = chunks,
            Plane = RetrievalPlane.Public,
            Source = RetrievalSource.Bing
        };
    }
}

internal sealed class BingSearchResponse
{
    [JsonPropertyName("webPages")]
    public BingWebPagesResult? WebPages { get; set; }
}

internal sealed class BingWebPagesResult
{
    [JsonPropertyName("value")]
    public BingWebPageItem[]? Value { get; set; }
}

internal sealed class BingWebPageItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("snippet")]
    public string? Snippet { get; set; }
}

[JsonSerializable(typeof(BingSearchResponse))]
internal partial class BingSerializerContext : JsonSerializerContext;