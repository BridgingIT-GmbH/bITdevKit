[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('bitdevkit-api-index-' + [guid]::NewGuid().ToString('N'))
$metadataRoot = Join-Path $fixtureRoot 'metadata'
$outputRoot = Join-Path $fixtureRoot 'output'
New-Item -ItemType Directory -Path $metadataRoot -Force | Out-Null

try {
    @'
### YamlMime:ManagedReference
items:
- uid: BridgingIT.DevKit.Common.IndexFixture
  name: IndexFixture
  fullName: BridgingIT.DevKit.Common.IndexFixture
  type: Class
  namespace: BridgingIT.DevKit.Common
- uid: BridgingIT.DevKit.Common.IndexFixture.Run(System.String)
  name: Run
  fullName: BridgingIT.DevKit.Common.IndexFixture.Run(System.String)
  type: Method
  parent: BridgingIT.DevKit.Common.IndexFixture
  summary: Runs the fixture.
- uid: BridgingIT.DevKit.Common.IndexFixture.#ctor
  name: IndexFixture
  fullName: BridgingIT.DevKit.Common.IndexFixture.#ctor
  type: Constructor
  parent: BridgingIT.DevKit.Common.IndexFixture
- uid: BridgingIT.DevKit.Common.IndexFixture.MaximumAge
  name: MaximumAge
  fullName: BridgingIT.DevKit.Common.IndexFixture.MaximumAge
  type: Property
  parent: BridgingIT.DevKit.Common.IndexFixture
  summary: Preserves the final member.
references:
- uid: BridgingIT.DevKit.Common.ExternalFixture
  isExternal: true
  name: ExternalFixture
  fullName: BridgingIT.DevKit.Common.ExternalFixture
  href: external.html
'@ | Set-Content -Path (Join-Path $metadataRoot 'IndexFixture.yml') -Encoding utf8

    @'
### YamlMime:ManagedReference
items:
- uid: BridgingIT.DevKit.Common.IndexFixtureWithoutReferences
  name: IndexFixtureWithoutReferences
  fullName: BridgingIT.DevKit.Common.IndexFixtureWithoutReferences
  type: Class
'@ | Set-Content -Path (Join-Path $metadataRoot 'IndexFixtureWithoutReferences.yml') -Encoding utf8

    foreach ($batch in @('First', 'Second')) {
        $batchRoot = Join-Path $metadataRoot $batch
        New-Item -ItemType Directory -Path $batchRoot | Out-Null
        @"
### YamlMime:ManagedReference
items:
- uid: BridgingIT.DevKit.Common.BatchFixture
  name: BatchFixture
  fullName: BridgingIT.DevKit.Common.BatchFixture
  type: Namespace
  children:
  - BridgingIT.DevKit.Common.BatchFixture.$batch
- uid: BridgingIT.DevKit.Common.BatchFixture.$batch
  name: $batch
  fullName: BridgingIT.DevKit.Common.BatchFixture.$batch
  type: Class
"@ | Set-Content -Path (Join-Path $batchRoot 'BatchFixture.yml') -Encoding utf8
    }

    & (Join-Path $PSScriptRoot 'build-agent-index.ps1') -MetadataRoot $metadataRoot -OutputRoot $outputRoot
    $index = Get-Content -Path (Join-Path $outputRoot 'agent-index.json') -Raw | ConvertFrom-Json
    if ($index.symbols.Count -ne 8) {
        throw "Expected eight distinct symbols after combining the batches. Found $($index.symbols.Count)."
    }

    $expectedMembers = @{
        'BridgingIT.DevKit.Common.IndexFixture.Run(System.String)' = 'BridgingIT_DevKit_Common_IndexFixture_Run_System_String_'
        'BridgingIT.DevKit.Common.IndexFixture.#ctor' = 'BridgingIT_DevKit_Common_IndexFixture__ctor'
        'BridgingIT.DevKit.Common.IndexFixture.MaximumAge' = 'BridgingIT_DevKit_Common_IndexFixture_MaximumAge'
    }
    foreach ($uid in $expectedMembers.Keys) {
        $symbol = @($index.symbols | Where-Object { $_.uid -eq $uid })
        $expectedHref = 'obj/api/IndexFixture.html#' + $expectedMembers[$uid]
        if ($symbol.Count -ne 1 -or $symbol[0].href -ne $expectedHref) {
            throw "Expected the actual DocFX anchor '$expectedHref' for '$uid'."
        }

        $page = Get-Content -Path (Join-Path $outputRoot $symbol[0].detail) -Raw | ConvertFrom-Json
        $detail = @($page.symbols | Where-Object { $_.uid -eq $uid })
        if ($detail.Count -ne 1 -or -not $detail[0].url.EndsWith($expectedHref, [System.StringComparison]::Ordinal)) {
            throw "Missing matching detail URL for '$uid'."
        }
    }

    $last = $index.symbols | Where-Object { $_.uid -eq 'BridgingIT.DevKit.Common.IndexFixture.MaximumAge' }
    if ($last.kind -ne 'Property' -or $last.summary -ne 'Preserves the final member.') {
        throw 'Reference metadata must not overwrite the final local member.'
    }

    $namespaceUid = 'BridgingIT.DevKit.Common.BatchFixture'
    $namespaceEntry = @($index.symbols | Where-Object uid -eq $namespaceUid)
    if ($namespaceEntry.Count -ne 1) {
        throw 'Expected one index entry for the namespace shared across batches.'
    }

    $batchPage = Get-Content -Path (Join-Path $outputRoot $namespaceEntry[0].detail) -Raw | ConvertFrom-Json
    $namespaceDetail = $batchPage.symbols | Where-Object uid -eq $namespaceUid
    foreach ($batch in @('First', 'Second')) {
        $uid = "$namespaceUid.$batch"
        if ($uid -notin $namespaceDetail.children -or $uid -notin $batchPage.symbols.uid) {
            throw "Missing namespace child or symbol detail for '$uid' after combining batches."
        }
    }

    Write-Host 'API index regression passed: item boundaries, anchors, detail URLs, and pages shared across batches.'
}
finally {
    Remove-Item -Path $fixtureRoot -Recurse -Force
}
