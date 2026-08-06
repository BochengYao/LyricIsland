param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\LyricHover.App\Assets')
)

Add-Type -AssemblyName System.Drawing

function New-HandPngBytes {
    param(
        [int]$Size,
        [bool]$Closed
    )

    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $scale = $Size / 32.0
        if ($Closed) {
            $rawPoints = @(
                @(7,27), @(5,21), @(6,15), @(9,12), @(9,9), @(12,7),
                @(15,8), @(17,6), @(20,7), @(22,7), @(26,11), @(26,20),
                @(23,27), @(16,30), @(10,29)
            )
        } else {
            $rawPoints = @(
                @(9,28), @(6,23), @(3,20), @(3,17), @(5,15), @(8,16),
                @(10,19), @(10,6), @(12,3), @(14,5), @(14,3), @(16,1),
                @(18,3), @(18,5), @(20,3), @(22,5), @(22,8), @(24,6),
                @(26,9), @(26,19), @(23,26), @(19,30), @(13,30)
            )
        }

        $points = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
        foreach ($point in $rawPoints) {
            $points.Add((New-Object System.Drawing.PointF ([single]($point[0] * $scale)), ([single]($point[1] * $scale))))
        }
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        try {
            $path.AddPolygon($points.ToArray())
            $fill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
            $outline = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(230, 22, 25, 30)), ([single][Math]::Max(1.4, 1.8 * $scale))
            try {
                $outline.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
                $graphics.FillPath($fill, $path)
                $graphics.DrawPath($outline, $path)
                $detail = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(190, 45, 49, 58)), ([single][Math]::Max(0.8, $scale))
                try {
                    if ($Closed) {
                        foreach ($x in @(11,15,19,23)) {
                            $graphics.DrawLine($detail, [single]($x * $scale), [single](10 * $scale), [single]($x * $scale), [single](16 * $scale))
                        }
                        $graphics.DrawLine($detail, [single](8 * $scale), [single](19 * $scale), [single](23 * $scale), [single](19 * $scale))
                    } else {
                        foreach ($x in @(14,18,22)) {
                            $graphics.DrawLine($detail, [single]($x * $scale), [single](7 * $scale), [single]($x * $scale), [single](16 * $scale))
                        }
                    }
                } finally {
                    $detail.Dispose()
                }
            } finally {
                $fill.Dispose()
                $outline.Dispose()
            }
        } finally {
            $path.Dispose()
        }

        $stream = New-Object System.IO.MemoryStream
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            return $stream.ToArray()
        } finally {
            $stream.Dispose()
        }
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Write-CursorFile {
    param(
        [string]$Path,
        [bool]$Closed
    )

    $sizes = @(32, 48, 64)
    $images = @()
    foreach ($size in $sizes) {
        $images += ,(New-HandPngBytes -Size $size -Closed $Closed)
    }

    $stream = [System.IO.File]::Create($Path)
    $writer = New-Object System.IO.BinaryWriter $stream
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]2)
        $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $size = $sizes[$index]
            $writer.Write([byte]$size)
            $writer.Write([byte]$size)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16][Math]::Round($size * 0.5))
            $writer.Write([uint16][Math]::Round($size * 0.55))
            $writer.Write([uint32]$images[$index].Length)
            $writer.Write([uint32]$offset)
            $offset += $images[$index].Length
        }
        foreach ($image in $images) {
            $writer.Write([byte[]]$image)
        }
    } finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Write-CursorFile -Path (Join-Path $OutputDirectory 'grab-open.cur') -Closed $false
Write-CursorFile -Path (Join-Path $OutputDirectory 'grab-closed.cur') -Closed $true
