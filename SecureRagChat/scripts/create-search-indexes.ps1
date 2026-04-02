param(
  [Parameter(Mandatory = $true)]
  [string]$SearchServiceName,

  [string]$PublicIndexName = 'public-index',
  [string]$EntitledIndexName = 'entitled-index',
  [string]$ApiVersion = '2024-07-01'
)

$ErrorActionPreference = 'Stop'

$endpoint = "https://$SearchServiceName.search.windows.net"

function Invoke-SearchPut {
  param(
    [string]$Url,
    [string]$Body
  )

  az rest --method put --uri $Url --headers "Content-Type=application/json" --body $Body --resource "https://search.azure.com"
  if ($LASTEXITCODE -ne 0) {
    throw "Request failed: $Url"
  }
}

$publicIndex = @{
  name = $PublicIndexName
  fields = @(
    @{ name = 'id'; type = 'Edm.String'; key = $true; searchable = $false; filterable = $true; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'title'; type = 'Edm.String'; searchable = $true; filterable = $false; sortable = $true; facetable = $false; retrievable = $true },
    @{ name = 'url'; type = 'Edm.String'; searchable = $false; filterable = $true; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'snippet'; type = 'Edm.String'; searchable = $true; filterable = $false; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'category'; type = 'Edm.String'; searchable = $true; filterable = $true; sortable = $false; facetable = $true; retrievable = $true }
  )
}

$entitledIndex = @{
  name = $EntitledIndexName
  fields = @(
    @{ name = 'id'; type = 'Edm.String'; key = $true; searchable = $false; filterable = $true; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'title'; type = 'Edm.String'; searchable = $true; filterable = $false; sortable = $true; facetable = $false; retrievable = $true },
    @{ name = 'url'; type = 'Edm.String'; searchable = $false; filterable = $true; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'snippet'; type = 'Edm.String'; searchable = $true; filterable = $false; sortable = $false; facetable = $false; retrievable = $true },
    @{ name = 'category'; type = 'Edm.String'; searchable = $true; filterable = $true; sortable = $false; facetable = $true; retrievable = $true },
    @{ name = 'authorizedUsers'; type = 'Collection(Edm.String)'; searchable = $false; filterable = $true; sortable = $false; facetable = $true; retrievable = $true },
    @{ name = 'authorizedGroups'; type = 'Collection(Edm.String)'; searchable = $false; filterable = $true; sortable = $false; facetable = $true; retrievable = $true }
  )
}

Write-Host "Creating/updating index '$PublicIndexName'..." -ForegroundColor Cyan
Invoke-SearchPut -Url "$endpoint/indexes/$PublicIndexName`?api-version=$ApiVersion" -Body ($publicIndex | ConvertTo-Json -Depth 10)

Write-Host "Creating/updating index '$EntitledIndexName'..." -ForegroundColor Cyan
Invoke-SearchPut -Url "$endpoint/indexes/$EntitledIndexName`?api-version=$ApiVersion" -Body ($entitledIndex | ConvertTo-Json -Depth 10)

Write-Host "Indexes are ready." -ForegroundColor Green
