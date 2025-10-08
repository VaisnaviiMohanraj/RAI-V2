using Backend.Models;
using Backend.Configuration;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Options;
using ChatMessage = Backend.Models.ChatMessage;
using System.Linq;
using System.Collections.Concurrent;

namespace Backend.Services.Chat;

public class ChatService : IChatService
{
    private readonly ILogger<ChatService> _logger;
    private readonly IAzureFunctionService _azureFunctionService;
    private readonly IConversationManager _conversationManager;
    private readonly IDocumentContextService _documentContextService;
    private readonly OpenAISettings _openAISettings;
    private AzureOpenAIClient? _azureClient;
    private ChatClient? _chatClient;
    private static readonly ConcurrentDictionary<string, List<ChatMessage>> _chatHistories = new();
    private static readonly ConcurrentDictionary<string, string> _functionConversationIds = new(); // Maps UI sessionId -> Function GUID (persist across requests)
    private static readonly ConcurrentDictionary<string, string> _loadedConversationIds = new(); // Maps userId -> currently loaded conversationId
    // In-flight assistant buffer so quick follow-ups include the latest assistant response even before it's finalized
    private static readonly ConcurrentDictionary<string, string> _inflightAssistantByUser = new();
    private bool _clientInitialized = false;
    private static readonly object _initLock = new object();

    public ChatService(
        ILogger<ChatService> logger,
        IAzureFunctionService azureFunctionService,
        IConversationManager conversationManager,
        IDocumentContextService documentContextService,
        IOptions<OpenAISettings> openAISettings)
    {
        _logger = logger;
        _azureFunctionService = azureFunctionService;
        _conversationManager = conversationManager;
        _documentContextService = documentContextService;
        _openAISettings = openAISettings.Value;
        
        _logger.LogInformation("ChatService created successfully - OpenAI client will be initialized on first use");
    }

    private bool EnsureOpenAIClientInitialized()
    {
        if (_clientInitialized)
            return _chatClient != null;

        lock (_initLock)
        {
            if (_clientInitialized)
                return _chatClient != null;

            try
            {
                _logger.LogInformation("Initializing Azure OpenAI client...");
                _logger.LogInformation("Endpoint: {Endpoint}", _openAISettings.Endpoint);
                _logger.LogInformation("Deployment: {Deployment}", _openAISettings.DeploymentName);
                
                var endpoint = new Uri(_openAISettings.Endpoint);
                var credential = new System.ClientModel.ApiKeyCredential(_openAISettings.ApiKey);
                _azureClient = new AzureOpenAIClient(endpoint, credential);
                _chatClient = _azureClient.GetChatClient(_openAISettings.DeploymentName);
                
                _logger.LogInformation("Azure OpenAI client initialized successfully");
                _clientInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure OpenAI client");
                _clientInitialized = true; // Don't keep trying
                return false;
            }
        }
    }

    public async Task<ChatResponse> SendMessageAsync(ChatRequest request, string userId)
    {
        try
        {
            if (!EnsureOpenAIClientInitialized())
            {
                throw new Exception("Failed to initialize Azure OpenAI client");
            }

            // Establish/track current conversation early to avoid race on quick follow-ups
            var currentConversationId = request.ConversationId ?? $"conv_{userId}_{DateTime.UtcNow:yyyyMMdd}";
            _loadedConversationIds[currentConversationId] = currentConversationId; // mark as active by its own id
            _loadedConversationIds[userId] = currentConversationId; // maintain legacy mapping
            _logger.LogInformation($"[HISTORY] Using conversation {currentConversationId} for user {userId}");

            // If conversationId is provided and we don't have history loaded, restore it from backend
            await RestoreConversationHistoryIfNeededAsync(userId, currentConversationId);

            // Add user message to history BEFORE calling OpenAI to avoid race condition
            var userMessage = new ChatMessage
            {
                Content = request.Message,
                Role = "user",
                DocumentContext = await _documentContextService.GetDocumentContextAsync(request.DocumentIds, userId),
                DocumentIds = request.DocumentIds
            };

            var userHistory = _chatHistories.GetOrAdd(userId, _ => new List<ChatMessage>());
            lock (userHistory)
            {
                userHistory.Add(userMessage);
            }

            var messages = await PrepareMessagesAsync(request, userId);
            var requestOptions = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = 4096,
                Temperature = 0.7f,
                TopP = 1.0f,
            };

            var response = await _chatClient.CompleteChatAsync(messages, requestOptions);
            var assistantMessage = response.Value.Content[0].Text;

            // Store assistant response in history (user message already added)
            var assistantResponse = new ChatMessage
            {
                Content = assistantMessage,
                Role = "assistant"
            };

            if (_chatHistories.TryGetValue(userId, out var userHistory2))
            {
                lock (userHistory2)
                {
                    userHistory2.Add(assistantResponse);
                }
            }

            // Save conversation to Azure Function Service with conversation ID (best effort - don't block on failure)
            var conversationId = currentConversationId;
            _loadedConversationIds[userId] = conversationId; // Track currently loaded conversation
            var historyCount = _chatHistories.TryGetValue(userId, out var hist) ? hist.Count : 0;
            _logger.LogInformation($"[DEBUG] About to save conversation - userId: {userId}, UI conversationId: {conversationId}, historyCount: {historyCount}");
            try
            {
                // Look up Function GUID for this UI session, or pass UI session if first time
                string? functionGuid = null;
                if (_functionConversationIds.TryGetValue(conversationId, out var existingGuid))
                {
                    functionGuid = existingGuid;
                    _logger.LogInformation($"[DEBUG] Found existing Function GUID for session: {functionGuid}");
                }
                else
                {
                    _logger.LogInformation($"[DEBUG] No existing GUID - will create new conversation in Function");
                }
                
                // Pass the full conversation history for audit logging
                var (saveResult, returnedGuid) = await _azureFunctionService.SaveConversationAsync(userId, _chatHistories[userId], functionGuid ?? conversationId);
                _logger.LogInformation($"[DEBUG] Save result: {saveResult}, Returned GUID: {returnedGuid}");
                
                // Store the GUID mapping if we got one back and don't have it yet
                if (saveResult && !string.IsNullOrEmpty(returnedGuid))
                {
                    _functionConversationIds.AddOrUpdate(conversationId, returnedGuid, (k, v) => returnedGuid);
                    _logger.LogInformation($"[DEBUG] Stored mapping (global): UI '{conversationId}' -> Function '{returnedGuid}'");
                }
            }
            catch (Exception saveEx)
            {
                _logger.LogWarning(saveEx, $"[DEBUG] Save failed with exception: {saveEx.Message}");
            }

            return new ChatResponse
            {
                Content = assistantMessage,
                IsStreaming = false,
                ConversationId = conversationId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending chat message for user {UserId}", userId);
            
            // Return detailed error for debugging
            var errorMessage = $"Error: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $"\nInner: {ex.InnerException.Message}";
            }
            
            _logger.LogError("Detailed error: {ErrorDetails}", errorMessage);
            
            return new ChatResponse
            {
                Content = $"I apologize, but I encountered an error processing your request.\n\nError details: {errorMessage}\n\nPlease check the OpenAI configuration.",
                IsStreaming = false
            };
        }
    }

    public Task<IAsyncEnumerable<string>> SendStreamingMessageAsync(ChatRequest request, string userId)
    {
        return Task.FromResult(StreamResponseAsync(request, userId));
    }

    public async Task<List<ChatMessage>> GetChatHistoryAsync(string userId)
    {
        try
        {
            // Try to get from Azure Function Service first
            var conversationHistory = await _azureFunctionService.GetConversationHistoryAsync(userId);
            
            if (conversationHistory.Any())
            {
                // Convert ConversationEntry to ChatMessage
                var chatMessages = conversationHistory.Select(entry => new ChatMessage
                {
                    Id = entry.Id,
                    Content = entry.Content,
                    Role = entry.Role,
                    Timestamp = entry.Timestamp,
                    UserId = entry.UserId
                }).ToList();

                // Update in-memory cache
                _chatHistories[userId] = chatMessages;
                return chatMessages;
            }

            // Fallback to in-memory storage
            if (_chatHistories.TryGetValue(userId, out var history))
            {
                return history;
            }
            
            return new List<ChatMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat history for user {UserId}", userId);
            
            // Fallback to in-memory storage on error
            if (_chatHistories.TryGetValue(userId, out var history))
            {
                return history;
            }
            
            return new List<ChatMessage>();
        }
    }

    public async Task<bool> ClearChatHistoryAsync(string userId)
    {
        try
        {
            // Clear from Azure Function Service
            var success = await _azureFunctionService.ClearConversationHistoryAsync(userId);
            
            // Also clear from in-memory storage
            if (_chatHistories.TryGetValue(userId, out var history))
            {
                lock (history)
                {
                    history.Clear();
                }
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing chat history for user {UserId}", userId);
            
            // Fallback: clear in-memory storage only
            if (_chatHistories.TryGetValue(userId, out var history))
            {
                lock (history)
                {
                    history.Clear();
                }
                return true;
            }
            
            return false;
        }
    }

    public async Task<List<ConversationSession>> GetAllConversationSessionsAsync(string userId)
    {
        return await _conversationManager.GetAllConversationSessionsAsync(userId);
    }

    public async Task<List<ChatMessage>> GetConversationHistoryBySessionAsync(string userId, string sessionId)
    {
        return await _conversationManager.GetConversationHistoryBySessionAsync(userId, sessionId);
    }

    public async Task<bool> DeleteConversationSessionAsync(string userId, string sessionId)
    {
        var result = await _conversationManager.DeleteConversationSessionAsync(userId, sessionId);
        
        _chatHistories.TryRemove(userId, out _);
        
        return result;
    }


    private async IAsyncEnumerable<string> StreamResponseAsync(ChatRequest request, string userId)
    {
        if (!EnsureOpenAIClientInitialized())
        {
            throw new Exception("Failed to initialize Azure OpenAI client");
        }

        // Establish/track current conversation early to avoid race on quick follow-ups
        var currentConversationId = request.ConversationId ?? $"conv_{userId}_{DateTime.UtcNow:yyyyMMdd}";
        _loadedConversationIds[currentConversationId] = currentConversationId; // mark as active by its own id
        _loadedConversationIds[userId] = currentConversationId; // maintain legacy mapping
        _logger.LogInformation($"[HISTORY STREAM] Using conversation {currentConversationId} for user {userId}");

        // If conversationId is provided and we don't have history loaded, restore it from backend
        await RestoreConversationHistoryIfNeededAsync(userId, currentConversationId);

        // Add user message to history BEFORE streaming to avoid race condition
        var userMessage = new ChatMessage
        {
            Content = request.Message,
            Role = "user",
            DocumentContext = await _documentContextService.GetDocumentContextAsync(request.DocumentIds, userId),
            DocumentIds = request.DocumentIds
        };

        var streamHistory = _chatHistories.GetOrAdd(userId, _ => new List<ChatMessage>());
        lock (streamHistory)
        {
            streamHistory.Add(userMessage);
        }

        // Initialize in-flight buffer for this user
        _inflightAssistantByUser[userId] = string.Empty;

        var messages = await PrepareMessagesAsync(request, userId);
        var response = _chatClient.CompleteChatStreamingAsync(messages);

        var fullResponse = string.Empty;
        await foreach (var update in response)
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    fullResponse += contentPart.Text;
                    // Update in-flight buffer so concurrent requests can see the latest assistant content
                    _inflightAssistantByUser[userId] = fullResponse;
                    yield return contentPart.Text;
                }
            }
        }

        // Store assistant response in chat history (user message already added before streaming)
        var assistantMessage = new ChatMessage
        {
            Content = fullResponse,
            Role = "assistant"
        };

        if (_chatHistories.TryGetValue(userId, out var streamHistory2))
        {
            lock (streamHistory2)
            {
                streamHistory2.Add(assistantMessage);
            }
        }

        // Finalize: remove in-flight buffer for this user
        _inflightAssistantByUser.TryRemove(userId, out _);

        // Save conversation to Azure Function Service (best effort - don't block on failure)
        var uiSessionId = currentConversationId;
        _loadedConversationIds[userId] = uiSessionId; // Track currently loaded conversation
        var streamHistoryCount = _chatHistories.TryGetValue(userId, out var hist) ? hist.Count : 0;
        _logger.LogInformation($"[DEBUG STREAMING] About to save - userId: {userId}, UI sessionId: {uiSessionId}, historyCount: {streamHistoryCount}");
        try
        {
            // Look up Function GUID for this UI session
            string? functionGuid = null;
            if (!string.IsNullOrEmpty(uiSessionId) && _functionConversationIds.TryGetValue(uiSessionId, out var existingGuid))
            {
                functionGuid = existingGuid;
                _logger.LogInformation($"[DEBUG STREAMING] Found existing Function GUID: {functionGuid}");
            }
            else
            {
                _logger.LogInformation($"[DEBUG STREAMING] No existing GUID - will create new in Function");
            }
            
            // Pass the full conversation history for audit logging
            var (saveResult, returnedGuid) = await _azureFunctionService.SaveConversationAsync(userId, _chatHistories[userId], functionGuid ?? uiSessionId);
            _logger.LogInformation($"[DEBUG STREAMING] Save result: {saveResult}, Returned GUID: {returnedGuid}");
            
            // Store mapping if we got a GUID back
            if (saveResult && !string.IsNullOrEmpty(returnedGuid) && !string.IsNullOrEmpty(uiSessionId) && !_functionConversationIds.ContainsKey(uiSessionId))
            {
                _functionConversationIds[uiSessionId] = returnedGuid;
                _logger.LogInformation($"[DEBUG STREAMING] Stored mapping: '{uiSessionId}' -> '{returnedGuid}'");
            }
        }
        catch (Exception saveEx)
        {
            _logger.LogWarning(saveEx, $"[DEBUG STREAMING] Save failed: {saveEx.Message}");
        }
    }

    private async Task RestoreConversationHistoryIfNeededAsync(string userId, string? conversationId)
    {
        // If no conversationId provided, nothing to restore
        if (string.IsNullOrEmpty(conversationId))
        {
            _logger.LogInformation($"[HISTORY] No conversationId provided for user {userId}");
            return;
        }

        // Check if we already have THIS conversation loaded
        if (_loadedConversationIds.TryGetValue(userId, out var loadedConvId) && loadedConvId == conversationId)
        {
            if (_chatHistories.ContainsKey(userId) && _chatHistories[userId].Count > 0)
            {
                _logger.LogInformation($"[HISTORY] Conversation {conversationId} already loaded with {_chatHistories[userId].Count} messages");
                return;
            }
        }

        // Different conversation or not loaded - need to restore from backend/SQL
        _logger.LogInformation($"[HISTORY] Loading conversation {conversationId} for user {userId} (previous: {loadedConvId ?? "none"})");
        try
        {
            // If we have a GUID mapping for this UI session, use it to query Azure Function; otherwise use UI session id
            var lookupId = conversationId;
            if (_functionConversationIds.TryGetValue(conversationId, out var mappedGuid) && !string.IsNullOrWhiteSpace(mappedGuid))
            {
                _logger.LogInformation($"[HISTORY] Found GUID mapping for session {conversationId}: {mappedGuid}");
                lookupId = mappedGuid;
            }

            var history = await _conversationManager.GetConversationHistoryBySessionAsync(userId, lookupId);
            
            if (history != null && history.Count > 0)
            {
                // Convert to ChatMessage format
                var chatMessages = history.Select(msg => new ChatMessage
                {
                    Content = msg.Content,
                    Role = msg.Role?.ToLower() ?? "user",
                    Timestamp = msg.Timestamp
                }).ToList();

                _chatHistories[userId] = chatMessages;
                _loadedConversationIds[userId] = conversationId; // Track which conversation is loaded
                _logger.LogInformation($"[HISTORY] Restored {chatMessages.Count} messages for user {userId} from session {conversationId}");
            }
            else
            {
                _logger.LogInformation($"[HISTORY] No history found for session {conversationId}");
                // Clear old history since we're switching to a new/empty conversation
                if (_chatHistories.ContainsKey(userId))
                {
                    _chatHistories[userId].Clear();
                }
                _loadedConversationIds[userId] = conversationId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"[HISTORY] Failed to restore conversation history for user {userId}, session {conversationId}: {ex.Message}");
        }
    }

    private async Task<List<OpenAI.Chat.ChatMessage>> PrepareMessagesAsync(ChatRequest request, string userId)
    {
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage(GetSystemPrompt())
        };

        // Add all chat history including the current message (already added to _chatHistories before this method was called)
        // Filter out document context from deleted documents
        if (_chatHistories.TryGetValue(userId, out var history))
        {
            _logger.LogInformation($"[PREPARE] Found {history.Count} messages in history for user {userId}");
            var historyToSend = history.TakeLast(10).ToList();
            _logger.LogInformation($"[PREPARE] Sending last {historyToSend.Count} messages to OpenAI");
            
            foreach (var msg in historyToSend)
            {
                _logger.LogInformation($"[PREPARE] Adding message - Role: {msg.Role}, Content preview: {msg.Content?.Substring(0, Math.Min(50, msg.Content?.Length ?? 0))}...");
                
                if (msg.Role == "user")
                {
                    // For user messages, validate document IDs and filter out deleted documents
                    var cleanedContent = await _documentContextService.FilterDeletedDocumentContextAsync(msg.Content, msg.DocumentIds, userId);
                    messages.Add(new UserChatMessage(cleanedContent));
                }
                else if (msg.Role == "assistant")
                {
                    messages.Add(new AssistantChatMessage(msg.Content));
                }
            }
        }
        else
        {
            _logger.LogWarning($"[PREPARE] WARNING: No history found for user {userId}!");
        }

        // Include in-flight assistant content if any (handles quick follow-ups during streaming)
        if (_inflightAssistantByUser.TryGetValue(userId, out var inflight) && !string.IsNullOrWhiteSpace(inflight))
        {
            _logger.LogInformation($"[PREPARE] Including in-flight assistant content of length {inflight.Length}");
            messages.Add(new AssistantChatMessage(inflight));
        }

        _logger.LogInformation($"[PREPARE] Total messages being sent to OpenAI: {messages.Count} (including system prompt)");
        // Note: Current user message already included in history above, no need to add again
        return messages;
    }

    private string GetSystemPrompt()
    {
        return @"You are RAI, a knowledgeable real estate assistant for R&R Realty.

Your role is to:
- Help employees with searches and market information
- Answer questions about real estate processes
- Provide guidance on buying, selling, and renting properties

Your personality:
- Professional yet friendly
- Patient and thorough
- Knowledgeable about Des Moines and Omaha commercial and multifamily real estate Markets

When answering questions:
1. Check any uploaded documents first for specific information
2. Provide clear, actionable advice
3. Be honest when you don't have specific information
4. Always maintain client confidentiality

=== ELEVATOR PITCH ===

More than just a place to work, an office space should support and enhance the way you work. As long-term property owners and stewards, R&R Realty Group continually invests in our properties to create spaces with the technology and amenities that allow innovation to flourish. And our commitment to service matches our best-in-class portfolio. Our local teams provide interior and exterior services — from indoor maintenance to landscaping and snow removal — so you and your organization remain comfortable, worry-free and focused on what matters: your business.

=== COMPANY BACKGROUND ===

Excerpts from 'The Ground Up'
The reasons we succeeded are:
1. Conducting our business with integrity.
2. Caring for our employees and customers like family.
3. Focusing on our strengths.
4. Being a leader in our industry.
5. Giving back to the community.

Core Values:
- Respect others.
- Do the right thing.
- Keep our commitments.
- Passionately pursue excellence.
- We before me.

=== COMPANY HISTORY ===

Founded: 1985 by Dan Rupprecht
R&R Realty celebrates 35+ years in business (as of 2020), owning and managing more than 9 million square feet of commercial real estate with 130+ employees across Iowa and Nebraska.

The company was built on the foundation of 'Doing the right thing,' meeting stakeholders, partners, employees, and the community with the highest standards possible. The theme of integrity runs through every building, relationship, and project.

In 2007, Mark Rupprecht assumed the role of president of R&R Realty Group, continuing the family's commitment to relationship-based leadership and excellence. Mark's leadership style is relationship-based rather than transaction-based, emphasizing listening, asking questions, and being well-prepared.

=== SUBSIDIARIES ===

R&R Real Estate Advisors (REA) - Leadership: Paul Rupprecht
Brokerage arm specializing in broker relations, tenant relationships, and leasing services. Represents tenants, negotiating leases and acquisitions, performing market research, and site selection.

R&R is the largest owner and property manager of Class A commercial real estate in Iowa with assets including Class A offices, single tenant offices, flex offices, industrial/warehouses, and multifamily apartments.

Management Professionals, Inc. (MPI) - Leadership: Joe Price
Oversees the portfolio of R&R properties, offering management services including financial, facilities, exterior, risk, and vendor management with 24/7 availability.

Development Services Corporation (DSC) - Leadership: Tom Rupprecht
Manages projects from concept to build, handling design, negotiations, zoning, and ensuring all stakeholder needs are met.

ICON Construction - Leadership: Tom Rupprecht
Provides contractor services for space overhaul, restoration, renovation, and new builds, creating elegant, high-quality, functional spaces.

Realty Technology Services (RTS) - Leadership: Luke Anderson
Technology and IT subsidiary providing state-of-the-art services including low voltage, upgraded digital, hi-def AV, sound masking, video surveillance, and controlled access.

Nebraska Division - Leadership: Mike Homa
R&R's expansion into Omaha, continuing development efforts and services.

=== AWARDS & RECOGNITION ===

- 'Best of the Best' by Real Estate News for Top Midwest Commercial Real Estate Owner, Brokerage, and Property Manager
- 'Top Place to Work' winner every year since 2012 (Des Moines Register)

=== CONTACT INFORMATION ===

Des Moines Office: 1080 Jordan Creek Parkway, Suite 200 North, West Des Moines, IA 50266 - Phone: (515) 223-4500
Omaha Office: 18881 W Dodge Rd, Elkhorn, NE 68022 - Phone: (402) 885-4002

=== COMMUNITY COMMITMENT ===

Community service is core to R&R's culture. In 2019, employees pledged a record $175,713 to the United Way of Central Iowa with over 90% employee participation.";
    }

    private string GenerateTitle(string firstMessage)
    {
        if (string.IsNullOrWhiteSpace(firstMessage))
            return "New Chat";
        
        // Take first 50 characters and add ellipsis if longer
        return firstMessage.Length > 50 
            ? firstMessage.Substring(0, 50) + "..." 
            : firstMessage;
    }
}
