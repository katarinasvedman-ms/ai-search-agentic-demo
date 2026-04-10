# SecureRagChat — Secure RAG/Chat Backend with Per-User Authorization

ASP.NET Core backend that implements a secure Retrieval-Augmented Generation (RAG) pipeline with per-user authorization enforced at the Azure AI Search layer. It supports two retrieval modes:

- `Traditional`: explicit Azure AI Search index queries (public or entitled) with optional Bing fallback for anonymous requests.
- `Agentic`: Azure AI Search Knowledge Base retrieval via a single retrieve call.

Anonymous users can fall back to Bing-backed public retrieval when curated public Azure AI Search content is unavailable.

## Architecture

```
┌─────────────┐     ┌─────────────────────┐     ┌────────────────────┐     ┌─────────────────┐
│  Client      │────▶│  ChatController     │────▶│  ChatOrchestrator  │────▶│  Azure AI Search │
│  (anon/auth) │     │  POST /api/chat     │     │                    │     │  (public/ent.)  │
└─────────────┘     └─────────────────────┘     │  1. Auth inspect   │     └─────────────────┘
                                                 │  2. Route retrieve │             │
                                                 │  3. Generate       │             │ public miss/failure
                                                 │  4. Assemble       │             ▼
                                                 └────────────────────┘     ┌─────────────────┐
                                                                            │   Bing Search    │
                                                                            │  (anonymous)     │
                                                                            └─────────────────┘
                                                                                     │
                                                                                     ▼
                                                                            ┌─────────────────┐
                                                                            │  Azure OpenAI    │
                                                                            │  Responses API   │
                                                                            └─────────────────┘
```

### Key principle: Retrieval before generation

The model **never** sees unauthorized data. Authorization is enforced by Azure AI Search using RBAC and security trimming headers — not by prompts or model logic.

## Flows

### Anonymous Flow

1. Client sends `POST /api/chat` with no `Authorization` header
2. `UserTokenAccessor` returns `null` (no token)
3. Orchestrator selects **Public** retrieval plane
4. `AzureSearchRetrievalService` queries the **public index**
   - Sets `Authorization: Bearer <service-token>` (app's own credential)
   - Does **NOT** set `x-ms-query-source-authorization`
5. If curated public content is unavailable, the orchestrator falls back to `BingRetrievalService`
6. Retrieved chunks are passed to Azure OpenAI Responses API
7. Grounded answer returned with citations

### Authenticated Flow

1. Client sends `POST /api/chat` with `Authorization: Bearer <user-token>`
2. JWT is validated by ASP.NET Core middleware (Microsoft Identity Web)
3. `UserTokenAccessor` extracts the user token
4. Orchestrator selects **Entitled** retrieval plane
5. `AzureSearchRetrievalService` queries the **entitled index**
   - Sets `Authorization: Bearer <service-token>` (app's own credential)
   - Sets `x-ms-query-source-authorization: Bearer <user-token>` (user's identity)
6. Azure AI Search enforces per-user security trimming — only returns documents the user is authorized to see
7. Retrieved chunks (already filtered by Search) are passed to Azure OpenAI Responses API
8. Grounded answer returned with citations

### Agentic Flow

1. Client sends `POST /api/chat` with `"mode": "Agentic"`
2. Orchestrator still determines retrieval plane (`Public` or `Entitled`) from auth context
3. `AgenticRetrievalService` issues one Knowledge Base retrieve call
4. For entitled requests, the service forwards `x-ms-query-source-authorization` with the caller token
5. Retrieved chunks and references are normalized into the existing retrieval contract
6. Chunks are passed to Azure OpenAI Responses API for final answer generation

> Traditional mode remains the clearest proof path because its filters and ranking settings are explicit in the app. Agentic mode now forwards the same caller token to the Knowledge Base retrieve API while still demonstrating platform-managed retrieval orchestration.

## Why authorization is enforced in Search, not by the model

- **LLMs cannot enforce access control.** If sensitive data reaches the model's context, it may leak into the response regardless of prompt instructions.
- **Azure AI Search supports security trimming** via the `x-ms-query-source-authorization` header, which lets the search service evaluate the user's identity against document-level ACLs.
- **The model only sees chunks that passed Search's authorization check.** This is a defense-in-depth approach where the security boundary is at the data retrieval layer.

## Why MAF is used only for orchestration

Microsoft Agent Framework provides the structural pattern for the orchestration pipeline, but the agent is **deterministic** — it does not autonomously decide which tools to call or what data to access. The flow is:

1. Inspect auth context (deterministic)
2. Call retrieval service (deterministic)
3. Call LLM with retrieved chunks (deterministic)
4. Assemble and return response (deterministic)

No autonomous tool-calling. No hidden decisions. Full control over what data the model sees.

## Why NOT using Foundry AI Search tool

The Foundry built-in Azure AI Search tool does not support passing per-user authorization headers (`x-ms-query-source-authorization`). To enforce per-user security trimming, the retrieval call must be made directly via HttpClient with explicit header control.

## Where `x-ms-query-source-authorization` is applied

In both `AzureSearchRetrievalService.RetrieveAsync()` and `AgenticRetrievalService.RetrieveAsync()`:

```csharp
if (userToken is not null)
{
  request.Headers.Add("x-ms-query-source-authorization", userToken);
}
```

This header is **only** added for authenticated users. Anonymous users never have this header set, and they stay on the public retrieval path.

## Anonymous Bing Fallback

- Bing is used only for **anonymous public retrieval**.
- The orchestrator remains deterministic: it explicitly chooses when to fall back to Bing.
- Bing results are normalized into the same `RetrievedChunk` shape before generation.
- Authenticated requests never use Bing, so protected content stays behind Azure AI Search authorization.

## API

### `POST /api/chat`

**Request:**
```json
{
  "query": "What are our Q4 revenue numbers?",
  "conversationId": "optional-id",
  "preferEntitledContent": true,
  "mode": "Traditional"
}
```

`mode` values:
- `Traditional` (default when omitted)
- `Agentic`

**Response:**
```json
{
  "answer": "Based on the available documents, Q4 revenue was... [Source 1]",
  "retrievalPlane": "Entitled",
  "citations": [
    { "title": "Q4 Financial Report", "url": "https://...", "sourceIndex": 1 }
  ],
  "diagnostics": {
    "chunkCount": 5,
    "isAuthenticated": true,
    "retrievalMode": "Traditional",
    "retrievalSource": "AzureSearch"
  }
}
```

**Headers:**
- Request: `X-Correlation-Id` (optional, auto-generated if absent)
- Response: `X-Correlation-Id` (echoed back)

## Configuration

Update `appsettings.json` with your Azure resource details:

| Section | Key | Description |
|---------|-----|-------------|
| `AzureAd` | `TenantId` | Your Entra tenant ID |
| `AzureAd` | `ClientId` | App registration client ID |
| `AzureAd` | `Audience` | API audience URI |
| `AzureSearch` | `Endpoint` | Search service URL |
| `AzureSearch` | `PublicIndex` | Index for anonymous users |
| `AzureSearch` | `EntitledIndex` | Index with per-user ACLs |
| `AzureSearch` | `UseLoggedInDeveloperIdentityForUserToken` | Enables local dev fallback to Azure CLI user token |
| `AgenticRetrieval` | `Endpoint` | Search service URL for Knowledge Base retrieve API |
| `AgenticRetrieval` | `KnowledgeBaseName` | Knowledge Base name for `mode=Agentic` |
| `AgenticRetrieval` | `ApiVersion` | KB retrieve API version (default `2025-11-01-preview`) |
| `BingSearch` | `Enabled` | Enables anonymous Bing fallback |
| `BingSearch` | `Endpoint` | Bing Web Search endpoint |
| `BingSearch` | `ApiKey` | Bing API key |
| `BingSearch` | `Market` | Bing market (e.g., `en-US`) |
| `BingSearch` | `Count` | Max Bing results returned |
| `AzureOpenAI` | `Endpoint` | Azure OpenAI resource URL |
| `AzureOpenAI` | `Model` | Deployment name (e.g., `gpt-4o`) |

## Required Azure Setup

### 1. Azure AI Search
- Create a Search service with **RBAC authentication** enabled
- Create two indexes:
  - **Public index**: Contains publicly accessible content
  - **Entitled index**: Contains content with document-level security fields (e.g., `authorized_users`, `authorized_groups`)
- Configure security trimming on the entitled index
- Grant the app's managed identity the **Search Index Data Reader** role

### 1b. Azure AI Search Agentic Retrieval
- Create a Knowledge Source and Knowledge Base in the same Search service
- Set `AgenticRetrieval:KnowledgeBaseName` to that KB name
- Set `AgenticRetrieval:OutputMode` to `extractiveData`
- In the knowledge source, include citation-friendly `source_data_fields` such as:
  - `id`
  - `title`
  - `url`
  - optional locator (`page_number` or `section`)
- Ensure the backend identity can call the KB retrieve endpoint

### 2. Entra ID (Azure AD)
- Register an app for this API (used as `ClientId`/`Audience`)
- Configure the app to accept user tokens (JWT Bearer)
- Users authenticating to this API must have tokens that Azure AI Search can evaluate for security trimming

### 3. Azure OpenAI
- Deploy an Azure OpenAI resource
- Deploy a model (e.g., `gpt-4o`)
- Grant the app's managed identity the **Cognitive Services OpenAI User** role

### 4. Bing Search
- Provision Bing Search or another supported Bing Web Search endpoint
- Store the API key securely (environment variable or user secrets preferred over plain config)
- Enable Bing fallback only for anonymous/public retrieval

### 5. App Identity
- Use Managed Identity (recommended) or a service principal
- The app needs:
  - `Search Index Data Reader` on the Search service
  - `Cognitive Services OpenAI User` on the OpenAI resource

## Running Locally

```bash
cd SecureRagChat
dotnet run
```

The API will be available at `https://localhost:5001/api/chat`.

For local development, `DefaultAzureCredential` will use Azure CLI credentials:
```bash
az login
```

## Demo Provisioning Assets

This repository now includes repeatable assets for the Traditional vs Agentic retrieval demo:

- `infra/main.bicep` - provisions core Azure resources (Search, OpenAI, Storage)
- `infra/main.parameters.example.json` - example deployment parameters
- `scripts/deploy-infra.ps1` - deploys the Bicep template to a resource group
- `scripts/create-search-indexes.ps1` - creates `public-index` and `entitled-index`
- `scripts/seed-search-docs.ps1` - uploads curated demo documents
- `scripts/bootstrap-local-config.ps1` - sets backend user-secrets and prints frontend env values
- `demo-data/public-documents.json` - guest-safe product and solution discovery documents
- `demo-data/entitled-documents.json` - protected manuals, detail sheets, and runbooks for entitlement scenarios
- `demo-data/knowledge-base/` - richer source documents for Azure AI Search Knowledge Base ingestion
- `DEMO_SETUP.md` - end-to-end runbook for provisioning and rehearsal

## Future Improvements

1. **Caching**: Cache retrieval results for identical queries from the same user to reduce Search calls
2. **Ranking tuning**: Fine-tune semantic ranking behavior or add custom scoring profiles in Azure AI Search
3. **Conversation memory**: Add multi-turn support with conversation history stored in Cosmos DB or in-memory
4. **Evaluation**: Add automated evaluation of answer quality, groundedness, and citation accuracy
5. **Streaming**: Support streamed responses from Azure OpenAI for lower time-to-first-token
6. **Rate limiting**: Add ASP.NET Core rate limiting middleware to protect against abuse
7. **Provider controls**: Add domain allowlists and deduplication between public search and Bing fallback

## Project Structure

```
SecureRagChat/
├── Api/
│   └── ChatController.cs          # POST /api/chat endpoint
├── Orchestration/
│   └── ChatOrchestrator.cs         # Deterministic RAG orchestration pipeline
├── Services/
│   ├── IRetrievalService.cs        # Retrieval interface
│   ├── IAgenticRetrievalService.cs # Agentic retrieval interface
│   ├── IBingRetrievalService.cs    # Bing retrieval interface
│   ├── AzureSearchRetrievalService.cs  # Azure AI Search with per-user headers
│   ├── AgenticRetrievalService.cs  # Azure AI Search Knowledge Base retrieval
│   ├── BingRetrievalService.cs     # Bing Web Search fallback for anonymous users
│   ├── IResponsesApiService.cs     # Generation interface
│   └── ResponsesApiService.cs      # Azure OpenAI Responses API client
├── Models/
│   ├── ChatRequest.cs              # API request DTO
│   ├── ChatResponse.cs             # API response DTO + diagnostics
│   ├── RetrievalMode.cs            # Traditional | Agentic enum
│   ├── RetrievedChunk.cs           # Search result chunk
│   ├── RetrievalResult.cs          # Retrieval result with metadata
│   ├── RetrievalPlane.cs           # Public | Entitled enum
│   ├── RetrievalSource.cs          # AzureSearch | Bing | KnowledgeBase enum
│   ├── Citation.cs                 # Citation model
│   └── GenerationResult.cs         # LLM response model
├── Auth/
│   └── UserTokenAccessor.cs        # Extracts user bearer token from HttpContext
├── Configuration/
│   ├── AzureSearchOptions.cs       # Search config options
│   ├── AgenticRetrievalOptions.cs  # Knowledge Base retrieval config options
│   ├── BingSearchOptions.cs        # Bing config options
│   └── AzureOpenAIOptions.cs       # OpenAI config options
├── Program.cs                      # DI, auth, middleware setup
└── appsettings.json                # Configuration (fill in your values)
```
