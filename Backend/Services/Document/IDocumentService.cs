using Backend.Models;

namespace Backend.Services.Document;

public interface IDocumentService
{
    Task<FileUploadResponse> UploadDocumentAsync(IFormFile file, string userId, string? conversationId = null);
    Task<DocumentModel?> GetDocumentAsync(string documentId, string userId);
    Task<List<DocumentModel>> GetUserDocumentsAsync(string userId);
    Task<List<DocumentModel>> GetConversationDocumentsAsync(string userId, string conversationId);
    Task<bool> DeleteDocumentAsync(string documentId, string userId);
    Task<bool> DeleteConversationDocumentsAsync(string userId, string conversationId);
    Task<bool> ClearLegacyDocumentsAsync(string userId);
    Task<string> ExtractTextFromDocumentAsync(string filePath, string contentType);
    Task<List<DocumentChunk>> ChunkDocumentAsync(string text, string documentId);
}

public interface IFileValidationService
{
    ValidationResult ValidateFile(IFormFile file);
    string GenerateSecureFileName(string originalFileName);
    Task<bool> ScanForVirusAsync(Stream fileStream);
}
