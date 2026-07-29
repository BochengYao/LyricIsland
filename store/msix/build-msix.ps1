param(
    [switch]$SkipTests,
    [switch]$KeepStaging
)

$ErrorActionPreference = 'Stop'
$scriptRoot = $PSScriptRoot
$root = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot '..\..'))
$propsPath = Join-Path $root 'Directory.Build.props'
$outputRoot = Join-Path $root 'store\package\msix'
$storePublishPath = Join-Path $outputRoot '.publish'
$stagingPath = Join-Path $outputRoot '.staging'
$verifyPath = Join-Path $outputRoot '.verify'
$manifestTemplatePath = Join-Path $scriptRoot 'AppxManifest.template.xml'
$assetsPath = Join-Path $scriptRoot 'Assets'
$buildToolsProject = Join-Path $scriptRoot 'BuildTools.csproj'
$packagesRoot = Join-Path $scriptRoot '.packages'

function Assert-ChildPath([string]$candidate, [string]$parent) {
    $resolvedCandidate = [System.IO.Path]::GetFullPath($candidate)
    $resolvedParent = [System.IO.Path]::GetFullPath($parent).TrimEnd('\') + '\'
    if (-not $resolvedCandidate.StartsWith($resolvedParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝操作 MSIX 输出目录之外的路径：$resolvedCandidate"
    }
}

function Remove-VerifiedDirectory([string]$path) {
    Assert-ChildPath $path $outputRoot
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

$props = [System.IO.File]::ReadAllText($propsPath)
$versionMatch = [regex]::Match($props, '<VersionPrefix>(?<version>\d+\.\d+\.\d+)</VersionPrefix>')
if (-not $versionMatch.Success) {
    throw 'Directory.Build.props 中没有有效的 VersionPrefix。'
}

$productVersion = $versionMatch.Groups['version'].Value
$packageVersion = "$productVersion.0"

if (-not (Test-Path -LiteralPath $outputRoot)) {
    New-Item -ItemType Directory -Path $outputRoot | Out-Null
}
Remove-VerifiedDirectory $storePublishPath
Remove-VerifiedDirectory $stagingPath
Remove-VerifiedDirectory $verifyPath

Push-Location $root
try {
    dotnet restore LyricsIsland.sln
    if ($LASTEXITCODE -ne 0) {
        throw '应用依赖还原失败。'
    }

    dotnet restore --runtime win-x64 LyricsIsland.App\LyricsIsland.App.csproj
    if ($LASTEXITCODE -ne 0) {
        throw 'MSIX win-x64 运行时依赖还原失败。'
    }

    if (-not $SkipTests) {
        dotnet run --no-restore --configuration Release --project LyricsIsland.Tests
        if ($LASTEXITCODE -ne 0) {
            throw '自动测试失败。'
        }
    }

    dotnet publish --no-restore --configuration Release --runtime win-x64 --self-contained true --output $storePublishPath LyricsIsland.App\LyricsIsland.App.csproj
    if ($LASTEXITCODE -ne 0) {
        throw 'MSIX 自包含应用发布失败。'
    }
}
finally {
    Pop-Location
}

$appExe = Join-Path $storePublishPath 'LyricsIsland.App.exe'
$appDll = Join-Path $storePublishPath 'LyricsIsland.App.dll'
if (-not (Test-Path -LiteralPath $appExe) -or -not (Test-Path -LiteralPath $appDll)) {
    throw 'MSIX 自包含发布目录缺少 LyricsIsland.App。'
}

$fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($appDll).FileVersion
if (-not $fileVersion.StartsWith("$productVersion.", [StringComparison]::Ordinal)) {
    throw "MSIX 自包含产物版本 $fileVersion 与源码版本 $productVersion 不一致。"
}

dotnet restore $buildToolsProject --packages $packagesRoot
if ($LASTEXITCODE -ne 0) {
    throw 'Microsoft.Windows.SDK.BuildTools 还原失败。'
}

$makeAppxCandidates = @(Get-ChildItem -LiteralPath $packagesRoot -Recurse -File -Filter 'MakeAppx.exe')
$makeAppx = $makeAppxCandidates |
    Where-Object { $_.FullName -match '\\x64\\MakeAppx\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if ($null -eq $makeAppx) {
    $makeAppx = $makeAppxCandidates | Sort-Object FullName -Descending | Select-Object -First 1
}
if ($null -eq $makeAppx) {
    throw '没有在 Microsoft.Windows.SDK.BuildTools 中找到 MakeAppx.exe。'
}

New-Item -ItemType Directory -Path $stagingPath | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stagingPath 'Assets') | Out-Null

Get-ChildItem -LiteralPath $storePublishPath -Force | ForEach-Object {
    if ($_.Extension -ne '.pdb') {
        Copy-Item -LiteralPath $_.FullName -Destination $stagingPath -Recurse -Force
    }
}
Get-ChildItem -LiteralPath $assetsPath -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $stagingPath 'Assets') -Force
}

$manifestText = [System.IO.File]::ReadAllText($manifestTemplatePath)
$manifestText = $manifestText.Replace('__PACKAGE_VERSION__', $packageVersion)
$manifestPath = Join-Path $stagingPath 'AppxManifest.xml'
[System.IO.File]::WriteAllText($manifestPath, $manifestText, [System.Text.UTF8Encoding]::new($false))

$packagePath = Join-Path $outputRoot "LyricIsland_$($packageVersion)_x64.msix"
if (Test-Path -LiteralPath $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

& $makeAppx.FullName pack /d $stagingPath /p $packagePath /o
if ($LASTEXITCODE -ne 0) {
    throw 'MakeAppx 生成 MSIX 失败。'
}

& $makeAppx.FullName unpack /p $packagePath /d $verifyPath /o
if ($LASTEXITCODE -ne 0) {
    throw 'MakeAppx 无法重新解包生成的 MSIX。'
}

[xml]$verifiedManifest = Get-Content -LiteralPath (Join-Path $verifyPath 'AppxManifest.xml')
$identity = $verifiedManifest.Package.Identity
if ($identity.Name -ne '70643607.LyricIsland' -or
    $identity.Publisher -ne 'CN=D0EA2A8A-59FF-4BC5-AB6E-5ABC356AF3E3' -or
    $identity.Version -ne $packageVersion -or
    $identity.ProcessorArchitecture -ne 'x64') {
    throw '生成的 MSIX 身份、版本或架构不正确。'
}
if (-not (Test-Path -LiteralPath (Join-Path $verifyPath 'LyricsIsland.App.exe'))) {
    throw '生成的 MSIX 中缺少主程序。'
}

$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
$package = Get-Item -LiteralPath $packagePath
Write-Host "MSIX 生成完成：$($package.FullName)"
Write-Host "版本：$packageVersion"
Write-Host "大小：$($package.Length) bytes"
Write-Host "SHA256：$hash"
Write-Host '签名：未签名（提交 Partner Center 后由 Microsoft Store 签名）'

if (-not $KeepStaging) {
    Remove-VerifiedDirectory $storePublishPath
    Remove-VerifiedDirectory $stagingPath
    Remove-VerifiedDirectory $verifyPath
}
