targetScope = 'subscription'

@minLength(1)
@description('Name of the azd environment; used to derive resource names.')
param environmentName string

@minLength(1)
@description('Primary Azure region for all resources (must have quota for the chosen models).')
param location string

@description('Object id of the signed-in user/principal to grant data-plane access. azd injects this.')
param principalId string

@description('Principal type for the role assignment (User for azd up; ServicePrincipal in CI).')
@allowed([ 'User', 'ServicePrincipal' ])
param principalType string = 'User'

@description('Model deployments to create on the Foundry account. Trim to match region quota.')
param deployments array = [
  {
    name: 'gpt-4o'
    model: { format: 'OpenAI', name: 'gpt-4o', version: '2024-11-20' }
    sku: { name: 'GlobalStandard', capacity: 30 }
  }
  {
    name: 'gpt-4.1'
    model: { format: 'OpenAI', name: 'gpt-4.1', version: '2025-04-14' }
    sku: { name: 'GlobalStandard', capacity: 30 }
  }
  {
    name: 'o4-mini'
    model: { format: 'OpenAI', name: 'o4-mini', version: '2025-04-16' }
    sku: { name: 'GlobalStandard', capacity: 30 }
  }
  {
    // Image generation (token-billed). Powers the image-output token-usage feature.
    // Capacity units differ from text models; keep modest to fit GlobalStandard image quota.
    name: 'gpt-image-1.5'
    model: { format: 'OpenAI', name: 'gpt-image-1.5', version: '2025-12-16' }
    sku: { name: 'GlobalStandard', capacity: 1 }
  }
]

var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module foundry 'modules/ai-foundry.bicep' = {
  name: 'ai-foundry'
  scope: rg
  params: {
    accountName: 'aoai-${resourceToken}'
    location: location
    tags: tags
    deployments: deployments
    projectName: 'proj-${resourceToken}'
  }
}

module rbac 'modules/rbac.bicep' = {
  name: 'rbac'
  scope: rg
  params: {
    accountName: foundry.outputs.accountName
    principalId: principalId
    principalType: principalType
  }
}

@description('Keyless endpoint for the app config (AzureFoundry:Endpoint).')
output AZURE_FOUNDRY_ENDPOINT string = foundry.outputs.openAiEndpoint

@description('Foundry project endpoint (Azure.AI.Projects / portal experience).')
output AZURE_FOUNDRY_PROJECT_ENDPOINT string = foundry.outputs.projectEndpoint

@description('Name of the Foundry project.')
output AZURE_FOUNDRY_PROJECT string = foundry.outputs.projectName

@description('Names of the created deployments (map these into AzureFoundry:Deployments).')
output AZURE_FOUNDRY_DEPLOYMENTS array = foundry.outputs.deploymentNames

output AZURE_LOCATION string = location
output AZURE_RESOURCE_GROUP string = rg.name
