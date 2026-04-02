param(
  [Parameter(Mandatory = $true)]
  [string]$ResourceGroupName,

  [Parameter(Mandatory = $true)]
  [string]$ParametersFile
)

$ErrorActionPreference = 'Stop'

Write-Host "Deploying Bicep template to resource group '$ResourceGroupName'..." -ForegroundColor Cyan

$deploymentName = "secure-rag-demo-$(Get-Date -Format 'yyyyMMddHHmmss')"

az deployment group create `
  --resource-group $ResourceGroupName `
  --name $deploymentName `
  --template-file "./infra/main.bicep" `
  --parameters "@$ParametersFile"

if ($LASTEXITCODE -ne 0) {
  throw "Bicep deployment failed."
}

Write-Host "Deployment completed successfully." -ForegroundColor Green
