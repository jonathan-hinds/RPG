param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

function Fail($Message) {
    Write-Error $Message
    exit 1
}

Push-Location $ProjectRoot
try {
    if (-not (Test-Path -LiteralPath '.git')) {
        Fail "This does not look like the project root: $ProjectRoot"
    }

    $editorSettings = 'ProjectSettings/EditorSettings.asset'
    if (-not (Select-String -Path $editorSettings -Pattern '^\s*m_SerializationMode:\s*2\s*$' -Quiet)) {
        Fail "Unity serialization is not Force Text. Expected m_SerializationMode: 2 in $editorSettings."
    }

    $attrOutput = git check-attr text -- 'Assets/Scenes/OrcishStarterValley.unity' 'Assets/_Project/Generated/Terrain/OrcishStarterValleyTerrain.asset'
    if ($attrOutput -match 'text: set') {
        Fail "Git text normalization is enabled for Unity scene/binary-prone asset files. Check .gitattributes."
    }

    $sceneFiles = Get-ChildItem -LiteralPath 'Assets/Scenes' -Filter '*.unity' -File -ErrorAction SilentlyContinue
    foreach ($scene in $sceneFiles) {
        $bytes = [System.IO.File]::ReadAllBytes($scene.FullName)
        if ($bytes.Length -lt 9) {
            Fail "Scene file is too small: $($scene.FullName)"
        }

        $headLength = [Math]::Min(16, $bytes.Length)
        $head = [System.Text.Encoding]::ASCII.GetString($bytes[0..($headLength - 1)])
        if (-not $head.StartsWith('%YAML 1.1')) {
            Fail "Scene is not Force Text YAML: $($scene.FullName)"
        }

        if ([Array]::IndexOf($bytes, [byte]0) -ge 0) {
            Fail "Scene contains NUL bytes and is likely binary/corrupt for this repo policy: $($scene.FullName)"
        }
    }

    $missingPrefabGuid = '5903183c629e22d498ff9e83cccc9ad3'
    $mainScene = 'Assets/Scenes/OrcishStarterValley.unity'
    if ((Test-Path -LiteralPath $mainScene) -and (Select-String -Path $mainScene -Pattern $missingPrefabGuid -Quiet)) {
        Fail "Main scene still references missing prefab GUID $missingPrefabGuid."
    }

    Write-Host "Unity Git source checks passed."
}
finally {
    Pop-Location
}
