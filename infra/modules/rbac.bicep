@description('Name of the existing AI Services (Foundry) account to grant access on.')
param accountName string

@description('Object id of the principal to grant access.')
param principalId string

@allowed([ 'User', 'ServicePrincipal' ])
param principalType string = 'User'

@description('Built-in role GUIDs to grant on the account. Account-scope assignments inherit to the project and model deployments.')
param roleDefinitionIds array = [
  'a97b65f3-24c7-4388-baec-2e87135dc908' // Cognitive Services OpenAI User  — keyless model inference
  '64702f94-c441-49e6-a78b-ef80e0188fee' // Azure AI Developer             — Foundry project + model use
  '25fbc0a9-bd7c-42a3-aa1a-3b75d497ee68' // Cognitive Services Contributor — view/manage account, project, deployments
]

resource account 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: accountName
}

resource roleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for roleId in roleDefinitionIds: {
  name: guid(account.id, principalId, roleId)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleId)
    principalId: principalId
    principalType: principalType
  }
}]
