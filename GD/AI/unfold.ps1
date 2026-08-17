$ErrorActionPreference = "Stop"

function Unfold-CSharpCode {
    # Dynamically find the root project directory (GD) and its parent workspace
    $ProjectRoot = Split-Path -Path $PSScriptRoot -Parent
    $WorkspaceRoot = Split-Path -Path $ProjectRoot -Parent
    
    # Define the sibling output directory (GDAlpha) and the target file (Program.cs)
    $TargetDir = Join-Path -Path $WorkspaceRoot -ChildPath 'GDAlpha'
    $programFile = Join-Path -Path $TargetDir -ChildPath 'Program.cs'

    if (-not (Test-Path -Path $programFile)) {
        Write-Host "Error: Program.cs not found in the sibling directory ($TargetDir)." -ForegroundColor Red
        return
    }

    Write-Host "Reading $programFile..." -ForegroundColor Cyan
    
    # Read file line by line
    $lines = Get-Content -Path $programFile -Encoding UTF8
    
    $currentFile = $null
    $currentContent = [System.Collections.Generic.List[string]]::new()

    # 1. Parse Delimiters
    foreach ($line in $lines) {
        if ($line -match '^// Start\s+(.+)$') {
            $currentFile = $matches[1].Trim()
            $currentContent.Clear()
        }
        elseif ($line -match '^// End\s+(.+)$') {
            $endFile = $matches[1].Trim()
            
            if ($currentFile -eq $endFile) {
                # 2. Reconstruct Files relative to the GD Project Root
                if ($currentFile -eq "GlobalUsings.cs") {
                    # Explicitly route GlobalUsings.cs to the project root
                    $safePath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($ProjectRoot, "GlobalUsings.cs"))
                }
                else {
                    # Strip any relative prefixes (like ./) before combining to ensure clean paths
                    $cleanRelativePath = $currentFile -replace '^\./', ''
                    $safePath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($ProjectRoot, $cleanRelativePath))
                }

                $directory = [System.IO.Path]::GetDirectoryName($safePath)

                # 3. Create Missing Directories
                if (-not (Test-Path -Path $directory)) {
                    $null = New-Item -ItemType Directory -Path $directory -Force
                }

                # 4. Write/Overwrite the file back into the GD project
                Write-Host "Unpacking: $currentFile" -ForegroundColor Yellow
                $joinedContent = ($currentContent -join "`n").Trim() + "`n"
                Set-Content -Path $safePath -Value $joinedContent -Encoding UTF8 -Force

                $currentFile = $null
            }
        }
        elseif ($null -ne $currentFile) {
            # Collect content between the blocks
            $currentContent.Add($line)
        }
    }

    Write-Host "Done! Files successfully unfolded back into $ProjectRoot." -ForegroundColor Green
}

Unfold-CSharpCode