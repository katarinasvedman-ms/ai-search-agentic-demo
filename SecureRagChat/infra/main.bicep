@description('Azure region for demo resources')
param location string = resourceGroup().location

@description('Base name used for generated resources')
param namePrefix string

@description('Azure AI Search SKU (basic, standard, standard2, standard3)')
@allowed([
  'basic'
  'standard'
  'standard2'
  'standard3'
])
param searchSku string = 'standard'

@description('OpenAI model deployment name used by the backend')
param openAiDeploymentName string = 'gpt-4.1'

@description('OpenAI model name')
param openAiModelName string = 'gpt-4.1'

@description('OpenAI model version')
param openAiModelVersion string = '2025-01-01'

var searchServiceName = toLower('srch-${namePrefix}')
var openAiAccountName = toLower('aoai-${namePrefix}')
var storageAccountName = toLower(replace('st${namePrefix}', '-', ''))

resource searchService 'Microsoft.Search/searchServices@2025-05-01' = {
  name: searchServiceName
  location: location
  sku: {
    name: searchSku
  }
  properties: {
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
    disableLocalAuth: false
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http401WithBearerChallenge'
      }
    }
  }
}

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: openAiAccountName
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAiAccountName
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
  }
}

resource openAiDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  name: openAiDeploymentName
  parent: openAiAccount
  sku: {
    name: 'Standard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: openAiModelName
      version: openAiModelVersion
    }
    versionUpgradeOption: 'NoAutoUpgrade'
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Enabled'
  }
}

output searchServiceName string = searchService.name
output searchEndpoint string = 'https://${searchService.name}.search.windows.net'
output openAiEndpoint string = 'https://${openAiAccount.name}.openai.azure.com'
output openAiDeployment string = openAiDeployment.name
output storageAccountName string = storage.name
output suggestedPublicIndexName string = 'public-index'
output suggestedEntitledIndexName string = 'entitled-index'
