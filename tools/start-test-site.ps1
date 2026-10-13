#Requires -Version 7.0
# Starts one test site in the foreground (Ctrl+C stops it). Create the sites first with ./tools/new-test-sites.ps1.
# To run all three, prefer ./tools/start-test-sites.ps1: it builds once and starts them together.
# Usage: ./tools/start-test-site.ps1 -Name A|B|C [-ConfiguredRemotes] [-NoBuild]
#   -ConfiguredRemotes (site A only): also configure B and C as remotes, instead of only connecting them at runtime.
#   -NoBuild: start the current build; use it while other test sites are running, so a build doesn't rewrite what
#             they serve.
param(
    [Parameter(Mandatory)][ValidateSet('A', 'B', 'C')] [string] $Name,
    [switch] $ConfiguredRemotes,
    [switch] $NoBuild)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-sites.common.ps1')

$path = Get-TestSitePath $Name
if (-not (Test-Path (Join-Path $path 'umbraco/Data'))) { throw "Site $Name does not exist yet. Run ./tools/new-test-sites.ps1 first." }

if (-not $NoBuild) { Build-TestSite }
$variables = Get-TestSiteEnvironment $Name -ConfiguredRemotes:$ConfiguredRemotes
foreach ($key in $variables.Keys) { Set-Item "Env:\$key" $variables[$key] }
Push-Location $path
try {
    Write-Host "Site $Name on $(Get-TestSiteUrl $Name)/umbraco"
    if ($Name -eq 'A') {
        # The test sites' API user (local test credentials from appsettings.local.json), to connect B or C at runtime.
        $apiUser = (Get-Content (Join-Path $script:TestSiteProject 'appsettings.local.json') -Raw | ConvertFrom-Json).TransfrTestSite.ApiUser
        Write-Host "Connect a remote in the Transfr section with:"
        Write-Host "  Address       $(Get-TestSiteUrl 'B')  (site B)  or  $(Get-TestSiteUrl 'C')  (site C)"
        Write-Host "  Client id     $($apiUser.ClientId)"
        Write-Host "  Client secret $($apiUser.ClientSecret)"
    }
    dotnet $script:TestSiteDll --contentRoot $path --webroot (Join-Path $path 'wwwroot') --urls (Get-TestSiteUrl $Name)
}
finally {
    Pop-Location
    foreach ($key in $variables.Keys) { Remove-Item "Env:\$key" -ErrorAction SilentlyContinue }
}