[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$siteRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path

& (Join-Path $PSScriptRoot 'sync-docs.ps1')

docker run --rm `
    -v "${repoRoot}:/docs" `
    squidfunk/mkdocs-material:9 `
    build --clean
