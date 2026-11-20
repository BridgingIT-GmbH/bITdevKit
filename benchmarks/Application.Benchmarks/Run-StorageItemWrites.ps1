[CmdletBinding()]
param([Parameter(Mandatory)][string] $Output, [switch] $Smoke, [switch] $Resume,
    [ValidateSet('sqlserver','postgres')][string[]] $Engines = @('sqlserver','postgres'), [switch] $BulkUploadsOnly,
    [ValidateSet(0,10000,100000,1000000)][int] $Owners = 0)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root = [IO.Path]::GetFullPath($Output, $workspace)
$artifacts = Join-Path $workspace 'artifacts/storage-dimensions'
if (-not $root.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be under storage benchmark artifacts.' }
$active = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object { $_.CommandLine -match '--storage-(item-writes|dimensions-(qualification|diagnostic)|random-reads)\b' })
if ($active.Count) { throw 'A storage workload is already active.' }
$binary = Join-Path $PSScriptRoot 'bin/Release/net10.0/Application.Benchmarks.dll'
$files = @($binary, (Join-Path (Split-Path $binary) 'BridgingIT.DevKit.Application.Storage.dll'), (Join-Path (Split-Path $binary) 'BridgingIT.DevKit.Infrastructure.EntityFramework.dll'), (Join-Path (Split-Path $binary) 'Application.Benchmarks.runtimeconfig.json'))
function Get-Fingerprint { (@($files | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }) -join ':') }
$identity = [ordered]@{ Fingerprint = Get-Fingerprint; Smoke = [bool]$Smoke; ServerGC = $env:DOTNET_gcServer; Engines = $Engines; BulkUploadsOnly = [bool]$BulkUploadsOnly }
if ($Owners -ne 0) { $identity.Owners = $Owners }
$stateFile = Join-Path $root 'progress.json'
if ($Resume) {
    $state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json -AsHashtable
    if (($state.Identity | ConvertTo-Json -Compress) -cne ($identity | ConvertTo-Json -Compress)) { throw 'Resume requires identical binaries and options.' }
} else {
    if (Test-Path -LiteralPath $root) { throw 'Output already exists; use a new path or Resume.' }
    New-Item -ItemType Directory -Path $root | Out-Null
    $state = [ordered]@{ Identity = $identity; StartedAtUtc = [DateTimeOffset]::UtcNow; Complete = $false; Cases = @(foreach($engine in $Engines) {
        [ordered]@{ Engine = $engine; Status = 'Pending' } }) }
}
function Save-Progress {
    $state.UpdatedAtUtc = [DateTimeOffset]::UtcNow
    $temporary = $stateFile + '.tmp'
    $state | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $temporary -Encoding utf8
    [IO.File]::Move($temporary, $stateFile, $true)
}
Push-Location -LiteralPath $workspace
try {
    Save-Progress
    foreach ($case in $state.Cases) {
        if ($case.Status -eq 'Passed') { continue }
        if ((Get-Fingerprint) -cne $state.Identity.Fingerprint) { throw 'Binaries changed during the matrix.' }
        if ($case.Status -in @('Running', 'Failed')) {
            if (-not $case.Contains('PreviousAttempts')) { $case.PreviousAttempts = @() }
            $case.PreviousAttempts += @{ Output = $case.Output; Status = $case.Status }
        }
        $case.Output = Join-Path $root ($case.Engine + '-' + [guid]::NewGuid().ToString('N').Substring(0, 12))
        $case.Status = 'Running'; $case.StartedAtUtc = [DateTimeOffset]::UtcNow
        Save-Progress
        $arguments = @('--storage-item-writes', '--engine', $case.Engine, '--feature', 'combined', '--output', $case.Output)
        if ($Owners -ne 0) { $arguments += @('--owners', [string]$Owners) }
        if ($Smoke) { $arguments += '--smoke' }
        if ($BulkUploadsOnly) { $arguments += '--bulk-uploads-only' }
        Write-Output "Starting item writes on $($case.Engine); log $($case.Output).log"
        & dotnet $binary @arguments *> ($case.Output + '.log')
        $case.ExitCode = $LASTEXITCODE; $case.CompletedAtUtc = [DateTimeOffset]::UtcNow
        $case.Status = if ($case.ExitCode -eq 0) { 'Passed' } else { 'Failed' }
        Save-Progress
        if ($case.ExitCode -ne 0) { throw "Case $($case.Engine) failed; inspect evidence before resuming." }
        if (Test-Path -LiteralPath (Join-Path $root 'STOP-AFTER-CASE')) { return }
    }
    $state.Complete = $true; Save-Progress
} finally { Pop-Location }
