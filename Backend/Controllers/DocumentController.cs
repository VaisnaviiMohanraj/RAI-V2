using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend.Services.Document;
using Backend.Services.Auth;
using Backend.Models;
using System.Linq;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireAuthenticatedUser")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IAuthService _authService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(
        IDocumentService documentService,
        IAuthService authService,
        ILogger<DocumentController> logger)
    {
        _documentService = documentService;
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<FileUploadResponse>> UploadDocument([FromForm] FileUploadRequest request)
    {
        try
        {
            // Get authenticated user ID
            var userId = _authService.GetUserIdFromClaims(User);
            var conversationId = request.ConversationId;
            
            _logger.LogInformation($"Upload request - UserId: {userId}, ConversationId: {conversationId}");

            var response = await _documentService.UploadDocumentAsync(request.File, userId, conversationId);
            
            if (response.Success)
            {
                return Ok(response);
            }

            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UploadDocument endpoint");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{documentId}")]
    public async Task<ActionResult<DocumentModel>> GetDocument(string documentId)
    {
        try
        {
            // Get authenticated user ID
            var actualUserId = _authService.GetUserIdFromClaims(User);

            var document = await _documentService.GetDocumentAsync(documentId, actualUserId);
            if (document == null)
            {
                return NotFound("Document not found");
            }

            return Ok(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetDocument endpoint");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<DocumentModel>>> GetUserDocuments([FromQuery] string? conversationId = null)
    {
        try
        {
            // Get authenticated user ID
            var actualUserId = _authService.GetUserIdFromClaims(User);

            // Clear legacy documents first to prevent cross-contamination
            await _documentService.ClearLegacyDocumentsAsync(actualUserId);

            List<DocumentModel> documents;
            if (!string.IsNullOrEmpty(conversationId))
            {
                _logger.LogInformation("Getting documents for conversation: {ConversationId}", conversationId);
                documents = await _documentService.GetConversationDocumentsAsync(actualUserId, conversationId);
                _logger.LogInformation("Found {Count} documents for conversation {ConversationId}: {DocumentIds}", 
                    documents.Count, conversationId, string.Join(", ", documents.Select(d => $"{d.Id}({d.OriginalFileName})")));
            }
            else
            {
                _logger.LogInformation("Getting all documents for user: {UserId} - THIS SHOULD NOT HAPPEN FOR CONVERSATION-SPECIFIC REQUESTS", actualUserId);
                documents = await _documentService.GetUserDocumentsAsync(actualUserId);
                _logger.LogInformation("Found {Count} total documents for user {UserId}: {DocumentIds}", 
                    documents.Count, actualUserId, string.Join(", ", documents.Select(d => $"{d.Id}({d.OriginalFileName})")));
            }
            
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUserDocuments endpoint");
            // Return empty array instead of 500 error to prevent frontend .map() failures
            return Ok(new List<DocumentModel>());
        }
    }

    [HttpDelete("{documentId}")]
    public async Task<IActionResult> DeleteDocument(string documentId)
    {
        try
        {
            // Get authenticated user ID
            var actualUserId = _authService.GetUserIdFromClaims(User);

            var success = await _documentService.DeleteDocumentAsync(documentId, actualUserId);
            if (success)
            {
                return Ok(new { message = "Document deleted successfully" });
            }

            return NotFound("Document not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteDocument endpoint");
            return StatusCode(500, "Internal server error");
        }
    }

}
