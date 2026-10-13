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

    & (Join-Path $PSScriptRoot 'build-agent-index.ps1') -MetadataRoot $metadataRoot -OutputRoot $outputRoot
    $index = Get-Content -Path (Join-Path $outputRoot 'agent-index.json') -Raw | ConvertFrom-Json
    if ($index.symbols.Count -ne 5) {
        throw "Expected five local symbols, including the last item before references and the last item at EOF; found $($index.symbols.Count)."
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

    Write-Host 'API index regression passed: final-item boundaries, reference exclusion, member anchors and detail URLs.'
}
finally {
    Remove-Item -Path $fixtureRoot -Recurse -Force
}
