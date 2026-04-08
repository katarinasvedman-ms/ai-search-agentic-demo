using SecureRagChat.Auth;
using SecureRagChat.Models;
using SecureRagChat.Services;

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
        // Anonymous requests can fall back to Bing, but retrieval still happens before generation.
        RetrievalResult retrievalResult;
        if (mode == RetrievalMode.Agentic)
        {
            try
            {
                retrievalResult = await _agenticRetrievalService.RetrieveAsync(request.Query, plane, effectiveToken, ct);
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
            return new ChatResponse
            {
                Answer = "I don't have relevant information to answer your question.",
                RetrievalPlane = retrievalResult.Plane,
                Citations = [],
                Diagnostics = new ChatDiagnostics
                {
                    ChunkCount = 0,
                    IsAuthenticated = isAuthenticated,
                    RetrievalMode = mode,
                    RetrievalSource = retrievalResult.Source
                },
                RetrievalDetails = BuildRetrievalDetails(request.Query, mode, retrievalResult, isAuthenticated)
            };
        }

        // Step 5: Generate — model receives ONLY authorized chunks
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
        var responseCitations = generationResult.Citations.Length > 0
            ? generationResult.Citations
            : retrievalResult.Citations;

        _logger.LogInformation("Orchestration complete. Citations={CitationCount}",
            responseCitations.Length);

        return new ChatResponse
        {
            Answer = generationResult.Answer,
            RetrievalPlane = retrievalResult.Plane,
            Citations = responseCitations,
            Diagnostics = new ChatDiagnostics
            {
                ChunkCount = retrievalResult.ChunkCount,
                IsAuthenticated = isAuthenticated,
                RetrievalMode = mode,
                RetrievalSource = retrievalResult.Source
            },
            RetrievalDetails = BuildRetrievalDetails(request.Query, mode, retrievalResult, isAuthenticated)
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
