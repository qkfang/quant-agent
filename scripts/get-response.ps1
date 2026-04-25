$projectEndpoint = "https://quant-fndry.services.ai.azure.com/api/projects/quant-fndry-project"
$apiVersion = "2025-05-15-preview"
$responseId = "resp_02f76850ff12c73b0069ecbb6f2f508197a6c23255608df389"

$token = (az account get-access-token --resource "https://ai.azure.com/" --query accessToken -o tsv)
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

$url = "$projectEndpoint/openai/responses/$responseId`?api-version=$apiVersion"
$response = Invoke-RestMethod -Uri $url -Headers $headers -Method Get

$response | ConvertTo-Json -Depth 10
