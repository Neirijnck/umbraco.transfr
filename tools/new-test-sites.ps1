#Requires -Version 7.0
# Creates the three throwaway test environments under .sites/:
#   A - fresh install, seeded with test schema and content
#   B - a copy of A's folder (database and media), so it shares A's keys; diverge it by editing in its backoffice
#   C - a separate fresh install, seeded the same way: same aliases, different keys (the schema check must refuse it)
# Usage: ./tools/new-test-sites.ps1 [-Force]   (-Force deletes existing sites first)
param([switch] $Force)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-sites.common.ps1')

if (Test-Path $script:SitesRoot) {
    if (-not $Force) { throw "$($script:SitesRoot) exists. Use -Force to recreate the test sites." }
    Remove-Item -Recurse -Force $script:SitesRoot
}

Build-TestSite

foreach ($name in 'A', 'C') {
    Initialize-TestSiteFolder $name | Out-Null
    $process = Start-TestSite $name -Seed
    try {
        # Both seeders run after startup, the API user's last: stopping the site before it logs can leave its client
        # id saved without the sign-in client behind it, which every later start then takes as done.
        Wait-TestSite $name $process -LogLine 'Seeded test content', 'Created client credentials'
        Write-Host "Site $name installed and seeded."
    }
    finally {
        Stop-TestSite $process
    }
}

# B starts from A's database and media, like an environment restored from another one.
$a = Get-TestSitePath 'A'
$b = Get-TestSitePath 'B'
Copy-Item $a $b -Recurse
Remove-Item -Recurse -Force (Join-Path $b 'logs'), (Join-Path $b 'umbraco/Logs'), (Join-Path $b 'umbraco/Data/TEMP') -ErrorAction SilentlyContinue
$process = Start-TestSite 'B'
try {
    Wait-TestSite 'B' $process
    Write-Host 'Site B started from a copy of A.'
}
finally {
    Stop-TestSite $process
}

Write-Host "Done. Start a site with: ./tools/start-test-site.ps1 -Name A|B|C"