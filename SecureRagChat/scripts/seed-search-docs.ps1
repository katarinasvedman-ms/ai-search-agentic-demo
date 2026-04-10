param(
  [Parameter(Mandatory = $true)]
  [string]$SearchServiceName,

  [string]$PublicIndexName = 'public-index',
  [string]$EntitledIndexName = 'entitled-index',
  [string]$AgenticIndexName = 'agentic-index',
  [string]$PublicDocsPath = './demo-data/public-documents.json',
  [string]$EntitledDocsPath = './demo-data/entitled-documents.json',
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
      content = $doc.content
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
  $tempPayloadPath = [System.IO.Path]::GetTempFileName()

  Write-Host "Uploading $($uploadDocs.Count) document(s) to '$IndexName'..." -ForegroundColor Cyan

  try {
    Set-Content -Path $tempPayloadPath -Value $payload -Encoding utf8

    Invoke-RestMethod -Method Post `
      -Uri "$endpoint/indexes/$IndexName/docs/index`?api-version=$ApiVersion" `
      -Headers $headers `
      -InFile $tempPayloadPath | Out-Null
  }
  finally {
    Remove-Item -Path $tempPayloadPath -ErrorAction SilentlyContinue
  }
}

function Submit-AgenticDocuments {
  param(
    [string]$IndexName,
    [string]$PublicDocsPath,
    [string]$EntitledDocsPath
  )

  if (-not (Test-Path $PublicDocsPath)) {
    throw "Public document file not found: $PublicDocsPath"
  }

  if (-not (Test-Path $EntitledDocsPath)) {
    throw "Entitled document file not found: $EntitledDocsPath"
  }

  $publicDocs = Get-Content -Raw -Path $PublicDocsPath | ConvertFrom-Json
  $entitledDocs = Get-Content -Raw -Path $EntitledDocsPath | ConvertFrom-Json
  $docs = @($publicDocs) + @($entitledDocs)

  if ($null -eq $docs -or $docs.Count -eq 0) {
    throw "No documents found for agentic seeding."
  }

  $uploadDocs = @()
  foreach ($doc in $docs) {
    $item = @{
      '@search.action' = 'mergeOrUpload'
      id = $doc.id
      title = $doc.title
      url = $doc.url
      snippet = $doc.snippet
      content = $doc.content
      category = $doc.category
      section = $doc.category
      page_number = $null
      authorizedUsers = @()
      authorizedGroups = @()
    }

    if ($doc.PSObject.Properties.Name -contains 'authorizedUsers' -and $null -ne $doc.authorizedUsers) {
      $item.authorizedUsers = @($doc.authorizedUsers)
    }

    if ($doc.PSObject.Properties.Name -contains 'authorizedGroups' -and $null -ne $doc.authorizedGroups) {
      $item.authorizedGroups = @($doc.authorizedGroups)
    }

    $uploadDocs += $item
  }

  $payload = @{ value = $uploadDocs } | ConvertTo-Json -Depth 12
  $tempPayloadPath = [System.IO.Path]::GetTempFileName()

  Write-Host "Uploading $($uploadDocs.Count) document(s) to '$IndexName'..." -ForegroundColor Cyan

  try {
    Set-Content -Path $tempPayloadPath -Value $payload -Encoding utf8

    Invoke-RestMethod -Method Post `
      -Uri "$endpoint/indexes/$IndexName/docs/index`?api-version=$ApiVersion" `
      -Headers $headers `
      -InFile $tempPayloadPath | Out-Null
  }
  finally {
    Remove-Item -Path $tempPayloadPath -ErrorAction SilentlyContinue
  }
}

Submit-Documents -IndexName $PublicIndexName -DocsPath $PublicDocsPath
Submit-Documents -IndexName $EntitledIndexName -DocsPath $EntitledDocsPath
Submit-AgenticDocuments -IndexName $AgenticIndexName -PublicDocsPath $PublicDocsPath -EntitledDocsPath $EntitledDocsPath

Write-Host "Document seeding completed." -ForegroundColor Green
