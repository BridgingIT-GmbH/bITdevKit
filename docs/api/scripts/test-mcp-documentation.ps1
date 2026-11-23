[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [Parameter(Mandatory = $true)]
    [string] $PackageDirectory,
    [switch] $VerifyBuilds,
    [switch] $NetworkIsolated
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$packageRoot = (Resolve-Path $PackageDirectory).Path
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('bdk-mcp-docs-' + [guid]::NewGuid().ToString('N'))
$workspace = Join-Path $temporaryRoot 'HarborWorkspace'
$toolPath = Join-Path $temporaryRoot 'tool'
$evidenceRoot = Join-Path $repoRoot 'plan/evidence/mcp-embedded-documentation'
$checks = [System.Collections.Generic.List[string]]::new()
$session = $null

function Invoke-Child {
    param([string] $Executable, [string[]] $Arguments, [string] $WorkingDirectory, [switch] $AllowFailure, [string] $PackageCache)

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    if (-not [string]::IsNullOrWhiteSpace($PackageCache)) {
        $startInfo.Environment['NUGET_PACKAGES'] = $PackageCache
    }
    foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        $output = $process.StandardOutput.ReadToEndAsync()
        $errorOutput = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(1800000)) {
            $process.Kill($true)
            throw "Timed out running $Executable."
        }
        $result = [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output = $output.GetAwaiter().GetResult()
            Error = $errorOutput.GetAwaiter().GetResult()
        }
        if ($result.ExitCode -ne 0 -and -not $AllowFailure) {
            throw "$Executable failed with exit code $($result.ExitCode).`n$($result.Output)`n$($result.Error)"
        }
        return $result
    }
    finally { $process.Dispose() }
}

function Start-McpSession {
    param([string] $Executable, [string[]] $Arguments, [string] $WorkingDirectory)

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }
    # These apply only to the child. The consuming workspace has no source docs or API files.
    $startInfo.Environment['XDG_RUNTIME_DIR'] = Join-Path $temporaryRoot 'runtime'
    $startInfo.Environment['TMPDIR'] = Join-Path $temporaryRoot 'runtime'
    $startInfo.Environment['LOCALAPPDATA'] = Join-Path $temporaryRoot 'local-app-data'
    $startInfo.Environment['HTTP_PROXY'] = 'http://127.0.0.1:9'
    $startInfo.Environment['HTTPS_PROXY'] = 'http://127.0.0.1:9'
    $startInfo.Environment['ALL_PROXY'] = 'http://127.0.0.1:9'
    $startInfo.Environment['NO_PROXY'] = ''
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $state = [pscustomobject]@{ Process = $process; ErrorTask = $process.StandardError.ReadToEndAsync(); RequestId = 0; Stopped = $false }
    try {
        $null = Invoke-McpRequest $state 'initialize' @{
            protocolVersion = '2025-06-18'
            capabilities = @{}
            clientInfo = @{ name = 'embedded-docs-smoke'; version = '1.0' }
        }
        return $state
    }
    catch {
        try { Stop-McpSession $state } catch { }
        throw
    }
}

function Invoke-McpRequest {
    param($State, [string] $Method, [hashtable] $Parameters)

    $State.RequestId++
    $requestId = $State.RequestId
    $request = @{ jsonrpc = '2.0'; id = $requestId; method = $Method; params = $Parameters }
    $State.Process.StandardInput.WriteLine(($request | ConvertTo-Json -Depth 30 -Compress))
    $State.Process.StandardInput.Flush()
    while ($true) {
        $pending = $State.Process.StandardOutput.ReadLineAsync()
        if (-not $pending.Wait(60000)) { throw "Timed out waiting for MCP response to $Method." }
        $line = $pending.GetAwaiter().GetResult()
        if ($null -eq $line) { throw "MCP exited before responding to $Method." }
        $response = $line | ConvertFrom-Json -AsHashtable -Depth 100
        if (-not $response.ContainsKey('id')) { continue }
        if ($response.id -ne $requestId) { throw "MCP response ID differs from request ID $requestId." }
        if ($response.ContainsKey('error')) { throw "MCP protocol error: $($response.error | ConvertTo-Json -Compress)" }
        return $response.result
    }
}

function Invoke-McpTool {
    param($State, [string] $Name, [hashtable] $Arguments, [switch] $ExpectUnavailable)

    $result = Invoke-McpRequest $State 'tools/call' @{ name = $Name; arguments = $Arguments }
    $content = $result.structuredContent
    if ($null -eq $content) { throw "MCP tool $Name omitted structuredContent." }
    if ($ExpectUnavailable) {
        if ($content.available -or $content.code -ne 'documentation_unavailable') {
            throw "Expected local documentation_unavailable from $Name."
        }
    }
    elseif (-not $content.available) {
        throw "MCP tool $Name is unavailable: $($content | ConvertTo-Json -Depth 20 -Compress)"
    }
    return $content
}

function Stop-McpSession {
    param($State)
    if ($null -eq $State -or $State.Stopped) { return }
    $State.Stopped = $true
    try {
        $State.Process.StandardInput.Close()
        if (-not $State.Process.WaitForExit(10000)) {
            $State.Process.Kill($true)
            $State.Process.WaitForExit()
        }
        $errorText = $State.ErrorTask.GetAwaiter().GetResult()
        if ($State.Process.ExitCode -ne 0) { throw "MCP exited with $($State.Process.ExitCode): $errorText" }
    }
    finally { $State.Process.Dispose() }
}

function Read-SourceTool {
    param([string] $AssemblyPath, [string] $ToolName, [hashtable] $Arguments, [switch] $ExpectUnavailable)
    $sourceSession = $null
    try {
        $sourceSession = Start-McpSession 'dotnet' @($AssemblyPath, '--workspace', $workspace, 'mcp') $workspace
        return Invoke-McpTool $sourceSession $ToolName $Arguments -ExpectUnavailable:$ExpectUnavailable
    }
    finally { Stop-McpSession $sourceSession }
}

try {
    New-Item -ItemType Directory -Force -Path $workspace, $toolPath, $evidenceRoot, (Join-Path $temporaryRoot 'runtime') | Out-Null
    Set-Content -Path (Join-Path $workspace 'HarborWorkspace.slnx') -Value '<Solution />'
    $packages = @(Get-ChildItem $packageRoot -Filter 'BridgingIT.DevKit.Cli.*.nupkg' -File | Sort-Object LastWriteTimeUtc -Descending)
    if ($packages.Count -eq 0) { throw "No BridgingIT.DevKit.Cli package found under $packageRoot." }
    $package = $packages[0]
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $nuspec = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec') })[0]
        $reader = [System.IO.StreamReader]::new($nuspec.Open())
        try { [xml] $metadata = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $packageVersion = $metadata.SelectSingleNode('//*[local-name()="metadata"]/*[local-name()="version"]').InnerText
    }
    finally { $archive.Dispose() }

    $configPath = Join-Path $temporaryRoot 'NuGet.Config'
    $escapedSource = [System.Security.SecurityElement]::Escape($packageRoot)
    Set-Content $configPath "<configuration><packageSources><clear/><add key=`"local`" value=`"$escapedSource`"/></packageSources></configuration>"
    $null = Invoke-Child 'dotnet' @('tool', 'install', 'BridgingIT.DevKit.Cli', '--version', $packageVersion, '--tool-path', $toolPath, '--configfile', $configPath) $workspace -PackageCache (Join-Path $temporaryRoot 'nuget-packages')
    $executable = Join-Path $toolPath $(if ($IsWindows) { 'bdk.exe' } else { 'bdk' })
    $session = Start-McpSession $executable @('--workspace', $workspace, 'mcp') $workspace
    $catalog = Invoke-McpRequest $session 'tools/list' @{}
    $required = @('bdk_docs_search', 'bdk_docs_get', 'bdk_api_search', 'bdk_api_get', 'bdk_guidance_list', 'bdk_guidance_get')
    foreach ($name in $required) {
        if ($name -notin $catalog.tools.name) { throw "Missing MCP tool $name." }
    }
    $checks.Add('stable documentation/API/guidance tool catalog')

    $docs = Invoke-McpTool $session 'bdk_docs_search' @{ query = 'mcp'; limit = 2 }
    if ($docs.data.results.Count -eq 0) { throw 'Embedded docs search returned no MCP document.' }
    $documentId = $docs.data.results[0].source
    $document = Invoke-McpTool $session 'bdk_docs_get' @{ source = $documentId; maxChars = 128 }
    if ([string]::IsNullOrWhiteSpace($document.data.content)) { throw 'Embedded document was empty.' }
    $guide = Invoke-McpTool $session 'bdk_docs_get' @{ source = 'docs/features-jobs.md'; maxChars = 128 }
    if ([string]::IsNullOrWhiteSpace($guide.data.content)) { throw 'A public feature guide was not embedded.' }
    $null = Invoke-McpTool $session 'bdk_docs_get' @{ source = "https://github.com/bridgingit/bitdevkit/blob/an-unrelated-tag/$documentId"; maxChars = 128 }
    $null = Invoke-McpTool $session 'bdk_docs_get' @{ source = 'docs/unknown-smoke-document.md' } -ExpectUnavailable
    $checks.Add('embedded Markdown search/get, version-agnostic URL alias, local missing-document error')

    $api = Invoke-McpTool $session 'bdk_api_search' @{ query = 'Result'; limit = 2 }
    if ($api.data.results.Count -eq 0) { throw 'Embedded API index returned no Result symbol.' }
    $null = Invoke-McpTool $session 'bdk_api_get' @{ uid = $api.data.results[0].uid; maxChars = 1024 }
    $null = Invoke-McpTool $session 'bdk_api_get' @{ uid = 'HarborWorkspace.UnknownSymbol' } -ExpectUnavailable
    $checks.Add('embedded API search/get and local missing-symbol error')

    $null = Invoke-McpTool $session 'bdk_guidance_list' @{}
    $null = Invoke-McpTool $session 'bdk_guidance_get' @{ topic = 'jobs' }
    $status = Invoke-McpTool $session 'bdk_mcp_status' @{}
    if ($status.data.documentation.source -ne 'embedded') { throw 'Status does not identify embedded documentation.' }
    $null = Invoke-McpTool $session 'bdk_mcp_self_test' @{}
    $checks.Add('guidance, embedded status, and self-test without an application runtime')
    Stop-McpSession $session
    $session = $null

    if ($VerifyBuilds) {
        $copyRoot = Join-Path $temporaryRoot 'source'
        New-Item -ItemType Directory -Force -Path $copyRoot | Out-Null
        $tracked = Invoke-Child 'git' @('-C', $repoRoot, 'ls-files', '--cached', '--others', '--exclude-standard', '-z') $repoRoot
        foreach ($relative in $tracked.Output.Split([char]0, [System.StringSplitOptions]::RemoveEmptyEntries)) {
            $source = Join-Path $repoRoot $relative
            if (-not (Test-Path $source -PathType Leaf)) { continue }
            $destination = Join-Path $copyRoot $relative
            New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination
        }
        # MinVer only reads Git metadata during these builds. Do not create a new commit or change Git configuration.
        $gitDirectory = (Invoke-Child 'git' @('-C', $repoRoot, 'rev-parse', '--absolute-git-dir') $repoRoot).Output.Trim()
        Set-Content (Join-Path $copyRoot '.git') "gitdir: $gitDirectory"
        $project = Join-Path $copyRoot 'src/Presentation.Cli/Presentation.Cli.csproj'
        $assembly = Join-Path $copyRoot 'src/Presentation.Cli/bin/Debug/net10.0/BridgingIT.DevKit.Cli.dll'
        $probePath = Join-Path $copyRoot 'docs/features-mcp-smoke.md'
        Set-Content $probePath '# Harbor resource probe: first'
        $null = Invoke-Child 'dotnet' @('build', $project, '--configuration', 'Debug', '-p:BdkGenerateMcpApiReference=false', '--nologo') $copyRoot
        $first = Read-SourceTool $assembly 'bdk_docs_get' @{ source = 'docs/features-mcp-smoke.md' }
        if ($first.data.content -notmatch 'first') { throw 'New matching document was not embedded.' }
        $null = Read-SourceTool $assembly 'bdk_api_search' @{ query = 'Result' } -ExpectUnavailable
        $partialPackages = Join-Path $temporaryRoot 'partial-packages'
        $negative = Invoke-Child 'dotnet' @('pack', $project, '--configuration', 'Debug', '--no-build', '--output', $partialPackages) $copyRoot -AllowFailure
        if ($negative.ExitCode -eq 0 -or ($negative.Output + $negative.Error) -notmatch 'agent-index|API index|api index') {
            throw 'Packing without the API index did not produce the expected marker failure.'
        }
        Set-Content $probePath '# Harbor resource probe: edited'
        $null = Invoke-Child 'dotnet' @('build', $project, '--configuration', 'Debug', '-p:BdkGenerateMcpApiReference=false', '--nologo') $copyRoot
        if ((Read-SourceTool $assembly 'bdk_docs_get' @{ source = 'docs/features-mcp-smoke.md' }).data.content -notmatch 'edited') { throw 'Document edit was not embedded.' }
        $renamedPath = Join-Path $copyRoot 'docs/features-mcp-smoke-renamed.md'
        Move-Item $probePath $renamedPath
        $excludedPath = Join-Path $copyRoot 'docs/specs/features-mcp-smoke-excluded.md'
        Set-Content $excludedPath '# Excluded probe'
        $null = Invoke-Child 'dotnet' @('build', $project, '--configuration', 'Debug', '-p:BdkGenerateMcpApiReference=false', '--nologo') $copyRoot
        $null = Read-SourceTool $assembly 'bdk_docs_get' @{ source = 'docs/features-mcp-smoke.md' } -ExpectUnavailable
        $null = Read-SourceTool $assembly 'bdk_docs_get' @{ source = 'docs/features-mcp-smoke-renamed.md' }
        $null = Read-SourceTool $assembly 'bdk_docs_get' @{ source = 'docs/specs/features-mcp-smoke-excluded.md' } -ExpectUnavailable
        Remove-Item $renamedPath
        $null = Invoke-Child 'dotnet' @('build', $project, '--configuration', 'Debug', '-p:BdkGenerateMcpApiReference=false', '--nologo') $copyRoot
        $null = Read-SourceTool $assembly 'bdk_docs_get' @{ source = 'docs/features-mcp-smoke-renamed.md' } -ExpectUnavailable
        $checks.Add('incremental Markdown addition/edit/rename/removal and excluded specs in disposable source copy')
        $checks.Add('fresh Debug skips API generation and no-build packaging rejects a missing index')

        $markerRoot = Join-Path $copyRoot 'src/Presentation.Cli/obj/McpDocumentation/Debug/api'
        New-Item -ItemType Directory -Force -Path $markerRoot | Out-Null
        Set-Content (Join-Path $markerRoot 'agent-index.json') '{"schemaVersion":1,"siteUrl":"https://example.test/","symbols":[{"uid":"Harbor.MissingDetail","name":"MissingDetail","fullName":"Harbor.MissingDetail","detail":"agent-symbols/absent.json"}]}'
        $null = Invoke-Child 'dotnet' @('build', $project, '--configuration', 'Debug', '-p:BdkGenerateMcpApiReference=false', '--nologo') $copyRoot
        $null = Invoke-Child 'dotnet' @('pack', $project, '--configuration', 'Debug', '--no-build', '--output', $partialPackages) $copyRoot
        $null = Read-SourceTool $assembly 'bdk_api_get' @{ uid = 'Harbor.MissingDetail' } -ExpectUnavailable
        $checks.Add('marker-only packaging accepts an index with a missing detail; lookup fails locally')

        # The installed package above exercises real DocFX output. Use a small producer here
        # to test build ordering and package contents without repeating the full extraction.
        $fixtureProducer = @'
param([string] $Configuration, [string] $OutputRoot, [string] $BootstrapRoot)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path (Join-Path $OutputRoot 'agent-symbols') | Out-Null
Set-Content (Join-Path $OutputRoot 'agent-index.json') '{"schemaVersion":1,"siteUrl":"https://example.test/","symbols":[{"uid":"Harbor.Result","name":"Result","fullName":"Harbor.Result","kind":"Class","namespace":"Harbor","assembly":"Harbor","summary":"A result fixture.","href":"Harbor.Result.html","detail":"agent-symbols/result.json","topics":["results"]}]}'
Set-Content (Join-Path $OutputRoot 'agent-symbols/result.json') '{"uid":"Harbor.Result","name":"Result","fullName":"Harbor.Result","kind":"Class","namespace":"Harbor","assembly":"Harbor","summary":"A result fixture."}'
Add-Content (Join-Path $PSScriptRoot 'generation-invocations.txt') $Configuration
'@
        Set-Content (Join-Path $copyRoot 'docs/api/scripts/build-mcp-api.ps1') $fixtureProducer
        $null = Invoke-Child 'dotnet' @('build', $project, '--configuration', 'Release', '--nologo') $copyRoot
        $releaseAssembly = Join-Path $copyRoot 'src/Presentation.Cli/bin/Release/net10.0/BridgingIT.DevKit.Cli.dll'
        $null = Read-SourceTool $releaseAssembly 'bdk_api_search' @{ query = 'Result'; limit = 1 }
        $generatedPackages = Join-Path $temporaryRoot 'generated-packages'
        $null = Invoke-Child 'dotnet' @('pack', $project, '--configuration', 'Debug', '--output', $generatedPackages) $copyRoot
        $null = Read-SourceTool $assembly 'bdk_api_search' @{ query = 'Result'; limit = 1 }
        $invocations = @(Get-Content (Join-Path $copyRoot 'docs/api/scripts/generation-invocations.txt'))
        if ('Release' -notin $invocations -or 'Debug' -notin $invocations) { throw 'Release build or Debug packaging skipped API generation.' }
        $null = Invoke-Child 'pwsh' @('-NoProfile', '-File', (Join-Path $copyRoot 'docs/api/scripts/test-mcp-documentation.ps1'), '-Configuration', 'Debug', '-PackageDirectory', $generatedPackages) $copyRoot
        $checks.Add('Release build and Debug packaging invoke the API producer and embed its results')
        $checks.Add('normal Debug package passes installed-tool smoke checks after API preparation, not only bin assembly inspection')
    }

    $report = [ordered]@{
        timestamp = [DateTimeOffset]::UtcNow.ToString('o')
        operatingSystem = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        configuration = $Configuration
        package = $package.Name
        networkIsolatedEnvironment = $NetworkIsolated.IsPresent
        childHttpProxy = 'closed loopback port'
        buildChecksExecuted = $VerifyBuilds.IsPresent
        buildChecksApiProducer = $(if ($VerifyBuilds) { 'fixture; installed package smoke uses real API metadata' } else { 'not used' })
        checks = $checks.ToArray()
    }
    $reportJson = $report | ConvertTo-Json -Depth 10
    $reportJson | Set-Content (Join-Path $evidenceRoot 'package-smoke.json')
    $reportName = if ($VerifyBuilds) { 'package-smoke-build-checks.json' }
        elseif ($NetworkIsolated) { "package-smoke-$($Configuration.ToLowerInvariant())-offline.json" }
        else { "package-smoke-$($Configuration.ToLowerInvariant()).json" }
    $reportJson | Set-Content (Join-Path $evidenceRoot $reportName)
    Write-Host "Passed $($checks.Count) embedded documentation checks. Evidence: $evidenceRoot"
}
finally {
    try { Stop-McpSession $session }
    finally {
        if (Test-Path $temporaryRoot) { Remove-Item $temporaryRoot -Recurse -Force }
    }
}
