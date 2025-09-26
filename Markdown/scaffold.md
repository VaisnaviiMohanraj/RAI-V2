# RR Realty AI - Code Scaffolding Guide

## 🏗️ Project Scaffolding Overview
This document provides the core code structure and logic for recreating RR Realty AI with the 400-line rule enforced and proper Oryx integration for Azure deployment.

## 📁 Directory Structure
```
rr-realty-ai-v2/
├── Backend/
├── Frontend/
├── AzureFunctions/
├── .deployment          # Oryx build configuration
├── .gitignore
└── extract/ (this documentation)
```

## 🔧 Oryx Configuration Files

### .deployment (Root level)
```ini
[config]
project = Backend/Backend.csproj
```

### .gitignore (Root level)
```gitignore
# Build outputs
bin/
obj/
dist/
node_modules/

# Environment files
.env
.env.local
.env.production

# Azure
.azure/
publish/

# IDE
.vs/
.vscode/
*.user
```

## 🔧 Environment Variables

### Backend (.env)
```bash
# Azure OpenAI Configuration
AZURE_OPENAI_ENDPOINT=https://gto4o.cognitiveservices.azure.com/
AZURE_OPENAI_API_KEY=your-openai-key-here
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o
AZURE_OPENAI_API_VERSION=2024-08-01-preview

# Azure AD Authentication
AZURE_CLIENT_ID=d4c452c4-5324-40ff-b43b-25f3daa2a45c
AZURE_TENANT_ID=99848873-e61d-44cc-9862-d05151c567ab
AZURE_CLIENT_SECRET=your-client-secret-here

# Azure Storage
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=your-storage;AccountKey=your-key;EndpointSuffix=core.windows.net
AZURE_STORAGE_CONTAINER_NAME=documents

# Azure Key Vault (Production)
AZURE_KEY_VAULT_URL=https://your-keyvault.vault.azure.net/

# Database (if using Cosmos DB)
COSMOS_DB_CONNECTION_STRING=AccountEndpoint=https://your-cosmos.documents.azure.com:443/;AccountKey=your-key;

# Application Settings
ASPNETCORE_ENVIRONMENT=Development
SESSION_TIMEOUT_DAYS=30
```

### Frontend (.env)
```bash
# Authentication
VITE_AUTH_CLIENT_ID=d4c452c4-5324-40ff-b43b-25f3daa2a45c
VITE_AUTH_AUTHORITY=https://login.microsoftonline.com/99848873-e61d-44cc-9862-d05151c567ab
VITE_AUTH_REDIRECT_URI=http://localhost:5173
VITE_API_SCOPE=User.Read

# API Configuration
VITE_API_BASE_URL=http://localhost:5000
VITE_API_TIMEOUT=30000

# Feature Flags
VITE_ENABLE_DOCUMENT_UPLOAD=true
VITE_ENABLE_CONVERSATION_SAVE=true
VITE_MAX_FILE_SIZE_MB=10
```

## 🎯 Backend Core Files

### Program.cs (< 400 lines)
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Backend.Services.Chat;
using Backend.Services.Document;
using Backend.Services.Auth;
using DotNetEnv;

// Load environment variables
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://testing.rrrealty.ai")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Custom services
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.UseStaticFiles();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
```

### ChatController.cs (< 400 lines)
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend.Services.Chat;
using Backend.Models;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    
    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] ChatMessageRequest request)
    {
        try
        {
            var userId = User.Identity?.Name ?? "anonymous";
            var response = await _chatService.ProcessMessageAsync(
                request.Message, 
                request.SessionId, 
                userId,
                request.DocumentContext
            );
            
            return Ok(new ChatMessageResponse
            {
                Message = response.Message,
                SessionId = response.SessionId,
                Timestamp = response.Timestamp
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        try
        {
            var userId = User.Identity?.Name ?? "anonymous";
            var sessions = await _chatService.GetUserSessionsAsync(userId);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        try
        {
            var userId = User.Identity?.Name ?? "anonymous";
            var session = await _chatService.CreateSessionAsync(userId, request.Title);
            return Ok(session);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(string sessionId)
    {
        try
        {
            var userId = User.Identity?.Name ?? "anonymous";
            await _chatService.DeleteSessionAsync(sessionId, userId);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
```

### ChatService.cs (< 400 lines)
```csharp
using Azure.AI.OpenAI;
using Backend.Models;
using System.Text.Json;

namespace Backend.Services.Chat;

public interface IChatService
{
    Task<ChatResponse> ProcessMessageAsync(string message, string sessionId, string userId, string? documentContext = null);
    Task<List<ChatSession>> GetUserSessionsAsync(string userId);
    Task<ChatSession> CreateSessionAsync(string userId, string? title = null);
    Task DeleteSessionAsync(string sessionId, string userId);
}

public class ChatService : IChatService
{
    private readonly OpenAIClient _openAIClient;
    private readonly IConfiguration _configuration;
    private readonly string _deploymentName;

    public ChatService(IConfiguration configuration)
    {
        _configuration = configuration;
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        _deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o";
        
        _openAIClient = new OpenAIClient(new Uri(endpoint!), new Azure.AzureKeyCredential(apiKey!));
    }

    public async Task<ChatResponse> ProcessMessageAsync(string message, string sessionId, string userId, string? documentContext = null)
    {
        var systemPrompt = GetRealEstateSystemPrompt();
        var conversationHistory = await GetConversationHistoryAsync(sessionId, userId);
        
        var messages = new List<ChatRequestMessage>
        {
            new ChatRequestSystemMessage(systemPrompt)
        };

        // Add conversation history
        messages.AddRange(conversationHistory);

        // Add document context if provided
        if (!string.IsNullOrEmpty(documentContext))
        {
            messages.Add(new ChatRequestSystemMessage($"Document Context: {documentContext}"));
        }

        // Add current message
        messages.Add(new ChatRequestUserMessage(message));

        var chatCompletionsOptions = new ChatCompletionsOptions(_deploymentName, messages)
        {
            Temperature = 0.7f,
            MaxTokens = 1000,
            NucleusSamplingFactor = 0.95f
        };

        var response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
        var aiResponse = response.Value.Choices[0].Message.Content;

        // Save conversation
        await SaveConversationAsync(sessionId, userId, message, aiResponse);

        return new ChatResponse
        {
            Message = aiResponse,
            SessionId = sessionId,
            Timestamp = DateTime.UtcNow
        };
    }

    private string GetRealEstateSystemPrompt()
    {
        return @"You are RR Realty AI, an expert real estate assistant with deep knowledge of:
        - Property analysis and valuation
        - Market trends and investment strategies
        - Real estate law and regulations
        - Contract analysis and negotiation
        - Property management and development
        
        Provide professional, accurate, and helpful responses. Always consider legal and financial implications.
        If asked about specific legal advice, recommend consulting with a qualified attorney.";
    }

    private async Task<List<ChatRequestMessage>> GetConversationHistoryAsync(string sessionId, string userId)
    {
        // Implementation for retrieving conversation history
        // This would typically involve database/storage calls
        return new List<ChatRequestMessage>();
    }

    private async Task SaveConversationAsync(string sessionId, string userId, string userMessage, string aiResponse)
    {
        // Implementation for saving conversation
        // This would typically involve database/storage calls
        await Task.CompletedTask;
    }

    public async Task<List<ChatSession>> GetUserSessionsAsync(string userId)
    {
        // Implementation for retrieving user sessions
        return new List<ChatSession>();
    }

    public async Task<ChatSession> CreateSessionAsync(string userId, string? title = null)
    {
        var sessionId = Guid.NewGuid().ToString();
        var session = new ChatSession
        {
            Id = sessionId,
            UserId = userId,
            Title = title ?? "New Conversation",
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };

        // Save session to storage
        return session;
    }

    public async Task DeleteSessionAsync(string sessionId, string userId)
    {
        // Implementation for deleting session
        await Task.CompletedTask;
    }
}
```

## 🎨 Frontend Core Files

### App.tsx (< 400 lines)
```typescript
import React, { useState, useEffect } from 'react';
import { useIsAuthenticated } from '@azure/msal-react';
import { Sidebar } from './components/Sidebar';
import { ChatInterface } from './components/Chat/ChatInterface';
import { DocumentSidebar } from './components/Document/DocumentSidebar';
import { AuthWrapper } from './components/Auth/AuthWrapper';
import { chatService } from './services/chatService';
import { Message, ChatSession, Document } from './types';
import './App.css';

function App() {
  const isAuthenticated = useIsAuthenticated();
  const [messages, setMessages] = useState<Message[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [chatSessions, setChatSessions] = useState<ChatSession[]>([]);
  const [currentSessionId, setCurrentSessionId] = useState<string | null>(null);
  const [documents, setDocuments] = useState<Document[]>([]);
  const [selectedDocument, setSelectedDocument] = useState<Document | null>(null);

  useEffect(() => {
    if (isAuthenticated) {
      loadChatSessions();
    }
  }, [isAuthenticated]);

  const loadChatSessions = async () => {
    try {
      const sessions = await chatService.getSessions();
      setChatSessions(sessions);
    } catch (error) {
      console.error('Failed to load chat sessions:', error);
    }
  };

  const handleSendMessage = async (message: string) => {
    if (!message.trim()) return;

    setIsLoading(true);
    const userMessage: Message = {
      id: Date.now().toString(),
      content: message,
      sender: 'user',
      timestamp: new Date()
    };

    setMessages(prev => [...prev, userMessage]);

    try {
      const response = await chatService.sendMessage({
        message,
        sessionId: currentSessionId,
        documentContext: selectedDocument?.content
      });

      const aiMessage: Message = {
        id: (Date.now() + 1).toString(),
        content: response.message,
        sender: 'ai',
        timestamp: new Date(response.timestamp)
      };

      setMessages(prev => [...prev, aiMessage]);
      
      if (response.sessionId !== currentSessionId) {
        setCurrentSessionId(response.sessionId);
        loadChatSessions();
      }
    } catch (error) {
      console.error('Failed to send message:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleSessionSelect = async (sessionId: string) => {
    setCurrentSessionId(sessionId);
    try {
      const sessionMessages = await chatService.getSessionMessages(sessionId);
      setMessages(sessionMessages);
    } catch (error) {
      console.error('Failed to load session messages:', error);
    }
  };

  const handleNewChat = () => {
    setCurrentSessionId(null);
    setMessages([]);
    setSelectedDocument(null);
  };

  if (!isAuthenticated) {
    return <AuthWrapper />;
  }

  return (
    <div className="app">
      <Sidebar
        sessions={chatSessions}
        currentSessionId={currentSessionId}
        onSessionSelect={handleSessionSelect}
        onNewChat={handleNewChat}
      />
      
      <main className="main-content">
        <ChatInterface
          messages={messages}
          isLoading={isLoading}
          onSendMessage={handleSendMessage}
          selectedDocument={selectedDocument}
        />
      </main>

      <DocumentSidebar
        documents={documents}
        selectedDocument={selectedDocument}
        onDocumentSelect={setSelectedDocument}
        onDocumentsChange={setDocuments}
      />
    </div>
  );
}

export default App;
```

### chatService.ts (< 400 lines)
```typescript
import axios from 'axios';
import { Message, ChatSession } from '../types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

export interface SendMessageRequest {
  message: string;
  sessionId?: string | null;
  documentContext?: string;
}

export interface SendMessageResponse {
  message: string;
  sessionId: string;
  timestamp: string;
}

class ChatService {
  private apiClient = axios.create({
    baseURL: `${API_BASE_URL}/api`,
    timeout: 30000,
  });

  constructor() {
    this.setupInterceptors();
  }

  private setupInterceptors() {
    this.apiClient.interceptors.request.use(
      (config) => {
        const token = this.getAuthToken();
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    this.apiClient.interceptors.response.use(
      (response) => response,
      (error) => {
        if (error.response?.status === 401) {
          // Handle authentication error
          window.location.reload();
        }
        return Promise.reject(error);
      }
    );
  }

  private getAuthToken(): string | null {
    // Implementation would get token from MSAL
    return localStorage.getItem('authToken');
  }

  async sendMessage(request: SendMessageRequest): Promise<SendMessageResponse> {
    const response = await this.apiClient.post('/chat/message', request);
    return response.data;
  }

  async getSessions(): Promise<ChatSession[]> {
    const response = await this.apiClient.get('/chat/sessions');
    return response.data;
  }

  async createSession(title?: string): Promise<ChatSession> {
    const response = await this.apiClient.post('/chat/sessions', { title });
    return response.data;
  }

  async deleteSession(sessionId: string): Promise<void> {
    await this.apiClient.delete(`/chat/sessions/${sessionId}`);
  }

  async getSessionMessages(sessionId: string): Promise<Message[]> {
    const response = await this.apiClient.get(`/chat/sessions/${sessionId}/messages`);
    return response.data;
  }

  // Local storage for offline capability
  saveSessionToLocal(sessionId: string, messages: Message[]) {
    const key = `chat_session_${sessionId}`;
    localStorage.setItem(key, JSON.stringify(messages));
  }

  loadSessionFromLocal(sessionId: string): Message[] | null {
    const key = `chat_session_${sessionId}`;
    const data = localStorage.getItem(key);
    return data ? JSON.parse(data) : null;
  }
}

export const chatService = new ChatService();
```

## 🔧 Azure Functions

### ConversationPersistence/index.ts (< 400 lines)
```typescript
import { AzureFunction, Context, HttpRequest } from "@azure/functions";
import { BlobServiceClient } from "@azure/storage-blob";

const httpTrigger: AzureFunction = async function (context: Context, req: HttpRequest): Promise<void> {
    const connectionString = process.env.AZURE_STORAGE_CONNECTION_STRING;
    const containerName = "conversations";
    
    if (!connectionString) {
        context.res = {
            status: 500,
            body: "Storage connection string not configured"
        };
        return;
    }

    const blobServiceClient = BlobServiceClient.fromConnectionString(connectionString);
    const containerClient = blobServiceClient.getContainerClient(containerName);

    try {
        const { method } = req;
        const { userId, sessionId } = req.query;

        switch (method) {
            case 'GET':
                await handleGetConversation(context, containerClient, userId, sessionId);
                break;
            case 'POST':
                await handleSaveConversation(context, req, containerClient, userId, sessionId);
                break;
            case 'DELETE':
                await handleDeleteConversation(context, containerClient, userId, sessionId);
                break;
            default:
                context.res = {
                    status: 405,
                    body: "Method not allowed"
                };
        }
    } catch (error) {
        context.log.error('Error in conversation persistence:', error);
        context.res = {
            status: 500,
            body: { error: 'Internal server error' }
        };
    }
};

async function handleGetConversation(context: Context, containerClient: any, userId: string, sessionId: string) {
    const blobName = `${userId}/${sessionId}.json`;
    const blobClient = containerClient.getBlobClient(blobName);
    
    if (await blobClient.exists()) {
        const downloadResponse = await blobClient.download();
        const content = await streamToString(downloadResponse.readableStreamBody);
        
        context.res = {
            status: 200,
            body: JSON.parse(content)
        };
    } else {
        context.res = {
            status: 404,
            body: { error: 'Conversation not found' }
        };
    }
}

async function handleSaveConversation(context: Context, req: HttpRequest, containerClient: any, userId: string, sessionId: string) {
    const conversationData = req.body;
    const blobName = `${userId}/${sessionId}.json`;
    const blobClient = containerClient.getBlockBlobClient(blobName);
    
    await blobClient.upload(
        JSON.stringify(conversationData), 
        JSON.stringify(conversationData).length,
        {
            blobHTTPHeaders: { blobContentType: 'application/json' }
        }
    );
    
    context.res = {
        status: 200,
        body: { message: 'Conversation saved successfully' }
    };
}

async function handleDeleteConversation(context: Context, containerClient: any, userId: string, sessionId: string) {
    const blobName = `${userId}/${sessionId}.json`;
    const blobClient = containerClient.getBlobClient(blobName);
    
    await blobClient.deleteIfExists();
    
    context.res = {
        status: 200,
        body: { message: 'Conversation deleted successfully' }
    };
}

async function streamToString(readableStream: any): Promise<string> {
    return new Promise((resolve, reject) => {
        const chunks: any[] = [];
        readableStream.on("data", (data: any) => {
            chunks.push(data.toString());
        });
        readableStream.on("end", () => {
            resolve(chunks.join(""));
        });
        readableStream.on("error", reject);
    });
}

export default httpTrigger;
```

## 🚨 Critical Deployment Considerations

### Avoiding Previous Issues

#### ❌ What NOT to Do (Lessons Learned):
```bash
# DON'T disable Oryx unnecessarily
ENABLE_ORYX_BUILD=false
SCM_DO_BUILD_DURING_DEPLOYMENT=false

# DON'T use self-contained deployment unless required
dotnet publish --self-contained true --runtime win-x64

# DON'T deploy incomplete packages
# (Missing runtime DLLs, incomplete project structure)
```

#### ✅ What TO Do (Best Practices):
```bash
# Let Oryx handle the build process
SCM_DO_BUILD_DURING_DEPLOYMENT=true
WEBSITE_RUN_FROM_PACKAGE=1

# Use framework-dependent deployment
dotnet publish --configuration Release --no-self-contained

# Deploy complete source structure
# Include: Backend/, Frontend/dist/, .deployment
```

### Deployment Package Structure (Oryx-Optimized)
```
deployment.zip:
├── Backend/
│   ├── Controllers/
│   ├── Services/
│   ├── Models/
│   ├── Backend.csproj        # Critical for Oryx detection
│   ├── Program.cs
│   ├── appsettings.json
│   └── web.config
├── Frontend/
│   ├── dist/                 # Pre-built frontend assets
│   │   ├── index.html
│   │   └── assets/
│   └── package.json          # For Oryx Node.js detection
└── .deployment               # Tells Oryx which project to build
```

### Authentication Fix (Production URLs)
```typescript
// CRITICAL: Use production URLs in build process
const msalConfig = {
  auth: {
    clientId: process.env.VITE_AUTH_CLIENT_ID!,
    authority: process.env.VITE_AUTH_AUTHORITY!,
    redirectUri: "https://testing.rrrealty.ai"  // NEVER localhost in production
  }
};
```

---

**Scaffold Version**: 2.0
**Code Compliance**: All files under 400 lines
**Architecture**: Feature-isolated, Azure-optimized with Oryx
**Deployment**: Framework-dependent, Oryx-enabled
