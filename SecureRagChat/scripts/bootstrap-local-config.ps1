param(
  [Parameter(Mandatory = $true)]
  [string]$SearchServiceName,

  [Parameter(Mandatory = $true)]
  [string]$OpenAiEndpoint,

  [Parameter(Mandatory = $true)]
  [string]$OpenAiModel,

  [Parameter(Mandatory = $true)]
  [string]$KnowledgeBaseName,

  [string]$PublicIndexName = 'public-index',
  [string]$EntitledIndexName = 'entitled-index',
  [switch]$UseDeveloperIdentityFallback
)

$ErrorActionPreference = 'Stop'

$backendPath = Resolve-Path './'
$searchEndpoint = "https://$SearchServiceName.search.windows.net"

Write-Host "Setting backend user-secrets..." -ForegroundColor Cyan

Push-Location $backendPath
try {
  dotnet user-secrets set "AzureSearch:Endpoint" $searchEndpoint
  dotnet user-secrets set "AzureSearch:PublicIndex" $PublicIndexName
  dotnet user-secrets set "AzureSearch:EntitledIndex" $EntitledIndexName
  dotnet user-secrets set "AzureSearch:UseLoggedInDeveloperIdentityForUserToken" ($UseDeveloperIdentityFallback.IsPresent.ToString().ToLower())

  dotnet user-secrets set "AgenticRetrieval:Endpoint" $searchEndpoint
  dotnet user-secrets set "AgenticRetrieval:KnowledgeBaseName" $KnowledgeBaseName

  dotnet user-secrets set "AzureOpenAI:Endpoint" $OpenAiEndpoint
  dotnet user-secrets set "AzureOpenAI:Model" $OpenAiModel
}
finally {
  Pop-Location
}

Write-Host "Backend user-secrets updated." -ForegroundColor Green

Write-Host "\nFrontend .env values to set (secureragchat-web/.env):" -ForegroundColor Yellow
Write-Host "VITE_API_BASE_URL=https://localhost:7113"
Write-Host "VITE_BACKEND_PROXY_TARGET=https://localhost:7113"
Write-Host "VITE_AZURE_TENANT_ID=<your-tenant-id>"
Write-Host "VITE_AZURE_CLIENT_ID=<your-frontend-client-id>"
Write-Host "VITE_AZURE_API_SCOPE=<your-api-scope>"
