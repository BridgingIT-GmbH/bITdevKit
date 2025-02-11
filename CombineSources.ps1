param (
  # Core parameters
  [string]$OutputDirectory = "./.tmp",

  # File patterns and exclusions
  [string[]]$ExcludePatterns = @(
    'examples?', # Matches 'example' or 'examples'
    'tests?', # Matches 'test' or 'tests'
    'docs?', # Matches 'doc' or 'docs'
    'obj', # Build output
    'bin', # Build output
    'debug', # Debug folders
    'release', # Release folders
    'packages', # NuGet packages
    'node_modules'  # NPM packages
  ),

  # Content processing options
  [bool]$StripHeaderComments = $true,
  [bool]$StripRegions = $true,
  [bool]$StripComments = $false,
  [bool]$StripEmptyLines = $false,
  [bool]$StripUsings = $true,
  [bool]$CombineUsings = $true,
  [bool]$StripAttributes = $false,
  [bool]$SkipGeneratedFiles = $true,
  [bool]$UpdateGitIgnore = $true
)

# Statistics tracking
$stats = @{
  ProjectsProcessed  = 0
  ProjectsSkipped    = 0
  TotalSourceFiles   = 0
  TotalMarkdownFiles = 0
  OriginalSize       = 0
  FinalSize          = 0
  TotalUsings        = 0
  FilesSkipped       = 0
  StartTime          = Get-Date
}

# Logging functions
function Write-Progress-Info {
  param([string]$message)
  Write-Host "[INFO] $message" -ForegroundColor Cyan
}

function Write-Progress-Warning {
  param([string]$message)
  Write-Host "[WARN] $message" -ForegroundColor Yellow
}

function Write-Progress-Success {
  param([string]$message)
  Write-Host "[SUCCESS] $message" -ForegroundColor Green
}

# Progress bar function
function Write-ProgressBar {
  param (
    [int]$Current,
    [int]$Total,
    [string]$Activity,
    [string]$Status
  )
  $percentComplete = [math]::Min(100, ($Current / $Total * 100))
  Write-Progress -Activity $Activity -Status $Status -PercentComplete $percentComplete
}

# Using statement handling
function Get-UsingStatements {
  param (
    [string]$content
  )

  $regex = '(?m)^using\s+(?<using>[^;]+);'
  $matches = [regex]::Matches($content, $regex)
  return $matches | ForEach-Object { $_.Groups['using'].Value.Trim() }
}

function Remove-UsingStatements {
  param (
    [string]$content
  )
  return $content -replace '(?m)^using\s+[^;]+;\r?\n?', ''
}

function Format-UsingStatements {
  param (
    [string[]]$usings
  )

  $uniqueUsings = $usings | Select-Object -Unique | Sort-Object

  $systemUsings = $uniqueUsings | Where-Object { $_ -like "System*" } | Sort-Object
  $microsoftUsings = $uniqueUsings | Where-Object { $_ -like "Microsoft*" } | Sort-Object
  $otherUsings = $uniqueUsings | Where-Object { -not ($_ -like "System*" -or $_ -like "Microsoft*") } | Sort-Object

  $result = ""

  if ($systemUsings) {
    $result += ($systemUsings | ForEach-Object { "using $_;" }) -join "`n"
    $result += "`n`n"
  }

  if ($microsoftUsings) {
    $result += ($microsoftUsings | ForEach-Object { "using $_;" }) -join "`n"
    $result += "`n`n"
  }

  if ($otherUsings) {
    $result += ($otherUsings | ForEach-Object { "using $_;" }) -join "`n"
  }

  return $result.Trim()
}

# Code cleanup
function Remove-CodeElements {
  param (
    [string]$content,
    [bool]$stripRegions,
    [bool]$stripComments,
    [bool]$stripEmptyLines,
    [bool]$stripAttributes,
    [bool]$stripHeaderComments
  )

  if ($stripHeaderComments) {
    # Remove multi-line license headers with /* */
    $content = $content -replace '(?sm)^\/\*.*?\*\/', ''

    # Remove single-line license headers (including MIT, license references, etc.)
    $content = $content -replace '(?m)^\/\/.*MIT.*$\n?', ''
    $content = $content -replace '(?m)^\/\/.*[Ll]icense.*$\n?', ''
    $content = $content -replace '(?m)^\/\/.*[Cc]opyright.*$\n?', ''
    $content = $content -replace '(?m)^\/\/.*[Aa]ll [Rr]ights.*$\n?', ''
    $content = $content -replace '(?m)^\/\/.*[Gg]overned by.*$\n?', ''
    $content = $content -replace '(?m)^\/\/.*[Uu]se of this source.*$\n?', ''

    # Remove any blank lines at the start of the file
    $content = $content -replace '^\s*\n', ''
  }

  if ($stripRegions) {
    $content = $content -replace '(?m)^\s*#region.*$\n?', ''
    $content = $content -replace '(?m)^\s*#endregion.*$\n?', ''
  }

  if ($stripComments) {
    $content = $content -replace '(?m)^\s*//.*$\n?', ''
    $content = $content -replace '(?s)/\*.*?\*/', ''
  }

  if ($stripEmptyLines) {
    $content = $content -replace '(?m)^\s*$\n', ''
  }

  if ($stripAttributes) {
    $content = $content -replace '(?m)^\s*\[.*?\]\r?\n', ''
  }

  return $content.Trim()
}

function Should-ProcessFile {
  param (
    [System.IO.FileInfo]$file,
    [bool]$skipGenerated,
    [string[]]$excludePatterns
  )

  # Check if path contains any exclude patterns
  foreach ($pattern in $excludePatterns) {
    if ($file.FullName -match $pattern) {
      $stats.FilesSkipped++
      Write-Progress-Info "Skipping file: $($file.FullName) (matched pattern: $pattern)"
      return $false
    }
  }

  if ($skipGenerated) {
    $generatedPatterns = @(
      '\.g\.cs$',
      '\.designer\.cs$',
      '\.generated\.cs$',
      'TemporaryGeneratedFile',
      '\.AssemblyInfo\.cs$'
    )

    foreach ($pattern in $generatedPatterns) {
      if ($file.Name -match $pattern) {
        $stats.FilesSkipped++
        Write-Progress-Info "Skipping generated file: $($file.Name)"
        return $false
      }
    }
  }

  return $true
}

function Get-RelativePath {
  param (
    [string]$fullPath,
    [string]$basePath
  )
  return $fullPath.Substring($basePath.Length).TrimStart('\', '/')
}

# Main processing
Write-Progress-Info "Starting documentation generation..."
Write-Progress-Info "Output directory: $OutputDirectory"

# Convert relative path to absolute and ensure directory exists
$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

# Find all .csproj files
$projects = Get-ChildItem -Recurse -Filter "*.csproj"
$projectCount = $projects.Count

Write-Progress-Info "Found $projectCount projects to process"

$projects | ForEach-Object -Begin {
  $currentProject = 0
} -Process {
  $currentProject++
  Write-ProgressBar -Current $currentProject -Total $projectCount -Activity "Processing Projects" -Status "Project $currentProject of $projectCount"

  $projectFile = $_
  $projectDir = $projectFile.Directory
  $projectName = $projectFile.BaseName

  Write-Progress-Info "`nProcessing project: $projectName"

  # Skip if excluded
  $shouldExclude = $false
  foreach ($pattern in $ExcludePatterns) {
    if ($projectFile.FullName -match $pattern) {
      $shouldExclude = $true
      Write-Progress-Warning "Skipping $projectName (matched exclude pattern: $pattern)"
      $stats.ProjectsSkipped++
      break
    }
  }
  if ($shouldExclude) { return }

  $stats.ProjectsProcessed++

  # Initialize markdown content
  $markdown = "# $projectName`n`n"

  # Process source files
  $allUsings = @()
  $processedFiles = @()

  $sourceFiles = @(Get-ChildItem -Path $projectDir -Recurse -Filter "*.cs" |
    Where-Object { Should-ProcessFile -file $_ -skipGenerated $SkipGeneratedFiles -excludePatterns $ExcludePatterns })
  $totalFiles = $sourceFiles.Count
  $currentFile = 0

  Write-Progress-Info "Processing $totalFiles source files..."

  $sourceFiles | ForEach-Object {
    $currentFile++
    Write-ProgressBar -Current $currentFile -Total $totalFiles -Activity "Processing Source Files" -Status "File $currentFile of $totalFiles"

    $content = Get-Content $_.FullName -Raw
    $stats.OriginalSize += $content.Length

    # Handle using statements based on settings
    if ($CombineUsings -or $StripUsings) {
      $usings = Get-UsingStatements -content $content
      $stats.TotalUsings += $usings.Count
      if ($CombineUsings) {
        $allUsings += $usings
      }
      $content = Remove-UsingStatements -content $content
    }

    $content = Remove-CodeElements -content $content `
      -stripRegions $StripRegions `
      -stripComments $StripComments `
      -stripEmptyLines $StripEmptyLines `
      -stripAttributes $StripAttributes `
      -stripHeaderComments $StripHeaderComments

    $processedFiles += @{
      Path    = Get-RelativePath $_.FullName $projectDir.FullName
      Content = $content
    }

    $stats.TotalSourceFiles++
  }

  # Add combined usings if enabled
  if ($CombineUsings -and -not $StripUsings -and $allUsings.Count -gt 0) {
    $combinedUsings = Format-UsingStatements -usings $allUsings
    $markdown += "## Global Usings`n`n"
    $markdown += '```csharp'
    $markdown += "`n"
    $markdown += $combinedUsings
    $markdown += "`n"
    $markdown += '```'
  }

  # Process markdown files
  Write-Progress-Info "Processing markdown files..."
  Get-ChildItem -Path $projectDir -Recurse -Filter "*.md" |
  Where-Object { $_.Name -ne "$projectName.g.md" -and (Should-ProcessFile -file $_ -skipGenerated $false -excludePatterns $ExcludePatterns) } |
  ForEach-Object {
    $mdContent = Get-Content $_.FullName -Raw
    $relativePath = Get-RelativePath $_.FullName $projectDir.FullName

    $markdown += "`n`n## Documentation: $relativePath`n`n"
    $markdown += $mdContent
    $stats.TotalMarkdownFiles++
  }

  # Add processed source files
  foreach ($file in $processedFiles) {
    $markdown += "`n`n## Source: $($file.Path)`n`n"
    $markdown += '```csharp'
    $markdown += "`n"
    $markdown += $file.Content
    $markdown += "`n"
    $markdown += '```'
  }

  # Write output file
  $outputFile = Join-Path $OutputDirectory "$projectName.g.md"
  $markdown | Out-File $outputFile -Encoding UTF8
  $stats.FinalSize += (Get-Item $outputFile).Length

  Write-Progress-Success "Generated documentation for $projectName"

} -End {
  Write-Progress -Activity "Processing Projects" -Completed
}

# Update .gitignore if needed
if ($UpdateGitIgnore) {
  $gitignorePath = Join-Path $PSScriptRoot ".gitignore"
  $outputDirName = Split-Path $OutputDirectory -Leaf
  $ignoreEntry = "/$outputDirName/"

  if (Test-Path $gitignorePath) {
    $gitignoreContent = Get-Content $gitignorePath
    if ($gitignoreContent -notcontains $ignoreEntry) {
      Add-Content $gitignorePath "`n$ignoreEntry"
      Write-Progress-Info "Added /$outputDirName/ to .gitignore"
    }
  }
  else {
    Set-Content $gitignorePath $ignoreEntry
    Write-Progress-Info "Created .gitignore with /$outputDirName/"
  }
}

# Generate final statistics report
$endTime = Get-Date
$duration = $endTime - $stats.StartTime

$report = @"

Generation Statistics Report
==========================
Duration: $($duration.ToString('hh\:mm\:ss'))

Projects
--------
Total Projects Found: $projectCount
Projects Processed:  $($stats.ProjectsProcessed)
Projects Skipped:    $($stats.ProjectsSkipped)

Files
-----
Source Files:        $($stats.TotalSourceFiles)
Markdown Files:      $($stats.TotalMarkdownFiles)
Files Skipped:       $($stats.FilesSkipped)
Total Files:         $($stats.TotalSourceFiles + $stats.TotalMarkdownFiles)
Total Using Stmts:   $($stats.TotalUsings)

Size
----
Original Size:       $([math]::Round($stats.OriginalSize / 1KB, 2)) KB
Final Size:          $([math]::Round($stats.FinalSize / 1KB, 2)) KB
Size Reduction:      $([math]::Round(100 - ($stats.FinalSize / $stats.OriginalSize * 100), 1))%

Output Location:     $OutputDirectory
"@

Write-Host $report -ForegroundColor Cyan
Write-Progress-Success "Documentation generation complete!"