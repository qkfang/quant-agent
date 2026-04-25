param name string
param location string
param tags object = {}
param aiSearchEndpoint string = ''
param aiSearchResourceId string = ''
param appInsightsConnectionString string = ''
param appInsightsResourceId string = ''
param appInsightsInstrumentationKey string = ''
param bingResourceId string = ''
param bingApiKey string = ''

resource aiHub 'Microsoft.CognitiveServices/accounts@2025-10-01-preview' = {
  name: name
  location: location
  tags: union(tags, { securityControl: 'Ignore' })
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: 'S0'
  }
  kind: 'AIServices'
  properties: {
    allowProjectManagement: true
    customSubDomainName: name
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

resource aiProject 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: aiHub
  name: '${name}-project'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
}

resource gpt5oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: aiHub
  name: 'gpt-5.4'
  sku: {
    name: 'GlobalStandard'
    capacity: 1000
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5.4'
      version: '2026-03-05'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    raiPolicyName: 'Microsoft.DefaultV2'
  }
}

resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: aiHub
  name: 'gpt-4o'
  dependsOn: [gpt5oDeployment]
  sku: {
    name: 'GlobalStandard'
    capacity: 1000
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-08-06'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    raiPolicyName: 'Microsoft.DefaultV2'
  }
}

resource gpt41Deployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: aiHub
  name: 'gpt-4.1'
  dependsOn: [gpt4oDeployment]
  sku: {
    name: 'GlobalStandard'
    capacity: 1000
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4.1'
      version: '2025-04-14'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    raiPolicyName: 'Microsoft.DefaultV2'
  }
}


resource aiSearchConnection 'Microsoft.CognitiveServices/accounts/connections@2025-10-01-preview' = if (aiSearchEndpoint != '') {
  parent: aiHub
  name: 'ai-search-connection'
  properties: {
    authType: 'AAD'
    category: 'CognitiveSearch'
    target: aiSearchEndpoint
    metadata: {
      type: 'azure_ai_search'
      ResourceId: aiSearchResourceId
      useWorkspaceManagedIdentity: 'false'
    }
  }
}

resource aiSearchProjectConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-10-01-preview' = if (aiSearchEndpoint != '') {
  parent: aiProject
  name: 'ai-search-connection-project'
  properties: {
    authType: 'AAD'
    category: 'CognitiveSearch'
    target: aiSearchEndpoint
    metadata: {
      type: 'azure_ai_search'
      ResourceId: aiSearchResourceId
      useWorkspaceManagedIdentity: 'false'
    }
  }
}

#disable-next-line BCP081
resource bingSearchConnection 'Microsoft.CognitiveServices/accounts/connections@2025-10-01-preview' = if (bingResourceId != '') {
  parent: aiHub
  name: '${name}-bingsearchconnection'
  properties: {
    authType: 'ApiKey'
    category: 'GroundingWithBingSearch'
    target: 'https://api.bing.microsoft.com/'
    credentials: {
      key: bingApiKey
    }
    metadata: {
      displayName: 'quant-bing'
      type: 'bing_grounding'
      ApiType: 'Azure'
      ResourceId: bingResourceId
    }
  }
}

#disable-next-line BCP081
resource bingSearchProjectConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-10-01-preview' = if (bingResourceId != '') {
  parent: aiProject
  name: '${name}-bingsearchconnection'
  properties: {
    authType: 'ApiKey'
    category: 'GroundingWithBingSearch'
    target: 'https://api.bing.microsoft.com/'
    credentials: {
      key: bingApiKey
    }
    metadata: {
      displayName: 'quant-bing'
      type: 'bing_grounding'
      ApiType: 'Azure'
      ResourceId: bingResourceId
    }
  }
}

resource appInsightsConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01' = if (appInsightsConnectionString != '') {
  parent: aiProject
  name: 'app-insights-connection'
  properties: {
    authType: 'ApiKey'
    category: 'AppInsights'
    target: appInsightsConnectionString
    credentials: {
      key: appInsightsInstrumentationKey
    }
    metadata: {
      ResourceId: appInsightsResourceId
    }
  }
}

output accountName string = aiHub.name
output resourceId string = aiHub.id
output endpoint string = aiHub.properties.endpoint
output projectEndpoint string = '${aiHub.properties.endpoint}api/projects/${aiProject.name}'
output deploymentName string = gpt5oDeployment.name
output gpt4oDeploymentName string = gpt4oDeployment.name
output gpt41DeploymentName string = gpt41Deployment.name
output projectName string = aiProject.name
output location string = location
output principalId string = aiHub.identity.principalId
output projectPrincipalId string = aiProject.identity.principalId
output aiSearchConnectionName string = aiSearchEndpoint != '' ? aiSearchConnection.name : ''
output aiSearchProjectConnectionName string = aiSearchEndpoint != '' ? aiSearchProjectConnection.name : ''
output aiSearchProjectConnectionId string = aiSearchEndpoint != '' ? aiSearchProjectConnection.id : ''
output bingProjectConnectionName string = bingResourceId != '' ? bingSearchProjectConnection.name : ''
output bingProjectConnectionId string = bingResourceId != '' ? bingSearchProjectConnection.id : ''
