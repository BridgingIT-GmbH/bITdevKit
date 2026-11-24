[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Output,
    [switch] $Smoke,
    [switch] $RandomOnly,
    [switch] $UpdateOnly,
    [switch] $Resume
)
$ErrorActionPreference = 'Stop'
if ($RandomOnly -and $UpdateOnly) { throw 'Choose one workload filter.' }
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifactRoot = Join-Path $workspace 'artifacts/storage-dimensions'
$outputPath = [IO.Path]::GetFullPath($Output, $workspace)
if (-not $outputPath.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must stay under the storage benchmark artifacts directory.' }
$active = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object { $_.CommandLine -match '--storage-(dimensions-(qualification|diagnostic)|random-reads)\b' })
if ($active.Count) { throw 'A storage workload is already active.' }
$binary = Join-Path $PSScriptRoot 'bin/Release/net10.0/Application.Benchmarks.dll'
$fingerprints = @($binary, (Join-Path (Split-Path $binary) 'BridgingIT.DevKit.Application.Storage.dll'), (Join-Path (Split-Path $binary) 'BridgingIT.DevKit.Infrastructure.EntityFramework.dll'), (Join-Path (Split-Path $binary) 'Application.Benchmarks.runtimeconfig.json'))
function Get-Fingerprint { (@($fingerprints | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }) -join ':') }
$identity = [ordered]@{ BinarySHA256 = Get-Fingerprint; Smoke = [bool]$Smoke; RandomOnly = [bool]$RandomOnly; UpdateOnly = [bool]$UpdateOnly; GCServer = $env:DOTNET_gcServer }
$stateFile = Join-Path $outputPath 'progress.json'
if ($Resume) {
    $state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json -AsHashtable
    if (($state.Identity | ConvertTo-Json -Compress) -cne ($identity | ConvertTo-Json -Compress)) { throw 'Resume requires identical binaries, options and GC environment.' }
} else {
    if (Test-Path -LiteralPath $outputPath) { throw 'Output already exists; use Resume or a new directory.' }
    New-Item -ItemType Directory -Path $outputPath | Out-Null
    $cases = @()
    if (-not $RandomOnly) {
        $assessment = Get-Content -LiteralPath (Join-Path $artifactRoot 'qualification-final-01/assessment.json') -Raw | ConvertFrom-Json
        foreach ($engine in @('sqlserver', 'postgres')) {
            $original = $assessment.Cases | Where-Object { $_.Engine -eq $engine -and $_.Feature -eq 'combined' -and $_.Owners -eq 1000000 -and $_.Profile -eq 'reference' }
            $archive = (Get-Content -LiteralPath (Join-Path $original.Evidence 'restored-seed.json') -Raw | ConvertFrom-Json).Source
            $cases += [ordered]@{ Key = "update-$engine-1000000"; Workload = 'update'; Engine = $engine; Feature = 'combined'; Owners = 1000000; RestoreSeed = $(if ($Smoke) { $null } else { $archive }); OriginalEvidence = $original.Evidence; Status = 'Pending' }
        }
    }
    if (-not $UpdateOnly) {
        foreach ($owners in $(if ($Smoke) { @(10000) } else { @(10000, 100000, 1000000) })) {
            foreach ($engine in @('sqlserver', 'postgres')) {
                foreach ($feature in @('blob', 'document')) {
                    $cases += [ordered]@{ Key = "random-$engine-$feature-$owners"; Workload = 'random'; Engine = $engine; Feature = $feature; Owners = $owners; Status = 'Pending' }
                }
            }
        }
    }
    $state = [ordered]@{ Identity = $identity; StartedAtUtc = [DateTimeOffset]::UtcNow; Complete = $false; Cases = $cases }
}
function Save-Progress {
    $state.UpdatedAtUtc = [DateTimeOffset]::UtcNow
    $temporaryFile = $stateFile + '.tmp'
    $state | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $temporaryFile -Encoding utf8
    [IO.File]::Move($temporaryFile, $stateFile, $true)
}
Push-Location -LiteralPath $workspace
try {
    Save-Progress
    foreach ($case in $state.Cases) {
        if ($case.Status -eq 'Passed') { continue }
        if ((Get-Fingerprint) -cne $state.Identity.BinarySHA256) { throw 'Binaries changed during the matrix.' }
        if ($case.Status -in @('Running', 'Failed')) {
            if (-not $case.Contains('PreviousAttempts')) { $case.PreviousAttempts = @() }
            $case.PreviousAttempts += @{ Output = $case.Output; Status = $case.Status; ExitCode = $case.ExitCode }
        }
        $case.Output = Join-Path $outputPath ($case.Key + '-' + [guid]::NewGuid().ToString('N').Substring(0, 12))
        $case.Status = 'Running'; $case.ExitCode = $null; $case.StartedAtUtc = [DateTimeOffset]::UtcNow
        Save-Progress
        $arguments = @($(if ($case.Workload -eq 'random') { '--storage-random-reads' } else { '--storage-dimensions-qualification' }), '--engine', $case.Engine, '--feature', $case.Feature, '--owners', [string]$case.Owners, '--output', $case.Output, '--sql-cost-threshold', '50', '--sql-max-dop', '1')
        if ($case.Workload -eq 'update') { $arguments += '--dimension-update-recheck' }
        if ($case.RestoreSeed) { $arguments += @('--restore-seed', $case.RestoreSeed) }
        if ($Smoke) { $arguments += '--smoke' }
        Write-Output "Starting $($case.Key); log $($case.Output).log"
        & dotnet $binary @arguments *> ($case.Output + '.log')
        $case.ExitCode = $LASTEXITCODE
        $case.Status = if ($LASTEXITCODE -eq 0) { 'Passed' } else { 'Failed' }
        $case.CompletedAtUtc = [DateTimeOffset]::UtcNow
        Save-Progress
        if ($case.ExitCode -ne 0) { throw "Case $($case.Key) failed; evidence retained. Inspect before resuming." }
    }
    $state.Complete = $true
    Save-Progress
} finally { Pop-Location }
