namespace Backend.Configuration;

/// <summary>
/// Configuration settings for Azure Storage
/// </summary>
public class StorageConfig
{
    public const string SectionName = "Azure:Storage";

    /// <summary>
    /// Azure Storage connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Container name for document storage
    /// </summary>
    public string ContainerName { get; set; } = "documents";

    /// <summary>
    /// Container name for conversation storage
    /// </summary>
    public string ConversationContainerName { get; set; } = "conversations";

    /// <summary>
    /// Maximum file size in bytes (default: 10MB)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Allowed file extensions
    /// </summary>
    public string[] AllowedExtensions { get; set; } = { ".pdf", ".docx", ".doc", ".txt" };

    /// <summary>
    /// Base URL for blob access (optional, for custom domains)
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Enable public access to blobs
    /// </summary>
    public bool EnablePublicAccess { get; set; } = false;

    /// <summary>
    /// Blob access tier (Hot, Cool, Archive)
    /// </summary>
    public string AccessTier { get; set; } = "Hot";
}
