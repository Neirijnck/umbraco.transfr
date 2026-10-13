#Requires -Version 7.0
# Checks a running test site's Transfr API user: it can get a token and read, and the remote itself refuses the
# operations the tool never performs (delete, recycle bin, unpublish, move) plus anything outside content and media.
# Sends those forbidden requests on purpose, so it only runs against the local test sites.
# Usage: ./tools/probe-test-site-api-user.ps1 -Name B   (the site must be running: ./tools/start-test-site.ps1 -Name B)
param([Parameter(Mandatory)][ValidateSet('A', 'B', 'C')] [string] $Name)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-sites.common.ps1')

$base = Get-TestSiteUrl $Name
$api = "$base/umbraco/management/api/v1"
$settings = Get-Content (Join-Path (Get-TestSitePath $Name) 'appsettings.local.json') -Raw | ConvertFrom-Json
$client = $settings.TransfrTestSite.ApiUser
if (-not $client.ClientSecret) { throw "No TransfrTestSite:ApiUser in site $Name's appsettings.local.json" }

$token = Invoke-RestMethod "$api/security/back-office/token" -Method Post -SkipCertificateCheck -Body @{
    grant_type = 'client_credentials'; client_id = $client.ClientId; client_secret = $client.ClientSecret
}
$headers = @{ Authorization = "Bearer $($token.access_token)" }

function Send([string] $Method, [string] $Path, $Body) {
    $arguments = @{ Uri = "$api$Path"; Method = $Method; Headers = $headers; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) { $arguments.Body = ($Body | ConvertTo-Json -Depth 5); $arguments.ContentType = 'application/json' }
    Invoke-WebRequest @arguments
}

$results = [System.Collections.Generic.List[object]]::new()
function Check([string] $What, [int] $Expected, $Response) {
    $results.Add([pscustomobject]@{ Check = $What; Expected = $Expected; Actual = [int]$Response.StatusCode; Ok = [int]$Response.StatusCode -eq $Expected })
}

Write-Host "Token: $($token.token_type), expires in $($token.expires_in)s"

$root = Send GET '/tree/document/root?skip=0&take=100'
Check 'read tree root' 200 $root
$homeNode = ($root.Content | ConvertFrom-Json).items | Select-Object -First 1
$children = Send GET "/tree/document/children?parentId=$($homeNode.id)&skip=0&take=100"
Check 'read tree children' 200 $children
$items = ($children.Content | ConvertFrom-Json).items
$draft = $items | Where-Object { $_.variants[0].name -eq 'Draft Page' }
$variant = $items | Where-Object { $_.variants[0].name -eq 'Variant Page' }
Check 'read a document with values' 200 (Send GET "/document/$($draft.id)")

Check 'delete document' 403 (Send DELETE "/document/$($draft.id)")
Check 'move to recycle bin' 403 (Send PUT "/document/$($draft.id)/move-to-recycle-bin")
Check 'move document' 403 (Send PUT "/document/$($draft.id)/move" @{ target = $null })
Check 'unpublish culture' 403 (Send PUT "/document/$($variant.id)/unpublish" @{ cultures = @('en-US') })
Check 'list users (no users section)' 403 (Send GET '/user?skip=0&take=10')

# The targets are untouched: still there, not trashed, the variant still published in en-US.
$after = (Send GET "/item/document?id=$($draft.id)&id=$($variant.id)").Content | ConvertFrom-Json
$draftAfter = $after | Where-Object id -eq $draft.id
$variantAfter = $after | Where-Object id -eq $variant.id
$results.Add([pscustomobject]@{ Check = 'targets untouched'; Expected = 'present, published'; Actual = "draft trashed=$($draftAfter.isTrashed), variant en-US=$(($variantAfter.variants | Where-Object culture -eq 'en-US').state)"; Ok = ($draftAfter -and -not $draftAfter.isTrashed -and (($variantAfter.variants | Where-Object culture -eq 'en-US').state -eq 'Published')) })

$results | Format-Table -AutoSize | Out-String -Width 160 | Write-Host
if ($results | Where-Object { -not $_.Ok }) { throw 'API user probe failed' }
Write-Host "Site ${Name}: API user probe passed."