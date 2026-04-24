
az group create --name 'rg-quant' --location 'australiaeast'

az deployment group create --name 'quant-dev' --resource-group 'rg-quant' --template-file './main.bicep'

