using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureRagChat.Models;
using SecureRagChat.Orchestration;

namespace SecureRagChat.Api;

[ApiController]
[Route("api/chat")]
[AllowAnonymous]
public sealed class ChatController : ControllerBase
{
    private readonly ChatOrchestrator _orchestrator;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ChatOrchestrator orchestrator, ILogger<ChatController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken ct)
    {
        var correlationId = HttpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        HttpContext.Response.Headers["X-Correlation-Id"] = correlationId;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["ConversationId"] = request.ConversationId ?? "none"
        });

        _logger.LogInformation("Chat request received. Query length={Length}", request.Query.Length);

        try
        {
            var response = await _orchestrator.OrchestrateAsync(request, ct);
            return Ok(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Upstream service error");
            return StatusCode(502, new { error = "An upstream service error occurred.", correlationId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing chat request");
            return StatusCode(500, new { error = "An internal error occurred.", correlationId });
        }
    }
}
