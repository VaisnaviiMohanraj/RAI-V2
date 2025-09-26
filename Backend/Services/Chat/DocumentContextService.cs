using Backend.Models;
using Backend.Services.Document;

namespace Backend.Services.Chat;

public interface IDocumentContextService
{
    Task<string> GetDocumentContextAsync(List<string>? documentIds, string userId);
    Task<string> FilterDeletedDocumentContextAsync(string originalContent, List<string>? documentIds, string userId);
}

public class DocumentContextService : IDocumentContextService
{
    private readonly ILogger<DocumentContextService> _logger;
    private readonly IDocumentService _documentService;

    public DocumentContextService(
        ILogger<DocumentContextService> logger,
        IDocumentService documentService)
    {
        _logger = logger;
        _documentService = documentService;
    }

    public async Task<string> GetDocumentContextAsync(List<string>? documentIds, string userId)
    {
        if (documentIds == null || !documentIds.Any())
        {
            _logger.LogInformation("No document IDs provided for context");
            return string.Empty;
        }

        _logger.LogInformation($"Getting document context for {documentIds.Count} documents");
        var contextBuilder = new List<string>();
        
        foreach (var docId in documentIds)
        {
            _logger.LogInformation($"Retrieving document: {docId}");
            var document = await _documentService.GetDocumentAsync(docId, userId);
            if (document != null)
            {
                _logger.LogInformation($"Document found: {document.OriginalFileName}, Chunks: {document.Chunks?.Count ?? 0}");
                // Use first few chunks for context
                var relevantChunks = document.Chunks?.Take(3) ?? new List<DocumentChunk>();
                foreach (var chunk in relevantChunks)
                {
                    contextBuilder.Add($"From {document.OriginalFileName}: {chunk.Content}");
                }
            }
            else
            {
                _logger.LogWarning($"Document not found: {docId}");
            }
        }

        var context = string.Join("\n\n", contextBuilder);
        _logger.LogInformation($"Document context length: {context.Length} characters");
        return context;
    }

    public async Task<string> FilterDeletedDocumentContextAsync(string originalContent, List<string>? documentIds, string userId)
    {
        if (documentIds == null || !documentIds.Any())
        {
            return originalContent;
        }

        var validDocIds = new List<string>();

        // Check which documents still exist
        foreach (var docId in documentIds)
        {
            var document = await _documentService.GetDocumentAsync(docId, userId);
            if (document != null)
            {
                validDocIds.Add(docId);
            }
            else
            {
                _logger.LogInformation("Document {DocumentId} no longer exists, filtering from chat history", docId);
            }
        }

        // If no documents exist anymore, remove document context entirely
        if (!validDocIds.Any())
        {
            return ExtractUserQuestionFromContent(originalContent);
        }

        // If some documents still exist, rebuild context with only valid documents
        var newDocumentContext = await GetDocumentContextAsync(validDocIds, userId);
        if (!string.IsNullOrEmpty(newDocumentContext))
        {
            var userQuestion = ExtractUserQuestionFromContent(originalContent);
            _logger.LogInformation("Rebuilt document context with {ValidCount} valid documents out of {OriginalCount}", 
                validDocIds.Count, documentIds.Count);
            return $"Document context:\n{newDocumentContext}\n\nUser question: {userQuestion}";
        }

        return originalContent;
    }

    private string ExtractUserQuestionFromContent(string originalContent)
    {
        // Extract just the user question part
        var userQuestionMatch = System.Text.RegularExpressions.Regex.Match(
            originalContent, @"User question:\s*(.+)$", System.Text.RegularExpressions.RegexOptions.Singleline);
        
        if (userQuestionMatch.Success)
        {
            _logger.LogInformation("Removed all document context from chat history message");
            return userQuestionMatch.Groups[1].Value.Trim();
        }
        
        // Fallback: return content without document context
        var cleanContent = originalContent.Split(new[] { "User question:" }, StringSplitOptions.None).LastOrDefault()?.Trim() ?? originalContent;
        
        // Also try to remove "Document context:" sections
        if (cleanContent.Contains("Document context:"))
        {
            var parts = cleanContent.Split(new[] { "Document context:" }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                // Find the actual user message after document context
                var afterContext = parts[1];
                var userMsgIndex = afterContext.IndexOf("User question:");
                if (userMsgIndex >= 0)
                {
                    cleanContent = afterContext.Substring(userMsgIndex + "User question:".Length).Trim();
                }
            }
        }
        
        return cleanContent;
    }
}
