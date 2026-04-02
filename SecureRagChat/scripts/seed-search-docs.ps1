param(
  [Parameter(Mandatory = $true)]
  [string]$SearchServiceName,

  [string]$PublicIndexName = 'public-index',
  [string]$EntitledIndexName = 'entitled-index',
  [string]$PublicDocsPath = './demo-data/public-documents.json',
  [string]$EntitledDocsPath = './demo-data/entitled-documents.json',
  [string]$ApiVersion = '2024-07-01'
)

$ErrorActionPreference = 'Stop'

$endpoint = "https://$SearchServiceName.search.windows.net"

function Submit-Documents {
  param(
    [string]$IndexName,
    [string]$DocsPath
  )

  if (-not (Test-Path $DocsPath)) {
    throw "Document file not found: $DocsPath"
  }

  $docs = Get-Content -Raw -Path $DocsPath | ConvertFrom-Json
  if ($null -eq $docs -or $docs.Count -eq 0) {
    throw "No documents found in $DocsPath"
  }

  $uploadDocs = @()
  foreach ($doc in $docs) {
    $item = @{
      '@search.action' = 'mergeOrUpload'
      id = $doc.id
      title = $doc.title
      url = $doc.url
      snippet = $doc.snippet
      category = $doc.category
    }

    if ($doc.PSObject.Properties.Name -contains 'authorizedUsers') {
      $item.authorizedUsers = $doc.authorizedUsers
    }

    if ($doc.PSObject.Properties.Name -contains 'authorizedGroups') {
      $item.authorizedGroups = $doc.authorizedGroups
    }

    $uploadDocs += $item
  }

  $payload = @{ value = $uploadDocs } | ConvertTo-Json -Depth 12

  Write-Host "Uploading $($uploadDocs.Count) document(s) to '$IndexName'..." -ForegroundColor Cyan

  az rest --method post `
    --uri "$endpoint/indexes/$IndexName/docs/index`?api-version=$ApiVersion" `
    --headers "Content-Type=application/json" `
    --body $payload `
    --resource "https://search.azure.com"

  if ($LASTEXITCODE -ne 0) {
    throw "Upload failed for index '$IndexName'"
  }
}

Submit-Documents -IndexName $PublicIndexName -DocsPath $PublicDocsPath
Submit-Documents -IndexName $EntitledIndexName -DocsPath $EntitledDocsPath

Write-Host "Document seeding completed." -ForegroundColor Green
