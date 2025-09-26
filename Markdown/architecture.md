# RR Realty AI - Architecture Documentation

## 🏗️ System Architecture Overview

### High-Level Architecture
```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Frontend      │    │     Backend      │    │  Azure Services │
│   React SPA     │◄──►│   .NET 9 API     │◄──►│   OpenAI/KeyVault│
│                 │    │                  │    │   Storage/Functions│
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

### Deployment Architecture
- **Frontend**: Built by Vite, served as static files from Backend wwwroot
- **Backend**: Framework-dependent .NET 9 deployment optimized by Oryx
- **Functions**: Separate Azure Functions app for background processing
- **Storage**: Azure Storage Account for documents and conversation data
- **Build System**: Azure Oryx handles compilation and runtime optimization, deploy through github

## 📦 Technology Stack

### Frontend Dependencies
```json
{
  "name": "rr-realty-ai-frontend",
  "version": "2.0.0",
  "type": "module",
  "engines": {
    "node": ">=18.20.8",
    "npm": ">=10.8.2"
  },
  "dependencies": {
    "react": "^18.2.0",
    "react-dom": "^18.2.0",
    "@azure/msal-browser": "^4.22.1",
    "@azure/msal-react": "^3.0.19",
    "axios": "^1.5.0",
    "framer-motion": "^10.16.4",
    "lucide-react": "^0.263.1",
    "react-markdown": "^10.1.0",
    "remark-gfm": "^4.0.1"
  },
  "devDependencies": {
    "@types/node": "^18.17.0",
    "@types/react": "^18.2.15",
    "@types/react-dom": "^18.2.7",
    "@typescript-eslint/eslint-plugin": "^6.0.0",
    "@typescript-eslint/parser": "^6.0.0",
    "@vitejs/plugin-react": "^4.0.3",
    "eslint": "^8.45.0",
    "eslint-plugin-react-hooks": "^4.6.0",
    "eslint-plugin-react-refresh": "^0.4.3",
    "typescript": "^5.0.2",
    "vite": "^4.4.5"
  }
}
```

### Backend Dependencies (.NET 9)
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PublishSingleFile>false</PublishSingleFile>
    <SelfContained>false</SelfContained>
    <!-- Let Oryx handle runtime dependencies -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Azure.AI.OpenAI" Version="2.1.0" />
    <PackageReference Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="1.3.2" />
    <PackageReference Include="Azure.Identity" Version="1.12.1" />
    <PackageReference Include="Azure.Security.KeyVault.Secrets" Version="4.6.0" />
    <PackageReference Include="Azure.Storage.Blobs" Version="12.21.2" />
    <PackageReference Include="DocumentFormat.OpenXml" Version="3.1.0" />
    <PackageReference Include="DotNetEnv" Version="3.1.1" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.0" />
    <PackageReference Include="Microsoft.Identity.Web" Version="3.2.2" />
    <PackageReference Include="PdfPig" Version="0.1.9" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="7.0.0" />
  </ItemGroup>
</Project>
```

### Azure Functions Dependencies
```json
{
  "name": "rr-realty-ai-functions",
  "version": "2.0.0",
  "dependencies": {
    "@azure/functions": "^4.5.1",
    "@azure/storage-blob": "^12.24.0",
    "@azure/cosmos": "^4.1.1",
    "axios": "^1.7.7"
  },
  "devDependencies": {
    "@azure/functions-core-tools": "^4.0.6",
    "@types/node": "^18.19.54",
    "typescript": "^5.6.2"
  }
}
```

## 🔧 Configuration Management

### Environment Variables (Development)
```bash
# Azure OpenAI Configuration
AZURE_OPENAI_ENDPOINT=https://gto4o.cognitiveservices.azure.com/
AZURE_OPENAI_API_KEY=your-api-key-here
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o
AZURE_OPENAI_API_VERSION=2024-08-01-preview

# Azure AD Authentication
AZURE_CLIENT_ID=d4c452c4-5324-40ff-b43b-25f3daa2a45c
AZURE_TENANT_ID=99848873-e61d-44cc-9862-d05151c567ab
AZURE_CLIENT_SECRET=your-client-secret-here

# Frontend Environment Variables
VITE_AUTH_CLIENT_ID=d4c452c4-5324-40ff-b43b-25f3daa2a45c
VITE_AUTH_AUTHORITY=https://login.microsoftonline.com/99848873-e61d-44cc-9862-d05151c567ab
VITE_AUTH_REDIRECT_URI=https://testing.rrrealty.ai
VITE_API_SCOPE=User.Read
VITE_API_BASE_URL=https://site-net-rrai-blue-fsgabaardkdhhnhf.centralus-01.azurewebsites.net
```

### Azure App Service Configuration
```bash
# Runtime Configuration (Oryx Enabled)
WEBSITE_NODE_DEFAULT_VERSION=18.20.8
SCM_DO_BUILD_DURING_DEPLOYMENT=true
WEBSITE_RUN_FROM_PACKAGE=1
ORYX_ENABLE_DYNAMIC_INSTALL=true

# Session Configuration
SESSION_TIMEOUT_DAYS=30
COOKIE_SECURE_POLICY=Always
COOKIE_SAME_SITE=Lax

# Logging Configuration
ASPNETCORE_ENVIRONMENT=Production
LOGGING_LEVEL_DEFAULT=Information
LOGGING_LEVEL_MICROSOFT=Warning
```

## 🏛️ Project Structure

### Backend Structure (Feature-Isolated)
```
Backend/
├── Program.cs                          # Application entry point (< 400 lines)
├── Backend.csproj                      # Project dependencies
├── appsettings.json                    # Base configuration
├── appsettings.Development.json        # Development overrides
├── web.config                          # IIS configuration
├── Controllers/                        # API endpoints (< 400 lines each)
│   ├── ChatController.cs              # Chat operations
│   ├── DocumentController.cs          # Document management
│   └── HealthController.cs            # Health checks
├── Services/                           # Business logic (< 400 lines each)
│   ├── Chat/
│   │   ├── IChatService.cs            # Chat service interface
│   │   ├── ChatService.cs             # Azure OpenAI integration
│   │   └── ConversationManager.cs     # Session management
│   ├── Document/
│   │   ├── IDocumentService.cs        # Document service interface
│   │   ├── DocumentService.cs         # File processing
│   │   ├── PdfProcessor.cs            # PDF text extraction
│   │   └── WordProcessor.cs           # Word document processing
│   ├── Auth/
│   │   ├── IAuthService.cs            # Auth service interface
│   │   └── AuthService.cs             # Azure AD integration
│   └── Storage/
│       ├── IStorageService.cs         # Storage interface
│       └── AzureStorageService.cs     # Azure Storage operations
├── Models/                             # Data models (< 400 lines each)
│   ├── ChatModels.cs                  # Chat-related DTOs
│   ├── DocumentModels.cs              # Document-related DTOs
│   └── UserModels.cs                  # User-related DTOs
└── Configuration/
    ├── AzureOpenAIConfig.cs           # OpenAI configuration
    ├── AuthConfig.cs                  # Authentication configuration
    └── StorageConfig.cs               # Storage configuration
```

### Frontend Structure (Component-Isolated)
```
Frontend/
├── src/
│   ├── main.tsx                       # Application entry point
│   ├── App.tsx                        # Main app component (< 400 lines)
│   ├── App.css                        # Global styles
│   ├── index.css                      # CSS variables and reset
│   ├── authConfig.ts                  # MSAL configuration
│   ├── components/                    # React components (< 400 lines each)
│   │   ├── Chat/
│   │   │   ├── ChatInterface.tsx      # Main chat interface
│   │   │   ├── MessageList.tsx        # Message display
│   │   │   ├── MessageInput.tsx       # Input handling
│   │   │   └── TypingIndicator.tsx    # Loading states
│   │   ├── Auth/
│   │   │   ├── AuthWrapper.tsx        # Authentication wrapper
│   │   │   ├── LoginButton.tsx        # Login component
│   │   │   └── UserProfile.tsx        # User info display
│   │   ├── Document/
│   │   │   ├── DocumentSidebar.tsx    # Document management
│   │   │   ├── DocumentUpload.tsx     # File upload
│   │   │   ├── DocumentList.tsx       # Document listing
│   │   │   └── DocumentPreview.tsx    # Document preview
│   │   └── UI/
│   │       ├── Sidebar.tsx            # Navigation sidebar
│   │       ├── Button.tsx             # Reusable button
│   │       ├── Modal.tsx              # Modal component
│   │       └── LoadingSpinner.tsx     # Loading indicator
│   ├── services/                      # API services (< 400 lines each)
│   │   ├── chatService.ts             # Chat API calls
│   │   ├── documentService.ts         # Document API calls
│   │   └── authService.ts             # Authentication helpers
│   ├── types/                         # TypeScript definitions
│   │   ├── chat.ts                    # Chat-related types
│   │   ├── document.ts                # Document-related types
│   │   └── user.ts                    # User-related types
│   ├── hooks/                         # Custom React hooks
│   │   ├── useChat.ts                 # Chat state management
│   │   ├── useDocuments.ts            # Document state management
│   │   └── useAuth.ts                 # Authentication state
│   └── utils/                         # Utility functions
│       ├── constants.ts               # Application constants
│       ├── formatters.ts              # Data formatting
│       └── validators.ts              # Input validation
├── public/
│   ├── index.html                     # HTML template
│   ├── favicon.ico                    # App icon
│   └── manifest.json                  # PWA manifest
├── package.json                       # Dependencies and scripts
├── tsconfig.json                      # TypeScript configuration
├── vite.config.ts                     # Vite build configuration
└── .env.example                       # Environment variables template
```

### Azure Functions Structure
```
AzureFunctions/
├── ConversationPersistence/
│   ├── index.ts                       # HTTP trigger function (< 400 lines)
│   └── function.json                  # Function configuration
├── DocumentProcessing/
│   ├── index.ts                       # Document processing (< 400 lines)
│   └── function.json                  # Function configuration
├── host.json                          # Functions host configuration
├── local.settings.json                # Local development settings
└── package.json                       # Function dependencies
```

## 🎨 Design System

### Brand Color Palette
```css
:root {
  /* Primary Brand Colors */
  --color-primary: #165540;           /* Deep forest green */
  --color-primary-light: #b4ceb3;     /* Light sage green */
  --color-primary-dark: #013220;      /* Dark forest green */
  
  /* Secondary Colors */
  --color-secondary: #3a668c;         /* Professional blue */
  --color-secondary-light: #b6ccd7;   /* Light blue-gray */
  --color-secondary-dark: #192839;    /* Dark navy */
  
  /* Accent Colors */
  --color-accent: #e6b751;            /* Warm gold */
  --color-accent-light: #f2d084;      /* Light gold */
  --color-accent-dark: #d49e2a;       /* Dark gold */
  
  /* Neutral Grays */
  --color-gray-50: #f8faf9;           /* Lightest gray */
  --color-gray-100: #f1f5f3;          /* Very light gray */
  --color-gray-200: #e2ebe6;          /* Light gray */
  --color-gray-300: #c8d6cc;          /* Medium-light gray */
  --color-gray-400: #9bb0a1;          /* Medium gray */
  --color-gray-500: #6d8574;          /* Medium-dark gray */
  --color-gray-600: #4a5d50;          /* Dark gray */
  --color-gray-700: #2d3a32;          /* Very dark gray */
  --color-gray-800: #192839;          /* Almost black */
  --color-gray-900: #013220;          /* Darkest */
  
  /* Semantic Colors */
  --color-success: #10b981;           /* Success green */
  --color-warning: #f59e0b;           /* Warning amber */
  --color-error: #ef4444;             /* Error red */
  --color-info: #3b82f6;              /* Info blue */
}
```

### Typography System
```css
/* Font Stack */
font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;

/* Font Sizes */
--font-size-xs: 0.75rem;     /* 12px */
--font-size-sm: 0.875rem;    /* 14px */
--font-size-base: 1rem;      /* 16px */
--font-size-lg: 1.125rem;    /* 18px */
--font-size-xl: 1.25rem;     /* 20px */
--font-size-2xl: 1.5rem;     /* 24px */
--font-size-3xl: 1.875rem;   /* 30px */
--font-size-4xl: 2.25rem;    /* 36px */

/* Font Weights */
--font-weight-normal: 400;
--font-weight-medium: 500;
--font-weight-semibold: 600;
--font-weight-bold: 700;
```

### Component Specifications
- **Border Radius**: 8px for cards, 6px for buttons, 4px for inputs
- **Shadows**: Subtle box-shadows with rgba(0,0,0,0.1) opacity
- **Animations**: 200ms ease-in-out transitions
- **Spacing**: 8px base unit (0.5rem, 1rem, 1.5rem, 2rem, 3rem)

## 🔒 Security Architecture

### Authentication Flow
1. **Azure AD Integration**: Microsoft Identity Web for backend or use simplest method
2. **MSAL Browser**: Frontend authentication with PKCE
3. **JWT Tokens**: Secure API communication
4. **Session Management**: 30-day sliding expiration

### Data Protection
- **Encryption at Rest**: Azure Storage encryption
- **Encryption in Transit**: HTTPS/TLS 1.2+
- **Key Management**: Azure Key Vault for secrets
- **Access Control**: Role-based permissions

### API Security
- **CORS Configuration**: Restricted origins
- **Rate Limiting**: Per-user request limits
- **Input Validation**: Comprehensive data validation
- **Error Handling**: Secure error responses

## 📊 Performance Specifications

### Frontend Performance
- **Bundle Size**: < 2MB total
- **First Contentful Paint**: < 2 seconds
- **Time to Interactive**: < 3 seconds
- **Core Web Vitals**: All metrics in "Good" range

### Backend Performance
- **API Response Time**: < 500ms for standard requests
- **Chat Response Time**: < 5 seconds for AI responses
- **Document Processing**: < 30 seconds for typical documents
- **Concurrent Users**: Support 100+ simultaneous users

### Azure Resource Sizing
- **App Service**: Standard S1 minimum (1 core, 1.75GB RAM)
- **Storage Account**: Standard LRS with hot tier
- **Functions**: Consumption plan with 512MB memory
- **Key Vault**: Standard tier

## 🔄 Data Flow Architecture

### Chat Flow
```
User Input → Frontend → Backend API → Azure OpenAI → Response → Frontend → Display
                    ↓
            Conversation Storage (Azure Functions) → Azure Storage
```

### Document Flow
```
File Upload → Frontend → Backend API → Document Processing → Text Extraction
                                   ↓
                            Azure Storage → Searchable Chunks → Chat Context
```

### Authentication Flow
```
User Login → Azure AD → MSAL → JWT Token → Backend Validation → Authorized Access
```

---

**Architecture Version**: 2.0
**Last Updated**: 2025-09-22
**Compliance**: Azure Well-Architected Framework
