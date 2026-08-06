param(
    [string]$Source = "artifacts/pro-supporter-badge/blender/supporter-badge.obj",
    [string]$Destination = "LyricHover.App/Assets/Models/supporter-badge.obj.gz"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Source))
$destinationPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Destination))

if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "找不到 Blender 导出的 OBJ：$sourcePath"
}

$requiredObjects = @(
    "Badge_Gold_Side",
    "Badge_Front_Enamel",
    "Badge_Music_Note",
    "Badge_Lyric_Hover_Text",
    "Badge_Back_Metal",
    "Badge_Back_NamePlate",
    "Badge_Top_Capsule_Inner_Wall"
)
$sourceText = [System.IO.File]::ReadAllText($sourcePath)
foreach ($objectName in $requiredObjects) {
    if (-not $sourceText.Contains("o $objectName")) {
        throw "OBJ 缺少运行时必需对象：$objectName"
    }
}

if ($sourceText.Contains("LYRIC ISLAND") -or
    $sourceText.Contains("LyricHover") -or
    $sourceText.Contains("Badge_Lyric_Island_Text")) {
    throw "OBJ 仍包含旧英文品牌名，已拒绝覆盖运行时模型。"
}

$destinationDirectory = Split-Path -Parent $destinationPath
[System.IO.Directory]::CreateDirectory($destinationDirectory) | Out-Null
$temporaryPath = $destinationPath + ".tmp"

$inputStream = $null
$outputStream = $null
$gzipStream = $null
try {
    $inputStream = [System.IO.File]::OpenRead($sourcePath)
    $outputStream = [System.IO.File]::Create($temporaryPath)
    $gzipStream = [System.IO.Compression.GZipStream]::new(
        $outputStream,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $true)
    $inputStream.CopyTo($gzipStream)
}
finally {
    if ($gzipStream) { $gzipStream.Dispose() }
    if ($outputStream) { $outputStream.Dispose() }
    if ($inputStream) { $inputStream.Dispose() }
}

[System.IO.File]::Copy($temporaryPath, $destinationPath, $true)
[System.IO.File]::Delete($temporaryPath)

$sourceBytes = (Get-Item -LiteralPath $sourcePath).Length
$destinationBytes = (Get-Item -LiteralPath $destinationPath).Length
Write-Host "已更新LyricHover LYRIC HOVER 支持者徽章运行时模型。"
Write-Host "OBJ：$sourceBytes bytes"
Write-Host "OBJ.GZ：$destinationBytes bytes"
