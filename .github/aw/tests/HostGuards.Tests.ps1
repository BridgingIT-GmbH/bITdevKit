$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot '../Set-AgenticWorkflowHostGuards.ps1'
$directory = Join-Path ([IO.Path]::GetTempPath()) "agentic-host-policy-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $directory | Out-Null

try {
    $fixture = @'
name: Fixture
on:
  workflow_dispatch:
jobs:
  activation:
    runs-on: ubuntu-latest
    steps:
      - run: echo ready
  agent:
    if: >
      needs.activation.result == 'success' &&
      (github.event_name == 'issues' || github.event_name == 'workflow_dispatch')
    runs-on: ubuntu-latest
    steps:
      - run: echo agent
        if: always()
  cleanup:
    if: ${{ always() && needs.agent.result != 'skipped' }}
    runs-on: ubuntu-latest
    steps:
      - run: echo cleanup
'@
    $fixture = $fixture -replace '\r\n', "`n"
    $paths = @('fixture.lock.yml', 'agentics-maintenance.yml') | ForEach-Object { Join-Path $directory $_ }
    foreach ($path in $paths) {
        [IO.File]::WriteAllText($path, $fixture)
    }

    $rejected = $false
    try { & $script -WorkflowDirectory $directory -Check } catch { $rejected = $true }
    if (!$rejected) { throw 'Check accepted unguarded jobs.' }

    & $script -WorkflowDirectory $directory
    & $script -WorkflowDirectory $directory -Check
    $result = [IO.File]::ReadAllText($paths[0])
    $expectedConditions = @(
        "    if: github.server_url != 'https://github.com'",
        "    if: github.server_url != 'https://github.com' && (needs.activation.result == 'success' && (github.event_name == 'issues' || github.event_name == 'workflow_dispatch'))",
        "    if: github.server_url != 'https://github.com' && (always() && needs.agent.result != 'skipped')"
    )
    $conditions = @($result -split '\r?\n' | Where-Object { $_ -match '^    if:' })
    if (($conditions -join "`n") -cne ($expectedConditions -join "`n")) {
        throw 'Job conditions were not preserved under the host guard.'
    }
    if (!$result.Contains('        if: always()')) { throw 'Step condition changed.' }
    if (!$result.StartsWith("name: Fixture`non:`n  workflow_dispatch:")) { throw 'Workflow trigger changed.' }

    & $script -WorkflowDirectory $directory
    if ([IO.File]::ReadAllText($paths[0]) -cne $result) { throw 'Guard generation is not idempotent.' }

    # Check must also reject a missing maintenance guard, even with guarded locks.
    [IO.File]::WriteAllText($paths[1], $fixture)
    $rejected = $false
    try { & $script -WorkflowDirectory $directory -Check } catch { $rejected = $true }
    if (!$rejected) { throw 'Check accepted unguarded maintenance.' }

    Write-Host 'Host policy tests passed.'
}
finally {
    # Only remove the unique fixture files created by this test.
    foreach ($path in $paths) { Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath $directory
}
