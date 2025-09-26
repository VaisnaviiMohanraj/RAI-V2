using Backend.Models;
using Backend.Configuration;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Options;
using ChatMessage = Backend.Models.ChatMessage;
using System.Linq;

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
    private readonly Dictionary<string, List<ChatMessage>> _chatHistories = new();
    private bool _clientInitialized = false;
    private readonly object _initLock = new object();

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

            var messages = await PrepareMessagesAsync(request, userId);
            var requestOptions = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = 4096,
                Temperature = 0.7f,
                TopP = 1.0f,
            };

            var response = await _chatClient.CompleteChatAsync(messages, requestOptions);
            var assistantMessage = response.Value.Content[0].Text;

            // Store chat history with document IDs instead of full context
            var userMessage = new ChatMessage
            {
                Content = request.Message,
                Role = "user",
                DocumentContext = await _documentContextService.GetDocumentContextAsync(request.DocumentIds, userId),
                DocumentIds = request.DocumentIds // Store document IDs for future validation
            };

            var assistantResponse = new ChatMessage
            {
                Content = assistantMessage,
                Role = "assistant"
            };

            if (!_chatHistories.ContainsKey(userId))
            {
                _chatHistories[userId] = new List<ChatMessage>();
            }

            _chatHistories[userId].Add(userMessage);
            _chatHistories[userId].Add(assistantResponse);

            // Save conversation to Azure Function Service with conversation ID
            var conversationId = request.ConversationId ?? $"conv_{userId}_{DateTime.UtcNow:yyyyMMdd}";
            await _azureFunctionService.SaveConversationAsync(userId, request.Message, assistantMessage, conversationId);

            return new ChatResponse
            {
                Content = assistantMessage,
                IsStreaming = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending chat message for user {UserId}", userId);
            return new ChatResponse
            {
                Content = "I apologize, but I encountered an error processing your request. Please try again.",
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
            if (_chatHistories.ContainsKey(userId))
            {
                _chatHistories[userId].Clear();
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing chat history for user {UserId}", userId);
            
            // Fallback: clear in-memory storage only
            if (_chatHistories.ContainsKey(userId))
            {
                _chatHistories[userId].Clear();
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
        
        if (_chatHistories.ContainsKey(userId))
        {
            _chatHistories.Remove(userId);
        }
        
        return result;
    }


    private async IAsyncEnumerable<string> StreamResponseAsync(ChatRequest request, string userId)
    {
        if (!EnsureOpenAIClientInitialized())
        {
            throw new Exception("Failed to initialize Azure OpenAI client");
        }

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
                    yield return contentPart.Text;
                }
            }
        }

        // Store complete response in chat history
        var userMessage = new ChatMessage
        {
            Content = request.Message,
            Role = "user",
            DocumentContext = await _documentContextService.GetDocumentContextAsync(request.DocumentIds, userId),
            DocumentIds = request.DocumentIds
        };

        var assistantMessage = new ChatMessage
        {
            Content = fullResponse,
            Role = "assistant"
        };

        if (!_chatHistories.ContainsKey(userId))
        {
            _chatHistories[userId] = new List<ChatMessage>();
        }

        _chatHistories[userId].Add(userMessage);
        _chatHistories[userId].Add(assistantMessage);

        // Save conversation to Azure Function Service
        await _azureFunctionService.SaveConversationAsync(userId, request.Message, fullResponse, request.ConversationId);
    }

    private async Task<List<OpenAI.Chat.ChatMessage>> PrepareMessagesAsync(ChatRequest request, string userId)
    {
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage(GetSystemPrompt())
        };

        // Add chat history, but filter out document context from deleted documents
        if (_chatHistories.TryGetValue(userId, out var history))
        {
            foreach (var msg in history.TakeLast(10)) // Limit history to last 10 messages
            {
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

        // Add document context if available
        var documentContext = await _documentContextService.GetDocumentContextAsync(request.DocumentIds, userId);
        var userMessageContent = request.Message;
        
        // Combine all context
        var contextParts = new List<string>();
        
        if (!string.IsNullOrEmpty(documentContext))
        {
            contextParts.Add($"Document context:\n{documentContext}");
        }
        
        if (contextParts.Any())
        {
            userMessageContent = $"{string.Join("\n\n", contextParts)}\n\nUser question: {request.Message}";
        }

        messages.Add(new UserChatMessage(userMessageContent));
        return messages;
    }



    private string GetSystemPrompt()
    {
        return @"You are an intelligent AI assistant designed to help with a wide variety of questions and tasks.

You can assist with:
- General knowledge questions on any topic
- Technical support and programming help
- Research and analysis
- Creative writing and content generation
- Problem-solving and decision making
- Educational content and explanations
- Business and professional questions
- Document analysis and insights
- And much more

Guidelines:
- Be helpful, accurate, and informative
- If you're unsure about something, acknowledge the uncertainty
- Ask clarifying questions when needed to provide better assistance
- Maintain a professional yet friendly tone
- Be concise but thorough in your responses
- You can answer questions using your knowledge base and any provided document context";
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
