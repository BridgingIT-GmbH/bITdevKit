[CmdletBinding()]
param([Parameter(Mandatory)][string] $Output, [switch] $Smoke, [switch] $Resume)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root = [IO.Path]::GetFullPath($Output, $workspace)
$artifacts = Join-Path $workspace 'artifacts/storage-dimensions'
if (-not $root.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must stay under the storage benchmark artifacts directory.' }
$active = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object { $_.CommandLine -match '--storage-(dimensions-(qualification|diagnostic)|random-reads)\b' })
if ($active.Count) { throw 'A storage workload is already active.' }
$binary = Join-Path $PSScriptRoot 'bin/Release/net10.0/Application.Benchmarks.dll'
$files = @($binary, (Join-Path (Split-Path $binary) 'BridgingIT.DevKit.Application.Storage.dll'), (Join-Path (Split-Path $binary) 'BridgingIT.DevKit.Infrastructure.EntityFramework.dll'), (Join-Path (Split-Path $binary) 'Application.Benchmarks.runtimeconfig.json'))
function Get-Fingerprint { (@($files | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }) -join ':') }
$identity = [ordered]@{ Fingerprint = Get-Fingerprint; Smoke = [bool]$Smoke; ServerGC = $env:DOTNET_gcServer; ReadRate = 200; WriteRate = 100 }
$stateFile = Join-Path $root 'progress.json'
if ($Resume) {
    $state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json -AsHashtable
    if (($state.Identity | ConvertTo-Json -Compress) -cne ($identity | ConvertTo-Json -Compress)) { throw 'Resume requires the original binaries, options and GC environment.' }
} else {
    if (Test-Path -LiteralPath $root) { throw 'Output already exists; use a new directory or Resume.' }
    New-Item -ItemType Directory -Path $root | Out-Null
    $cases = @(foreach ($owners in $(if ($Smoke) { @(10000) } else { @(10000, 100000) })) {
        foreach ($engine in $(if ($owners -eq 100000) { @('postgres', 'sqlserver') } else { @('sqlserver', 'postgres') })) {
            [ordered]@{ Key = "$engine-$owners"; Engine = $engine; Owners = $owners; Status = 'Pending'; Attempts = @() }
        }
    })
    $state = [ordered]@{ Identity = $identity; StartedAtUtc = [DateTimeOffset]::UtcNow; Complete = $false; Cases = $cases }
}
function Save-Progress {
    $state.UpdatedAtUtc = [DateTimeOffset]::UtcNow
    $state | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath ($stateFile + '.tmp') -Encoding utf8
    [IO.File]::Move($stateFile + '.tmp', $stateFile, $true)
}
Push-Location -LiteralPath $workspace
try {
    Save-Progress
    foreach ($case in $state.Cases) {
        if ($case.Status -eq 'Passed') { continue }
        if ((Get-Fingerprint) -cne $state.Identity.Fingerprint) { throw 'Binaries changed during the diagnostic matrix.' }
        if ($case.Status -in @('Running', 'Failed')) { $case.Attempts += @{ Output = $case.Output; Status = $case.Status; ExitCode = $case.ExitCode } }
        $case.Output = Join-Path $root ($case.Key + '-' + [guid]::NewGuid().ToString('N').Substring(0, 12))
        $case.Status = 'Running'; $case.ExitCode = $null; $case.StartedAtUtc = [DateTimeOffset]::UtcNow
        Save-Progress
        $arguments = @('--storage-dimensions-qualification', '--dimension-contention-profile', '--engine', $case.Engine, '--feature', 'combined', '--owners', [string]$case.Owners, '--output', $case.Output, '--sql-cost-threshold', '50', '--sql-max-dop', '1')
        if ($Smoke) { $arguments += '--smoke' }
        Write-Output "Starting $($case.Key); log $($case.Output).log"
        & dotnet $binary @arguments *> ($case.Output + '.log')
        $case.ExitCode = $LASTEXITCODE; $case.Status = if ($LASTEXITCODE -eq 0) { 'Passed' } else { 'Failed' }
        $case.CompletedAtUtc = [DateTimeOffset]::UtcNow
        Save-Progress
        if ($case.ExitCode -ne 0) { throw "Diagnostic $($case.Key) failed; evidence retained." }
    }
    $state.Complete = $true
    Save-Progress
} finally { Pop-Location }
