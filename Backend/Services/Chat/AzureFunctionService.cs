using Backend.Models;
using System.Text.Json;
using System.Text;
using System.Linq;

namespace Backend.Services.Chat;

public class AzureFunctionService : IAzureFunctionService
{
    private readonly ILogger<AzureFunctionService> _logger;
    private readonly HttpClient _httpClient;
    
    // Azure Function configuration
    private readonly string _azureFunctionUrl;

    public AzureFunctionService(ILogger<AzureFunctionService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        
        // Get Azure Function URL from environment variables
        _azureFunctionUrl = Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL") ?? "https://fn-conversationsave.azurewebsites.net";
        
        _logger.LogInformation("Azure Function URL configured: {Url}", _azureFunctionUrl);
    }

    public async Task<bool> SaveConversationAsync(string userId, string userMessage, string assistantResponse, string? conversationId = null)
    {
        try
        {
            _logger.LogInformation("Saving conversation for user: {UserId}, conversationId: {ConversationId}", userId, conversationId);

            // Save to Azure Function for SQL audit logging
            bool success = await SaveToAzureFunctionAsync(userId, userMessage, assistantResponse, conversationId);

            _logger.LogInformation("Conversation save result - Azure Function: {Success}", success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save conversation for user: {UserId}, conversationId: {ConversationId}", userId, conversationId);
            return false;
        }
    }

    private async Task<bool> SaveToAzureFunctionAsync(string userId, string userMessage, string assistantResponse, string? conversationId)
    {
        try
        {
            var conversationData = new
            {
                UserId = userId,
                ConversationId = conversationId ?? $"conv_{userId}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                UserMessage = userMessage,
                AssistantResponse = assistantResponse,
                Timestamp = DateTime.UtcNow,
                Source = "RR-Realty-AI"
            };

            var json = JsonSerializer.Serialize(conversationData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling Azure Function: {Url}", _azureFunctionUrl);

            var response = await _httpClient.PostAsync($"{_azureFunctionUrl}/api/SaveConversation", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully saved conversation to Azure Function SQL audit");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Azure Function returned error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Azure Function for conversation audit");
            return false;
        }
    }

    public async Task<List<ConversationEntry>> GetConversationHistoryAsync(string userId, string? conversationId = null)
    {
        try
        {
            _logger.LogInformation("Retrieving conversation history for user: {UserId}", userId);

            // Try to get from Azure Function (SQL database)
            var requestUriBuilder = new StringBuilder($"{_azureFunctionUrl}/api/GetConversations?userId={Uri.EscapeDataString(userId)}");

            if (!string.IsNullOrEmpty(conversationId))
            {
                requestUriBuilder.Append($"&conversationId={Uri.EscapeDataString(conversationId)}");
            }

            var response = await _httpClient.GetAsync(requestUriBuilder.ToString());
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var conversations = JsonSerializer.Deserialize<List<ConversationEntry>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                _logger.LogInformation("Retrieved {Count} conversation entries from Azure Function", conversations?.Count ?? 0);
                return conversations ?? new List<ConversationEntry>();
            }
            else
            {
                _logger.LogWarning("Failed to retrieve conversations from Azure Function: {StatusCode}", response.StatusCode);
                return new List<ConversationEntry>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation history for user: {UserId}", userId);
            return new List<ConversationEntry>();
        }
    }

    public async Task<bool> ClearConversationHistoryAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Clearing conversation history for user: {UserId}", userId);

            var response = await _httpClient.DeleteAsync($"{_azureFunctionUrl}/api/ClearConversations?userId={userId}");
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully cleared conversation history from Azure Function");
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to clear conversations from Azure Function: {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing conversation history for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<List<ConversationSession>> GetAllConversationSessionsAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Retrieving all conversation sessions for user: {UserId}", userId);

            var response = await _httpClient.GetAsync($"{_azureFunctionUrl}/api/GetSessions?userId={userId}");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var sessions = JsonSerializer.Deserialize<List<ConversationSession>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                _logger.LogInformation("Retrieved {Count} conversation sessions from Azure Function", sessions?.Count ?? 0);
                return sessions ?? new List<ConversationSession>();
            }
            else
            {
                _logger.LogWarning("Failed to retrieve sessions from Azure Function: {StatusCode}", response.StatusCode);
                return new List<ConversationSession>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation sessions for user: {UserId}", userId);
            return new List<ConversationSession>();
        }
    }

    public async Task<bool> DeleteConversationSessionAsync(string userId, string conversationId)
    {
        try
        {
            _logger.LogInformation("Deleting conversation session {ConversationId} for user: {UserId}", conversationId, userId);

            var requestUri = $"{_azureFunctionUrl}/api/DeleteConversation?userId={Uri.EscapeDataString(userId)}&conversationId={Uri.EscapeDataString(conversationId)}";
            var response = await _httpClient.DeleteAsync(requestUri);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted conversation session via Azure Function");
                return true;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to delete conversation session: {StatusCode} - {Content}", response.StatusCode, content);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation session {ConversationId} for user {UserId}", conversationId, userId);
            return false;
        }
    }
}
