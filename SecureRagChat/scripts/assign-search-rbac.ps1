[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [Parameter(Mandatory = $true)]
  [string]$ResourceGroupName,

  [Parameter(Mandatory = $true)]
  [string]$SearchServiceName,

  [string]$PrincipalObjectId,
  [string]$RoleName = 'Search Index Data Reader',
  [string]$SubscriptionId
)

$ErrorActionPreference = 'Stop'

function Require-AzCli {
  if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI ('az') is not installed or not available in PATH."
  }
}

function Resolve-PrincipalObjectId {
  param([string]$ProvidedPrincipalObjectId)

  if (-not [string]::IsNullOrWhiteSpace($ProvidedPrincipalObjectId)) {
    return $ProvidedPrincipalObjectId
  }

  $resolved = az ad signed-in-user show --query id -o tsv
  if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($resolved)) {
    throw "Failed to resolve signed-in user object ID. Provide -PrincipalObjectId explicitly."
  }

  return $resolved.Trim()
}

Require-AzCli

if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
  Write-Host "Setting active subscription to '$SubscriptionId'..." -ForegroundColor Cyan
  az account set --subscription $SubscriptionId | Out-Null
  if ($LASTEXITCODE -ne 0) {
    throw "Failed to set active subscription to '$SubscriptionId'."
  }
}

$principalId = Resolve-PrincipalObjectId -ProvidedPrincipalObjectId $PrincipalObjectId
$searchServiceId = az search service show --resource-group $ResourceGroupName --name $SearchServiceName --query id -o tsv

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($searchServiceId)) {
  throw "Failed to resolve Azure AI Search service resource ID for '$SearchServiceName' in '$ResourceGroupName'."
}

$searchServiceId = $searchServiceId.Trim()

Write-Host "Checking existing role assignments..." -ForegroundColor Cyan
$existingJson = az role assignment list `
  --assignee-object-id $principalId `
  --scope $searchServiceId `
  --role "$RoleName" `
  -o json

if ($LASTEXITCODE -ne 0) {
  throw "Failed to query current role assignments."
}

$existingAssignments = @($existingJson | ConvertFrom-Json)
$existing = $existingAssignments.Count

if ([int]$existing -gt 0) {
  Write-Host "Role '$RoleName' is already assigned for principal '$principalId' at scope '$searchServiceId'." -ForegroundColor Yellow
  return
}

$operation = "Assign role '$RoleName' to principal '$principalId' on '$SearchServiceName'"
if ($PSCmdlet.ShouldProcess($searchServiceId, $operation)) {
  Write-Host "Creating role assignment..." -ForegroundColor Cyan

  az role assignment create `
    --assignee-object-id $principalId `
    --role "$RoleName" `
    --scope $searchServiceId | Out-Null

  if ($LASTEXITCODE -ne 0) {
    throw "Role assignment failed."
  }

  Write-Host "Role assignment created successfully." -ForegroundColor Green
  Write-Host "Propagation may take a few minutes before data-plane requests succeed." -ForegroundColor Yellow
}
