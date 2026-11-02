<#
.SYNOPSIS
Applies repository host policy after gh aw compile, or checks the generated files.
#>
[CmdletBinding()]
param(
    [string] $WorkflowDirectory = (Join-Path $PSScriptRoot '../workflows'),
    [switch] $Check
)

$ErrorActionPreference = 'Stop'
$hostGuard = "github.server_url != 'https://github.com'"
$encoding = [Text.UTF8Encoding]::new($false)
$files = @(Get-ChildItem -LiteralPath $WorkflowDirectory -Filter '*.lock.yml')
$files += Get-Item -LiteralPath (Join-Path $WorkflowDirectory 'agentics-maintenance.yml')
if ($files.Count -lt 2) {
    throw 'Expected compiled agentic workflows and their maintenance workflow.'
}

foreach ($file in $files) {
    $content = [IO.File]::ReadAllText($file.FullName)
    $jobsStart = [regex]::Match($content, '(?m)^jobs:\r?\n')
    if (!$jobsStart.Success) {
        throw "Missing jobs map in $($file.Name)."
    }

    $prefix = $content.Substring(0, $jobsStart.Index + $jobsStart.Length)
    $jobs = $content.Substring($prefix.Length)
    $jobPattern = '(?ms)^  [\w-]+:\r?\n.*?(?=^  [\w-]+:|^\S|\z)'
    if (![regex]::IsMatch($jobs, $jobPattern)) {
        throw "No generated jobs found in $($file.Name)."
    }

    $guardedJobs = [regex]::Replace($jobs, $jobPattern, {
        param($jobMatch)
        $job = $jobMatch.Value
        $condition = [regex]::Match($job, '(?m)^    if: ([^\r\n]*)(?:\r?\n      [^\r\n]*)*')
        if (!$condition.Success) {
            return [regex]::Replace($job, '^(  [\w-]+:\r?\n)', "`$1    if: $hostGuard`n")
        }

        $expression = $condition.Value.Substring('    if: '.Length).Trim()
        if ($expression.StartsWith('>')) {
            $expression = ($expression -replace '^>[-+]?\s*', '') -replace '\r?\n\s*', ' '
        }
        if ($expression.StartsWith('${{') -and $expression.EndsWith('}}')) {
            $expression = $expression.Substring(3, $expression.Length - 5).Trim()
        }
        if ($expression -eq $hostGuard -or $expression.StartsWith("$hostGuard && (")) {
            return $job
        }
        if ($expression.StartsWith('|') -or $expression.StartsWith('"') -or $expression.StartsWith("'")) {
            throw "Unsupported job condition format in $($file.Name). Review the generated YAML."
        }

        $replacement = "    if: $hostGuard && ($expression)"
        return $job.Substring(0, $condition.Index) + $replacement + $job.Substring($condition.Index + $condition.Length)
    })

    $updated = $prefix + $guardedJobs
    if ($updated -ne $content) {
        if ($Check) {
            throw "Missing host guards in $($file.Name). Run Set-AgenticWorkflowHostGuards.ps1 after gh aw compile."
        }
        [IO.File]::WriteAllText($file.FullName, $updated, $encoding)
    }
}

Write-Host "Host policy verified for $($files.Count) generated workflows."
