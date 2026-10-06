$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$assetDirectory = Join-Path $projectRoot 'src/OledAurora/Assets'
New-Item -ItemType Directory -Force -Path $assetDirectory | Out-Null
$bitmap = [System.Drawing.Bitmap]::new(64, 64)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(255, 22, 28, 37))
$waveColors = @([System.Drawing.Color]::FromArgb(255, 153, 210, 194), [System.Drawing.Color]::FromArgb(255, 168, 164, 211), [System.Drawing.Color]::FromArgb(255, 121, 167, 185))
for ($waveIndex = 0; $waveIndex -lt 3; $waveIndex++) {
    $pen = [System.Drawing.Pen]::new($waveColors[$waveIndex], 5)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $waveY = 19 + $waveIndex * 13
    $graphics.DrawBezier($pen, 10, $waveY + 4, 24, $waveY - 12, 40, $waveY + 12, 54, $waveY - 4)
    $pen.Dispose()
}
$pngStream = [System.IO.MemoryStream]::new()
$bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $pngStream.ToArray()
$file = [System.IO.File]::Create((Join-Path $assetDirectory 'aurora.ico'))
$writer = [System.IO.BinaryWriter]::new($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]64); $writer.Write([byte]64); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$pngBytes.Length); $writer.Write([uint32]22)
$writer.Write($pngBytes)
$writer.Dispose(); $pngStream.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
