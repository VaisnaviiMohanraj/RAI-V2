using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend.Services.Chat;
using Backend.Services.Auth;
using Backend.Services.Document;
using Backend.Models;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireDomainUser")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IAuthService _authService;
    private readonly IDocumentService _documentService;
    private readonly IConversationManager _conversationManager;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService,
        IAuthService authService,
        IDocumentService documentService,
        IConversationManager conversationManager,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _authService = authService;
        _documentService = documentService;
        _conversationManager = conversationManager;
        _logger = logger;
    }

    // Documented route alias: POST /api/chat/message
    [HttpPost("message")]
    public async Task<ActionResult<ChatResponse>> SendMessageAlias([FromBody] ChatRequest request)
    {
        return await SendMessage(request);
    }

    public class CreateSessionRequest
    {
        public string? Title { get; set; }
    }

    [HttpPost("send")]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            // Get authenticated user ID
            var userId = _authService.GetUserIdFromClaims(User);

            // Debug logging for document integration
            _logger.LogInformation($"Chat request received - Message: {request.Message?.Substring(0, Math.Min(50, request.Message?.Length ?? 0))}...");
            _logger.LogInformation($"Document IDs count: {request.DocumentIds?.Count ?? 0}");
            if (request.DocumentIds?.Any() == true)
            {
                _logger.LogInformation($"Document IDs: {string.Join(", ", request.DocumentIds)}");
            }

            var response = await _chatService.SendMessageAsync(request, userId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendMessage endpoint");
            // Return a proper error response structure that frontend can handle
            return StatusCode(500, new { error = "Failed to send message", details = ex.Message });
        }
    }

    [HttpPost("stream")]
    public async Task<IActionResult> SendStreamingMessage([FromBody] ChatRequest request)
    {
        try
        {
            // Get authenticated user ID
            var userId = _authService.GetUserIdFromClaims(User);

            Response.Headers["Content-Type"] = "text/plain";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            var responseStream = await _chatService.SendStreamingMessageAsync(request, userId);
            
            await foreach (var chunk in responseStream)
            {
                await Response.WriteAsync(chunk);
                await Response.Body.FlushAsync();
            }

            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendStreamingMessage endpoint");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<ChatMessage>>> GetChatHistory()
    {
        try
        {
            // Get authenticated user ID
            var userId = _authService.GetUserIdFromClaims(User);

            var history = await _chatService.GetChatHistoryAsync(userId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetChatHistory endpoint");
            // Return empty array instead of 500 error to prevent frontend .map() failures
            return Ok(new List<ChatMessage>());
        }
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<List<ConversationSession>>> GetConversationSessions()
    {
        try
        {
            var userId = _authService.GetUserIdFromClaims(User);
            var sessions = await _chatService.GetAllConversationSessionsAsync(userId);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetConversationSessions endpoint");
            // Return empty array instead of 500 error to prevent frontend .map() failures
            return Ok(new List<ConversationSession>());
        }
    }

    // Documented route: POST /api/chat/sessions (create session)
    [HttpPost("sessions")]
    public async Task<ActionResult<ConversationSession>> CreateConversationSession([FromBody] CreateSessionRequest? request)
    {
        try
        {
            var userId = _authService.GetUserIdFromClaims(User);
            var session = await _conversationManager.CreateSessionAsync(userId, request?.Title);
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateConversationSession endpoint");
            return StatusCode(500, new { error = "Failed to create session", details = ex.Message });
        }
    }

    [HttpGet("history/{sessionId}")]
    public async Task<ActionResult<List<ChatMessage>>> GetConversationHistory(string sessionId)
    {
        try
        {
            var userId = _authService.GetUserIdFromClaims(User);
            var history = await _chatService.GetConversationHistoryBySessionAsync(userId, sessionId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetConversationHistory endpoint");
            // Return empty array instead of 500 error to prevent frontend .map() failures
            return Ok(new List<ChatMessage>());
        }
    }

    // Documented route alias: GET /api/chat/sessions/{sessionId}/messages
    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<ActionResult<List<ChatMessage>>> GetConversationMessagesAlias(string sessionId)
    {
        return await GetConversationHistory(sessionId);
    }

    [HttpDelete("history")]
    public async Task<IActionResult> ClearChatHistory()
    {
        try
        {
            // Get authenticated user ID
            var userId = _authService.GetUserIdFromClaims(User);

            var success = await _chatService.ClearChatHistoryAsync(userId);
            if (success)
            {
                return Ok(new { message = "Chat history cleared successfully" });
            }

            return NotFound("No chat history found for user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ClearChatHistory endpoint");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteConversationSession(string sessionId)
    {
        try
        {
            // First delete all documents associated with this conversation
            var userId = _authService.GetUserIdFromClaims(User);
            await _documentService.DeleteConversationDocumentsAsync(userId, sessionId);
            _logger.LogInformation("Deleted documents for conversation {SessionId}", sessionId);
            
            // Then delete the conversation session
            var success = await _chatService.DeleteConversationSessionAsync(userId, sessionId);
            if (success)
            {
                return Ok(new { message = "Conversation session and associated documents deleted successfully" });
            }
            return NotFound("Conversation session not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteConversationSession endpoint");
            return StatusCode(500, "Internal server error");
        }
    }
}
