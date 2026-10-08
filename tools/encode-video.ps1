param(
    [Parameter(Mandatory = $true)][string]$FramesDirectory,
    [string]$Output,
    [string]$FFmpeg = 'ffmpeg'
)
$ErrorActionPreference = 'Stop'
$frames = (Resolve-Path -LiteralPath $FramesDirectory).Path
$meta = Join-Path $frames 'capture.txt'
if (!(Test-Path -LiteralPath $meta)) { throw 'capture.txt 不存在，請指定 GEKIDRIVE 輸出影格目錄。' }
$fpsLine = Get-Content -LiteralPath $meta | Where-Object { $_ -match '^fps=\d+$' } | Select-Object -First 1
if (!$fpsLine) { throw 'capture.txt 缺少 fps。' }
$fps = [int]$fpsLine.Substring(4)
if ($fps -lt 10 -or $fps -gt 120) { throw '不合法的影格率。' }
if (!(Test-Path -LiteralPath (Join-Path $frames 'frame000000.png'))) { throw '沒有可轉換的影格。' }
if (!$Output) { $Output = Join-Path $frames 'chart.mp4' }
if (Test-Path -LiteralPath $Output) { throw '輸出檔已存在；請指定另一個 Output，避免覆寫。' }
if (!(Get-Command $FFmpeg -ErrorAction SilentlyContinue)) { throw '找不到 FFmpeg；請安裝 FFmpeg 或以 -FFmpeg 指定 ffmpeg.exe 路徑。' }
# Argument array keeps paths literal and avoids shell command construction.
$ffmpegArgs = @('-n', '-framerate', [string]$fps, '-start_number', '0', '-i', (Join-Path $frames 'frame%06d.png'), '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-vf', 'pad=ceil(iw/2)*2:ceil(ih/2)*2', '-an', $Output)
& $FFmpeg @ffmpegArgs
if ($LASTEXITCODE -ne 0) { throw "FFmpeg 失敗，exit code=$LASTEXITCODE" }
Write-Host "影片已輸出：$Output"
