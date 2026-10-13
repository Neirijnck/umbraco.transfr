# Starts one test site in the foreground (Ctrl+C stops it). Create the sites first with ./tools/new-test-sites.ps1.
# Usage: ./tools/start-test-site.ps1 -Name A|B|C
param([Parameter(Mandatory)][ValidateSet('A', 'B', 'C')] [string] $Name)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-sites.common.ps1')

$path = Get-TestSitePath $Name
if (-not (Test-Path (Join-Path $path 'umbraco/Data'))) { throw "Site $Name does not exist yet. Run ./tools/new-test-sites.ps1 first." }

Build-TestSite
$variables = Get-TestSiteEnvironment $Name
foreach ($key in $variables.Keys) { Set-Item "Env:\$key" $variables[$key] }
Push-Location $path
try {
    Write-Host "Site $Name on $(Get-TestSiteUrl $Name)/umbraco"
    dotnet $script:TestSiteDll --contentRoot $path --webroot (Join-Path $path 'wwwroot') --urls (Get-TestSiteUrl $Name)
}
finally {
    Pop-Location
    foreach ($key in $variables.Keys) { Remove-Item "Env:\$key" -ErrorAction SilentlyContinue }
}