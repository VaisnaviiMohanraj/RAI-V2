# RR Realty AI - Azure Function for Conversation Audit

This Azure Function provides SQL database audit logging for the RR Realty AI chat application.

## Features

- **SaveConversation**: Saves chat conversations to SQL database
- **GetConversations**: Retrieves conversation history for a user
- **GetSessions**: Gets conversation sessions with summaries
- **ClearConversations**: Clears conversation history for a user

## API Endpoints

### POST /api/SaveConversation
Saves a conversation to the SQL database.

**Request Body:**
```json
{
  "UserId": "user-123",
  "ConversationId": "conv-456",
  "UserMessage": "Hello",
  "AssistantResponse": "Hi there!",
  "Timestamp": "2025-01-17T15:30:00Z",
  "Source": "RR-Realty-AI"
}
```

### GET /api/GetConversations?userId={userId}
Retrieves all conversation entries for a user.

### GET /api/GetSessions?userId={userId}
Gets conversation sessions with metadata (message count, preview, etc.).

### DELETE /api/ClearConversations?userId={userId}
Clears all conversations for a user.

## Database Schema

The function automatically creates the `Conversations` table:

```sql
CREATE TABLE Conversations (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(255) NOT NULL,
    ConversationId NVARCHAR(255) NOT NULL,
    UserMessage NVARCHAR(MAX) NOT NULL,
    AssistantResponse NVARCHAR(MAX) NOT NULL,
    Timestamp DATETIME2 NOT NULL,
    Source NVARCHAR(100) NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
)
```

## Configuration

Set the following environment variable in Azure Function App settings:

- `SqlConnectionString`: Connection string to your SQL database

## Deployment

1. Build the project:
   ```bash
   dotnet build
   ```

2. Deploy to Azure:
   ```bash
   func azure functionapp publish fn-conversationsave
   ```

## Local Development

1. Update `local.settings.json` with your SQL connection string
2. Run locally:
   ```bash
   func start
   ```

## Security

- All endpoints use Function-level authorization
- SQL injection protection via parameterized queries
- Comprehensive error handling and logging
