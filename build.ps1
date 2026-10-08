param([string]$GameDir = $env:GEKIDRIVE_GAME_DIR)
$ErrorActionPreference = 'Stop'
if (!$GameDir) { $GameDir = Join-Path $PSScriptRoot 'GameFiles\package' }
$required = @('mu3_Data\Managed\Assembly-CSharp.dll', 'mu3_Data\Managed\Assembly-CSharp-firstpass.dll', 'mu3_Data\Managed\UnityEngine.dll', 'mu3_Data\Managed\mscorlib.dll', 'BepInEx\core\BepInEx.dll', 'BepInEx\core\0Harmony.dll')
foreach ($relative in $required) {
    if (!(Test-Path -LiteralPath (Join-Path $GameDir $relative))) { throw "Missing dependency: $relative" }
}
dotnet build (Join-Path $PSScriptRoot 'src\GekiDrive\GekiDrive.csproj') -c Release "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host "Built: $PSScriptRoot\artifacts\GekiDrive.Public.dll"

