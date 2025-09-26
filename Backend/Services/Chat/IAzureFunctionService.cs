using Backend.Models;

namespace Backend.Services.Chat;

public interface IAzureFunctionService
{
    Task<bool> SaveConversationAsync(string userId, string userMessage, string assistantResponse, string? conversationId = null);
    Task<List<ConversationEntry>> GetConversationHistoryAsync(string userId, string? conversationId = null);
    Task<List<ConversationSession>> GetAllConversationSessionsAsync(string userId);
    Task<bool> ClearConversationHistoryAsync(string userId);
    Task<bool> DeleteConversationSessionAsync(string userId, string conversationId);
}

public class ConversationEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
