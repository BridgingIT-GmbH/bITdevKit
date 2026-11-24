[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$sourceRoot = Join-Path $repoRoot 'src'
$apiReferenceProject = Join-Path $repoRoot 'docs\api\ApiReference.proj'
$docfxConfig = Join-Path $repoRoot 'docs\api\docfx.json'
$docfxObjRoot = Join-Path $repoRoot 'docs\api\obj'
$apiAssemblyRoot = Join-Path $docfxObjRoot 'assemblies'
$apiBatchesRoot = Join-Path $docfxObjRoot 'metadata-batches'
$apiMetadataScript = Join-Path $repoRoot 'docs/api/scripts/build-docfx-metadata.ps1'
$apiMetadataRoot = Join-Path $docfxObjRoot 'api'
$apiAgentIndexScript = Join-Path $repoRoot 'docs\api\scripts\build-agent-index.ps1'
$apiPagesRoot = Join-Path $repoRoot '.github\pages\api'

& (Join-Path $PSScriptRoot 'sync-docs.ps1')

$mkdocsImage = 'squidfunk/mkdocs-material:9'
$dockerUserArgs = @()
if ($IsLinux -or $IsMacOS) {
    $userId = (id -u).Trim()
    $groupId = (id -g).Trim()
    $dockerUserArgs = @('--user', "${userId}:${groupId}")
}

docker run --rm `
    @dockerUserArgs `
    -v "${repoRoot}:/docs" `
    $mkdocsImage `
    build --clean

dotnet tool restore

dotnet msbuild $apiReferenceProject `
    -target:Restore `
    -property:Configuration=Release `
    -property:BdkMcpApiBootstrap=true `
    -verbosity:minimal `
    -nologo

dotnet msbuild $apiReferenceProject `
    -target:Build `
    -property:Configuration=Release `
    -property:BdkMcpApiBootstrap=true `
    -maxCpuCount `
    -verbosity:minimal `
    -nologo

Remove-Item -Path $apiAssemblyRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $apiBatchesRoot -Recurse -Force -ErrorAction SilentlyContinue
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

& $apiMetadataScript -AssemblyRoot $apiAssemblyRoot -MetadataRoot $apiBatchesRoot

Write-Host 'Merging DocFX pages and navigation.'
docker run --rm `
    @dockerUserArgs `
    -v "${repoRoot}:/docs" `
    --workdir /docs `
    --entrypoint python `
    $mkdocsImage `
    docs/api/scripts/merge-docfx-metadata.py docs/api/obj/metadata-batches docs/api/obj/api

Write-Host 'Rendering DocFX API pages.'
dotnet docfx build $docfxConfig --maxParallelism 1 --logLevel Error

& (Join-Path $repoRoot 'docs\api\scripts\fix-namespace-links.ps1') -OutputRoot $apiPagesRoot

& (Join-Path $repoRoot 'docs\api\scripts\test-agent-index.ps1')

& $apiAgentIndexScript `
    -MetadataRoot $apiMetadataRoot `
    -OutputRoot $apiPagesRoot
