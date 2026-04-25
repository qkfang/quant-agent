$SearchServiceName = "quant-search"
$FoundryEndpoint = "https://quant-foundry.services.ai.azure.com"
$IndexPrefix = "quant"

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

Write-Host "`nKnowledge setup complete."
