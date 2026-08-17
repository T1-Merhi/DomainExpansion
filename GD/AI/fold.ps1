$ErrorActionPreference = "Stop"

function Fold-CSharpCode {
    # Dynamically find the root project directory (GD) and its parent workspace
    $ProjectRoot = Split-Path -Path $PSScriptRoot -Parent
    $WorkspaceRoot = Split-Path -Path $ProjectRoot -Parent
    
    # Define the sibling output directory (GDAlpha) and the target file (Program.cs)
    $TargetDir = Join-Path -Path $WorkspaceRoot -ChildPath 'GDAlpha'
    $outFile = Join-Path -Path $TargetDir -ChildPath 'Program.cs'

    # Ensure GDAlpha exists
    if (-not (Test-Path -Path $TargetDir)) {
        $null = New-Item -ItemType Directory -Path $TargetDir -Force
    }

    Write-Host "Scanning directory: $ProjectRoot for .cs files..." -ForegroundColor Cyan

    # UPDATED REGEX: Matches both 'using' and 'global using'
    $usingRegex = '(?m)^\s*(global\s+)?using\s+[A-Za-z0-9_.\s=]+;\s*$'
    
    $allUsings = [System.Collections.Generic.HashSet[string]]::new()
    $fileContents = @{}
    $programCsKey = $null

    # Find all .cs files (INCLUDING GlobalUsings.cs now), excluding bin and obj folders
    $csFiles = Get-ChildItem -Path $ProjectRoot -Filter *.cs -Recurse | Where-Object { 
        $_.FullName -notmatch '\\bin\\' -and 
        $_.FullName -notmatch '\\obj\\' -and
        $_.FullName -notmatch '/bin/' -and 
        $_.FullName -notmatch '/obj/'
    }

    foreach ($file in $csFiles) {
        $relPathRaw = $file.FullName.Substring($ProjectRoot.Length).TrimStart('\', '/')
        $relPath = "./" + $relPathRaw.Replace('\', '/')
        
        $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
        if ($null -eq $content) { $content = "" }

        # 1. Extract and deduplicate using/global using statements
        $usingMatches = [regex]::Matches($content, $usingRegex)
        foreach ($match in $usingMatches) {
            # Normalize spaces just in case
            $cleanUsing = $match.Value.Trim() -replace '\s+', ' '
            
            if (-not $cleanUsing.StartsWith('global ')) {
                $cleanUsing = $cleanUsing -replace '^using ', 'global using '
            }
            
            $null = $allUsings.Add($cleanUsing)
        }
        
        # 2. Strip using statements from the file's content
        $contentNoUsings = ($content -replace $usingRegex, '').Trim()
        
        # 3. Categorize the file
        if ($file.Name -eq 'Program.cs') {
            $programCsKey = $relPath
            $fileContents[$relPath] = $contentNoUsings
        }
        elseif ($file.Name -eq 'GlobalUsings.cs') {
            # Do nothing: We extract its references above, but we don't add it to 
            # $fileContents because we manually build this block at the top of the file.
        }
        else {
            $fileContents[$relPath] = $contentNoUsings
        }
    }

    # 4. Concatenate into Program.cs in the GDAlpha folder
    Write-Host "Generating folded file at $outFile..." -ForegroundColor Cyan
    Clear-Content -Path $outFile -ErrorAction SilentlyContinue | Out-Null

    # Write Global Usings block first
    $sortedUsings = $allUsings | Sort-Object
    Add-Content -Path $outFile -Value "// Start GlobalUsings.cs" -Encoding UTF8
    foreach ($u in $sortedUsings) {
        Add-Content -Path $outFile -Value $u -Encoding UTF8
    }
    Add-Content -Path $outFile -Value "// End GlobalUsings.cs`n" -Encoding UTF8

    # Ensure the original Program.cs is next
    if ($null -ne $programCsKey -and $fileContents.ContainsKey($programCsKey)) {
        Add-Content -Path $outFile -Value "// Start $programCsKey" -Encoding UTF8
        Add-Content -Path $outFile -Value $fileContents[$programCsKey] -Encoding UTF8
        Add-Content -Path $outFile -Value "// End $programCsKey`n" -Encoding UTF8
        $fileContents.Remove($programCsKey)
    }

    # Append the rest of the files
    foreach ($key in $fileContents.Keys) {
        Add-Content -Path $outFile -Value "// Start $key" -Encoding UTF8
        Add-Content -Path $outFile -Value $fileContents[$key] -Encoding UTF8
        Add-Content -Path $outFile -Value "// End $key`n" -Encoding UTF8
    }

    Write-Host "Done! Code successfully folded into $outFile." -ForegroundColor Green
}

Fold-CSharpCode