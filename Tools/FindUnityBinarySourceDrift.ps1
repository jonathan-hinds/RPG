param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

Push-Location $ProjectRoot
try {
    $tracked = git ls-files
    $binaryUnityFiles = foreach ($path in $tracked) {
        if ($path -notmatch '\.(unity|asset)$') {
            continue
        }

        if (-not (Test-Path -LiteralPath $path)) {
            continue
        }

        $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $path))
        if ([Array]::IndexOf($bytes, [byte]0) -lt 0) {
            continue
        }

        $headBytes = git cat-file -s "HEAD:$path" 2>$null
        if (-not $headBytes) {
            continue
        }

        [pscustomobject]@{
            Path = $path
            WorktreeBytes = $bytes.Length
            HeadBlobBytes = [int64]$headBytes
            MatchesHeadSize = ($bytes.Length -eq [int64]$headBytes)
        }
    }

    $drift = @($binaryUnityFiles | Where-Object { -not $_.MatchesHeadSize })
    if ($drift.Count -eq 0) {
        Write-Host "No binary Unity source drift detected."
        exit 0
    }

    $drift | Sort-Object Path | Format-Table -AutoSize
    Write-Error "Binary Unity files differ from HEAD blob size. After .gitattributes changes, re-add these files in the next safety commit so future resets reproduce the valid worktree bytes."
    exit 1
}
finally {
    Pop-Location
}
