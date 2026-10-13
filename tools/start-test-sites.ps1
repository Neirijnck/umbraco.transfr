#Requires -Version 7.0
# Builds the test site once and starts sites A, B and C together. Ctrl+C stops all three.
# Each site logs to .sites/<name>/logs/stdout.log. Create the sites first with ./tools/new-test-sites.ps1.
# Usage: ./tools/start-test-sites.ps1 [-ConfiguredRemotes]
#   -ConfiguredRemotes: also configure B and C as remotes on A, instead of only connecting them at runtime.
param([switch] $ConfiguredRemotes)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-sites.common.ps1')

foreach ($name in $script:SitePorts.Keys) {
    if (-not (Test-Path (Join-Path (Get-TestSitePath $name) 'umbraco/Data'))) { throw "Site $name does not exist yet. Run ./tools/new-test-sites.ps1 first." }
}

# One build up front: starting the sites must not rebuild (and so rewrite) what an already running site serves.
Build-TestSite

$processes = [ordered]@{}
try {
    foreach ($name in $script:SitePorts.Keys) {
        $processes[$name] = Start-TestSite $name -ConfiguredRemotes:($ConfiguredRemotes -and $name -eq 'A')
    }
    foreach ($name in $processes.Keys) {
        Wait-TestSite $name $processes[$name]
        Write-Host "Site $name running on $(Get-TestSiteUrl $name)/umbraco  (log: $(Join-Path (Get-TestSitePath $name) 'logs/stdout.log'))"
    }

    # The test sites' API user (local test credentials from appsettings.local.json), to connect B or C at runtime.
    $apiUser = (Get-Content (Join-Path $script:TestSiteProject 'appsettings.local.json') -Raw | ConvertFrom-Json).TransfrTestSite.ApiUser
    Write-Host ''
    Write-Host 'Connect a remote in the Transfr section of site A with:'
    Write-Host "  Address       $(Get-TestSiteUrl 'B')  (site B)  or  $(Get-TestSiteUrl 'C')  (site C)"
    Write-Host "  Client id     $($apiUser.ClientId)"
    Write-Host "  Client secret $($apiUser.ClientSecret)"
    Write-Host ''
    Write-Host 'Press Ctrl+C to stop all three sites.'

    while ($true) {
        Start-Sleep -Seconds 2
        foreach ($name in $processes.Keys) {
            if ($processes[$name].HasExited) { throw "Site $name stopped (exit code $($processes[$name].ExitCode)); see its log." }
        }
    }
}
finally {
    foreach ($name in $processes.Keys) { Stop-TestSite $processes[$name] }
    Write-Host 'Sites stopped.'
}