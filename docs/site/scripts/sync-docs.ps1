[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$siteRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$sourceRoot = Join-Path $repoRoot 'docs'
$targetRoot = Join-Path $siteRoot 'reference'
$repoBaseUrl = 'https://github.com/bridgingIT/bITdevKit/blob/main'

$includedPatterns = @(
    'INDEX.md',
    'introduction-ddd-guide.md',
    'common-*.md',
    'features-*.md',
    'testing-*.md'
)

$excludedDirectories = @(
    'adr',
    'assets',
    'pages',
    'presentations',
    'site',
    'specs'
)

$destinationNames = @{
    'INDEX.md' = 'index.md'
}

function Get-GitHubBlobUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $normalizedPath = ($Path -replace '\\', '/').TrimStart('/')
    return "$repoBaseUrl/$normalizedPath"
}

function Normalize-DocTarget {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Target
    )

    $cleanTarget = $Target.Trim()
    $cleanTarget = $cleanTarget -replace '\\', '/'

    if ($cleanTarget.StartsWith('./')) {
        return $cleanTarget.Substring(2)
    }

    return $cleanTarget
}

function Rewrite-MarkdownLinks {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Content,

        [Parameter(Mandatory = $true)]
        [hashtable] $ImportedMap
    )

    $pattern = '(?<prefix>!?\[[^\]]*\]\()(?<target>[^)\s]+)(?<suffix>[^)]*\))'

    return [regex]::Replace($Content, $pattern, {
        param($match)

        $target = $match.Groups['target'].Value
        $normalized = Normalize-DocTarget -Target $target

        if ($normalized.StartsWith('http://') -or
            $normalized.StartsWith('https://') -or
            $normalized.StartsWith('mailto:') -or
            $normalized.StartsWith('#')) {
            return $match.Value
        }

        if ($normalized.StartsWith('/f:/projects/bit/bIT.bITdevKit/') -or
            $normalized.StartsWith('/f:/projects/bit/bITdevKit/')) {
            $prefix = if ($normalized.StartsWith('/f:/projects/bit/bIT.bITdevKit/')) { '/f:/projects/bit/bIT.bITdevKit/' } else { '/f:/projects/bit/bITdevKit/' }
            $relative = $normalized.Substring($prefix.Length)
            return $match.Groups['prefix'].Value + (Get-GitHubBlobUrl -Path $relative) + $match.Groups['suffix'].Value
        }

        if ($normalized.StartsWith('/mnt/f/projects/bit/bIT.bITdevKit/') -or
            $normalized.StartsWith('/mnt/f/projects/bit/bITdevKit/')) {
            $prefix = if ($normalized.StartsWith('/mnt/f/projects/bit/bIT.bITdevKit/')) { '/mnt/f/projects/bit/bIT.bITdevKit/' } else { '/mnt/f/projects/bit/bITdevKit/' }
            $relative = $normalized.Substring($prefix.Length)
            return $match.Groups['prefix'].Value + (Get-GitHubBlobUrl -Path $relative) + $match.Groups['suffix'].Value
        }

        if ($normalized.StartsWith('/src/') -or
            $normalized.StartsWith('/tests/') -or
            $normalized.StartsWith('/examples/') -or
            $normalized.StartsWith('/docs/') -or
            $normalized.StartsWith('/README.md') -or
            $normalized.StartsWith('/CHANGELOG.md') -or
            $normalized.StartsWith('/CONTRIBUTION.md') -or
            $normalized.StartsWith('/LICENSE')) {
            $relative = $normalized.TrimStart('/')
            return $match.Groups['prefix'].Value + (Get-GitHubBlobUrl -Path $relative) + $match.Groups['suffix'].Value
        }

        $leaf = Split-Path $normalized -Leaf
        if ($ImportedMap.ContainsKey($leaf)) {
            return $match.Groups['prefix'].Value + './' + $ImportedMap[$leaf] + $match.Groups['suffix'].Value
        }

        if ($normalized.StartsWith('adr/') -or $normalized.StartsWith('specs/') -or $normalized.StartsWith('presentations/')) {
            return $match.Groups['prefix'].Value + (Get-GitHubBlobUrl -Path ('docs/' + $normalized)) + $match.Groups['suffix'].Value
        }

        if ($normalized.StartsWith('src/') -or
            $normalized.StartsWith('tests/') -or
            $normalized.StartsWith('examples/') -or
            $normalized.StartsWith('.agents/') -or
            $normalized.StartsWith('docs/')) {
            return $match.Groups['prefix'].Value + (Get-GitHubBlobUrl -Path $normalized) + $match.Groups['suffix'].Value
        }

        if ($normalized -in @('README.md', 'CHANGELOG.md', 'CONTRIBUTION.md', 'LICENSE')) {
            return $match.Groups['prefix'].Value + (Get-GitHubBlobUrl -Path $normalized) + $match.Groups['suffix'].Value
        }

        return $match.Value
    })
}

function Transform-ImportedContent {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Content,

        [Parameter(Mandatory = $true)]
        [string] $SourceFileName
    )

    if ($SourceFileName -eq 'INDEX.md') {
        $Content = [regex]::Replace($Content, '^#\s+Documentation Index\s*$', '# Overview', 'Multiline')
    }

    return $Content
}

New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null

Get-ChildItem -Path $targetRoot -File | Remove-Item -Force

$sourceFiles = Get-ChildItem -Path $sourceRoot -File | Where-Object {
    $name = $_.Name
    @($includedPatterns | Where-Object { $name -like $_ }).Count -gt 0 -and
    ($excludedDirectories -notcontains $_.Directory.Name)
}

$importedMap = @{}
foreach ($file in $sourceFiles) {
    $destinationName = if ($destinationNames.ContainsKey($file.Name)) { $destinationNames[$file.Name] } else { $file.Name }
    $importedMap[$file.Name] = $destinationName
}

foreach ($file in $sourceFiles) {
    $destinationName = $importedMap[$file.Name]
    $destinationPath = Join-Path $targetRoot $destinationName
    $content = Get-Content $file.FullName -Raw
    $transformed = Transform-ImportedContent -Content $content -SourceFileName $file.Name
    $rewritten = Rewrite-MarkdownLinks -Content $transformed -ImportedMap $importedMap
    Set-Content -Path $destinationPath -Value $rewritten -Encoding UTF8
}

Write-Host "Synced $($sourceFiles.Count) documentation file(s) to $targetRoot"
