param(
  [Parameter(Mandatory = $true)]
  [string]$SearchServiceName,

  [string]$PublicIndexName = 'public-index',
  [string]$EntitledIndexName = 'entitled-index',
  [string]$AgenticIndexName = 'agentic-index',
  [string]$ApiKey,
  [string]$ApiVersion = '2025-11-01-preview'
)

$ErrorActionPreference = 'Stop'

$endpoint = "https://$SearchServiceName.search.windows.net"
$headers = @{ 'Content-Type' = 'application/json' }

if (-not [string]::IsNullOrWhiteSpace($ApiKey)) {
  $headers['api-key'] = $ApiKey
}
else {
  $accessToken = az account get-access-token --resource "https://search.azure.com" --query accessToken -o tsv
  if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($accessToken)) {
    throw "Failed to acquire Azure AI Search access token from Azure CLI."
  }

  $headers['Authorization'] = "Bearer $accessToken"
}

function Invoke-SearchPut {
  param(
    [string]$Url,
    [string]$Body
  )

  Invoke-RestMethod -Method Put -Uri $Url -Headers $headers -Body $Body | Out-Null
}

$semanticConfig = @{
  defaultConfiguration = 'default'
  configurations = @(
    @{
      name = 'default'
      prioritizedFields = @{
        titleField = @{ fieldName = 'title' }
        prioritizedContentFields = @(
          @{ fieldName = 'content' },
          @{ fieldName = 'snippet' }
        )
      }
    }
  )
}

$publicIndex = @{
  name = $PublicIndexName
  semantic = $semanticConfig
  fields = @(
    @{ name = 'id'; type = 'Edm.String'; key = $true; searchable = $false; filterable = $true; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'title'; type = 'Edm.String'; searchable = $true; filterable = $false; sortable = $true; facetable = $false; retrievable = $true },
    @{ name = 'url'; type = 'Edm.String'; searchable = $false; filterable = $true; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'snippet'; type = 'Edm.String'; searchable = $true; filterable = $false; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'content'; type = 'Edm.String'; searchable = $true; filterable = $false; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'category'; type = 'Edm.String'; searchable = $true; filterable = $true; sortable = $false; facetable = $true; retrievable = $true }
  )
}

$entitledIndex = @{
  name = $EntitledIndexName
  permissionFilterOption = 'enabled'
  semantic = $semanticConfig
  fields = @(
    @{ name = 'id'; type = 'Edm.String'; key = $true; searchable = $false; filterable = $true; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'title'; type = 'Edm.String'; searchable = $true; filterable = $false; sortable = $true; facetable = $false; retrievable = $true },
    @{ name = 'url'; type = 'Edm.String'; searchable = $false; filterable = $true; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'snippet'; type = 'Edm.String'; searchable = $true; filterable = $false; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'content'; type = 'Edm.String'; searchable = $true; filterable = $false; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'category'; type = 'Edm.String'; searchable = $true; filterable = $true; sortable = $false; facetable = $true; retrievable = $true },
    @{ name = 'authorizedUsers'; type = 'Collection(Edm.String)'; permissionFilter = 'userIds'; searchable = $false; filterable = $true; sortable = $false; facetable = $true; retrievable = $true },
    @{ name = 'authorizedGroups'; type = 'Collection(Edm.String)'; permissionFilter = 'groupIds'; searchable = $false; filterable = $true; sortable = $false; facetable = $true; retrievable = $true }
  )
}

$agenticIndex = @{
  name = $AgenticIndexName
  permissionFilterOption = 'enabled'
  semantic = $semanticConfig
  fields = @(
    @{ name = 'id'; type = 'Edm.String'; key = $true; searchable = $false; filterable = $true; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'title'; type = 'Edm.String'; searchable = $true; filterable = $false; sortable = $true; facetable = $false; retrievable = $true },
    @{ name = 'url'; type = 'Edm.String'; searchable = $false; filterable = $true; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'snippet'; type = 'Edm.String'; searchable = $true; filterable = $false; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'content'; type = 'Edm.String'; searchable = $true; filterable = $false; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'category'; type = 'Edm.String'; searchable = $true; filterable = $true; sortable = $false; facetable = $true; retrievable = $true },
    @{ name = 'section'; type = 'Edm.String'; searchable = $true; filterable = $true; sortable = $false; facetable = $true; retrievable = $true },
    @{ name = 'page_number'; type = 'Edm.Int32'; searchable = $false; filterable = $true; sortable = $true; facetable = $true; retrievable = $true },
    @{ name = 'authorizedUsers'; type = 'Collection(Edm.String)'; permissionFilter = 'userIds'; searchable = $false; filterable = $true; sortable = $false; facetable = $true; retrievable = $true },
    @{ name = 'authorizedGroups'; type = 'Collection(Edm.String)'; permissionFilter = 'groupIds'; searchable = $false; filterable = $true; sortable = $false; facetable = $true; retrievable = $true }
  )
}

Write-Host "Creating/updating index '$PublicIndexName'..." -ForegroundColor Cyan
Invoke-SearchPut -Url "$endpoint/indexes/$PublicIndexName`?api-version=$ApiVersion" -Body ($publicIndex | ConvertTo-Json -Depth 10)

Write-Host "Creating/updating index '$EntitledIndexName'..." -ForegroundColor Cyan
Invoke-SearchPut -Url "$endpoint/indexes/$EntitledIndexName`?api-version=$ApiVersion" -Body ($entitledIndex | ConvertTo-Json -Depth 10)

Write-Host "Creating/updating index '$AgenticIndexName'..." -ForegroundColor Cyan
Invoke-SearchPut -Url "$endpoint/indexes/$AgenticIndexName`?api-version=$ApiVersion" -Body ($agenticIndex | ConvertTo-Json -Depth 10)

Write-Host "Indexes are ready." -ForegroundColor Green
