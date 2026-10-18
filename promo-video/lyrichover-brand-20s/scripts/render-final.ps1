[CmdletBinding()]
param(
    [switch]$PreflightOnly,
    [ValidateSet('draft', 'standard', 'high')]
    [string]$Quality = 'high',
    [string]$Output = 'renders/lyric-hover-brand-20s.mp4',
    [switch]$Gpu
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$wingetPackages = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'

if (-not (Test-Path -LiteralPath $wingetPackages)) {
    throw "WinGet package directory not found: $wingetPackages"
}

$ffmpeg = Get-ChildItem -LiteralPath $wingetPackages -Filter 'ffmpeg.exe' -File -Recurse |
    Where-Object { $_.FullName -match 'Gyan\.FFmpeg' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
$ffprobe = Get-ChildItem -LiteralPath $wingetPackages -Filter 'ffprobe.exe' -File -Recurse |
    Where-Object { $_.FullName -match 'Gyan\.FFmpeg' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $ffmpeg -or -not $ffprobe) {
    throw 'A complete Gyan.FFmpeg installation with ffmpeg.exe and ffprobe.exe was not found.'
}

$env:HYPERFRAMES_FFMPEG_PATH = $ffmpeg.FullName
$env:HYPERFRAMES_FFPROBE_PATH = $ffprobe.FullName

$versionLine = (& $ffmpeg.FullName -hide_banner -version 2>&1 | Select-Object -First 1).ToString()
$filterText = (& $ffmpeg.FullName -hide_banner -filters 2>&1) -join "`n"
$encoderText = (& $ffmpeg.FullName -hide_banner -encoders 2>&1) -join "`n"
$capabilities = [ordered]@{
    ffmpeg = $ffmpeg.FullName
    ffprobe = $ffprobe.FullName
    version = $versionLine
    psnr = [bool]($filterText -match '\bpsnr\b')
    libx264 = [bool]($encoderText -match '\blibx264\b')
    h264_nvenc = [bool]($encoderText -match '\bh264_nvenc\b')
}

if (-not $capabilities.psnr -or -not $capabilities.libx264) {
    throw 'FFmpeg is missing the psnr filter or libx264 encoder required by the final render workflow.'
}

if ($PreflightOnly) {
    $capabilities | ConvertTo-Json -Depth 2
    exit 0
}

$renderArgs = @(
    '--yes',
    'hyperframes@0.8.63',
    'render',
    '--fps', '60',
    '--quality', $Quality,
    '--format', 'mp4',
    '--output', $Output,
    '--strict-all'
)
if ($Gpu) {
    $renderArgs += '--gpu'
}

Push-Location $projectRoot
try {
    & npx @renderArgs
    if ($LASTEXITCODE -ne 0) {
        throw "HyperFrames render failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
