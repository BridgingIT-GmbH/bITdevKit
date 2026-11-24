[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $AssemblyRoot,

    [Parameter(Mandatory = $true)]
    [string] $MetadataRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$AssemblyRoot = (Resolve-Path -LiteralPath $AssemblyRoot).Path
$MetadataRoot = [System.IO.Path]::GetFullPath($MetadataRoot)
$filterConfig = (Resolve-Path (Join-Path $PSScriptRoot '../filterConfig.yml')).Path
New-Item -ItemType Directory -Force -Path $MetadataRoot | Out-Null

$assemblyNames = @(Get-ChildItem -LiteralPath $AssemblyRoot -Filter '*.dll' -File | Sort-Object Name | ForEach-Object Name)
if ($assemblyNames.Count -eq 0) {
    throw 'The API bootstrap build did not stage any assemblies.'
}

# DocFX retains metadata for its input assemblies until it writes the batch.
# Exit the process between batches so memory is released before the next one.
$batchSize = 8
$batchCount = [Math]::Ceiling($assemblyNames.Count / $batchSize)
$previousProcessorCount = $env:DOTNET_PROCESSOR_COUNT
try {
    $env:DOTNET_PROCESSOR_COUNT = '1'
    for ($offset = 0; $offset -lt $assemblyNames.Count; $offset += $batchSize) {
        $batch = 1 + [int] [Math]::Floor($offset / $batchSize)
        $last = [Math]::Min($offset + $batchSize, $assemblyNames.Count) - 1
        $batchName = 'batch-{0:D3}' -f $batch
        $metadataConfiguration = Join-Path $MetadataRoot "$batchName.json"
        $docfx = @{
            metadata = @(@{
                # Other staged assemblies remain in the same directory for reference resolution.
                src = @(@{ src = $AssemblyRoot; files = @($assemblyNames[$offset..$last]) })
                dest = Join-Path $MetadataRoot $batchName
                filter = $filterConfig
            })
        }
        $docfx | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $metadataConfiguration -Encoding UTF8
        Write-Host "Extracting DocFX metadata batch ${batch}/${batchCount} ($($last - $offset + 1) assemblies)."
        & dotnet docfx metadata $metadataConfiguration --logLevel Error
        if ($LASTEXITCODE -ne 0) {
            throw "DocFX metadata batch $batch failed with exit code $LASTEXITCODE."
        }
    }
}
finally {
    if ($null -eq $previousProcessorCount) {
        Remove-Item Env:DOTNET_PROCESSOR_COUNT -ErrorAction SilentlyContinue
    }
    else {
        $env:DOTNET_PROCESSOR_COUNT = $previousProcessorCount
    }
}
