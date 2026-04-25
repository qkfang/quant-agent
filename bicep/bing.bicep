param foundryAccountName string
param bingSearchName string

#disable-next-line BCP081
resource foundryAccount 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: foundryAccountName
}

#disable-next-line BCP081
resource bingSearch 'Microsoft.Bing/accounts@2020-06-10' = {
  name: bingSearchName
  location: 'global'
  sku: {
    name: 'G1'
  }
  kind: 'Bing.Grounding'
}

#disable-next-line BCP081
resource bingSearchConnection 'Microsoft.CognitiveServices/accounts/connections@2025-04-01-preview' = {
  name: '${foundryAccountName}-bingsearchconnection'
  parent: foundryAccount
  properties: {
    category: 'ApiKey'
    target: 'https://api.bing.microsoft.com/'
    authType: 'ApiKey'
    credentials: {
      key: '${listKeys(bingSearch.id, '2020-06-10').key1}'
    }
    isSharedToAll: true
    metadata: {
      ApiType: 'Azure'
      Location: bingSearch.location
      ResourceId: bingSearch.id
    }
  }
}

output connectionName string = bingSearchConnection.name
