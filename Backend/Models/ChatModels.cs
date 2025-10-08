namespace Backend.Models;

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? DocumentContext { get; set; }
    public List<string>? DocumentIds { get; set; }
    public string UserId { get; set; } = string.Empty;
    public bool IsUser => Role == "user";
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<string>? DocumentIds { get; set; }
    public string? ConversationId { get; set; }
}

public class ChatResponse
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsStreaming { get; set; }
    public string? ConversationId { get; set; }
}

public class ConversationSession
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime LastMessageTime { get; set; } = DateTime.UtcNow;
    public int MessageCount { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
}
