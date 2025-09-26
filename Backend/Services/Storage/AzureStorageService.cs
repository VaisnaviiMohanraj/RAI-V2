using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Backend.Models;
using System.Text;

namespace Backend.Services.Storage;

/// <summary>
/// Azure Storage service implementation for document storage
/// Handles file upload, download, and management operations
/// </summary>
public class AzureStorageService : IStorageService
{
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly ILogger<AzureStorageService> _logger;
    private readonly string _containerName;
    private BlobContainerClient? _containerClient;

    public AzureStorageService(ILogger<AzureStorageService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _containerName = configuration.GetValue<string>("Azure:Storage:ContainerName") ?? "documents";
        
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") 
                              ?? configuration.GetConnectionString("AzureStorage");

        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("Azure Storage connection string not found. Storage operations will not work.");
            return;
        }

        try
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
            _logger.LogInformation("Azure Storage client initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure Storage client");
        }
    }

    private async Task<BlobContainerClient> GetContainerClientAsync()
    {
        if (_containerClient != null)
            return _containerClient;

        if (_blobServiceClient == null)
            throw new InvalidOperationException("Azure Storage client is not initialized");

        _containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        
        // Ensure container exists
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
        
        return _containerClient;
    }

    public async Task<string> UploadDocumentAsync(string fileName, byte[] content, string contentType)
    {
        try
        {
            var containerClient = await GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(fileName);

            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            };

            using var stream = new MemoryStream(content);
            await blobClient.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = blobHttpHeaders,
                Conditions = null // Allow overwrite
            });

            _logger.LogInformation("Document uploaded successfully: {FileName}", fileName);
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document: {FileName}", fileName);
            throw;
        }
    }

    public async Task<byte[]> DownloadDocumentAsync(string fileName)
    {
        try
        {
            var containerClient = await GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(fileName);

            if (!await blobClient.ExistsAsync())
            {
                throw new FileNotFoundException($"Document not found: {fileName}");
            }

            var response = await blobClient.DownloadContentAsync();
            _logger.LogInformation("Document downloaded successfully: {FileName}", fileName);
            
            return response.Value.Content.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download document: {FileName}", fileName);
            throw;
        }
    }

    public async Task<bool> DeleteDocumentAsync(string fileName)
    {
        try
        {
            var containerClient = await GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(fileName);

            var response = await blobClient.DeleteIfExistsAsync();
            
            if (response.Value)
            {
                _logger.LogInformation("Document deleted successfully: {FileName}", fileName);
            }
            else
            {
                _logger.LogWarning("Document not found for deletion: {FileName}", fileName);
            }

            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document: {FileName}", fileName);
            throw;
        }
    }

    public async Task<List<DocumentModel>> ListUserDocumentsAsync(string userId)
    {
        try
        {
            var containerClient = await GetContainerClientAsync();
            var documents = new List<DocumentModel>();

            // List blobs with the user prefix
            var prefix = $"{userId}/";
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
            {
                var document = new DocumentModel
                {
                    Id = Guid.NewGuid().ToString(),
                    FileName = Path.GetFileName(blobItem.Name),
                    StoragePath = blobItem.Name,
                    ContentType = blobItem.Properties.ContentType ?? "application/octet-stream",
                    FileSize = blobItem.Properties.ContentLength ?? 0,
                    UploadDate = blobItem.Properties.CreatedOn?.DateTime ?? DateTime.UtcNow,
                    UserId = userId
                };

                documents.Add(document);
            }

            _logger.LogInformation("Listed {Count} documents for user: {UserId}", documents.Count, userId);
            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list documents for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> DocumentExistsAsync(string fileName)
    {
        try
        {
            var containerClient = await GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(fileName);

            var response = await blobClient.ExistsAsync();
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check document existence: {FileName}", fileName);
            return false;
        }
    }

    public string GetDocumentUrl(string fileName)
    {
        if (_blobServiceClient == null)
            throw new InvalidOperationException("Azure Storage client is not initialized");

        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(fileName);
        
        return blobClient.Uri.ToString();
    }
}
