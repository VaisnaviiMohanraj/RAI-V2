using Backend.Models;
using System.Text.Json;
using System.Linq;
using System.IO;

namespace Backend.Services.Chat;

public interface IConversationManager
{
    Task<List<ConversationSession>> GetAllConversationSessionsAsync(string userId);
    Task<List<ChatMessage>> GetConversationHistoryBySessionAsync(string userId, string sessionId);
    Task<bool> DeleteConversationSessionAsync(string userId, string sessionId);
    string GenerateTitle(string firstMessage);
    Task<ConversationSession> CreateSessionAsync(string userId, string? title = null);
}

public class ConversationManager : IConversationManager
{
    private readonly ILogger<ConversationManager> _logger;
    private readonly IAzureFunctionService _azureFunctionService;

    public ConversationManager(
        ILogger<ConversationManager> logger,
        IAzureFunctionService azureFunctionService)
    {
        _logger = logger;
        _azureFunctionService = azureFunctionService;
    }

    public async Task<List<ConversationSession>> GetAllConversationSessionsAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Getting all conversation sessions for user: {UserId}", userId);

            var sessions = await _azureFunctionService.GetAllConversationSessionsAsync(userId);
            if (sessions.Any())
            {
                _logger.LogInformation("Returning {Count} sessions from Azure Function", sessions.Count);
                return sessions.OrderByDescending(s => s.LastMessageTime).ToList();
            }

            _logger.LogInformation("No sessions returned from Azure Function, falling back to local storage");

            return await LoadSessionsFromLocalStorageAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation sessions for user: {UserId}", userId);
            return await LoadSessionsFromLocalStorageAsync(userId);
        }
    }

    public async Task<List<ChatMessage>> GetConversationHistoryBySessionAsync(string userId, string sessionId)
    {
        try
        {
            var conversations = await _azureFunctionService.GetConversationHistoryAsync(userId, sessionId);

            if (conversations.Any())
            {
                return conversations.Select(c => new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Content = c.Content,
                    Role = c.Role,
                    Timestamp = c.Timestamp,
                    UserId = c.UserId
                }).ToList();
            }

            _logger.LogInformation("Azure Function returned no messages for session {SessionId}, using local fallback", sessionId);
            return await LoadConversationFromLocalStorageAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversation history for session: {SessionId}", sessionId);
            return await LoadConversationFromLocalStorageAsync(sessionId);
        }
    }

    public async Task<bool> DeleteConversationSessionAsync(string userId, string sessionId)
    {
        try
        {
            var deleted = await _azureFunctionService.DeleteConversationSessionAsync(userId, sessionId);
            if (deleted)
            {
                return true;
            }

            _logger.LogWarning("Azure Function failed to delete session {SessionId}, attempting local cleanup", sessionId);
            return await DeleteSessionFromLocalStorageAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete conversation session: {SessionId}", sessionId);
            return await DeleteSessionFromLocalStorageAsync(sessionId);
        }
    }

    public string GenerateTitle(string firstMessage)
    {
        if (string.IsNullOrWhiteSpace(firstMessage))
            return "New Chat";
        
        // Take first 50 characters and add ellipsis if longer
        return firstMessage.Length > 50 
            ? firstMessage.Substring(0, 50) + "..." 
            : firstMessage;
    }

    public async Task<ConversationSession> CreateSessionAsync(string userId, string? title = null)
    {
        try
        {
            // Create a new conversation session ID matching existing conventions
            var conversationId = $"conv_{userId}_{DateTime.UtcNow:yyyyMMddHHmmssffff}";

            var storageDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ConversationStorage");
            if (!Directory.Exists(storageDirectory))
            {
                Directory.CreateDirectory(storageDirectory);
            }

            // Initialize an empty conversation file so session discovery works
            var conversationFilePath = Path.Combine(storageDirectory, $"conversation_{conversationId}.json");
            if (!File.Exists(conversationFilePath))
            {
                await File.WriteAllTextAsync(conversationFilePath, "[]");
            }

            var session = new ConversationSession
            {
                Id = conversationId,
                ConversationId = conversationId,
                Title = string.IsNullOrWhiteSpace(title) ? "New Conversation" : title!,
                LastMessageTime = DateTime.UtcNow,
                MessageCount = 0,
                LastMessage = string.Empty
            };

            _logger.LogInformation("Created new conversation session {ConversationId} for user {UserId}", conversationId, userId);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create conversation session for user {UserId}", userId);
            // Return a minimal session even on failure to allow UI flow; caller can handle
            return new ConversationSession
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = string.Empty,
                Title = string.IsNullOrWhiteSpace(title) ? "New Conversation" : title!,
                LastMessageTime = DateTime.UtcNow,
                MessageCount = 0,
                LastMessage = string.Empty
            };
        }
    }

    private async Task<List<ConversationSession>> LoadSessionsFromLocalStorageAsync(string userId)
    {
        var sessions = new List<ConversationSession>();
        var storageDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ConversationStorage");

        if (!Directory.Exists(storageDirectory))
        {
            return sessions;
        }

        var conversationFiles = Directory.GetFiles(storageDirectory, "conversation_*.json");

        foreach (var filePath in conversationFiles)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var conversationId = fileName.Replace("conversation_", "");

                if (!conversationId.StartsWith("conv_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var conversations = JsonSerializer.Deserialize<List<ConversationEntry>>(json) ?? new List<ConversationEntry>();

                if (!conversations.Any())
                {
                    continue;
                }

                if (!conversations.Any(c => string.Equals(c.UserId, userId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var lastMessage = conversations.OrderByDescending(c => c.Timestamp).FirstOrDefault();
                var firstUserMessage = conversations.FirstOrDefault(c => c.Role == "user");

                sessions.Add(new ConversationSession
                {
                    Id = conversationId,
                    ConversationId = conversationId,
                    Title = GenerateTitle(firstUserMessage?.Content ?? "New Chat"),
                    LastMessageTime = lastMessage?.Timestamp ?? DateTime.UtcNow,
                    MessageCount = conversations.Count,
                    LastMessage = lastMessage?.Content ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process conversation file: {FilePath}", filePath);
            }
        }

        return sessions.OrderByDescending(s => s.LastMessageTime).ToList();
    }

    private async Task<List<ChatMessage>> LoadConversationFromLocalStorageAsync(string sessionId)
    {
        var storageDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ConversationStorage");
        var conversationFilePath = Path.Combine(storageDirectory, $"conversation_{sessionId}.json");

        if (!File.Exists(conversationFilePath))
        {
            return new List<ChatMessage>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(conversationFilePath);
            var conversations = JsonSerializer.Deserialize<List<ConversationEntry>>(json) ?? new List<ConversationEntry>();

            return conversations.Select(c => new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Content = c.Content,
                Role = c.Role,
                Timestamp = c.Timestamp,
                UserId = c.UserId
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load conversation from local storage for session {SessionId}", sessionId);
            return new List<ChatMessage>();
        }
    }

    private Task<bool> DeleteSessionFromLocalStorageAsync(string sessionId)
    {
        var storageDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ConversationStorage");
        var conversationFilePath = Path.Combine(storageDirectory, $"conversation_{sessionId}.json");

        try
        {
            if (File.Exists(conversationFilePath))
            {
                File.Delete(conversationFilePath);
                _logger.LogInformation("Deleted local conversation session file: {SessionId}", sessionId);
                return Task.FromResult(true);
            }

            _logger.LogDebug("Local conversation file not found for session {SessionId}", sessionId);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete local conversation session file: {SessionId}", sessionId);
            return Task.FromResult(false);
        }
    }
}
