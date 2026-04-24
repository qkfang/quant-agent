@description('Base name used to derive all resource names')
param baseName string = 'melt'

@description('Azure region for all resources')
param location string = 'eastus2'

param azureAIFoundryEndpoint string = 'https://fsi-foundry.openai.azure.com'
param azureAIFoundryDeployment string = 'gpt-4.1'
param azureAIFoundryTenantId string = '9d2116ce-afe6-4ce8-8bc3-c7c7b69856c2'

param principals array = []

@description('UPN email addresses of Fabric capacity administrators')
param fabricAdminMembers array = []

@secure()
@description('Event Hub connection string for the OTel collector')
param eventhubConnectionString string

param eventhubBroker string
param eventhubTopic string

param sqlAadAdminLogin string
param sqlAadAdminSid string

param fabricDatabaseConnectionString string = 'Data Source=zylcdhpgv7uezc6dy7d3ngcwyi-b5l3uoo37ijuxbntne4gq2ska4.database.fabric.microsoft.com,1433;Initial Catalog=fx_data_sqldb-af3802bf-c4ca-4c83-aa5a-366c574104d4;Multiple Active Result Sets=False;Connect Timeout=30;Encrypt=True;Trust Server Certificate=False;Authentication=Active Directory Default'

var uniqueSuffix = uniqueString(resourceGroup().id)
var commonTags = {
  SecurityControl: 'Ignore'
}
var fabricCapacityName = '${baseName}fabric'
var keyVaultName = '${baseName}-kv'
var storageAccountName = '${baseName}st'
var appInsightsName = '${baseName}-appi'
var foundryName = '${baseName}-foundry'
var logAnalyticsWorkspaceName = '${baseName}-log'
var acrName = '${baseName}acr'
var containerIdentityName = '${baseName}-container-identity'
var aiSearchName = '${baseName}-search'

var appPlanSvcName = '${baseName}-svc-asp'
var svcWebName = '${baseName}-svc-web'
var svcAgentName = '${baseName}-svc-agent'
var svcSimName = '${baseName}-svc-sim'

var appPlanLogicName = '${baseName}-svc-logic-asp'
var svcLogicName = '${baseName}-svc-logic'

var otelCollectorName = '${baseName}-otel-collector'
var otelCollectorCustName = '${baseName}-otel-collector-cust'
var otelTestName = '${baseName}-otel-test'

var appPlanMockName = '${baseName}-mock-asp'
var mockNwName = '${baseName}-mock-nw'

var appPlanAppName = '${baseName}-app-asp'
var appPgwName = '${baseName}-api-pgw'
var appGwName = '${baseName}-api-gw'
var appClcName = '${baseName}-app-clc'
var appCocName = '${baseName}-app-coc'
var appDcName = '${baseName}-app-dc'
var appMobName = '${baseName}-app-mob'
var appNcName = '${baseName}-app-nc'
var appQnbName = '${baseName}-app-qnb'
var sqlServerName = '${baseName}-sql'
var claimsDbName = 'ClaimsDb'
var quotesDbName = 'QuotesDb'
var sreAgentName = '${baseName}-sre'
var vnetName = '${baseName}-vnet'
var iisVmName = '${baseName}-iis-vm'
var bastionName = '${baseName}-bastion'
var apimName = '${baseName}-apim'
var apimEventHubNsName = '${baseName}-apim-eh'
var privateDnsZoneName = 'iis.internal'

@secure()
@description('Admin username for the IIS VM')
param vmAdminUsername string

@secure()
@description('Admin password for the IIS VM')
param vmAdminPassword string

@description('Deploy Bastion host (set false if one already exists in the VNet)')
param deployBastion bool = true

@description('Deploy Fabric capacity (set false if capacity is suspended or already up to date)')
param deployFabric bool = true

param sreClientPrincipalId string

module aiSearch 'modules/platform/aisearch.bicep' = {
  name: 'aiSearchDeployment'
  params: {
    name: aiSearchName
    location: location
    tags: commonTags
  }
}

module azureFoundry 'modules/platform/foundry.bicep' = {
  name: 'foundryDeployment'
  params: {
    name: foundryName
    location: location
    tags: commonTags
    aiSearchEndpoint: aiSearch.outputs.endpoint
    aiSearchResourceId: aiSearch.outputs.id
  }
}

module sreAgent 'modules/platform/sreagent.bicep' = {
  name: 'sreAgentDeployment'
  params: {
    name: sreAgentName
    location: location
    appInsightsConnectionString: appInsights.outputs.connectionString
    appInsightsAppId: appInsights.outputs.appId
    accessLevel: 'High'
    deployerPrincipalId: sqlAadAdminSid
    sreClientPrincipalId: sreClientPrincipalId
  }
}

module keyVault 'modules/platform/keyvault.bicep' = {
  name: 'keyVaultDeployment'
  params: {
    name: keyVaultName
    location: location
  }
}

// Key Vault Secrets User role ID
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'


// API key secrets in Key Vault
resource kvResource 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
  dependsOn: [keyVault]
}

resource apiGwApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kvResource
  name: 'api-gw-apikey'
  properties: {
    value: '${uniqueString(resourceGroup().id, 'api-gw-apikey')}${guid(resourceGroup().id, 'api-gw-apikey')}'
  }
}

resource apiPgwApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kvResource
  name: 'api-pgw-apikey'
  properties: {
    value: '${uniqueString(resourceGroup().id, 'api-pgw-apikey')}${guid(resourceGroup().id, 'api-pgw-apikey')}'
  }
}

resource apiOgwApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kvResource
  name: 'api-ogw-apikey'
  properties: {
    value: '${uniqueString(resourceGroup().id, 'api-ogw-apikey')}${guid(resourceGroup().id, 'api-ogw-apikey')}'
  }
}

module storageAccount 'modules/platform/storage.bicep' = {
  name: 'storageAccountDeployment'
  params: {
    name: storageAccountName
    location: location
    tags: commonTags
    webAppPrincipalId: svcAgent.outputs.principalId
    principals: principals
  }
}

module logAnalytics 'modules/platform/loganalytics.bicep' = {
  name: 'logAnalyticsDeployment'
  params: {
    name: logAnalyticsWorkspaceName
    location: location
  }
}

module appInsights 'modules/platform/appinsights.bicep' = {
  name: 'appInsightsDeployment'
  params: {
    name: appInsightsName
    location: location
    workspaceResourceId: logAnalytics.outputs.id
  }
}


module acr 'modules/platform/acr.bicep' = {
  name: 'acrDeployment'
  params: {
    name: acrName
    location: location
  }
}

var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource containerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: containerIdentityName
  location: location
}

resource acrResource 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: acrName
  dependsOn: [acr]
  scope: resourceGroup()
}

resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acrResource.id, containerIdentity.id, acrPullRoleId)
  scope: acrResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: containerIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}


module fabricCapacity 'modules/platform/fabriccapacity.bicep' = if (deployFabric) {
  name: 'fabricCapacityDeployment'
  params: {
    name: fabricCapacityName
    location: location
    adminMembers: fabricAdminMembers
  }
}


// Private network with IIS VM
module network 'modules/networking/network.bicep' = {
  name: 'networkDeployment'
  params: {
    name: vnetName
    location: location
    tags: commonTags
  }
}

module iisVm 'modules/networking/iisvm.bicep' = {
  name: 'iisVmDeployment'
  params: {
    name: iisVmName
    location: location
    tags: commonTags
    subnetId: network.outputs.vmSubnetId
    adminUsername: vmAdminUsername
    adminPassword: vmAdminPassword
  }
}

module bastion 'modules/networking/bastion.bicep' = if (deployBastion) {
  name: 'bastionDeployment'
  params: {
    name: bastionName
    location: location
    tags: commonTags
    subnetId: network.outputs.bastionSubnetId
  }
}

resource privateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: privateDnsZoneName
  location: 'global'
  tags: commonTags
}

resource privateDnsVnetLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: privateDnsZone
  name: '${vnetName}-link'
  location: 'global'
  properties: {
    virtualNetwork: {
      id: network.outputs.vnetId
    }
    registrationEnabled: false
  }
}

resource iisVmDnsRecord 'Microsoft.Network/privateDnsZones/A@2024-06-01' = {
  parent: privateDnsZone
  name: iisVmName
  properties: {
    ttl: 300
    aRecords: [
      {
        ipv4Address: iisVm.outputs.privateIp
      }
    ]
  }
}

module apim 'modules/networking/apim.bicep' = {
  name: 'apimDeployment'
  params: {
    name: apimName
    location: location
    tags: commonTags
    subnetId: network.outputs.apimSubnetId
    appInsightsId: appInsights.outputs.id
    appInsightsInstrumentationKey: appInsights.outputs.instrumentationKey
  }
}

module apimEventHub 'modules/platform/eventhub_apim.bicep' = {
  name: 'apimEventHubDeployment'
  params: {
    name: apimEventHubNsName
    location: location
    tags: commonTags
    apimPrincipalId: apim.outputs.principalId
  }
}

// SQL Database
module claimsDb 'modules/platform/sqldb.bicep' = {
  name: 'claimsDbDeployment'
  params: {
    name: sqlServerName
    tags: commonTags
    claimsDbName: claimsDbName
    aadAdminLogin: sqlAadAdminLogin
    aadAdminSid: sqlAadAdminSid
    claimsDbDtuCapacity: 20
    quotesDbName: quotesDbName
    quotesDbDtuCapacity: 20
  }
}

// logic app
module appServicePlanLogic 'modules/apps/appserviceplan.bicep' = {
  name: 'appServicePlanLogicDeployment'
  params: {
    name: appPlanLogicName
    location: location
    skuName: 'WS1'
    skuTier: 'WorkflowStandard'
    kind: 'windows'
    reserved: false
  }
}

module svcLogic 'modules/apps/logicapp.bicep' = {
  name: 'svcLogicDeployment'
  params: {
    name: svcLogicName
    location: location
    appServicePlanId: appServicePlanLogic.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    storageAccountName: storageAccountName
  }
}

// MELT Service App
module appServicePlanSvc 'modules/apps/appserviceplan.bicep' = {
  name: 'appServicePlanSvcDeployment'
  params: {
    name: appPlanSvcName
    location: location
  }
}

module svcAgent 'modules/apps/meltapp.bicep' = {
  name: 'svcAgentDeployment'
  params: {
    name: svcAgentName
    location: location
    appServicePlanId: appServicePlanSvc.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    otelCollectorEndpoint: 'https://${otelCollector.outputs.fqdn}'
    appCommandLine: 'dotnet svc_agent.dll'
  }
}

module svcWeb 'modules/apps/meltapp.bicep' = {
  name: 'svcWebDeployment'
  params: {
    name: svcWebName
    location: location
    appServicePlanId: appServicePlanSvc.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    otelCollectorEndpoint: 'https://${otelCollector.outputs.fqdn}'
    appCommandLine: 'dotnet svc_web.dll'
  }
}

module svcSim 'modules/apps/containerapp_sim.bicep' = {
  name: 'svcSimDeployment'
  params: {
    name: svcSimName
    location: location
    logAnalyticsCustomerId: logAnalytics.outputs.customerId
    logAnalyticsSharedKey: logAnalytics.outputs.primarySharedKey
    simImage: '${acr.outputs.loginServer}/melt-svc-sim:latest'
    registryServer: acr.outputs.loginServer
    identityId: containerIdentity.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    servicesAppClcUrl: 'https://${appClc.outputs.defaultHostName}'
    servicesAppQnbUrl: 'https://${appQnb.outputs.defaultHostName}'
    servicesAppMobUrl: 'https://${appMob.outputs.defaultHostName}'
  }
}

// MELT OTEL Apps
module otelCollector 'modules/apps/containerapp_otel.bicep' = {
  name: 'otelCollectorDeployment'
  params: {
    name: otelCollectorName
    location: location
    logAnalyticsCustomerId: logAnalytics.outputs.customerId
    logAnalyticsSharedKey: logAnalytics.outputs.primarySharedKey
    eventhubConnectionString: eventhubConnectionString
    eventhubBroker: eventhubBroker
    eventhubTopic: eventhubTopic
    collectorImage: '${acr.outputs.loginServer}/melt-otel-collector:latest'
    registryServer: acr.outputs.loginServer
    identityId: containerIdentity.id
  }
}

module otelTest 'modules/apps/meltapp.bicep' = {
  name: 'otelTestDeployment'
  params: {
    name: otelTestName
    location: location
    appServicePlanId: appServicePlanSvc.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    otelCollectorEndpoint: 'https://${otelCollector.outputs.fqdn}'
    appCommandLine: 'dotnet otel_test.dll'
  }
}

module otelCollectorCust 'modules/apps/containerapp_otel_cust.bicep' = {
  name: 'otelCollectorCustDeployment'
  params: {
    name: otelCollectorCustName
    location: location
    logAnalyticsCustomerId: logAnalytics.outputs.customerId
    logAnalyticsSharedKey: logAnalytics.outputs.primarySharedKey
    apimEventhubConnectionString: apimEventHub.outputs.listenConnectionString
    apimEventhubName: apimEventHub.outputs.eventHubName
    apimEventhubConsumerGroup: apimEventHub.outputs.consumerGroupName
    checkpointStorageConnectionString: storageAccount.outputs.connectionString
    otlpEndpoint: 'https://${otelCollector.outputs.fqdn}'
    collectorImage: '${acr.outputs.loginServer}/melt-otel-collector-cust:latest'
    registryServer: acr.outputs.loginServer
    identityId: containerIdentity.id
  }
}


// MELT Apps
module appServicePlanApp 'modules/apps/appserviceplan.bicep' = {
  name: 'appServicePlanAppDeployment'
  params: {
    name: appPlanAppName
    location: location
  }
}

module appPgw 'modules/apps/meltapp.bicep' = {
  name: 'appPgwDeployment'
  params: {
    name: appPgwName
    location: location
    appServicePlanId: appServicePlanApp.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    otelCollectorEndpoint: 'https://${otelCollector.outputs.fqdn}'
    appCommandLine: 'dotnet api_pgw.dll'
    vnetSubnetId: network.outputs.appSubnetId
    servicesIisVmUrl: 'http://${iisVmName}.${privateDnsZoneName}'
    keyVaultName: keyVaultName
  }
}


module appGw 'modules/apps/meltapp.bicep' = {
  name: 'appGwDeployment'
  params: {
    name: appGwName
    location: location
    appServicePlanId: appServicePlanApp.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    otelCollectorEndpoint: 'https://${otelCollector.outputs.fqdn}'
    appCommandLine: 'dotnet api_gw.dll'
    claimsDbConnectionString: claimsDb.outputs.claimsConnectionString
    quotesDbConnectionString: claimsDb.outputs.quotesDbConnectionString
    keyVaultName: keyVaultName
  }
}

module appClc 'modules/apps/meltapp.bicep' = {
  name: 'appClcDeployment'
  params: {
    name: appClcName
    location: location
    appServicePlanId: appServicePlanApp.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    otelCollectorEndpoint: 'https://${otelCollector.outputs.fqdn}'
    appCommandLine: 'dotnet app_clc.dll'
    servicesApiGwUrl: '${apim.outputs.gatewayUrl}/gw'
    servicesAppDcUrl: 'https://${appDc.outputs.defaultHostName}'
    servicesApiPgwUrl: '${apim.outputs.gatewayUrl}/pgw'
    servicesAppNcUrl: 'https://${appNc.outputs.defaultHostName}'
    keyVaultName: keyVaultName
    servicesApiGwKeySecret: 'api-gw-apikey'
    servicesApiPgwKeySecret: 'api-pgw-apikey'
  }
}

module appCoc 'modules/apps/meltapp.bicep' = {
  name: 'appCocDeployment'
  params: {
    name: appCocName
    location: location
    appServicePlanId: appServicePlanApp.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    otelCollectorEndpoint: 'https://${otelCollector.outputs.fqdn}'
    appCommandLine: 'dotnet app_coc.dll'
  }
}

module appDc 'modules/apps/meltapp.bicep' = {
  name: 'appDcDeployment'
  params: {
    name: appDcName
    location: location
    appServicePlanId: appServicePlanApp.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    otelCollectorEndpoint: 'https://${otelCollector.outputs.fqdn}'
    appCommandLine: 'dotnet app_dc.dll'
  }
}

module appMob 'modules/apps/meltapp.bicep' = {
  name: 'appMobDeployment'
  params: {
    name: appMobName
    location: location
    appServicePlanId: appServicePlanApp.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    otelCollectorEndpoint: 'https://${otelCollector.outputs.fqdn}'
    appCommandLine: 'dotnet app_mob.dll'
    servicesApiGwUrl: '${apim.outputs.gatewayUrl}/gw'
    keyVaultName: keyVaultName
    servicesApiGwKeySecret: 'api-gw-apikey'
  }
}

module appNc 'modules/apps/meltapp.bicep' = {
  name: 'appNcDeployment'
  params: {
    name: appNcName
    location: location
    appServicePlanId: appServicePlanApp.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    otelCollectorEndpoint: 'https://${otelCollector.outputs.fqdn}'
    appCommandLine: 'dotnet app_nc.dll'
  }
}

module appQnb 'modules/apps/meltapp.bicep' = {
  name: 'appQnbDeployment'
  params: {
    name: appQnbName
    location: location
    appServicePlanId: appServicePlanApp.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    otelCollectorEndpoint: 'https://${otelCollector.outputs.fqdn}'
    appCommandLine: 'dotnet app_qnb.dll'
    servicesApiGwUrl: '${apim.outputs.gatewayUrl}/gw'
    servicesApiPgwUrl: '${apim.outputs.gatewayUrl}/pgw'
    keyVaultName: keyVaultName
    servicesApiGwKeySecret: 'api-gw-apikey'
    servicesApiPgwKeySecret: 'api-pgw-apikey'
  }
}

// Mock applications
module appServicePlanMock 'modules/apps/appserviceplan.bicep' = {
  name: 'appServicePlanMockDeployment'
  params: {
    name: appPlanMockName
    location: location
  }
}

module mockNw 'modules/apps/mockapp.bicep' = {
  name: 'mockNwDeployment'
  params: {
    name: mockNwName
    location: location
    appServicePlanId: appServicePlanMock.outputs.id
    appCommandLine: 'dotnet mock_nw.dll'
  }
}


// Foundry role assignments
var cognitiveServicesOpenAIUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
var cognitiveServicesUserRoleId = 'a97b65f3-24c7-4388-baec-2e87135dc908'
var azureAIUserRoleId = '53ca6127-db72-4b80-b1b0-d745d6d5456d'
var azureAIDeveloperRoleId = '64702f94-c441-49e6-a78b-ef80e0188fee'

resource foundryAccount 'Microsoft.CognitiveServices/accounts@2025-10-01-preview' existing = {
  name: foundryName
}

resource meltAgentOpenAIUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, svcAgentName, cognitiveServicesOpenAIUserRoleId)
  scope: foundryAccount
  dependsOn: [azureFoundry]
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAIUserRoleId)
    principalId: svcAgent.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource userOpenAIUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principal in principals: {
  name: guid(foundryAccount.id, principal.id, cognitiveServicesOpenAIUserRoleId)
  scope: foundryAccount
  dependsOn: [azureFoundry]
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAIUserRoleId)
    principalId: principal.id
    principalType: principal.principalType
  }
}]

resource meltAgentCogServicesUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, svcAgentName, cognitiveServicesUserRoleId)
  scope: foundryAccount
  dependsOn: [azureFoundry]
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesUserRoleId)
    principalId: svcAgent.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource userCogServicesUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principal in principals: {
  name: guid(foundryAccount.id, principal.id, cognitiveServicesUserRoleId)
  scope: foundryAccount
  dependsOn: [azureFoundry]
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesUserRoleId)
    principalId: principal.id
    principalType: principal.principalType
  }
}]

resource meltAgentAIUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, svcAgentName, azureAIUserRoleId)
  scope: foundryAccount
  dependsOn: [azureFoundry]
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', azureAIUserRoleId)
    principalId: svcAgent.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource meltAgentAIDeveloperRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, svcAgentName, azureAIDeveloperRoleId)
  scope: foundryAccount
  dependsOn: [azureFoundry]
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', azureAIDeveloperRoleId)
    principalId: svcAgent.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Secrets User RBAC for APIM and backend services

// AI Search role assignments
var searchIndexDataContributorRoleId = '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
var searchIndexDataReaderRoleId = '1407120a-92aa-4202-b7e9-c0e197c71c8f'
var searchServiceContributorRoleId = '7ca78c08-252a-4471-8644-bb5ff32d4ba0'

resource searchResource 'Microsoft.Search/searchServices@2024-06-01-preview' existing = {
  name: aiSearchName
  dependsOn: [aiSearch]
}

resource meltAgentSearchIndexDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchResource.id, svcAgentName, searchIndexDataContributorRoleId)
  scope: searchResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataContributorRoleId)
    principalId: svcAgent.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource meltAgentSearchIndexDataReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchResource.id, svcAgentName, searchIndexDataReaderRoleId)
  scope: searchResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataReaderRoleId)
    principalId: svcAgent.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource foundrySearchIndexDataReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchResource.id, foundryName, searchIndexDataReaderRoleId)
  scope: searchResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataReaderRoleId)
    principalId: azureFoundry.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource foundrySearchServiceContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchResource.id, foundryName, searchServiceContributorRoleId)
  scope: searchResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchServiceContributorRoleId)
    principalId: azureFoundry.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource userSearchIndexDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principal in principals: {
  name: guid(searchResource.id, principal.id, searchIndexDataContributorRoleId)
  scope: searchResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataContributorRoleId)
    principalId: principal.id
    principalType: principal.principalType
  }
}]
resource apimKvSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvResource.id, apimName, kvSecretsUserRoleId)
  scope: kvResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: apim.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource appGwKvSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvResource.id, appGwName, kvSecretsUserRoleId)
  scope: kvResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: appGw.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource appPgwKvSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvResource.id, appPgwName, kvSecretsUserRoleId)
  scope: kvResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: appPgw.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource appClcKvSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvResource.id, appClcName, kvSecretsUserRoleId)
  scope: kvResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: appClc.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource appMobKvSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvResource.id, appMobName, kvSecretsUserRoleId)
  scope: kvResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: appMob.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource appQnbKvSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvResource.id, appQnbName, kvSecretsUserRoleId)
  scope: kvResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: appQnb.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource iisVmKvSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvResource.id, iisVmName, kvSecretsUserRoleId)
  scope: kvResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: iisVm.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Secrets User RBAC for principals
resource kvSecretsUserPrincipalRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principal in principals: {
  name: guid(kvResource.id, principal.id, kvSecretsUserRoleId)
  scope: kvResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: principal.id
    principalType: principal.principalType
  }
}]
