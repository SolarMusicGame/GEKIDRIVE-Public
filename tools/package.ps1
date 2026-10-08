param([switch]$SkipBuild, [string]$GameDir = $env:GEKIDRIVE_GAME_DIR)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (!$SkipBuild) { & (Join-Path $root 'build.ps1') -GameDir $GameDir }
$project = [xml](Get-Content -LiteralPath (Join-Path $root 'src\GekiDrive\GekiDrive.csproj') -Raw)
$version = [string]$project.Project.PropertyGroup.Version
$artifact = Join-Path $root 'artifacts'
$stage = Join-Path $artifact "GEKIDRIVE-Public-$version"
New-Item -ItemType Directory -Path (Join-Path $stage 'docs') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stage 'examples') -Force | Out-Null
$source = Get-Content -LiteralPath (Join-Path $root 'src\GekiDrive\ModuleConfig.cs') -Raw
$pattern = '(?:c\.Bind|Range)\((?:c,\s*)?"(?<section>[^"]+)",\s*"(?<key>[^"]+)",\s*(?<value>"[^"]*"|[^,\r\n]+),[^\r\n]*"(?<description>[^"]*)"\);'
$groups = [ordered]@{}
foreach ($match in [regex]::Matches($source, $pattern)) {
    $section = $match.Groups['section'].Value
    if (!$groups.Contains($section)) { $groups[$section] = [System.Collections.Generic.List[string]]::new() }
    $value = $match.Groups['value'].Value.Trim()
    if ($value.StartsWith('"')) { $value = $value.Substring(1, $value.Length - 2) }
    elseif ($value.StartsWith('KeyCode.')) { $value = $value.Substring(8) }
    elseif ($value -match '^[-\d.]+f$') { $value = $value.TrimEnd('f') }
    $groups[$section].Add('# ' + $match.Groups['description'].Value)
    $groups[$section].Add(($match.Groups['key'].Value + ' = ' + $value).TrimEnd())
    $groups[$section].Add('')
}
if ($groups.Count -ne 10) { throw "Config extraction mismatch: expected 10 sections, got $($groups.Count)" }
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# GEKIDRIVE Public 完整預設範例；合併到 org.gekidrive.ongeki.public.cfg，保留個人設定。')
foreach ($section in $groups.psbase.Keys) { $lines.Add(''); $lines.Add("[$section]"); $lines.AddRange($groups[$section]) }
$core = [ordered]@{
    Display = @('ShowFps = true', 'EnableFrameControl = false', 'TargetFps = 60', 'ToggleKey = F7')
    AutoPlay = @('Enabled = false', 'ToggleKey = F8', 'CollectBells = true', 'AvoidBullets = true', 'TheoreticalJudgments = true', 'FollowTrack = true', 'MaxOverDamage = true')
    Performance = @('CacheChartAnalysis = true', 'DisableXmlSerializerCompilation = true')
    MusicSelect = @('PauseTimer = false')
}
foreach ($section in $core.psbase.Keys) { $lines.Add(''); $lines.Add("[$section]"); foreach ($entry in $core[$section]) { $lines.Add($entry) } }
# GP is already present; insert its legacy confirmation entries into that section.
$gpIndex = $lines.IndexOf('[GP]'); $lines.Insert($gpIndex + 1, 'KeyboardConfirm = true'); $lines.Insert($gpIndex + 2, 'ConfirmKey = Return')
$config = Join-Path $stage 'examples\org.gekidrive.ongeki.public.example.cfg'
[System.IO.File]::WriteAllLines($config, $lines, [System.Text.UTF8Encoding]::new($true))
Copy-Item -LiteralPath (Join-Path $artifact 'GekiDrive.Public.dll') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root 'CONTRIBUTING.md') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root 'AI-NOTICE.md') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'encode-video.ps1') -Destination $stage -Force
foreach ($name in @('modules-0.5.md', 'led-protocol.md', 'guide.md', 'public-edition.md', 'release-0.5.1-public.md', 'validation.md')) { Copy-Item -LiteralPath (Join-Path $root "docs\$name") -Destination (Join-Path $stage 'docs') -Force }
if (Test-Path -LiteralPath (Join-Path $stage 'docs\no-score-policy.md')) { Remove-Item -LiteralPath (Join-Path $stage 'docs\no-score-policy.md') }
Get-FileHash -LiteralPath (Join-Path $stage 'GekiDrive.Public.dll') -Algorithm SHA256 | ForEach-Object { [System.IO.File]::WriteAllText((Join-Path $stage 'SHA256.txt'), $_.Hash.ToLowerInvariant() + '  GekiDrive.Public.dll' + [Environment]::NewLine) }
$zip = Join-Path $artifact "GEKIDRIVE-Public-$version.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Write-Host "Package: $zip"
Write-Host "Config: $config"
New-Item -ItemType Directory -Path (Join-Path $root 'examples') -Force | Out-Null
Copy-Item -LiteralPath $config -Destination (Join-Path $root 'examples') -Force
