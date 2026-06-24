@description('Name of the Azure AI Services (Foundry) account.')
param accountName string

@description('Region for the account.')
param location string

@description('Resource tags.')
param tags object = {}

@description('Model deployments to create.')
param deployments array

@description('Name of the Foundry project to create under the account.')
param projectName string

// AI Services (Foundry) account. 100% identity-based: local (key) auth is disabled, so the
// only way in is Entra via managed identity / DefaultAzureCredential. A system-assigned
// identity is attached, and project management is enabled so a Foundry project can live here.
resource account 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: accountName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
    allowProjectManagement: true
  }
}

// Foundry project (portal experience: playground, agents, connections, evals). Model
// inference flows through the account data plane; account-scope RBAC inherits to the project.
resource project 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' = {
  parent: account
  name: projectName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: projectName
    description: 'Tokens & Credits demo project.'
  }
}

// Cognitive Services creates deployments serially, so batch size 1.
@batchSize(1)
resource deployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = [for d in deployments: {
  parent: account
  name: d.name
  sku: {
    name: d.sku.name
    capacity: d.sku.capacity
  }
  properties: {
    model: {
      format: d.model.format
      name: d.model.name
      version: d.model.version
    }
  }
}]

output accountName string = account.name
output projectName string = project.name
output openAiEndpoint string = 'https://${account.name}.openai.azure.com/'
output projectEndpoint string = 'https://${account.name}.services.ai.azure.com/api/projects/${project.name}'
output deploymentNames array = [for d in deployments: d.name]
