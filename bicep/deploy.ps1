
az group create --name 'rg-quant' --location 'eastus2'

az deployment group create --name 'quant-dev' --resource-group 'rg-quant' --template-file './main.bicep'

