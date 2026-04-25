$projectEndpoint = "https://quant-fndry.services.ai.azure.com/api/projects/quant-fndry-project"
$apiVersion = "2025-05-15-preview"

$knownAgentIds = @(
    "quant-alpha",
    "quant-pricing",
    "quant-risk",
    "quant-orchestrator",
    "quant-turn-orchestrator",
    "compare-orchestrator"
)

$token = (az account get-access-token --resource "https://ai.azure.com/" --query accessToken -o tsv)
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

# List all agent versions
$listUrl = "$projectEndpoint/agentversions?api-version=$apiVersion"
$response = Invoke-RestMethod -Uri $listUrl -Headers $headers -Method Get
$agentVersions = $response.value
Write-Host "Found $($agentVersions.Count) agent version(s) via API"

foreach ($av in $agentVersions) {
    $deleteUrl = "$projectEndpoint/agentversions/$($av.agentId)/$($av.version)?api-version=$apiVersion"
    Invoke-RestMethod -Uri $deleteUrl -Headers $headers -Method Delete | Out-Null
    Write-Host "Deleted version: $($av.agentId) v$($av.version)"
}

# Also delete by known agent IDs directly
Write-Host "`nDeleting known agents by ID..."
foreach ($agentId in $knownAgentIds) {
    try {
        $deleteUrl = "$projectEndpoint/agentversions/$agentId?api-version=$apiVersion"
        Invoke-RestMethod -Uri $deleteUrl -Headers $headers -Method Delete | Out-Null
        Write-Host "Deleted: $agentId"
    } catch {
        Write-Host "Not found or already deleted: $agentId"
    }
}

Write-Host "`nDone"
