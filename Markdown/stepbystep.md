# RR Realty AI - Step-by-Step Recreation Guide

## 🎯 Overview
Complete guide to recreate the RR Realty AI application from scratch with Azure optimization and proper architecture.

## 📋 Prerequisites

### Development Environment
- **Visual Studio Code** with extensions:
  - C# Dev Kit
  - Azure Tools
  - ES7+ React/Redux/React-Native snippets
  - Prettier - Code formatter
- **Node.js** 18.20.8+ with npm 10.8.2+
- **.NET 9 SDK** (9.0.305+)
- **Azure CLI** 2.65.0+
- **Git** for version control

### Azure Resources Required
- **Azure App Service** (Standard tier minimum)
- **Azure OpenAI Service** with GPT-4o model deployment
- **Azure Key Vault** for secrets management
- **Azure Functions** for conversation persistence
- **Azure Storage Account** for document storage
- **Azure Active Directory** app registration

## 🏗️ Phase 1: Project Structure Setup (30 minutes)

### Step 1: Create Project Structure
```bash
mkdir rr-realty-ai-v2
cd rr-realty-ai-v2

# Create directory structure
mkdir -p Backend/{Controllers,Services,Models,Configuration}
mkdir -p Backend/Services/{Chat,Document,Auth,Storage}
mkdir -p Frontend/src/{components,services,types,hooks,utils}
mkdir -p Frontend/src/components/{Chat,Auth,Document,UI}
mkdir -p AzureFunctions/{ConversationPersistence,DocumentProcessing}
mkdir -p Scripts/{Deployment,Testing}
mkdir -p .github/workflows
```

### Step 2: Initialize Backend (.NET 9 Web API)
```bash
cd Backend
dotnet new webapi --name Backend --framework net9.0
dotnet add package Azure.AI.OpenAI --version 2.1.0
dotnet add package Azure.Identity --version 1.12.1
dotnet add package Azure.Security.KeyVault.Secrets --version 4.6.0
dotnet add package Microsoft.Identity.Web --version 3.2.2
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 9.0.0
dotnet add package DocumentFormat.OpenXml --version 3.1.0
dotnet add package PdfPig --version 0.1.9
dotnet add package DotNetEnv --version 3.1.1
dotnet add package Swashbuckle.AspNetCore --version 7.0.0
```

### Step 3: Initialize Frontend (React + TypeScript + Vite)
```bash
cd ../Frontend
npm create vite@latest . -- --template react-ts
npm install @azure/msal-browser@^4.22.1 @azure/msal-react@^3.0.19
npm install axios@^1.5.0 framer-motion@^10.16.4 lucide-react@^0.263.1
npm install react-markdown@^10.1.0 remark-gfm@^4.0.1
npm install -D @types/node@^18.17.0
```

### Step 4: Initialize Azure Functions
```bash
cd ../AzureFunctions
func init . --typescript
func new --name ConversationPersistence --template "HTTP trigger"
func new --name DocumentProcessing --template "HTTP trigger"
```

## 🔧 Phase 2: Backend Implementation (2 hours)

### Step 5: Configure Program.cs (< 400 lines)
Key configurations:
- Azure Key Vault integration
- Microsoft Identity Web authentication
- CORS for SPA
- Session management (30-day timeout)
- Swagger for development
- Service registrations

### Step 6: Implement Core Services

#### Chat Service (Services/Chat/ChatService.cs)
- Azure OpenAI integration
- Conversation management
- Message history
- Real estate context injection

#### Document Service (Services/Document/DocumentService.cs)
- PDF processing with PdfPig
- Word document processing with DocumentFormat.OpenXml
- Text extraction and chunking
- Azure Storage integration

#### Authentication Service (Services/Auth/AuthService.cs)
- Azure AD token validation
- User profile management
- Role-based access control

### Step 7: Create Controllers (< 400 lines each)

#### ChatController.cs
- POST /api/chat/message
- GET /api/chat/sessions
- POST /api/chat/sessions
- DELETE /api/chat/sessions/{id}

#### DocumentController.cs
- POST /api/documents/upload
- GET /api/documents
- DELETE /api/documents/{id}
- POST /api/documents/{id}/chat

#### HealthController.cs
- GET /api/health

## 🎨 Phase 3: Frontend Implementation (2.5 hours)

### Step 8: Configure Authentication (authConfig.ts)
```typescript
export const msalConfig = {
  auth: {
    clientId: process.env.VITE_AUTH_CLIENT_ID!,
    authority: process.env.VITE_AUTH_AUTHORITY!,
    redirectUri: process.env.VITE_AUTH_REDIRECT_URI!
  },
  cache: {
    cacheLocation: "sessionStorage",
    storeAuthStateInCookie: false
  }
};
```

### Step 9: Implement Core Components (< 400 lines each)

#### App.tsx
- Main application layout
- Authentication wrapper
- State management for chat sessions
- Document management

#### ChatInterface Component
- Message display with markdown support
- Input handling with loading states
- Real-time typing indicators
- Message history scrolling

#### Sidebar Component
- Chat session list
- New chat creation
- Session management (rename, delete)
- User profile display

#### DocumentSidebar Component
- Document upload interface
- Document list with previews
- Document-specific chat initiation

### Step 10: Implement Services

#### chatService.ts
- API communication for chat operations
- Local storage for session persistence
- Error handling and retry logic

#### documentService.ts
- File upload with progress tracking
- Document management operations
- Integration with chat service

## ☁️ Phase 4: Azure Functions Implementation (1 hour)

### Step 11: Conversation Persistence Function
```typescript
// ConversationPersistence/index.ts
// Handles saving/loading chat sessions to Azure Storage
// Triggered by HTTP requests from frontend
// Implements user-specific data isolation
```

### Step 12: Document Processing Function
```typescript
// DocumentProcessing/index.ts
// Processes uploaded documents
// Extracts text and creates searchable chunks
// Stores processed data in Azure Storage
```

## 🚀 Phase 5: Azure Deployment Setup (1 hour)

### Step 13: Configure App Service
```bash
# Create App Service
az webapp create --resource-group rg-innovation --plan asp-standard --name rr-realty-ai-v2 --runtime "DOTNET:9.0"

# Configure app settings (ENABLE ORYX for proper Azure deployment)
az webapp config appsettings set --resource-group rg-innovation --name rr-realty-ai-v2 --settings \
  "AZURE_OPENAI_ENDPOINT=https://gto4o.cognitiveservices.azure.com/" \
  "AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o" \
  "WEBSITE_NODE_DEFAULT_VERSION=18.20.8" \
  "WEBSITE_RUN_FROM_PACKAGE=1" \
  "SCM_DO_BUILD_DURING_DEPLOYMENT=true"
```

### Step 14: Setup GitHub Actions
Create `.github/workflows/azure-deploy.yml` with:
- Frontend build with production environment variables
- Backend publish with self-contained deployment
- Azure Web App deployment
- Automated testing

### Step 15: Configure Azure Resources
- **Key Vault**: Store OpenAI keys and connection strings
- **Storage Account**: Document storage and conversation persistence
- **Azure Functions**: Deploy conversation and document processing functions
- **Azure AD**: Configure app registration with proper redirect URIs

## 🧪 Phase 6: Testing & Validation (30 minutes)

### Step 16: Local Testing
```bash
# Backend
cd Backend && dotnet run

# Frontend
cd Frontend && npm run dev

# Azure Functions
cd AzureFunctions && func start
```

### Step 17: Production Deployment (Oryx-Optimized)
```bash
# Build Frontend for production
cd Frontend
npm run build

# Create source deployment package (let Oryx handle compilation)
cd ..
zip -r deployment.zip Backend/ Frontend/dist/ .deployment

# Deploy to Azure (Oryx will build and optimize)
az webapp deploy --resource-group rg-innovation --name rr-realty-ai-v2 --src-path deployment.zip --type zip

# Alternative: Direct source deployment
git push azure main  # If using Git deployment
```

#### Create .deployment file for Oryx guidance:
```ini
[config]
project = Backend/Backend.csproj
```

## 📊 Success Criteria

### Functional Requirements
- ✅ User authentication with Azure AD
- ✅ Real-time chat with Azure OpenAI GPT-4o
- ✅ Document upload and processing (PDF, Word)
- ✅ Document-specific chat conversations
- ✅ Conversation persistence and recall
- ✅ Real estate context and expertise
- ✅ Responsive design with brand colors

### Technical Requirements
- ✅ All files under 400 lines of code
- ✅ Feature isolation by directory
- ✅ Azure-optimized deployment with Oryx
- ✅ Framework-dependent .NET deployment (optimized)
- ✅ Production-ready authentication
- ✅ Proper error handling and logging

### Performance Requirements
- ✅ Page load time < 3 seconds
- ✅ Chat response time < 5 seconds
- ✅ Document upload progress tracking
- ✅ Smooth animations and transitions

## 🔧 Troubleshooting

### Common Issues
1. **Authentication Redirect Errors**: Ensure redirect URIs match exactly in Azure AD
2. **CORS Issues**: Configure proper origins in backend Program.cs
3. **Oryx Build Failures**: Ensure proper project structure and .deployment file
4. **OpenAI Rate Limits**: Implement proper retry logic and error handling
5. **Missing Dependencies**: Let Oryx handle runtime dependencies automatically

### Monitoring
- Application Insights for performance monitoring
- Azure Monitor for resource utilization
- Custom logging for business logic tracking

## 📝 Post-Deployment Tasks

1. **DNS Configuration**: Setup custom domain
2. **SSL Certificate**: Configure HTTPS
3. **Monitoring Setup**: Configure alerts and dashboards
4. **Backup Strategy**: Implement data backup procedures
5. **Documentation**: Update API documentation and user guides

---

**Estimated Total Time**: 6-7 hours
**Complexity Level**: Intermediate to Advanced
**Team Size**: 1-2 developers
