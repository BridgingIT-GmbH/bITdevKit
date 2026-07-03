[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$siteRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$sourceRoot = Join-Path $repoRoot 'src'
$apiReferenceProject = Join-Path $repoRoot 'docs\api\ApiReference.proj'
$docfxConfig = Join-Path $repoRoot 'docs\api\docfx.json'
$docfxObjRoot = Join-Path $repoRoot 'docs\api\obj'
$apiAssemblyRoot = Join-Path $docfxObjRoot 'assemblies'
$apiMetadataRoot = Join-Path $docfxObjRoot 'api'

& (Join-Path $PSScriptRoot 'sync-docs.ps1')

docker run --rm `
    -v "${repoRoot}:/docs" `
    squidfunk/mkdocs-material:9 `
    build --clean

dotnet tool restore

dotnet msbuild $apiReferenceProject `
    -target:Restore `
    -property:Configuration=Release `
    -verbosity:minimal `
    -nologo

dotnet msbuild $apiReferenceProject `
    -target:Build `
    -property:Configuration=Release `
    -maxCpuCount `
    -verbosity:minimal `
    -nologo

Remove-Item -Path $apiAssemblyRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $apiMetadataRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $apiAssemblyRoot | Out-Null

$sourceProjects = Get-ChildItem -Path $sourceRoot -Filter '*.csproj' -Recurse | Where-Object {
    $_.Directory.Name -notin @('Common.Utilities.CodeGen', 'Domain.CodeGen')
} | Sort-Object FullName

foreach ($sourceProject in $sourceProjects) {
    [xml] $projectXml = Get-Content -Path $sourceProject.FullName -Raw
    $assemblyNameNode = Select-Xml -Xml $projectXml -XPath '//*[local-name()="AssemblyName"]' | Select-Object -First 1
    $assemblyName = if ($assemblyNameNode) { $assemblyNameNode.Node.InnerText.Trim() } else { $sourceProject.BaseName }
    $releaseOutputRoot = Join-Path $sourceProject.Directory.FullName 'bin\Release'
    $assemblyPath = Get-ChildItem -Path $releaseOutputRoot -Filter "$assemblyName.dll" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '[\\/](ref|refint)[\\/]' } |
        Sort-Object FullName |
        Select-Object -First 1

    if (-not $assemblyPath) {
        throw "Could not find API reference assembly for project '$($sourceProject.FullName)' using assembly name '$assemblyName'."
    }

    Copy-Item -Path $assemblyPath.FullName -Destination $apiAssemblyRoot -Force

    $xmlDocumentationPath = [System.IO.Path]::ChangeExtension($assemblyPath.FullName, '.xml')
    if (Test-Path $xmlDocumentationPath) {
        Copy-Item -Path $xmlDocumentationPath -Destination $apiAssemblyRoot -Force
    }
}

Write-Host "Staged $($sourceProjects.Count) API reference assemblies to $apiAssemblyRoot"

dotnet docfx $docfxConfig --logLevel Error
