$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType = WindowsRuntime]
$null = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties, Windows.Media.Control, ContentType = WindowsRuntime]

$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() |
    Where-Object {
        $_.Name -eq 'AsTask' -and
        $_.IsGenericMethod -and
        $_.GetParameters().Count -eq 1
    } |
    Select-Object -First 1)

function Await-WinRt($operation, [Type] $resultType) {
    $task = $asTask.MakeGenericMethod($resultType).Invoke($null, @($operation))
    return $task.GetAwaiter().GetResult()
}

function Escape-Json([string] $value) {
    if ($null -eq $value) {
        return ''
    }

    return $value.Replace('\', '\\').Replace('"', '\"').Replace("`r", '').Replace("`n", '\n')
}

try {
    $manager = Await-WinRt `
        ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]::RequestAsync()) `
        ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager])
}
catch {
    Write-Output ('{{"hasSession":false,"error":"{0}"}}' -f (Escape-Json $_.Exception.Message))
    exit 0
}

$session = $manager.GetCurrentSession()
if ($null -eq $session) {
    Write-Output '{"hasSession":false}'
    exit 0
}

$properties = Await-WinRt `
    ($session.TryGetMediaPropertiesAsync()) `
    ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties])

$timeline = $session.GetTimelineProperties()
$playback = $session.GetPlaybackInfo()
$status = $playback.PlaybackStatus.ToString()
$isPlaying = if ($status -eq 'Playing') { 'true' } else { 'false' }
$durationSeconds = [Math]::Max(0, [int][Math]::Round($timeline.EndTime.TotalSeconds))
$positionSeconds = [Math]::Max(0, [int][Math]::Round($timeline.Position.TotalSeconds))

Write-Output ('{{"hasSession":true,"title":"{0}","artist":"{1}","album":"{2}","durationSeconds":{3},"positionSeconds":{4},"isPlaying":{5},"sourceAppUserModelId":"{6}"}}' -f `
    (Escape-Json $properties.Title), `
    (Escape-Json $properties.Artist), `
    (Escape-Json $properties.AlbumTitle), `
    $durationSeconds, `
    $positionSeconds, `
    $isPlaying, `
    (Escape-Json $session.SourceAppUserModelId))
