param(
    [switch]$KeepVersion,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $root 'Directory.Build.props'
$publishRoot = Join-Path $root 'publish'
$currentPath = Join-Path $publishRoot 'current'
$archiveRoot = Join-Path $publishRoot 'archive'
$readmePath = Join-Path $publishRoot 'README.md'
$originalProps = [System.IO.File]::ReadAllText($propsPath)
$versionMatch = [regex]::Match($originalProps, '<VersionPrefix>(?<version>\d+\.\d+\.(?<patch>\d+))</VersionPrefix>')

if (-not $versionMatch.Success) {
    throw 'Directory.Build.props 中没有有效的 VersionPrefix。'
}

$currentVersion = $versionMatch.Groups['version'].Value
$currentPatch = [int]$versionMatch.Groups['patch'].Value
$nextPatch = $currentPatch + 1
$targetVersion = if ($KeepVersion) {
    $currentVersion
} else {
    $currentVersion.Substring(0, $currentVersion.LastIndexOf('.') + 1) + $nextPatch
}
$labelMatch = [regex]::Match($originalProps, '<VersionSuffix>(?<label>[^<]+)</VersionSuffix>')
$label = if ($labelMatch.Success) { $labelMatch.Groups['label'].Value.Trim() } else { 'Beta' }
$releaseName = "v$targetVersion $label"
$directoryName = "v$targetVersion-$label"
$stagingPath = Join-Path $publishRoot "staging-$directoryName"

function Assert-ChildPath([string]$candidate, [string]$parent) {
    $resolvedCandidate = [System.IO.Path]::GetFullPath($candidate)
    $resolvedParent = [System.IO.Path]::GetFullPath($parent).TrimEnd('\') + '\'
    if (-not $resolvedCandidate.StartsWith($resolvedParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝操作发布目录之外的路径：$resolvedCandidate"
    }
}

function Move-DirectoryWithRetry([string]$source, [string]$destination) {
    for ($attempt = 1; $attempt -le 15; $attempt++) {
        try {
            Move-Item -LiteralPath $source -Destination $destination -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq 15) {
                throw
            }
            Start-Sleep -Milliseconds 200
        }
    }
}

Assert-ChildPath $stagingPath $publishRoot
Assert-ChildPath $currentPath $publishRoot
Assert-ChildPath $archiveRoot $publishRoot

try {
    if (-not $KeepVersion) {
        $updatedProps = [regex]::Replace(
            $originalProps,
            '<VersionPrefix>\d+\.\d+\.\d+</VersionPrefix>',
            "<VersionPrefix>$targetVersion</VersionPrefix>",
            1)
        [System.IO.File]::WriteAllText($propsPath, $updatedProps, [System.Text.UTF8Encoding]::new($false))
    }

    if (Test-Path -LiteralPath $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }

    Push-Location $root
    try {
        dotnet run --no-restore --configuration Release --project LyricHover.Tests
        if ($LASTEXITCODE -ne 0) { throw '自动测试失败。' }

        dotnet build --no-restore --configuration Release --runtime win-x64 LyricHover.App\LyricHover.App.csproj
        if ($LASTEXITCODE -ne 0) { throw 'Release 构建失败。' }

        dotnet publish --no-restore --configuration Release --runtime win-x64 --self-contained false --output $stagingPath LyricHover.App\LyricHover.App.csproj
        if ($LASTEXITCODE -ne 0) { throw '发布失败。' }
    }
    finally {
        Pop-Location
    }

    $currentDirectoryPrefix = [System.IO.Path]::GetFullPath($currentPath).TrimEnd('\') + '\'
    $runningCurrent = @(Get-Process -ErrorAction SilentlyContinue |
        Where-Object {
            try { $_.Path -and [System.IO.Path]::GetFullPath($_.Path).StartsWith($currentDirectoryPrefix, [StringComparison]::OrdinalIgnoreCase) }
            catch { $false }
        })
    foreach ($runningProcess in $runningCurrent) {
        Stop-Process -Id $runningProcess.Id -Force
        try { $runningProcess.WaitForExit(5000) } catch { }
    }

    if (-not (Test-Path -LiteralPath $archiveRoot)) {
        New-Item -ItemType Directory -Path $archiveRoot | Out-Null
    }

    if (Test-Path -LiteralPath $currentPath) {
        $archiveName = "v$currentVersion-$label"
        $archivePath = Join-Path $archiveRoot $archiveName
        Assert-ChildPath $archivePath $archiveRoot
        if (Test-Path -LiteralPath $archivePath) {
            $archivePath = Join-Path $archiveRoot ($archiveName + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
            Assert-ChildPath $archivePath $archiveRoot
        }
        Move-DirectoryWithRetry $currentPath $archivePath
    }

    Move-DirectoryWithRetry $stagingPath $currentPath

    if (Test-Path -LiteralPath $readmePath) {
        $readme = [System.IO.File]::ReadAllText($readmePath)
        $readme = [regex]::Replace($readme, '当前版本：`[^`]+`', "当前版本：``$releaseName``")
        [System.IO.File]::WriteAllText($readmePath, $readme, [System.Text.UTF8Encoding]::new($false))
    }

    if (-not $NoLaunch) {
        Start-Process -FilePath (Join-Path $currentPath 'LyricHover.App.exe') -WorkingDirectory $currentPath
    }

    Write-Host "发布完成：$releaseName"
    exit 0
}
catch {
    [System.IO.File]::WriteAllText($propsPath, $originalProps, [System.Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
    Write-Error $_
    exit 1
}
