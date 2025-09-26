using Backend.Models;

namespace Backend.Services.Chat;

public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request, string userId);
    Task<IAsyncEnumerable<string>> SendStreamingMessageAsync(ChatRequest request, string userId);
    Task<List<ChatMessage>> GetChatHistoryAsync(string userId);
    Task<bool> ClearChatHistoryAsync(string userId);
    Task<List<ConversationSession>> GetAllConversationSessionsAsync(string userId);
    Task<List<ChatMessage>> GetConversationHistoryBySessionAsync(string userId, string sessionId);
    Task<bool> DeleteConversationSessionAsync(string userId, string sessionId);
}
