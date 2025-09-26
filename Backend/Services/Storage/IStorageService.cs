using Backend.Models;

namespace Backend.Services.Storage;

/// <summary>
/// Interface for Azure Storage operations
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Upload a document to Azure Storage
    /// </summary>
    /// <param name="fileName">Name of the file</param>
    /// <param name="content">File content as byte array</param>
    /// <param name="contentType">MIME type of the file</param>
    /// <returns>URL of the uploaded file</returns>
    Task<string> UploadDocumentAsync(string fileName, byte[] content, string contentType);

    /// <summary>
    /// Download a document from Azure Storage
    /// </summary>
    /// <param name="fileName">Name of the file to download</param>
    /// <returns>File content as byte array</returns>
    Task<byte[]> DownloadDocumentAsync(string fileName);

    /// <summary>
    /// Delete a document from Azure Storage
    /// </summary>
    /// <param name="fileName">Name of the file to delete</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteDocumentAsync(string fileName);

    /// <summary>
    /// List all documents for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>List of document metadata</returns>
    Task<List<DocumentModel>> ListUserDocumentsAsync(string userId);

    /// <summary>
    /// Check if a document exists
    /// </summary>
    /// <param name="fileName">Name of the file to check</param>
    /// <returns>True if the document exists</returns>
    Task<bool> DocumentExistsAsync(string fileName);

    /// <summary>
    /// Get the URL for a document
    /// </summary>
    /// <param name="fileName">Name of the file</param>
    /// <returns>URL of the document</returns>
    string GetDocumentUrl(string fileName);
}
