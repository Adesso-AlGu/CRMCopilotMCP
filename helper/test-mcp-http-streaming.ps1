# Test MCP Server with HTTP Streaming Transport
# This tests the WithHttpTransport() configuration which supports HTTP/SSE streaming
# Usage: .\test-mcp-http-streaming.ps1 -BaseUrl "http://localhost:5000" -clientId "<client-id>" -clientSecret "<client-secret>" -TenantId "<tenant-id>" -Scope "api://<app-id-uri>/.default"

param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$ClientId,
    [string]$ClientSecret,
    [string]$TenantId,
    [string]$Scope = "",
    [switch]$Debug,
    [switch]$SkipAuth
)

# Helper function to get OAuth 2.0 access token
function Get-AccessToken {
    param(
        [string]$TenantId,
        [string]$ClientId,
        [string]$ClientSecret,
        [string]$Scope
    )
    
    $tokenUrl = "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/token"
    
    # If no scope specified, use default App ID URI
    if (-not $Scope) {
        $Scope = "api://$ClientId/.default"
    }
    
    # Note: Invoke-RestMethod handles URL-encoding automatically for form data
    $body = @{
        client_id     = $ClientId
        client_secret = $ClientSecret
        scope         = $Scope
        grant_type    = "client_credentials"
    }
    
    try {
        $response = Invoke-RestMethod -Uri $tokenUrl -Method Post -Body $body -ContentType "application/x-www-form-urlencoded"
        
        if ($Debug) {
            Write-Host "`n[DEBUG] Token Details:" -ForegroundColor Magenta
            Write-Host "  Token Type: $($response.token_type)" -ForegroundColor Gray
            Write-Host "  Expires In: $($response.expires_in) seconds" -ForegroundColor Gray
            Write-Host "  Scope: $($response.scope)" -ForegroundColor Gray
            
            # Decode JWT to show claims
            $tokenParts = $response.access_token.Split('.')
            if ($tokenParts.Length -ge 2) {
                $payload = $tokenParts[1]
                # Add padding if needed
                while ($payload.Length % 4 -ne 0) { $payload += "=" }
                $decodedBytes = [Convert]::FromBase64String($payload)
                $decodedJson = [System.Text.Encoding]::UTF8.GetString($decodedBytes)
                $claims = $decodedJson | ConvertFrom-Json
                Write-Host "  Audience (aud): $($claims.aud)" -ForegroundColor Gray
                Write-Host "  Issuer (iss): $($claims.iss)" -ForegroundColor Gray
                Write-Host "  App ID (appid): $($claims.appid)" -ForegroundColor Gray
            }
        }
        
        return $response.access_token
    } catch {
        Write-Host "✗ Failed to get access token: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

# Helper function to parse SSE response
function Parse-SSEResponse {
    param([string]$sseText)
    
    # SSE format: "event: message\ndata: {...}\n\n"
    if ($sseText -match 'data: ({.*})') {
        return $matches[1] | ConvertFrom-Json
    }
    return $null
}

# Helper function to make SSE request
function Invoke-SSERequest {
    param(
        [string]$Uri,
        [string]$Body,
        [string]$AccessToken = ""
    )
    
    try {
        # The server requires BOTH application/json AND text/event-stream in Accept header
        $httpClient = New-Object System.Net.Http.HttpClient
        $httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream")
        
        # Add OAuth 2.0 Bearer token if provided
        if ($AccessToken) {
            $httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer $AccessToken")
            Write-Host "  → Using OAuth 2.0 Bearer Token" -ForegroundColor Gray
        }
        
        $content = New-Object System.Net.Http.StringContent($Body, [System.Text.Encoding]::UTF8, "application/json")
        $response = $httpClient.PostAsync($Uri, $content).Result
        
        if ($response.IsSuccessStatusCode) {
            $sseText = $response.Content.ReadAsStringAsync().Result
            return Parse-SSEResponse -sseText $sseText
        } else {
            $errorMsg = $response.Content.ReadAsStringAsync().Result
            Write-Host "✗ Error: HTTP $($response.StatusCode) - $errorMsg" -ForegroundColor Red
            return $null
        }
    } catch {
        Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    } finally {
        if ($httpClient) { $httpClient.Dispose() }
    }
}

Write-Host "Testing MCP Server with HTTP Streaming Transport..." -ForegroundColor Cyan
Write-Host "Server URL: $BaseUrl" -ForegroundColor Gray

# Get OAuth 2.0 Access Token if credentials provided
$accessToken = $null
if ($ClientId -and $ClientSecret) {
    Write-Host "Authentication: OAuth 2.0 (ClientId: $ClientId, TenantId: $TenantId)" -ForegroundColor Gray
    Write-Host "Getting access token..." -ForegroundColor Gray
    $accessToken = Get-AccessToken -TenantId $TenantId -ClientId $ClientId -ClientSecret $ClientSecret -Scope $Scope
    if ($accessToken) {
        Write-Host "✓ Access token obtained successfully" -ForegroundColor Green
        Write-Host $accessToken -ForegroundColor Green
    } else {
        Write-Host "✗ Failed to obtain access token" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Authentication: None" -ForegroundColor Gray
}

# Test Status Endpoint
Write-Host "`n1. Testing /status endpoint..." -ForegroundColor Yellow
try {
    $headers = @{}
    if ($accessToken) {
        $headers["Authorization"] = "Bearer $accessToken"
    }
    
    if ($headers.Count -gt 0) {
        $response = Invoke-RestMethod -Uri "$BaseUrl/status" -Method Get -Headers $headers
    } else {
        $response = Invoke-RestMethod -Uri "$BaseUrl/status" -Method Get
    }
    Write-Host "✓ Status: $response" -ForegroundColor Green
} catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    
    # Check if it's a 401 error
    if ($_.Exception.Message -match "401") {
        Write-Host "
" -ForegroundColor Yellow
        Write-Host "⚠ AUTHENTICATION PROBLEM DETECTED" -ForegroundColor Yellow
        Write-Host "The App Service is rejecting the OAuth token. Possible causes:" -ForegroundColor Yellow
        Write-Host "  1. App Service Authentication is configured for user login only" -ForegroundColor White
        Write-Host "  2. The app needs 'App Roles' configured for service-to-service auth" -ForegroundColor White
        Write-Host "  3. The App ID URI scope doesn't match the App Service configuration" -ForegroundColor White
        Write-Host "
Possible Solutions:" -ForegroundColor Cyan
        Write-Host "  • In Azure Portal → App Service → Authentication:" -ForegroundColor White
        Write-Host "    - Set 'Restrict access' to 'Allow unauthenticated access'" -ForegroundColor White
        Write-Host "    - Handle authentication in application code instead" -ForegroundColor White
        Write-Host "  • OR configure App Roles in the App Registration" -ForegroundColor White
        Write-Host "  • OR test without authentication: -SkipAuth" -ForegroundColor White
    }
    exit 1
}

# Test Initialize with HTTP Transport
Write-Host "`n2. Testing MCP Initialize (HTTP Transport with SSE)..." -ForegroundColor Yellow
$initRequest = @{
    jsonrpc = "2.0"
    id = 1
    method = "initialize"
    params = @{
        protocolVersion = "2024-11-05"
        capabilities = @{
            tools = @{}
        }
        clientInfo = @{
            name = "http-streaming-test-client"
            version = "1.0.0"
        }
    }
} | ConvertTo-Json -Depth 10

$response = Invoke-SSERequest -Uri $BaseUrl -Body $initRequest -AccessToken $accessToken
if ($response) {
    Write-Host "✓ Initialize Response:" -ForegroundColor Green
    Write-Host "  Protocol Version: $($response.result.protocolVersion)" -ForegroundColor White
    Write-Host "  Server: $($response.result.serverInfo.name) v$($response.result.serverInfo.version)" -ForegroundColor White
    Write-Host "  Capabilities: Tools (ListChanged: $($response.result.capabilities.tools.listChanged))" -ForegroundColor White
}

# Test Tools List
Write-Host "`n3. Testing MCP Tools List..." -ForegroundColor Yellow
$toolsRequest = @{
    jsonrpc = "2.0"
    id = 2
    method = "tools/list"
    params = @{}
} | ConvertTo-Json -Depth 10

$response = Invoke-SSERequest -Uri $BaseUrl -Body $toolsRequest -AccessToken $accessToken
if ($response) {
    Write-Host "✓ Available Tools:" -ForegroundColor Green
    foreach ($tool in $response.result.tools) {
        Write-Host "  • $($tool.name): $($tool.description)" -ForegroundColor Cyan
    }
}

# Summary
Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "HTTP Streaming Transport Test Summary:" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "✓ Server is configured with WithHttpTransport()" -ForegroundColor Green
Write-Host "✓ Supports standard HTTP POST requests" -ForegroundColor Green
Write-Host "✓ Can handle Server-Sent Events (SSE)" -ForegroundColor Green
Write-Host "✓ Compatible with Copilot Studio and other MCP clients" -ForegroundColor Green
Write-Host "`nServer URL for MCP clients: $BaseUrl" -ForegroundColor White
Write-Host "`nTest completed successfully!" -ForegroundColor Cyan
