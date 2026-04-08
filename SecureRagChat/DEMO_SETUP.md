# Demo Provisioning Runbook

This runbook sets up the Azure resources and curated data required for the Traditional vs Agentic retrieval demo.

## 1. Prerequisites

- Azure CLI logged in: `az login`
- Resource group already created
- .NET SDK installed (for user-secrets bootstrap)
- Access to deploy Azure AI Search and Azure OpenAI resources

## 2. Deploy Infrastructure

From `SecureRagChat`:

```powershell
pwsh ./scripts/deploy-infra.ps1 -ResourceGroupName <rg-name> -ParametersFile ./infra/main.parameters.example.json
```

The template provisions:

- Azure AI Search service
- Azure OpenAI account and model deployment
- Storage account (for optional content staging)

## 3. Create Traditional Search Indexes

```powershell
pwsh ./scripts/create-search-indexes.ps1 -SearchServiceName <search-service-name>
```

This creates:

- `public-index`
- `entitled-index`

The index schema includes a `content` field and a semantic configuration named `default` used by Traditional mode. If you have already created the indexes, recreate them before reseeding so semantic ranking and richer full-document text are both available to retrieval.

## 4. Seed Curated Demo Data

```powershell
pwsh ./scripts/seed-search-docs.ps1 -SearchServiceName <search-service-name>
```

This uploads:

- `demo-data/public-documents.json` -> `public-index`
- `demo-data/entitled-documents.json` -> `entitled-index`

The sample corpus is organized to mirror a telecom product experience:

- Public index: guest-safe product pages, solution briefs, and service overviews
- Entitled index: protected manuals, detail sheets, compatibility notes, and runbooks

## 5. Configure Backend and Frontend Locally

```powershell
pwsh ./scripts/bootstrap-local-config.ps1 `
  -SearchServiceName <search-service-name> `
  -OpenAiEndpoint https://<openai-name>.openai.azure.com `
  -OpenAiModel gpt-4.1 `
  -KnowledgeBaseName <knowledge-base-name> `
  -UseDeveloperIdentityFallback
```

Then set frontend env values in `secureragchat-web/.env`.

## 6. Agentic Retrieval Setup

Create a knowledge source and knowledge base in Azure AI Search, then set:

- `AgenticRetrieval:KnowledgeBaseName`

Suggested source material for the knowledge base is included under `demo-data/knowledge-base/`.

Current app behavior:

- Traditional mode demonstrates per-user security trimming via `x-ms-query-source-authorization`.
- Traditional mode can use semantic ranking when `AzureSearch:EnableSemanticRanking=true`.
- Agentic mode demonstrates platform-managed retrieval orchestration through Knowledge Base retrieve API.
- Do not present Agentic mode as equivalent to the Traditional per-user trimming proof unless separately validated.

## 7. Rehearsal Checklist

- Verify `public-index` and `entitled-index` document counts are non-zero.
- Run a guest query for Aurora RAN 6651 and confirm only public product summaries are returned.
- Run an authenticated query for the same prompt and confirm protected manuals and detail sheets become available.
- Switch to Agentic mode and confirm logs show one knowledge base retrieve call.
- Run a comparison prompt for Aurora RAN 6651 versus Nimbus Indoor 2400 and confirm the answer combines multiple sources.
- Run the no-answer prompt for lunar mining networks and confirm no hallucinated fallback.
