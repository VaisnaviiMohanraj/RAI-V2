using Backend.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using System.Text;

namespace Backend.Services.Document;

public class DocumentService : IDocumentService
{
    private readonly ILogger<DocumentService> _logger;
    private readonly IFileValidationService _fileValidationService;
    private readonly string _secureUploadPath;
    private static readonly Dictionary<string, DocumentModel> _staticDocuments = new();
    private static readonly object _lockObject = new object();

    public DocumentService(ILogger<DocumentService> logger, IFileValidationService fileValidationService)
    {
        _logger = logger;
        _fileValidationService = fileValidationService;
        _secureUploadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RRRealtyAI", "SecureUploads");
        
        Directory.CreateDirectory(_secureUploadPath);
        
        _logger.LogInformation("DocumentService instance created. Hash: {Hash}", this.GetHashCode());
    }

    public async Task<FileUploadResponse> UploadDocumentAsync(IFormFile file, string userId, string? conversationId = null)
    {
        try
        {
            _logger.LogInformation("Starting document upload for user: {UserId}, file: {FileName}", userId, file.FileName);
            
            var validationResult = _fileValidationService.ValidateFile(file);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("File validation failed for {FileName}: {Errors}", file.FileName, string.Join(", ", validationResult.Errors));
                return new FileUploadResponse
                {
                    Success = false,
                    ErrorMessage = string.Join(", ", validationResult.Errors)
                };
            }

            _logger.LogInformation("File validation passed for {FileName}", file.FileName);

            var secureFileName = _fileValidationService.GenerateSecureFileName(file.FileName);
            var filePath = Path.Combine(_secureUploadPath, secureFileName);

            _logger.LogInformation("Saving file to: {FilePath}", filePath);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("File saved successfully, extracting text content");
            
            // Extract actual text content from the document
            var extractedText = await ExtractTextFromDocumentAsync(filePath, file.ContentType);
            _logger.LogInformation("Text extraction completed. Length: {TextLength} characters", extractedText.Length);
            
            var document = new DocumentModel
            {
                FileName = secureFileName,
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                UserId = userId,
                ConversationId = conversationId ?? string.Empty,
                StoragePath = filePath,
                ExtractedText = extractedText
            };

            _logger.LogInformation("Document model created with ID: {DocumentId}", document.Id);

            // Create document chunks for better AI processing
            if (!string.IsNullOrEmpty(extractedText))
            {
                document.Chunks = await ChunkDocumentAsync(extractedText, document.Id);
                _logger.LogInformation("Created {ChunkCount} chunks for document processing", document.Chunks.Count);
            }
            else
            {
                document.Chunks = new List<DocumentChunk>();
                _logger.LogWarning("No text extracted from document, creating empty chunks list");
            }
            
            _logger.LogInformation("Storing document in dictionary");
            
            lock (_lockObject)
            {
                _staticDocuments[document.Id] = document;
                
                _logger.LogInformation("Document stored. Static count: {StaticCount}", 
                    _staticDocuments.Count);
            }
            
            _logger.LogInformation("Document uploaded successfully. ID: {DocumentId}, User: {UserId}", 
                document.Id, userId);

            return new FileUploadResponse
            {
                DocumentId = document.Id,
                FileName = document.OriginalFileName,
                FileSize = document.FileSize,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document for user: {UserId}, file: {FileName}", userId, file?.FileName ?? "unknown");
            return new FileUploadResponse
            {
                Success = false,
                ErrorMessage = "An error occurred while uploading the document"
            };
        }
    }

    public async Task<DocumentModel?> GetDocumentAsync(string documentId, string userId)
    {
        lock (_lockObject)
        {
            if (_staticDocuments.TryGetValue(documentId, out var document) && document.UserId == userId)
            {
                return document;
            }
        }
        return null;
    }

    public async Task<List<DocumentModel>> GetUserDocumentsAsync(string userId)
    {
        _logger.LogInformation("Retrieving documents for user: {UserId}, Total documents in memory: {Count}", userId, _staticDocuments.Count);
        
        List<DocumentModel> userDocuments;
        lock (_lockObject)
        {
            userDocuments = _staticDocuments.Values.Where(d => d.UserId == userId).ToList();
        }
        
        _logger.LogInformation("Found {UserDocumentCount} documents for user: {UserId}", userDocuments.Count, userId);
        
        return userDocuments;
    }

    public async Task<List<DocumentModel>> GetConversationDocumentsAsync(string userId, string conversationId)
    {
        _logger.LogInformation("Retrieving documents for user: {UserId}, conversation: {ConversationId}", userId, conversationId);
        
        List<DocumentModel> conversationDocuments;
        lock (_lockObject)
        {
            // Debug: Log all documents in storage
            _logger.LogInformation("Total documents in storage: {Count}", _staticDocuments.Count);
            foreach (var doc in _staticDocuments.Values)
            {
                _logger.LogInformation("Document {Id}: UserId={UserId}, ConversationId='{ConversationId}', FileName={FileName}", 
                    doc.Id, doc.UserId, doc.ConversationId, doc.OriginalFileName);
            }
            
            conversationDocuments = _staticDocuments.Values
                .Where(d => d.UserId == userId && 
                           !string.IsNullOrEmpty(d.ConversationId) && 
                           d.ConversationId == conversationId)
                .ToList();
        }
        
        _logger.LogInformation("Found {DocumentCount} documents for conversation: {ConversationId}", conversationDocuments.Count, conversationId);
        
        return conversationDocuments;
    }

    public async Task<bool> DeleteDocumentAsync(string documentId, string userId)
    {
        lock (_lockObject)
        {
            if (_staticDocuments.TryGetValue(documentId, out var document) && document.UserId == userId)
            {
                try
                {
                    _logger.LogInformation("Deleting document {DocumentId} for user {UserId}", documentId, userId);
                    
                    if (File.Exists(document.StoragePath))
                    {
                        File.Delete(document.StoragePath);
                        _logger.LogInformation("Deleted file at path: {StoragePath}", document.StoragePath);
                    }
                    
                    var removed = _staticDocuments.Remove(documentId);
                    _logger.LogInformation("Removed from static documents: {Removed}", removed);
                    
                    _logger.LogInformation("Document {DocumentId} successfully deleted. Remaining documents: {Count}", 
                        documentId, _staticDocuments.Count);
                    
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting document {DocumentId}", documentId);
                }
            }
            else
            {
                _logger.LogWarning("Document {DocumentId} not found or user {UserId} not authorized", documentId, userId);
            }
        }
        return false;
    }

    public async Task<bool> DeleteConversationDocumentsAsync(string userId, string conversationId)
    {
        _logger.LogInformation("Deleting all documents for conversation: {ConversationId}, user: {UserId}", conversationId, userId);
        
        List<string> documentsToDelete;
        lock (_lockObject)
        {
            documentsToDelete = _staticDocuments.Values
                .Where(d => d.UserId == userId && d.ConversationId == conversationId)
                .Select(d => d.Id)
                .ToList();
        }
        
        _logger.LogInformation("Found {Count} documents to delete for conversation {ConversationId}", documentsToDelete.Count, conversationId);
        
        bool allDeleted = true;
        foreach (var documentId in documentsToDelete)
        {
            var deleted = await DeleteDocumentAsync(documentId, userId);
            if (!deleted)
            {
                allDeleted = false;
                _logger.LogWarning("Failed to delete document {DocumentId} for conversation {ConversationId}", documentId, conversationId);
            }
        }
        
        _logger.LogInformation("Conversation cleanup completed. All documents deleted: {AllDeleted}", allDeleted);
        return allDeleted;
    }

    public async Task<bool> ClearLegacyDocumentsAsync(string userId)
    {
        _logger.LogInformation("Clearing legacy documents with empty conversationId for user: {UserId}", userId);
        
        List<string> legacyDocuments;
        lock (_lockObject)
        {
            legacyDocuments = _staticDocuments.Values
                .Where(d => d.UserId == userId && string.IsNullOrEmpty(d.ConversationId))
                .Select(d => d.Id)
                .ToList();
        }
        
        _logger.LogInformation("Found {Count} legacy documents to delete", legacyDocuments.Count);
        
        bool allDeleted = true;
        foreach (var documentId in legacyDocuments)
        {
            var deleted = await DeleteDocumentAsync(documentId, userId);
            if (!deleted)
            {
                allDeleted = false;
                _logger.LogWarning("Failed to delete legacy document {DocumentId}", documentId);
            }
        }
        
        _logger.LogInformation("Legacy document cleanup completed. All deleted: {AllDeleted}", allDeleted);
        return allDeleted;
    }

    public async Task<string> ExtractTextFromDocumentAsync(string filePath, string contentType)
    {
        try
        {
            _logger.LogInformation("Extracting text from document: {FilePath}, ContentType: {ContentType}", filePath, contentType);
            
            var extractedText = contentType.ToLower() switch
            {
                "application/pdf" => await ExtractPdfTextAsync(filePath),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => await ExtractDocxTextAsync(filePath),
                _ => string.Empty
            };
            
            _logger.LogInformation("Text extraction completed. Length: {TextLength}", extractedText.Length);
            return extractedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from document: {FilePath}", filePath);
            return string.Empty;
        }
    }

    public async Task<List<DocumentChunk>> ChunkDocumentAsync(string text, string documentId)
    {
        const int chunkSize = 1000;
        const int overlap = 200;
        var chunks = new List<DocumentChunk>();
        
        if (string.IsNullOrEmpty(text))
            return chunks;

        for (int i = 0; i < text.Length; i += chunkSize - overlap)
        {
            var endPos = Math.Min(i + chunkSize, text.Length);
            var chunkContent = text.Substring(i, endPos - i);
            
            chunks.Add(new DocumentChunk
            {
                DocumentId = documentId,
                Content = chunkContent,
                ChunkIndex = chunks.Count,
                StartPosition = i,
                EndPosition = endPos
            });
        }

        return chunks;
    }

    private async Task<string> ExtractPdfTextAsync(string filePath)
    {
        try
        {
            _logger.LogInformation("Starting PDF text extraction for: {FilePath}", filePath);
            var text = new StringBuilder();
            using var document = PdfDocument.Open(filePath);
            
            foreach (Page page in document.GetPages())
            {
                text.Append(page.Text);
            }
            
            var result = text.ToString();
            _logger.LogInformation("PDF text extraction completed. Length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting PDF text from: {FilePath}", filePath);
            throw; // Re-throw to let the caller handle it
        }
    }

    private async Task<string> ExtractDocxTextAsync(string filePath)
    {
        var text = new StringBuilder();
        using var doc = WordprocessingDocument.Open(filePath, false);
        
        if (doc.MainDocumentPart?.Document.Body != null)
        {
            foreach (var paragraph in doc.MainDocumentPart.Document.Body.Elements<Paragraph>())
            {
                text.AppendLine(paragraph.InnerText);
            }
        }
        
        return text.ToString();
    }
}
