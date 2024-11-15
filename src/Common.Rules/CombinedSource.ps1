# Define the source folder, defaulting to the current directory if not provided
param (
  [string]$sourceFolder = (Get-Location)
)

# Set the path for the output Markdown file
$outputMarkdown = "CombinedSource.md"

# Clear or create the output file
Out-File -FilePath $outputMarkdown -Encoding UTF8 -Force

# Loop through each .cs file in the folder recursively, excluding bin and obj folders
Get-ChildItem -Path $sourceFolder -Recurse -File -Include *.cs |
  Where-Object {
    $_.DirectoryName -notmatch '\\bin\\|\\obj\\'
  } | ForEach-Object {

$filePath = $_.FullName
$relativePath = $_.FullName.Substring($sourceFolder.Length + 1)  # Calculate relative path
$fileName = $_.Name

# Read file content
$fileContent = Get-Content -Path $filePath -Raw

# Append file header with relative path, code block, and content to the markdown file
Add-Content -Path $outputMarkdown -Value "### $relativePath`n"
Add-Content -Path $outputMarkdown -Value "```csharp"  # Start code block with language hint
Add-Content -Path $outputMarkdown -Value $fileContent
Add-Content -Path $outputMarkdown -Value '```'  # End code block
Add-Content -Path $outputMarkdown -Value "`n---`n"  # Divider between files
}

# Output message indicating completion
Write-Host "Markdown file generated at $outputMarkdown"