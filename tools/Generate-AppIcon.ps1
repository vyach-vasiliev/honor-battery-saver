param()

Add-Type -AssemblyName System.Drawing

function New-RoundedPath {
    param(
        [float] $X,
        [float] $Y,
        [float] $Width,
        [float] $Height,
        [float] $Radius
    )

    $diameter = $Radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-LogoPng {
    param([int] $Size)

    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform($Size / 48.0, $Size / 48.0)
    $accent = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(75, 104, 232))
    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $leaf = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(121, 230, 178))
    $outer = New-RoundedPath 0 0 48 48 12
    $battery = New-RoundedPath 7 14 32 22 6
    $terminal = New-RoundedPath 38 20 4 10 1.5
    $inner = New-RoundedPath 11 18 24 14 3
    $leafShape = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $leafShape.AddBezier(14, 29, 17, 22, 23, 20, 32, 20)
    $leafShape.AddBezier(32, 20, 30, 27, 25, 31, 18, 31)
    $leafShape.AddBezier(18, 31, 20, 28, 23, 25, 27, 23)
    $leafShape.AddBezier(27, 23, 22, 24, 18, 26, 14, 29)
    $leafShape.CloseFigure()

    $graphics.FillPath($accent, $outer)
    $graphics.FillPath($white, $battery)
    $graphics.FillPath($white, $terminal)
    $graphics.FillPath($accent, $inner)
    $graphics.FillPath($leaf, $leafShape)

    $memory = [System.IO.MemoryStream]::new()
    $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $memory.ToArray()

    $memory.Dispose()
    $leafShape.Dispose()
    $inner.Dispose()
    $terminal.Dispose()
    $battery.Dispose()
    $outer.Dispose()
    $leaf.Dispose()
    $white.Dispose()
    $accent.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
    return $bytes
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = foreach ($size in $sizes) {
    [pscustomobject]@{ Size = $size; Data = (New-LogoPng $size) }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$targetPath = Join-Path $projectRoot 'src\HonorBatterySaver.Tray\Assets\HonorBatterySaver.ico'
$stream = [System.IO.File]::Create($targetPath)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($image in $images) {
        $dimension = if ($image.Size -eq 256) { 0 } else { $image.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Data.Length)
        $writer.Write([uint32]$offset)
        $offset += $image.Data.Length
    }

    foreach ($image in $images) {
        $writer.Write([byte[]]$image.Data)
    }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Output $targetPath
