#Requires -Version 7.0
# Reads document types and documents straight from each test site's SQLite database and compares keys by alias/name.
# Expected after new-test-sites.ps1: A and B identical keys; C same aliases and names, different keys.
# Run while the sites are stopped (or at least idle). Usage: ./tools/compare-test-site-keys.ps1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-sites.common.ps1')

# The test site's build output carries Microsoft.Data.Sqlite and its native engine.
$bin = Split-Path $script:TestSiteDll -Parent
foreach ($assembly in 'SQLitePCLRaw.core', 'SQLitePCLRaw.provider.e_sqlite3', 'SQLitePCLRaw.batteries_v2', 'Microsoft.Data.Sqlite') {
    Add-Type -Path (Join-Path $bin "$assembly.dll")
}
$env:PATH = "$(Join-Path $bin 'runtimes/win-x64/native');$env:PATH"
[SQLitePCL.Batteries_V2]::Init()

# Umbraco object types (Constants.ObjectTypes). Types match by alias, everything else by name.
$kinds = [ordered]@{
    '30a2a501-1978-4ddb-a57b-f7efed43ba3c' = 'data type'
    'a2cb7800-f571-4787-9638-bc48539a0efb' = 'document type'
    '4ea4382b-2f5a-4c2b-9587-ae9b3cf3602e' = 'media type'
    'c66ba18e-eaf3-4cff-8a22-41b16d66a972' = 'document'
    'b796f64c-1f99-4ffb-b886-4bf4bc011a9c' = 'media'
}
$query = @"
SELECT lower(n.nodeObjectType) AS kind, lower(n.uniqueId) AS key, coalesce(ct.alias, n.text) AS name
FROM umbracoNode n LEFT JOIN cmsContentType ct ON ct.nodeId = n.id
WHERE lower(n.nodeObjectType) IN ('$(@($kinds.Keys) -join "','")') AND n.trashed = 0
"@

$rows = @{}
foreach ($name in $script:SitePorts.Keys) {
    $db = Join-Path (Get-TestSitePath $name) 'umbraco/Data/Umbraco.sqlite.db'
    if (-not (Test-Path $db)) { Write-Warning "Site $name has no database"; continue }
    $connection = [Microsoft.Data.Sqlite.SqliteConnection]::new("Data Source=$db;Mode=ReadOnly")
    $connection.Open()
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = $query
        $reader = $command.ExecuteReader()
        $rows[$name] = while ($reader.Read()) {
            [pscustomobject]@{ Kind = $kinds[[string]$reader['kind']]; Name = $reader['name']; Key = $reader['key'] }
        }
    }
    finally {
        $connection.Dispose()
    }
}

$rows.Keys | Sort-Object | ForEach-Object {
    $r = $rows[$_]
    $counts = foreach ($kind in $kinds.Values) { "{0} {1}" -f @($r | Where-Object Kind -eq $kind).Count, $kind }
    Write-Host ("Site {0}: {1}" -f $_, ($counts -join ', '))
}

function Compare-Site([string] $Left, [string] $Right) {
    $byName = @{}
    foreach ($row in $rows[$Right]) { $byName["$($row.Kind):$($row.Name)"] = $row.Key }
    foreach ($kind in $kinds.Values) {
        $same = 0; $different = 0; $missing = 0
        foreach ($row in $rows[$Left] | Where-Object Kind -eq $kind) {
            $other = $byName["$($row.Kind):$($row.Name)"]
            if ($null -eq $other) { $missing++ } elseif ($other -eq $row.Key) { $same++ } else { $different++ }
        }
        Write-Host ("{0} vs {1}, {2}: same key {3}, different key {4}, missing {5}" -f $Left, $Right, $kind, $same, $different, $missing)
    }
}

if ($rows.ContainsKey('A') -and $rows.ContainsKey('B')) { Compare-Site 'A' 'B' }
if ($rows.ContainsKey('A') -and $rows.ContainsKey('C')) { Compare-Site 'A' 'C' }