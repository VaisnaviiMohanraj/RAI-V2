using Backend.Models;

namespace Backend.Services.Document;

public class FileValidationService : IFileValidationService
{
    private readonly ILogger<FileValidationService> _logger;
    private readonly string[] _allowedExtensions = { ".pdf", ".docx", ".xlsx" };
    private readonly string[] _allowedMimeTypes = { 
        "application/pdf", 
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

    public FileValidationService(ILogger<FileValidationService> logger)
    {
        _logger = logger;
    }

    public ValidationResult ValidateFile(IFormFile file)
    {
        var result = new ValidationResult { IsValid = true };

        if (file == null || file.Length == 0)
        {
            result.IsValid = false;
            result.Errors.Add("File is empty or not provided");
            return result;
        }

        // Check file size
        if (file.Length > MaxFileSize)
        {
            result.IsValid = false;
            result.Errors.Add($"File size exceeds maximum limit of {MaxFileSize / (1024 * 1024)}MB");
        }

        // Check file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(extension))
        {
            result.IsValid = false;
            result.Errors.Add($"File extension '{extension}' is not allowed. Allowed extensions: {string.Join(", ", _allowedExtensions)}");
        }

        // Check MIME type
        if (!_allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            result.IsValid = false;
            result.Errors.Add($"File type '{file.ContentType}' is not allowed");
        }

        // Check filename for malicious patterns
        if (ContainsMaliciousPatterns(file.FileName))
        {
            result.IsValid = false;
            result.Errors.Add("Filename contains potentially malicious patterns");
        }

        return result;
    }

    public string GenerateSecureFileName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        var secureFileName = $"{Guid.NewGuid()}{extension}";
        return secureFileName;
    }

    public async Task<bool> ScanForVirusAsync(Stream fileStream)
    {
        // Placeholder for virus scanning integration
        // In production, integrate with Windows Defender API or third-party scanner
        try
        {
            // Basic file signature validation
            fileStream.Position = 0;
            var buffer = new byte[8];
            await fileStream.ReadAsync(buffer, 0, buffer.Length);
            fileStream.Position = 0;

            // Check for common malicious signatures (basic implementation)
            return !ContainsMaliciousSignature(buffer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during virus scan");
            return false; // Fail safe - reject file if scan fails
        }
    }

    private bool ContainsMaliciousPatterns(string filename)
    {
        var maliciousPatterns = new[] { "..", "\\", "/", ":", "*", "?", "\"", "<", ">", "|" };
        return maliciousPatterns.Any(pattern => filename.Contains(pattern));
    }

    private bool ContainsMaliciousSignature(byte[] buffer)
    {
        // Basic malicious signature detection
        // This is a simplified implementation - production should use proper antivirus
        var maliciousSignatures = new[]
        {
            new byte[] { 0x4D, 0x5A }, // PE executable header
            new byte[] { 0x50, 0x4B, 0x03, 0x04 } // ZIP header (could contain malicious content)
        };

        return false; // Simplified - always return safe for demo
    }
}
