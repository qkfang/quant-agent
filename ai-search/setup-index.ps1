$SearchServiceName = "quant-search"
$ResourceGroupName = "rg-quant"
$IndexPrefix = "quant"
$FoundryEndpoint = "https://ai-quant.services.ai.azure.com"

$ErrorActionPreference = "Stop"

$searchEndpoint = "https://${SearchServiceName}.search.windows.net"
$apiVersion = "2024-07-01"

# Get access token for Azure AI Search
$token = (az account get-access-token --resource "https://search.azure.com" --query accessToken -o tsv)
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

function New-SearchIndex {
    param([string]$IndexName, [object]$IndexDefinition)

    Write-Host "Creating index: $IndexName"
    $IndexDefinition.name = $IndexName
    $body = $IndexDefinition | ConvertTo-Json -Depth 30

    $uri = "$searchEndpoint/indexes/${IndexName}?api-version=$apiVersion"
    try {
        Invoke-RestMethod -Uri $uri -Method Put -Headers $headers -Body $body
        Write-Host "  Index '$IndexName' created/updated."
    }
    catch {
        Write-Warning "  Failed to create index '$IndexName': $_"
    }
}

function Upload-Documents {
    param([string]$IndexName, [string]$DocumentFolder)

    $files = Get-ChildItem -Path $DocumentFolder -Filter "*.json"
    if ($files.Count -eq 0) {
        Write-Host "  No JSON documents found in $DocumentFolder"
        return
    }

    $docs = @()
    foreach ($file in $files) {
        $doc = Get-Content $file.FullName -Raw | ConvertFrom-Json
        $doc | Add-Member -NotePropertyName "@search.action" -NotePropertyValue "upload" -Force
        $docs += $doc
    }

    $body = @{ value = $docs } | ConvertTo-Json -Depth 20
    $uri = "$searchEndpoint/indexes/${IndexName}/docs/index?api-version=$apiVersion"

    try {
        Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -Body $body
        Write-Host "  Uploaded $($docs.Count) documents to '$IndexName'."
    }
    catch {
        Write-Warning "  Failed to upload documents to '$IndexName': $_"
    }
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# --- quant_knowledge index ---
$knowledgeIndexName = "${IndexPrefix}_knowledge"
$knowledgeIndexDef = @{
    name   = $knowledgeIndexName
    fields = @(
        @{ name = "id"; type = "Edm.String"; key = $true; filterable = $true }
        @{ name = "title"; type = "Edm.String"; searchable = $true; filterable = $true; sortable = $true; analyzer = "en.microsoft" }
        @{ name = "category"; type = "Edm.String"; searchable = $true; filterable = $true; facetable = $true }
        @{ name = "content"; type = "Edm.String"; searchable = $true; analyzer = "en.microsoft" }
        @{ name = "severity"; type = "Edm.String"; filterable = $true; facetable = $true }
        @{ name = "service"; type = "Edm.String"; filterable = $true; facetable = $true }
        @{ name = "tags"; type = "Collection(Edm.String)"; searchable = $true; filterable = $true; facetable = $true }
        @{ name = "lastUpdated"; type = "Edm.DateTimeOffset"; filterable = $true; sortable = $true }
        @{ name = "contentVector"; type = "Collection(Edm.Single)"; searchable = $true; dimensions = 1536; vectorSearchProfile = "vector-profile-knowledge" }
    )
    vectorSearch = @{
        algorithms = @(
            @{
                name = "hnsw-algorithm"
                kind = "hnsw"
                hnswParameters = @{
                    m = 4
                    efConstruction = 400
                    efSearch = 500
                    metric = "cosine"
                }
            }
        )
        profiles = @(
            @{
                name = "vector-profile-knowledge"
                algorithm = "hnsw-algorithm"
                vectorizer = "openai-vectorizer"
            }
        )
        vectorizers = @(
            @{
                name = "openai-vectorizer"
                kind = "azureOpenAI"
                azureOpenAIParameters = @{
                    resourceUri = $FoundryEndpoint
                    deploymentId = "text-embedding-ada-002"
                    modelName = "text-embedding-ada-002"
                }
            }
        )
    }
    semantic = @{
        defaultConfiguration = "semantic-config-knowledge"
        configurations = @(
            @{
                name = "semantic-config-knowledge"
                prioritizedFields = @{
                    titleField = @{ fieldName = "title" }
                    prioritizedContentFields = @(
                        @{ fieldName = "content" }
                    )
                    prioritizedKeywordsFields = @(
                        @{ fieldName = "category" }
                        @{ fieldName = "service" }
                    )
                }
            }
        )
    }
}

New-SearchIndex -IndexName $knowledgeIndexName -IndexDefinition $knowledgeIndexDef
Upload-Documents -IndexName $knowledgeIndexName -DocumentFolder (Join-Path $scriptDir "quant_knowledge")

# --- quant_service_doc index ---
$serviceDocIndexName = "${IndexPrefix}_service_doc"
$serviceDocIndexDef = @{
    name   = $serviceDocIndexName
    fields = @(
        @{ name = "id"; type = "Edm.String"; key = $true; filterable = $true }
        @{ name = "serviceName"; type = "Edm.String"; searchable = $true; filterable = $true; sortable = $true; analyzer = "en.microsoft" }
        @{ name = "serviceType"; type = "Edm.String"; filterable = $true; facetable = $true }
        @{ name = "description"; type = "Edm.String"; searchable = $true; analyzer = "en.microsoft" }
        @{ name = "endpoints"; type = "Edm.String"; searchable = $true }
        @{ name = "dependencies"; type = "Collection(Edm.String)"; searchable = $true; filterable = $true; facetable = $true }
        @{ name = "owner"; type = "Edm.String"; filterable = $true }
        @{ name = "tags"; type = "Collection(Edm.String)"; searchable = $true; filterable = $true; facetable = $true }
        @{ name = "lastUpdated"; type = "Edm.DateTimeOffset"; filterable = $true; sortable = $true }
        @{ name = "descriptionVector"; type = "Collection(Edm.Single)"; searchable = $true; dimensions = 1536; vectorSearchProfile = "vector-profile-service-doc" }
    )
    vectorSearch = @{
        algorithms = @(
            @{
                name = "hnsw-algorithm"
                kind = "hnsw"
                hnswParameters = @{
                    m = 4
                    efConstruction = 400
                    efSearch = 500
                    metric = "cosine"
                }
            }
        )
        profiles = @(
            @{
                name = "vector-profile-service-doc"
                algorithm = "hnsw-algorithm"
                vectorizer = "openai-vectorizer"
            }
        )
        vectorizers = @(
            @{
                name = "openai-vectorizer"
                kind = "azureOpenAI"
                azureOpenAIParameters = @{
                    resourceUri = $FoundryEndpoint
                    deploymentId = "text-embedding-ada-002"
                    modelName = "text-embedding-ada-002"
                }
            }
        )
    }
    semantic = @{
        defaultConfiguration = "semantic-config-service-doc"
        configurations = @(
            @{
                name = "semantic-config-service-doc"
                prioritizedFields = @{
                    titleField = @{ fieldName = "serviceName" }
                    prioritizedContentFields = @(
                        @{ fieldName = "description" }
                        @{ fieldName = "endpoints" }
                    )
                    prioritizedKeywordsFields = @(
                        @{ fieldName = "serviceType" }
                        @{ fieldName = "owner" }
                    )
                }
            }
        )
    }
}

New-SearchIndex -IndexName $serviceDocIndexName -IndexDefinition $serviceDocIndexDef
Upload-Documents -IndexName $serviceDocIndexName -DocumentFolder (Join-Path $scriptDir "quant_service_doc")

Write-Host "`nSetup complete. Indexes created with semantic search and vector search enabled."
