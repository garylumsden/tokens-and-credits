#!/usr/bin/env pwsh
# azd postprovision hook: writes the provisioned Foundry endpoint into the app's
# git-ignored appsettings.Development.json so `dotnet run` works with no manual edits.
# Deployment names + capability flags already live in appsettings.json.

$ErrorActionPreference = 'Stop'

$endpoint = (azd env get-value AZURE_FOUNDRY_ENDPOINT).Trim()
if ([string]::IsNullOrWhiteSpace($endpoint)) {
    Write-Warning "AZURE_FOUNDRY_ENDPOINT not found in azd env; skipping app config write."
    exit 0
}

$configPath = Join-Path $PSScriptRoot '..\..\src\TokensAndCredits.Web\appsettings.Development.json'
$configPath = [System.IO.Path]::GetFullPath($configPath)

# Merge into any existing Development config rather than clobbering it.
$config = @{}
if (Test-Path $configPath) {
    try { $config = Get-Content $configPath -Raw | ConvertFrom-Json -AsHashtable } catch { $config = @{} }
}
if (-not $config.ContainsKey('AzureFoundry')) { $config['AzureFoundry'] = @{} }
$config['AzureFoundry']['Endpoint'] = $endpoint

$config | ConvertTo-Json -Depth 10 | Set-Content -Path $configPath -Encoding utf8

Write-Host "Wrote AzureFoundry:Endpoint = $endpoint"
Write-Host "-> $configPath"
Write-Host ""
Write-Host "Next: sign in for keyless access (az login OR azd auth login), then:"
Write-Host "  cd src/TokensAndCredits.Web; dotnet run"
