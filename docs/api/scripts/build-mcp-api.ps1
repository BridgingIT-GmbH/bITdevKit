[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration,

    [Parameter(Mandatory = $true)]
    [string] $OutputRoot,

    [Parameter(Mandatory = $true)]
    [string] $BootstrapRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$BootstrapRoot = [System.IO.Path]::GetFullPath($BootstrapRoot)
$artifactRoot = Join-Path $BootstrapRoot 'artifacts'
$assemblyRoot = Join-Path $BootstrapRoot 'assemblies'
$metadataRoot = Join-Path $BootstrapRoot 'metadata'
$apiReferenceProject = Join-Path $repoRoot 'docs/api/ApiReference.proj'
$properties = @(
    "-property:Configuration=$Configuration",
    '-property:BdkMcpApiBootstrap=true',
    '-property:UseArtifactsOutput=true',
    "-property:ArtifactsPath=$artifactRoot"
)

function Invoke-DotNet {
    param([string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repoRoot
try {
    New-Item -ItemType Directory -Force -Path $BootstrapRoot | Out-Null

    # This separate build uses isolated bin/obj paths and never generates its own API resources.
    Invoke-DotNet -Arguments (@('msbuild', $apiReferenceProject, '-target:Restore', '-nologo', '-verbosity:minimal') + $properties)
    Invoke-DotNet -Arguments (@('msbuild', $apiReferenceProject, '-target:Build', '-nologo', '-verbosity:minimal') + $properties)

    Remove-Item $assemblyRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $metadataRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $assemblyRoot | Out-Null

    $sourceProjects = Get-ChildItem (Join-Path $repoRoot 'src') -Filter '*.csproj' -Recurse | Where-Object {
        $_.Directory.Name -notin @('Common.Utilities.CodeGen', 'Domain.CodeGen') -and
        $_.FullName -notmatch '[\\/](obj|bin)[\\/]'
    } | Sort-Object FullName

    foreach ($sourceProject in $sourceProjects) {
        $targetOutput = & dotnet msbuild $sourceProject.FullName '-getProperty:TargetPath' '-nologo' @properties
        if ($LASTEXITCODE -ne 0) {
            throw "Could not resolve the API assembly output for '$($sourceProject.FullName)'."
        }

        $targetPath = @($targetOutput | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })[-1].Trim()
        if (-not (Test-Path $targetPath -PathType Leaf)) {
            throw "API assembly '$targetPath' was not produced by the bootstrap build."
        }

        Copy-Item $targetPath $assemblyRoot -Force
        $documentationPath = [System.IO.Path]::ChangeExtension($targetPath, '.xml')
        if (Test-Path $documentationPath -PathType Leaf) {
            Copy-Item $documentationPath $assemblyRoot -Force
        }
    }

    & (Join-Path $PSScriptRoot 'build-docfx-metadata.ps1') -AssemblyRoot $assemblyRoot -MetadataRoot $metadataRoot

    Write-Host 'Building the MCP API index from DocFX metadata.'
    & (Join-Path $PSScriptRoot 'build-agent-index.ps1') -MetadataRoot $metadataRoot -OutputRoot $OutputRoot
    if (-not (Test-Path (Join-Path $OutputRoot 'agent-index.json') -PathType Leaf)) {
        throw 'Embedded MCP API documentation is missing api/agent-index.json after generation.'
    }
}
finally {
    Pop-Location
}
