[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# DocFX splits namespace names into links even when their parent namespaces
# contain no types and therefore have no generated pages. Keep those labels
# as text, but retain links whenever a real parent page exists.
$parents = @{
    'BridgingIT' = 'BridgingIT'
    'BridgingIT.DevKit' = 'DevKit'
}
$updated = 0
foreach ($page in Get-ChildItem -Path $OutputRoot -Filter '*.html' -Recurse) {
    $original = [System.IO.File]::ReadAllText($page.FullName)
    $content = $original
    foreach ($parent in $parents.Keys) {
        if (-not (Test-Path (Join-Path $page.DirectoryName "$parent.html"))) {
            $label = $parents[$parent]
            $content = $content.Replace("<a class=`"xref`" href=`"$parent.html`">$label</a>", $label)
        }
    }

    if ($content -ne $original) {
        [System.IO.File]::WriteAllText($page.FullName, $content)
        $updated++
    }
}

Write-Host "Removed missing parent namespace links from $updated API page(s)"
