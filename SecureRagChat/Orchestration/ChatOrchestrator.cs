using SecureRagChat.Auth;
using SecureRagChat.Models;
using SecureRagChat.Services;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace SecureRagChat.Orchestration;

/// <summary>
/// Deterministic orchestrator that coordinates the secure RAG pipeline.
/// Flow: Auth inspection → Retrieval plane selection → Azure AI Search → Azure OpenAI → Response assembly.
/// All steps are explicit and deterministic; no autonomous tool-calling.
/// </summary>
public sealed class ChatOrchestrator
{
    private readonly IRetrievalService _retrievalService;
    private readonly IAgenticRetrievalService _agenticRetrievalService;
    private readonly IBingRetrievalService _bingRetrievalService;
    private readonly IResponsesApiService _responsesApiService;
    private readonly UserTokenAccessor _tokenAccessor;
    private readonly ILogger<ChatOrchestrator> _logger;

    public ChatOrchestrator(
        IRetrievalService retrievalService,
        IAgenticRetrievalService agenticRetrievalService,
        IBingRetrievalService bingRetrievalService,
        IResponsesApiService responsesApiService,
        UserTokenAccessor tokenAccessor,
        ILogger<ChatOrchestrator> logger)
    {
        _retrievalService = retrievalService;
        _agenticRetrievalService = agenticRetrievalService;
        _bingRetrievalService = bingRetrievalService;
        _responsesApiService = responsesApiService;
        _tokenAccessor = tokenAccessor;
        _logger = logger;
    }

    public async Task<ChatResponse> OrchestrateAsync(ChatRequest request, CancellationToken ct = default)
    {
        // Step 1: Auth inspection
        var allowDevelopmentFallback = request.PreferEntitledContent == true;
        var userToken = await _tokenAccessor.GetUserTokenAsync(allowDevelopmentFallback, ct);
        var isAuthenticated = _tokenAccessor.IsAuthenticated || userToken is not null;
        var mode = request.Mode;

        _logger.LogInformation("Orchestration started. IsAuthenticated={IsAuth}, PreferEntitled={Prefer}, RetrievalMode={Mode}",
            isAuthenticated, request.PreferEntitledContent, mode);

        // Step 2: Determine retrieval plane
        // Use entitled plane only when authenticated AND user token is available AND not explicitly opting out
        var useEntitled = userToken is not null && request.PreferEntitledContent != false;
        var effectiveToken = useEntitled ? userToken : null;

        var plane = useEntitled ? RetrievalPlane.Entitled : RetrievalPlane.Public;
        _logger.LogInformation("Selected retrieval plane: {Plane}", plane);

        // Step 3: Retrieve — authorization enforced by Azure AI Search for entitled content.
        RetrievalResult retrievalResult;
        if (mode == RetrievalMode.Agentic)
        {
            try
            {
                retrievalResult = await _agenticRetrievalService.RetrieveAsync(request.Query, plane, effectiveToken, ct);

                _logger.LogInformation("Retrieval complete: {ChunkCount} chunks from {Plane} via {Source}",
                    retrievalResult.ChunkCount, retrievalResult.Plane, retrievalResult.Source);

                if (retrievalResult.ChunkCount == 0)
                {
                    _logger.LogWarning("No chunks retrieved. Returning fallback response.");
                    return BuildNoChunkResponse(request.Query, mode, retrievalResult, isAuthenticated);
                }

                var answer = string.IsNullOrWhiteSpace(retrievalResult.AgenticAnswer)
                    ? "I don't know based on the available information."
                    : retrievalResult.AgenticAnswer;

                var citationSelectionText = answer;

                if (LooksLikeExtractiveJson(answer))
                {
                    try
                    {
                        var synthesis = await _responsesApiService.GenerateAsync(
                            request.Query,
                            retrievalResult.Chunks,
                            ct);

                        answer = synthesis.Answer;
                    }
                    catch (Exception synthesisEx)
                    {
                        _logger.LogWarning(
                            synthesisEx,
                            "Agentic extractive payload synthesis failed. Falling back to direct answer text.");
                    }
                }

                answer = SanitizeAgenticAnswer(answer);

                if (string.IsNullOrWhiteSpace(answer))
                {
                    answer = "I don't know based on the available information.";
                }

                var isNoAnswerAgentic = IsNoAnswerResponse(answer);
                var responseCitationsAgentic = isNoAnswerAgentic
                    ? []
                    : SelectAgenticCitationsByAnswerReferences(citationSelectionText, retrievalResult.Citations);

                _logger.LogInformation("Orchestration complete (agentic direct). Citations={CitationCount}",
                    responseCitationsAgentic.Length);

                return BuildChatResponse(
                    request.Query,
                    answer,
                    responseCitationsAgentic,
                    mode,
                    retrievalResult,
                    isAuthenticated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agentic retrieval failed for plane {Plane}", plane);
                throw;
            }
        }
        else if (useEntitled)
        {
            try
            {
                retrievalResult = await _retrievalService.RetrieveAsync(request.Query, effectiveToken, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retrieval failed for plane {Plane}", plane);
                throw;
            }
        }
        else
        {
            retrievalResult = await RetrieveAnonymousAsync(request.Query, ct);
        }

        _logger.LogInformation("Retrieval complete: {ChunkCount} chunks from {Plane} via {Source}",
            retrievalResult.ChunkCount, retrievalResult.Plane, retrievalResult.Source);

        // Step 4: Guard — if no chunks, return early
        if (retrievalResult.ChunkCount == 0)
        {
            _logger.LogWarning("No chunks retrieved. Returning fallback response.");
            return BuildNoChunkResponse(request.Query, mode, retrievalResult, isAuthenticated);
        }

        // Step 5: Generate — traditional mode model call with authorized chunks
        GenerationResult generationResult;
        try
        {
            generationResult = await _responsesApiService.GenerateAsync(
                request.Query, retrievalResult.Chunks, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generation failed");
            throw;
        }

        // Step 6: Assemble response
        var isNoAnswer = IsNoAnswerResponse(generationResult.Answer);

        var responseCitations = isNoAnswer
            ? []
            : ShouldPreferRetrievalCitations(generationResult.Citations, retrievalResult.Citations)
                ? retrievalResult.Citations
                : generationResult.Citations;

        _logger.LogInformation("Orchestration complete. Citations={CitationCount}",
            responseCitations.Length);

        return BuildChatResponse(
            request.Query,
            generationResult.Answer,
            responseCitations,
            mode,
            retrievalResult,
            isAuthenticated);
    }

    private static ChatResponse BuildNoChunkResponse(
        string query,
        RetrievalMode mode,
        RetrievalResult retrievalResult,
        bool isAuthenticated)
    {
        return BuildChatResponse(
            query,
            "I don't have relevant information to answer your question.",
            [],
            mode,
            retrievalResult,
            isAuthenticated,
            chunkCountOverride: 0);
    }

    private static ChatResponse BuildChatResponse(
        string query,
        string answer,
        Citation[] citations,
        RetrievalMode mode,
        RetrievalResult retrievalResult,
        bool isAuthenticated,
        int? chunkCountOverride = null)
    {
        return new ChatResponse
        {
            Answer = answer,
            RetrievalPlane = retrievalResult.Plane,
            Citations = citations,
            Diagnostics = new ChatDiagnostics
            {
                ChunkCount = chunkCountOverride ?? retrievalResult.ChunkCount,
                IsAuthenticated = isAuthenticated,
                RetrievalMode = mode,
                RetrievalSource = retrievalResult.Source
            },
            RetrievalDetails = BuildRetrievalDetails(query, mode, retrievalResult, isAuthenticated)
        };
    }

    private static RetrievalDetails BuildRetrievalDetails(
        string query,
        RetrievalMode mode,
        RetrievalResult retrievalResult,
        bool isAuthenticated)
    {
        return new RetrievalDetails
        {
            Mode = mode == RetrievalMode.Agentic ? "agentic" : "traditional",
            RetrievalStyle = ResolveRetrievalStyle(mode, retrievalResult.Source),
            Query = query,
            Filters = mode == RetrievalMode.Traditional && retrievalResult.Plane == RetrievalPlane.Entitled
                ? "security trimming applied by retrieval plane"
                : "none",
            ResultsCount = retrievalResult.ChunkCount,
            Authorization = ResolveAuthorizationLabel(retrievalResult.Plane, isAuthenticated),
            KnowledgeBaseUsed = mode == RetrievalMode.Agentic ? retrievalResult.Source == RetrievalSource.KnowledgeBase : null,
            QueryConstruction = mode == RetrievalMode.Agentic ? "handled by system" : null
        };
    }

    private static string ResolveRetrievalStyle(RetrievalMode mode, RetrievalSource source)
    {
        if (mode == RetrievalMode.Agentic)
        {
            return "Knowledge base";
        }

        return source == RetrievalSource.Bing ? "Web fallback" : "Semantic ranking";
    }

    private static string ResolveAuthorizationLabel(RetrievalPlane plane, bool isAuthenticated)
    {
        if (plane == RetrievalPlane.Entitled)
        {
            return "entitled user";
        }

        return isAuthenticated ? "user" : "guest";
    }

    private static bool ShouldPreferRetrievalCitations(Citation[] generationCitations, Citation[] retrievalCitations)
    {
        if (generationCitations.Length == 0)
        {
            return retrievalCitations.Length > 0;
        }

        return false;
    }

    private static bool IsNoAnswerResponse(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        var normalizedAnswer = answer.Trim().ToLowerInvariant();
        return normalizedAnswer == "i don't know based on the available information."
            || normalizedAnswer == "i don't have relevant information to answer your question.";
    }

    private static Citation[] SelectAgenticCitationsByAnswerReferences(string answer, Citation[] citations)
    {
        if (citations.Length == 0)
        {
            return [];
        }

        var referencedSourceIndexes = new HashSet<int>();

        var refIdMatches = Regex.Matches(answer, @"\[\s*ref_id\s*:\s*(\d+)\s*\]", RegexOptions.IgnoreCase);
        foreach (Match match in refIdMatches)
        {
            if (int.TryParse(match.Groups[1].Value, out var zeroBased))
            {
                referencedSourceIndexes.Add(zeroBased + 1);
            }
        }

        var sourceMatches = Regex.Matches(answer, @"\[\s*Source\s+(\d+)\s*\]", RegexOptions.IgnoreCase);
        foreach (Match match in sourceMatches)
        {
            if (int.TryParse(match.Groups[1].Value, out var oneBased) && oneBased > 0)
            {
                referencedSourceIndexes.Add(oneBased);
            }
        }

        if (referencedSourceIndexes.Count == 0)
        {
            return citations;
        }

        var filtered = citations
            .Where(citation => referencedSourceIndexes.Contains(citation.SourceIndex))
            .ToArray();

        return filtered.Length > 0 ? filtered : citations;
    }

    private static bool LooksLikeExtractiveJson(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        var trimmed = answer.TrimStart();
        if (!trimmed.StartsWith("[", StringComparison.Ordinal)
            && !trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(answer);
            return doc.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string SanitizeAgenticAnswer(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return string.Empty;
        }

        var normalized = answer.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();

        var referencesHeadingIndex = lines.FindIndex(line => IsReferenceAppendixHeading(line.Trim()));

        if (referencesHeadingIndex >= 0)
        {
            lines = lines.Take(referencesHeadingIndex).ToList();
        }

        var cleanedLines = lines
            .Select(line => line.TrimEnd())
            .Where(line => !line.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase))
            .Where(line => !Regex.IsMatch(line.Trim(), @"^\[\s*ref_id\s*:\s*\d+\s*\]$", RegexOptions.IgnoreCase))
            .ToList();

        var cleaned = string.Join("\n", cleanedLines);
        cleaned = Regex.Replace(cleaned, @"\[\s*ref_id\s*:\s*\d+\s*\]", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");

        return cleaned.Trim();
    }

    private static bool IsReferenceAppendixHeading(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized = line.Trim();

        if (normalized.StartsWith("#", StringComparison.Ordinal))
        {
            normalized = normalized.TrimStart('#').Trim();
        }

        if (normalized.EndsWith(":", StringComparison.Ordinal))
        {
            normalized = normalized[..^1].TrimEnd();
        }

        if (normalized.Equals("Reference", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("References", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Regex.IsMatch(normalized, @"^Sources\s*\(\d+\)$", RegexOptions.IgnoreCase);
    }

    private async Task<RetrievalResult> RetrieveAnonymousAsync(string query, CancellationToken ct)
    {
        try
        {
            var publicSearchResult = await _retrievalService.RetrieveAsync(query, userToken: null, ct);
            if (publicSearchResult.ChunkCount > 0)
            {
                return publicSearchResult;
            }

            _logger.LogInformation("Public Azure AI Search returned no chunks. Falling back to Bing.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Public Azure AI Search failed for anonymous request. Falling back to Bing.");
        }

        return await _bingRetrievalService.RetrieveAsync(query, ct);
    }
}
