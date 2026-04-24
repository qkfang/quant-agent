$SearchServiceName = "melt-search"
$FoundryEndpoint = "https://melt-foundry.services.ai.azure.com"
$IndexPrefix = "melt"

$ErrorActionPreference = "Stop"

$searchEndpoint = "https://${SearchServiceName}.search.windows.net"
$apiVersion = "2025-11-01-preview"

$token = (az account get-access-token --resource "https://search.azure.com" --query accessToken -o tsv)
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

function New-KnowledgeSource {
    param(
        [string]$Name,
        [string]$Description,
        [string]$IndexName,
        [string]$SemanticConfig
    )

    Write-Host "Creating knowledge source: $Name"

    $body = @{
        name = $Name
        kind = "searchIndex"
        description = $Description
        searchIndexParameters = @{
            searchIndexName = $IndexName
            semanticConfigurationName = $SemanticConfig
        }
    } | ConvertTo-Json -Depth 20

    $uri = "$searchEndpoint/knowledgesources/${Name}?api-version=$apiVersion"
    try {
        Invoke-RestMethod -Uri $uri -Method Put -Headers $headers -Body $body
        Write-Host "  Knowledge source '$Name' created/updated."
    }
    catch {
        Write-Warning "  Failed to create knowledge source '$Name': $_"
    }
}

function New-OneLakeKnowledgeSource {
    param(
        [string]$Name,
        [string]$Description,
        [string]$FabricWorkspaceId,
        [string]$LakehouseId,
        [string]$TargetPath
    )

    Write-Host "Creating OneLake knowledge source: $Name"

    $oneLakeParams = @{
        fabricWorkspaceId = $FabricWorkspaceId
        lakehouseId       = $LakehouseId
    }
    if ($TargetPath) {
        $oneLakeParams.targetPath = $TargetPath
    }

    $body = @{
        name                    = $Name
        kind                    = "indexedOneLake"
        description             = $Description
        indexedOneLakeParameters = $oneLakeParams
    } | ConvertTo-Json -Depth 20

    $uri = "$searchEndpoint/knowledgesources/${Name}?api-version=$apiVersion"
    try {
        Invoke-RestMethod -Uri $uri -Method Put -Headers $headers -Body $body
        Write-Host "  OneLake knowledge source '$Name' created/updated."
    }
    catch {
        Write-Warning "  Failed to create OneLake knowledge source '$Name': $_"
    }
}

function New-KnowledgeBase {
    param(
        [string]$Name,
        [string]$Description,
        [string[]]$KnowledgeSourceNames
    )

    Write-Host "Creating knowledge base: $Name"

    $sources = @()
    foreach ($ksName in $KnowledgeSourceNames) {
        $sources += @{ name = $ksName }
    }

    $body = @{
        name = $Name
        description = $Description
        knowledgeSources = $sources
        models = @(
            @{
                kind = "azureOpenAI"
                azureOpenAIParameters = @{
                    resourceUri = $FoundryEndpoint
                    deploymentId = "gpt-4.1"
                    modelName = "gpt-4.1"
                }
            }
        )
        outputMode = "extractiveData"
        retrievalReasoningEffort = @{
            kind = "low"
        }
    } | ConvertTo-Json -Depth 20

    $uri = "$searchEndpoint/knowledgebases/${Name}?api-version=$apiVersion"
    try {
        Invoke-RestMethod -Uri $uri -Method Put -Headers $headers -Body $body
        Write-Host "  Knowledge base '$Name' created/updated."
    }
    catch {
        Write-Warning "  Failed to create knowledge base '$Name': $_"
    }
}

# --- Knowledge sources ---
New-KnowledgeSource `
    -Name "${IndexPrefix}-knowledge-ks" `
    -Description "Production support knowledge base containing trace investigations, incident procedures, and troubleshooting guides." `
    -IndexName "${IndexPrefix}_knowledge" `
    -SemanticConfig "semantic-config-knowledge"

New-KnowledgeSource `
    -Name "${IndexPrefix}-service-doc-ks" `
    -Description "Application catalog and service documentation for all platform services, APIs, and their dependencies." `
    -IndexName "${IndexPrefix}_service_doc" `
    -SemanticConfig "semantic-config-service-doc"

# --- Knowledge base ---
New-KnowledgeBase `
    -Name "${IndexPrefix}-kb" `
    -Description "Combined knowledge base for production support and service documentation." `
    -KnowledgeSourceNames @("${IndexPrefix}-knowledge-ks", "${IndexPrefix}-service-doc-ks")

# --- OneLake knowledge source ---
New-OneLakeKnowledgeSource `
    -Name "${IndexPrefix}-onelake-ks" `
    -Description "OneLake lakehouse knowledge source for telemetry and operational data from Fabric." `
    -FabricWorkspaceId "f6ac1ad6-35ca-4153-b40b-ec3be38e1668" `
    -LakehouseId "cb641cf2-7039-48bd-8039-54e5862f668c"

# --- OneLake knowledge base ---
New-KnowledgeBase `
    -Name "${IndexPrefix}-data" `
    -Description "Knowledge base for OneLake telemetry and operational data." `
    -KnowledgeSourceNames @("${IndexPrefix}-onelake-ks")

Write-Host "`nKnowledge setup complete."
