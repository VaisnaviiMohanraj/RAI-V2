using Backend.Models;
using System.Text.Json;
using System.Text;
using System.Linq;
using Microsoft.AspNetCore.WebUtilities;

namespace Backend.Services.Chat;

public class AzureFunctionService : IAzureFunctionService
{
    private readonly ILogger<AzureFunctionService> _logger;
    private readonly HttpClient _httpClient;
    
    // Azure Function configuration
    private readonly string _azureFunctionUrl;
    private readonly string _functionApiBase;   // e.g. https://fn-xyz.azurewebsites.net/api/
    private readonly string? _functionCode;     // optional function key from query string or env
    public AzureFunctionService(ILogger<AzureFunctionService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        
        // Get Azure Function URL from environment variables (full URL with auth code)
        _azureFunctionUrl = Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL") ?? "https://fn-conversationsave.azurewebsites.net/api/SaveConversation";

        // Allow overriding via dedicated env vars
        var overrideBase = Environment.GetEnvironmentVariable("AZURE_FUNCTION_BASE_URL");
        var overrideCode = Environment.GetEnvironmentVariable("AZURE_FUNCTION_CODE");

        // Derive API base and code from provided URL if not explicitly set
        try
        {
            var uri = new Uri(_azureFunctionUrl);
            var root = $"{uri.Scheme}://{uri.Host}";
            // Include port if present
            if (!uri.IsDefaultPort)
            {
                root += $":{uri.Port}";
            }

            var path = uri.AbsolutePath; // e.g. /api/SaveConversation
            var apiIndex = path.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
            var apiBasePath = apiIndex >= 0 ? path.Substring(0, apiIndex + 5) : "/api/"; // include trailing '/'

            _functionApiBase = (overrideBase ?? (root + apiBasePath)).TrimEnd('/') + "/"; // ensure single trailing slash

            if (!string.IsNullOrEmpty(overrideCode))
            {
                _functionCode = overrideCode;
            }
            else
            {
                var query = QueryHelpers.ParseQuery(uri.Query);
                _functionCode = query.TryGetValue("code", out var codeVal) ? codeVal.ToString() : null;
            }

            _logger.LogInformation("Azure Function API base: {Base}", _functionApiBase);
            if (!string.IsNullOrEmpty(_functionCode))
            {
                _logger.LogInformation("Azure Function code is configured (hidden)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AZURE_FUNCTION_URL. Falling back to default base '/api/' without code");
            _functionApiBase = "/api/";
            _functionCode = null;
        }

        _logger.LogInformation("Azure Function URL configured (Save endpoint source): {Url}", _azureFunctionUrl.Split('?')[0] + "?code=***");
    }

    private string BuildFunctionUrl(string relativePathAndQuery)
    {
        // relativePathAndQuery should be like "SaveConversation" or "GetConversations?userId=..."
        var sep = relativePathAndQuery.Contains('?') ? "&" : "?";
        if (!string.IsNullOrEmpty(_functionCode))
        {
            return _functionApiBase + relativePathAndQuery + $"{sep}code={_functionCode}";
        }
        return _functionApiBase + relativePathAndQuery;
    }

    public async Task<(bool success, string? functionConversationId)> SaveConversationAsync(string userId, List<ChatMessage> conversationHistory, string? conversationId = null, string? userEmail = null)
    {
        try
        {
            _logger.LogInformation("Saving conversation for user: {UserId}, conversationId: {ConversationId}, messageCount: {MessageCount}", 
                userId, conversationId, conversationHistory?.Count ?? 0);

            // Save to Azure Function for SQL audit logging
            var (success, returnedGuid) = await SaveToAzureFunctionAsync(userId, conversationHistory, conversationId, userEmail);

            _logger.LogInformation("Conversation save result - Azure Function: {Success}, Returned GUID: {Guid}", success, returnedGuid);
            return (success, returnedGuid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save conversation for user: {UserId}, conversationId: {ConversationId}", userId, conversationId);
            return (false, null);
        }
    }

    private async Task<(bool success, string? returnedConversationId)> SaveToAzureFunctionAsync(string userId, List<ChatMessage> conversationHistory, string? conversationId, string? userEmail)
    {
        try
        {
            _logger.LogInformation("SaveToAzureFunctionAsync called - UserId: {UserId}, ConversationId: {ConversationId}, History count: {Count}", 
                userId, conversationId, conversationHistory?.Count ?? 0);

            // Validate if conversationId is a valid GUID - Azure Function expects GUID or null
            string? functionConversationId = null;
            string? uiSessionId = conversationId; // Store UI session for metadata
            
            if (!string.IsNullOrEmpty(conversationId) && Guid.TryParse(conversationId, out _))
            {
                functionConversationId = conversationId; // Valid GUID - use it
                Console.WriteLine($"[DEBUG] Valid GUID conversationId: {conversationId}");
            }
            else
            {
                Console.WriteLine($"[DEBUG] Non-GUID conversationId '{conversationId}' - sending null to create new");
            }

            // Build messages array in the format Azure Function expects
            var messages = conversationHistory?.Select(msg => new
            {
                role = msg.Role?.ToLower() ?? "user",
                content = msg.Content ?? string.Empty
            }).Cast<object>().ToList() ?? new List<object>();

            _logger.LogInformation("Built messages array with {Count} messages", messages.Count);

            // Build payload matching Azure Function schema
            var conversationData = new
            {
                conversationId = functionConversationId, // null for new, GUID for updates
                userId = userId,
                userEmail = userEmail ?? string.Empty,
                chatType = "general",
                messages = messages,
                totalTokens = 0, // Could be calculated if needed
                metadata = new { 
                    source = "RR-Realty-AI", 
                    timestamp = DateTime.UtcNow,
                    sessionId = uiSessionId // Store UI session for traceability
                }
            };

            var json = JsonSerializer.Serialize(conversationData);
            Console.WriteLine($"[DEBUG] Sending to Function - ConversationId: {functionConversationId ?? "null"}, SessionId: {uiSessionId}");
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var saveUrl = BuildFunctionUrl("SaveConversation");
            _logger.LogInformation("Calling Azure Function: {Url}, MessageCount: {Count}", saveUrl.Replace(_functionCode ?? string.Empty, "***"), messages.Count);

            var response = await _httpClient.PostAsync(saveUrl, content);

            _logger.LogInformation("Azure Function responded with status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] Function response: {responseBody}");
                
                // Parse response to get the conversationId (GUID) returned by function
                string? returnedGuid = null;
                try
                {
                    var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    if (responseJson.TryGetProperty("conversationId", out JsonElement idElement))
                    {
                        returnedGuid = idElement.GetString();
                        Console.WriteLine($"[DEBUG] Function returned GUID: {returnedGuid}");
                    }
                }
                catch (Exception parseEx)
                {
                    _logger.LogWarning(parseEx, "Could not parse conversationId from response");
                }
                
                _logger.LogInformation("Successfully saved conversation to Azure Function SQL audit. Response: {Response}", responseBody);
                return (true, returnedGuid);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] Function error: {response.StatusCode} - {errorContent}");
                _logger.LogWarning("Azure Function returned error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return (false, null);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Exception in SaveToAzureFunctionAsync: {ex.Message}");
            _logger.LogError(ex, "Failed to call Azure Function for conversation audit. URL: {Url}, UserId: {UserId}", 
                _azureFunctionUrl.Split('?')[0] + "?code=***", userId);
            return (false, null);
        }
    }

    public async Task<List<ConversationEntry>> GetConversationHistoryAsync(string userId, string? conversationId = null)
    {
        try
        {
            _logger.LogInformation("Retrieving conversation history for user: {UserId}", userId);

            // Try to get from Azure Function (SQL database)
            var requestUriBuilder = new StringBuilder($"GetConversations?userId={Uri.EscapeDataString(userId)}");

            if (!string.IsNullOrEmpty(conversationId))
            {
                requestUriBuilder.Append($"&conversationId={Uri.EscapeDataString(conversationId)}");
            }

            var getUrl = BuildFunctionUrl(requestUriBuilder.ToString());
            var response = await _httpClient.GetAsync(getUrl);
            
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

            var clearUrl = BuildFunctionUrl($"ClearConversations?userId={Uri.EscapeDataString(userId)}");
            var response = await _httpClient.DeleteAsync(clearUrl);
            
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

            var sessionsUrl = BuildFunctionUrl($"GetSessions?userId={Uri.EscapeDataString(userId)}");
            var response = await _httpClient.GetAsync(sessionsUrl);
            
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

            var deleteUrl = BuildFunctionUrl($"DeleteConversation?userId={Uri.EscapeDataString(userId)}&conversationId={Uri.EscapeDataString(conversationId)}");
            var response = await _httpClient.DeleteAsync(deleteUrl);

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
