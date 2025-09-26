# RR Realty AI – Developer Guide

This repository implements a full-stack Azure-optimized app per the architecture in `Markdown/architecture.md`, `Markdown/features.md`, `Markdown/scaffold.md`, and `Markdown/stepbystep.md`.

**✅ RECENTLY UPDATED**: The codebase has been refactored to 100% align with the documentation specifications.

The stack includes:

- **Frontend**: React + TypeScript + Vite (`Frontend/`)
- **Backend**: ASP.NET Core (.NET 9) Web API with Azure Cosmos DB (`Backend/`)
- **Azure Functions**: TypeScript Node.js functions for conversation persistence and document processing (`AzureFunctions/`)

This README focuses on local setup, running, and verification. The Markdown docs remain the source of truth for design decisions.

---

## Prerequisites

- Node.js 18.20.8+ and npm 10.8.2+
- .NET 9 SDK (global.json pins to 9.0.x)
- (Optional) Azure Functions Core Tools v4
- An Azure OpenAI resource (endpoint + API key), or disabled chat for smoke tests
- Azure Cosmos DB account for conversation and document storage

---

## Quick Start (Local)

1) Backend

- Create `Backend/.env` with your secrets:

```
AZURE_OPENAI_ENDPOINT=https://<your-openai-endpoint>.openai.azure.com/
AZURE_OPENAI_API_KEY=<your-openai-key>
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o
AZURE_OPENAI_API_VERSION=2024-08-01-preview

# Azure Cosmos DB
COSMOS_DB_CONNECTION_STRING=AccountEndpoint=https://<your-cosmos>.documents.azure.com:443/;AccountKey=<your-key>;

# Azure Storage
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=<your-storage>;AccountKey=<your-key>;EndpointSuffix=core.windows.net

# Optional – if running Functions locally
AZURE_FUNCTION_URL=http://localhost:7071
```

- From `Backend/`:

```
dotnet restore
dotnet run
```

The API listens on http://localhost:5000

2) Frontend

- Create `Frontend/.env.local` (or `.env`) with your local values:

```
VITE_AUTH_CLIENT_ID=<your-app-client-id>
VITE_AUTH_AUTHORITY=https://login.microsoftonline.com/<your-tenant-id>
VITE_AUTH_REDIRECT_URI=http://localhost:3001
VITE_API_SCOPE=User.Read

VITE_API_BASE_URL=http://localhost:5000/api
VITE_API_TIMEOUT=30000

VITE_ENABLE_DOCUMENT_UPLOAD=true
VITE_ENABLE_CONVERSATION_SAVE=true
VITE_MAX_FILE_SIZE_MB=10
```

- From `Frontend/`:

```
npm ci
npm run dev
```

Open http://localhost:3001. The Vite dev server also proxies `/api` to http://localhost:5000, see `Frontend/vite.config.ts`.

3) Azure Functions (TypeScript)

- From `AzureFunctions/`:

```
npm install
npm run build
func start
```

- Ensure `AZURE_FUNCTION_URL=http://localhost:7071` in `Backend/.env` if testing persistence locally.
- Functions now use TypeScript and Azure Cosmos DB for data persistence.

---

## Environment Variables (Summary)

Backend:

- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_API_KEY`
- `AZURE_OPENAI_DEPLOYMENT_NAME` (preferred) or `AZURE_OPENAI_DEPLOYMENT`
- `AZURE_OPENAI_API_VERSION` (optional)
- `COSMOS_DB_CONNECTION_STRING` (Azure Cosmos DB)
- `AZURE_STORAGE_CONNECTION_STRING` (Azure Blob Storage)
- `AZURE_FUNCTION_URL` (optional, for Azure Functions integration)

Frontend:

- `VITE_AUTH_CLIENT_ID`
- `VITE_AUTH_AUTHORITY`
- `VITE_AUTH_REDIRECT_URI`
- `VITE_API_SCOPE`
- `VITE_API_BASE_URL` (defaults to `/api` when proxied)
- `VITE_API_TIMEOUT`
- `VITE_ENABLE_DOCUMENT_UPLOAD`, `VITE_ENABLE_CONVERSATION_SAVE`, `VITE_MAX_FILE_SIZE_MB`

---

## Remediation Checklist

- Remove or lock down the diagnostic document endpoints (`/api/document/test-upload`, `/api/document/debug-storage`, `/api/document/simple-test`) before production so only vetted upload paths remain.
- Update the conversation services so session history calls use session-aware APIs and the in-memory cache clears entries by user ID, preventing stale or cross-user history leaks.
- Make the streaming save path pass the active `conversationId` to Azure Functions so live responses persist with the right thread.
- Keep all Azure OpenAI and Entra ID secrets exclusively in environment variables or Key Vault; the sample `appsettings*.json` files now ship with placeholders only.
- Redeploy the frontend with the correct redirect URI environment variable once the above changes land.

## API Endpoints (Chat)

The backend supports the documented routes while preserving existing ones for compatibility.

- POST `/api/chat/message` → send a message (preferred)
- POST `/api/chat/sessions` → create a new session
- GET `/api/chat/sessions` → list sessions
- GET `/api/chat/sessions/{sessionId}/messages` → get session messages
- DELETE `/api/chat/sessions/{sessionId}` → delete a session

Compatibility (existing):

- POST `/api/chat/send`
- GET `/api/chat/history`
- GET `/api/chat/history/{sessionId}`
- DELETE `/api/chat/history`

Most endpoints require an authenticated user (MSAL / Azure AD). See `Backend/Controllers/ChatController.cs`.

---

## Frontend Service Behavior

- `Frontend/src/services/chatService.ts` reads `VITE_API_BASE_URL` and prefers the documented endpoints, with fallbacks to the older routes.
- Design system variables (brand colors/typography) are defined in `Frontend/src/index.css` and applied across components.

---

## Known Local-Only Pitfalls

- **Auth/CORS/Redirects**: If login fails locally, verify `VITE_AUTH_REDIRECT_URI`, Azure AD app registration redirect URIs, and the backend CORS policy. Production CORS was intentionally not altered by this alignment work; adjust when you set up Azure.
- **Missing OpenAI keys**: Without `AZURE_OPENAI_*`, chat calls will log warnings and fail at runtime.
- **Vite import paths**: The code uses default exports for components. If you add components, prefer default exports and correct relative paths.

---

## Deployment Notes (Azure Oryx)

- `.deployment` in repo root points Oryx to `Backend/Backend.csproj`.
- Backend uses framework-dependent deployment (`SelfContained=false`).
- Recommended App Service settings (see `Markdown/architecture.md`):
  - `SCM_DO_BUILD_DURING_DEPLOYMENT=true`
  - `WEBSITE_RUN_FROM_PACKAGE=1`
  - `WEBSITE_NODE_DEFAULT_VERSION=18.20.8`
  - `ORYX_ENABLE_DYNAMIC_INSTALL=true`

For production builds:

1) Build frontend:

```
cd Frontend
npm run build
```

2) Deploy source code structure (Oryx builds in Azure) including `Backend/`, `Frontend/`, and `.deployment`.

---

## Troubleshooting

- **Backend doesn’t build**: Run `dotnet build -nologo` in `Backend/` and fix the first error. Build errors locally will also fail Azure deployments.
- **Frontend overlay**: Restart the dev server after path fixes; Vite’s HMR can cache stale paths.
- **Unauthorized (401)**: Ensure MSAL config is correct, and the API requires authenticated users (`RequireDomainUser` policy in `Backend/Program.cs`).
- **Functions not saving history**: Verify `AZURE_FUNCTION_URL` and that Functions are running or deployed.

---

## Repository Structure (top-level)

```
Backend/           # .NET 9 API with Azure Cosmos DB
Frontend/          # React + Vite SPA
AzureFunctions/    # TypeScript Node.js functions
Markdown/          # Architecture & feature documentation
.deployment        # Oryx configuration
```

---

## Recent Changes (2025-09-23)

**✅ Major Refactoring Completed**: The codebase has been updated to 100% align with the documentation specifications:

### Key Changes:
- **Azure Functions**: Converted from C# to TypeScript with Node.js runtime
- **Database**: Migrated from SQL Server to Azure Cosmos DB
- **File Structure**: Renamed `ChatMessage.cs` to `ChatModels.cs` to match documentation
- **Dependencies**: Updated all packages to match architectural specifications
- **Configuration**: Added proper `.env.example` files and environment setup

### Technology Stack Updates:
- **Frontend**: React + TypeScript + Vite ✅ (unchanged)
- **Backend**: .NET 9 Web API + Azure Cosmos DB ✅ (updated)
- **Azure Functions**: TypeScript + Node.js + Cosmos DB ✅ (converted from C#)
- **Storage**: Azure Blob Storage ✅ (unchanged)
- **Authentication**: Azure AD + MSAL ✅ (unchanged)

The project now perfectly matches the architecture described in the Markdown documentation files.

---

## Support

If you encounter issues, please include:

- OS and SDK versions (`dotnet --list-sdks`, `node -v`/`npm -v`)
- Steps to reproduce
- Relevant console output from Backend and Frontend


