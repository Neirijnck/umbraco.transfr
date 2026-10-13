# Shared helpers for the throwaway test sites under .sites/ (A, B, C).
# All three run the same test site build, each with its own content root: own SQLite database, media and logs.

$script:RepoRoot = Split-Path $PSScriptRoot -Parent
$script:SitesRoot = Join-Path $script:RepoRoot '.sites'
$script:TestSiteProject = Join-Path $script:RepoRoot 'tests/Neirijnck.Transfr.TestSite'
$script:TestSiteDll = Join-Path $script:TestSiteProject 'bin/Debug/net10.0/Neirijnck.Transfr.TestSite.dll'
$script:SitePorts = [ordered]@{ A = 44371; B = 44372; C = 44373 }

function Get-TestSitePath([string] $Name) {
    Join-Path $script:SitesRoot $Name
}

function Get-TestSiteUrl([string] $Name) {
    "https://localhost:$($script:SitePorts[$Name])"
}

function Build-TestSite {
    dotnet build $script:TestSiteProject --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Building the test site failed.' }
}

# A content root needs the configuration files; everything else (database, media, logs) is created by Umbraco.
function Initialize-TestSiteFolder([string] $Name) {
    $path = Get-TestSitePath $Name
    # Without an existing wwwroot under the content root, media ended up in the current directory: create it up front.
    New-Item -ItemType Directory -Force (Join-Path $path 'wwwroot') | Out-Null
    $local = Join-Path $script:TestSiteProject 'appsettings.local.json'
    if (-not (Test-Path $local)) {
        throw "Missing $local. Copy appsettings.local.example.json and fill in the admin credentials."
    }
    foreach ($file in 'appsettings.json', 'appsettings.Development.json', 'appsettings.local.json') {
        Copy-Item (Join-Path $script:TestSiteProject $file) $path -Force
    }
    $path
}

# Site A is the local environment: it gets B and C as remotes, signed in with the API user every test site has.
# B and C get no remotes, so they show what an environment without remotes looks like (no Transfr UI, 404s).
# Passed as environment variables: the shared appsettings.local.json is copied into every site.
function Get-TestSiteEnvironment([string] $Name) {
    $variables = [ordered]@{ ASPNETCORE_ENVIRONMENT = 'Development' }
    if ($Name -eq 'A') {
        $local = Get-Content (Join-Path $script:TestSiteProject 'appsettings.local.json') -Raw | ConvertFrom-Json
        $index = 0
        foreach ($remote in 'B', 'C') {
            $prefix = "Transfr__Remotes__$index"
            $variables["${prefix}__Alias"] = $remote.ToLowerInvariant()
            $variables["${prefix}__Name"] = "Test site $remote"
            $variables["${prefix}__BaseUrl"] = Get-TestSiteUrl $remote
            $variables["${prefix}__ClientId"] = $local.TransfrTestSite.ApiUser.ClientId
            $variables["${prefix}__ClientSecret"] = $local.TransfrTestSite.ApiUser.ClientSecret
            $index++
        }
    }
    $variables
}

function Start-TestSite([string] $Name, [switch] $Seed) {
    $path = Get-TestSitePath $Name
    $logs = Join-Path $path 'logs'
    New-Item -ItemType Directory -Force $logs | Out-Null
    $variables = Get-TestSiteEnvironment $Name
    $variables['TransfrTestSite__SeedOnEmptyDatabase'] = if ($Seed) { 'true' } else { 'false' }
    foreach ($key in $variables.Keys) { Set-Item "Env:\$key" $variables[$key] }
    try {
        Start-Process dotnet -PassThru -WindowStyle Hidden -WorkingDirectory $path `
            -ArgumentList "`"$($script:TestSiteDll)`"", '--contentRoot', "`"$path`"", '--webroot', "`"$(Join-Path $path 'wwwroot')`"", '--urls', (Get-TestSiteUrl $Name) `
            -RedirectStandardOutput (Join-Path $logs 'stdout.log') -RedirectStandardError (Join-Path $logs 'stderr.log')
    }
    finally {
        foreach ($key in $variables.Keys) { Remove-Item "Env:\$key" -ErrorAction SilentlyContinue }
    }
}

# Waits until the site reports runtime level Run (and, with -LogLine, until that text appears in its console log).
function Wait-TestSite([string] $Name, [System.Diagnostics.Process] $Process, [string] $LogLine, [int] $TimeoutSeconds = 300) {
    $status = "$(Get-TestSiteUrl $Name)/umbraco/management/api/v1/server/status"
    $log = Join-Path (Get-TestSitePath $Name) 'logs/stdout.log'
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) { throw "Site $Name exited with code $($Process.ExitCode); see $log" }
        # A failing startup handler is logged as fatal while the process keeps running: fail fast instead of timing out.
        if ((Test-Path $log) -and (Select-String -Path $log -Pattern 'FTL\]' -Quiet)) { throw "Site $Name logged a fatal error; see $log" }
        try {
            $running = (Invoke-RestMethod $status -SkipCertificateCheck -TimeoutSec 5).serverStatus -eq 'Run'
        }
        catch { $running = $false }
        $logged = -not $LogLine -or ((Test-Path $log) -and (Select-String -Path $log -SimpleMatch $LogLine -Quiet))
        if ($running -and $logged) { return }
        Start-Sleep -Seconds 2
    }
    throw "Site $Name did not become ready within $TimeoutSeconds seconds; see $log"
}

function Stop-TestSite([System.Diagnostics.Process] $Process) {
    if (-not $Process.HasExited) {
        Stop-Process -Id $Process.Id
        $Process.WaitForExit(30000) | Out-Null
    }
}