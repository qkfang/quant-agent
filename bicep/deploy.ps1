
az group create --name 'rg-melt' --location 'eastus2'

az deployment group create --name 'melt-dev' --resource-group 'rg-melt' --template-file './main.bicep' --parameters './parameters.dev.json'

