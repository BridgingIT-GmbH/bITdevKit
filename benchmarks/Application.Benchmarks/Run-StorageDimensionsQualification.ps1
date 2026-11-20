# Run the declared matrix sequentially. Each case owns its catalogs/files and keeps evidence.
[CmdletBinding()]
param(
    [ValidateSet('sqlite', 'postgres', 'sqlserver')][string[]] $Engines = @('sqlserver', 'postgres'),
    [ValidateSet('blob', 'document', 'combined')][string[]] $Features = @('blob', 'document', 'combined'),
    [ValidateSet(10000, 100000, 1000000)][int[]] $Owners = @(10000, 100000, 1000000),
    [ValidateSet('reference', 'sparse', 'skew')][string] $Profile = 'reference',
    [switch] $Stress,
    [switch] $Smoke,
    [switch] $Diagnostic,
    [switch] $Scaling,
    [string] $RestoreSeed,
    [ValidateRange(5, 32767)][int] $SqlCostThreshold = 5,
    [ValidateRange(0, 4)][int] $SqlMaxDop = 0,
    [ValidateSet(3, 5)][int] $OrdinaryComparisonPairs = 3,
    [string] $Output,
    [switch] $Resume
)
$ErrorActionPreference = 'Stop'
if ($RestoreSeed -and ($Engines.Count -ne 1 -or $Engines[0] -notin @('sqlserver', 'postgres') -or $Features.Count -ne 1 -or $Owners.Count -ne 1)) { throw 'Seed restore requires one SQL Server/PostgreSQL feature/count case.' }
if ($Scaling -and $Profile -ne 'reference') { throw 'Scaling acceptance uses the reference profile; use separate stress/diagnostic runs for sparse or skew data.' }
if (@($Smoke, $Diagnostic, $Scaling).Where({ [bool]$_ }).Count -gt 1) { throw 'Choose one of Smoke, Diagnostic or Scaling.' }
if (-not $PSBoundParameters.ContainsKey('Engines') -and ($Smoke -or $Diagnostic)) { $Engines = @('sqlserver', 'postgres', 'sqlite') }
if (-not $Smoke -and -not $Diagnostic -and $Engines -contains 'sqlite') { throw 'SQLite has diagnostic/correctness coverage; use -Diagnostic or -Smoke. Long performance qualification targets SQL Server/PostgreSQL.' }
$activeWorkloads = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
    Where-Object { $_.CommandLine -match '--storage-dimensions-(qualification|diagnostic|metadata-profile|snapshot-profile)\b' })
if ($activeWorkloads.Count -ne 0) { throw 'Another storage Dimensions workload is active. Finish it before starting or resuming this matrix.' }
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifacts = Join-Path $workspace 'artifacts/storage-dimensions'
$binary = Join-Path $PSScriptRoot 'bin/Release/net10.0/Application.Benchmarks.dll'
if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) { throw 'Build the Release benchmark before running the matrix.' }
if (-not $Output) { $Output = Join-Path $artifacts ('matrix-' + (Get-Date -Format 'yyyyMMddTHHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 12)) }
$outputPath = [IO.Path]::GetFullPath($Output, $workspace)
if (-not $outputPath.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Output must be a dedicated child of artifacts/storage-dimensions.'
}
$statePath = Join-Path $outputPath 'matrix-state.json'
$fingerprintFiles = @($binary, (Join-Path (Split-Path $binary) 'BridgingIT.DevKit.Application.Storage.dll'),
    (Join-Path (Split-Path $binary) 'BridgingIT.DevKit.Infrastructure.EntityFramework.dll'),
    (Join-Path (Split-Path $binary) 'Application.Benchmarks.runtimeconfig.json'))
function Get-BinaryFingerprint { (@($fingerprintFiles | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }) -join ':') }
$hash = Get-BinaryFingerprint
# Scaling visits both server engines at each size before advancing to the expensive larger fixtures.
$cases = if ($Scaling) {
    @(foreach ($count in $Owners) { foreach ($engine in $Engines) { foreach ($feature in $Features) {
        [ordered]@{ Key = "$engine-$feature-$count-$Profile"; Engine = $engine; Feature = $feature; Owners = $count; ExitCode = $null; Status = 'Pending' }
    } } })
} else {
    @(foreach ($engine in $Engines) { foreach ($feature in $Features) { foreach ($count in $Owners) {
        [ordered]@{ Key = "$engine-$feature-$count-$Profile"; Engine = $engine; Feature = $feature; Owners = $count; ExitCode = $null; Status = 'Pending' }
    } } })
}
$gcEnvironment = [ordered]@{}
foreach ($name in @('DOTNET_gcServer', 'COMPlus_gcServer', 'DOTNET_gcConcurrent', 'COMPlus_gcConcurrent', 'DOTNET_GCHeapCount', 'COMPlus_GCHeapCount')) {
    $gcEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}
$identity = [ordered]@{ BinarySHA256 = $hash; GCEnvironment = $gcEnvironment; Profile = $Profile; Stress = [bool]$Stress; Smoke = [bool]$Smoke; Diagnostic = [bool]$Diagnostic; Scaling = [bool]$Scaling; RestoreSeed = $RestoreSeed; SqlCostThreshold = $SqlCostThreshold; SqlMaxDop = $SqlMaxDop; OrdinaryComparisonPairs = $OrdinaryComparisonPairs; Keys = @($cases.Key) }
if ($Resume) {
    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) { throw 'Resume requires the existing matrix-state.json.' }
    $old = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json -AsHashtable
    if (($old.Identity | ConvertTo-Json -Compress -Depth 10) -cne ($identity | ConvertTo-Json -Compress -Depth 10)) {
        throw 'Matrix parameters or Release binary differ; use a new output directory.'
    }
    $cases = $old.Cases
} else {
    if (Test-Path -LiteralPath $outputPath) { throw 'Output already exists; use a new directory or explicit Resume.' }
    New-Item -ItemType Directory -Path $outputPath | Out-Null
}
$state = [ordered]@{ Identity = $identity; StartedAt = $(if ($Resume) { $old.StartedAt } else { [DateTimeOffset]::UtcNow }); Cases = $cases; MatrixMeasurementsPassed = $false; QualificationComplete = $false }
function Save-State {
    $state.UpdatedAt = [DateTimeOffset]::UtcNow
    $temporaryState = $statePath + '.tmp'
    $state | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $temporaryState -Encoding utf8
    [IO.File]::Move($temporaryState, $statePath, $true)
}
Push-Location -LiteralPath $workspace
try {
    Save-State
    foreach ($case in $cases) {
        if ($case.Status -eq 'Passed') { continue }
        if ((Get-BinaryFingerprint) -cne $hash) { throw 'Release binary changed during the matrix.' }
        # A crashed/failed attempt keeps its owned files. A fresh attempt always uses a new directory.
        $attempt = $case.Key + '-' + [guid]::NewGuid().ToString('N').Substring(0, 12)
        $case.Output = Join-Path $outputPath $attempt
        $case.Status = 'Running'
        $case.ExitCode = $null
        $case.Remove('InterruptionReason')
        Save-State
        $dispatch = if ($Diagnostic) { '--storage-dimensions-diagnostic' } else { '--storage-dimensions-qualification' }
        $arguments = @($dispatch, '--engine', $case.Engine, '--feature', $case.Feature,
            '--owners', [string]$case.Owners, '--profile', $Profile, '--seed', '17043', '--output', $case.Output)
        if ($Scaling) { $arguments += '--scaling' }
        if ($RestoreSeed) { $arguments += @('--restore-seed', $RestoreSeed) }
        if ($case.Engine -eq 'sqlserver') { $arguments += @('--sql-cost-threshold', [string]$SqlCostThreshold, '--sql-max-dop', [string]$SqlMaxDop) }
        $arguments += @('--ordinary-comparison-pairs', [string]$OrdinaryComparisonPairs)
        if ($Stress) { $arguments += '--stress' }
        if ($Smoke) { $arguments += '--smoke' }
        Write-Host "Running $($case.Key); log: $($case.Output).log"
        & dotnet $binary @arguments *> ($case.Output + '.log')
        $case.ExitCode = $LASTEXITCODE
        if ($case.ExitCode -ne 0) {
            $case.Status = 'Failed'
            Save-State
            throw "Case $($case.Key) failed. Inspect its evidence before resuming."
        }
        $report = Get-Content -LiteralPath (Join-Path $case.Output 'qualification.json') -Raw | ConvertFrom-Json
        if (-not $report.OperationChecksPassed) { throw "Case $($case.Key) has operation failures." }
        if (-not $Smoke -and -not $report.Preflight.EffectiveApplicationBudget.Verified) { throw 'Effective resource limits were not verified.' }
        if (-not $Smoke -and -not $Diagnostic -and -not $Scaling -and $Profile -eq 'reference' -and -not $report.MeasuredGateSubsetPassed) { throw 'The reference measured gate subset did not pass.' }
        if ($Scaling -and -not $report.ScalingChecksPassed) { throw 'Measured scalability checks did not pass.' }
        $case.Status = 'Passed'
        Save-State
    }
    $state.OperationMatrixPassed = $true
    $state.ScalingMatrixPassed = if ($Scaling) { $true } else { $null }
    $state.MatrixMeasurementsPassed = if ($Smoke -or $Diagnostic -or $Scaling -or $Profile -ne 'reference') { $null } else { $true }
    $state.Note = 'All requested cases passed their measured subset. Full qualification still requires the complete declared matrix, stress evidence and plan review; smoke/diagnostic modes have no performance acceptance claim.'
    Save-State
} finally { Pop-Location }
